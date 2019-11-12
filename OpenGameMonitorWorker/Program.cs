using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ServiceProcess;
using Microsoft.Extensions.Configuration;
using OpenGameMonitorLibraries;
//using JKang.IpcServiceFramework;
//using JKang.IpcServiceFramework.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.IO;
using System.Globalization;
using Microsoft.Extensions.Logging;
//using Microsoft.AspNetCore.Hosting;
using System.Net;
using MySql.Data.MySqlClient;
using FubarDev.FtpServer;
using FubarDev.FtpServer.FileSystem.DotNet;
using Microsoft.AspNetCore.Identity;

namespace OpenGameMonitorWorker
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = CreateHostBuilder(args);
            var host = builder.Build();

            host.TestConnection();

            try
            {
                host.CheckForUpdate();

                host.Run();
            }
            catch (Exception err)
            {
                Console.WriteLine("An error has occurred while starting the application!");
                Console.WriteLine("Note: This is NOT a normal error, if you see this, please report this error!");
                Console.WriteLine("{0}", err.Message);
                Console.WriteLine("{0}", err.StackTrace);
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;

                    config.AddJsonFile("appsettings.json", optional: false);
                    config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);

                    config.AddEnvironmentVariables();

                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Basic services
                    services.AddOptions();
                    services.AddLogging();
                    //services.Configure<MonitorConfig>(hostContext.Configuration.GetSection("AppConfig"));

                    // DB service and external connections
                    services.AddEntityFrameworkMySql();

                    var connectionStr = hostContext.Configuration.GetConnectionString("MonitorDatabase");

                    if (String.IsNullOrEmpty(connectionStr))
                    {
                        throw new Exception("No connection string has been found!");
                        //return;
                    }

                    services.AddDbContext<MonitorDBContext>(options => options.UseMySql(connectionStr));

                    services.AddDefaultIdentity<MonitorUser>()
                        .AddRoles<MonitorRole>()
                        .AddRoleManager<RoleManager<MonitorRole>>()
                        .AddEntityFrameworkStores<MonitorDBContext>();

                    services.AddIdentityServer()
                        .AddApiAuthorization<MonitorUser, MonitorDBContext>();


                    services.AddHostedService<IPCService>();

                    // Basic server services
                    services.AddSingleton<EventHandlerService>();

                    // services.AddHostedService<Worker>();

                    services.AddHostedService<QueuedHostedService>();
                    services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

                    services.AddSingleton<SteamAPIService>();
                    services.AddSingleton<SteamCMDService>();
                    services.AddSingleton<GameHandler>();

                    // FTP Server
                    services.AddFtpServer(builder =>
                        builder.UseDotNetFileSystem());

                    services.Configure<FtpServerOptions>(config => {
                        config.ServerAddress = "*";
                        config.Port = hostContext.Configuration.GetValue<int>("MonitorSettings:FtpPort", 25);
                    });

                    services.Configure<DotNetFileSystemOptions>(opt =>
                        opt.RootPath = Path.Combine(Path.GetTempPath(), "OpenGameMonitorFTP"));

                    services.AddHostedService<HostedFTPService>();

                });
                /*
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.Listen(IPAddress.Any, 5030, listenOptions =>
                        {
                            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
                            // listenOptions.UseHttps(); // TODO
                        });
                    });
                    webBuilder.UseStartup<gRPCStartup>();
                });
                */

        public static void TestConnection(this IHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            ILoggerFactory loggerF = host.Services.GetService<ILoggerFactory>();

            ILogger logger = loggerF.CreateLogger("Program");

            IServiceScopeFactory serviceScopeFactory = host.Services.GetService<IServiceScopeFactory>();

            using (var scope = serviceScopeFactory.CreateScope())
            {
                MonitorDBContext db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>();

                try
                {
                    if (!db.Database.CanConnect())
                    {
                        logger.LogError("The database connection settings is invalid, or the server can't connect to the database");
                        Environment.Exit(-1);
                    }
                }
                catch (MySqlException err)
                {
                    logger.LogError(err, "The database connection settings is invalid, or the server can't connect to the database");
                    Environment.Exit(-1);
                }
                catch (InvalidOperationException err)
                {
                    logger.LogError(err, "The database connection settings is invalid, or the server can't connect to the database");
                    Environment.Exit(-1);
                }
            }
        }

        public static void CheckForUpdate(this IHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            bool shouldMigrateDatabase = false;

            try
            {
                var appData = File.ReadLines("application.dat").ToList();
                int oldVer = Convert.ToInt32(appData[0], CultureInfo.InvariantCulture);
                
                if (oldVer < MonitorConfig.Version)
                {
                    shouldMigrateDatabase = true;
                }
            }
            catch
            {
                shouldMigrateDatabase = true;
            }

            if (shouldMigrateDatabase)
            {
                ILoggerFactory loggerF = host.Services.GetService<ILoggerFactory>();

                ILogger logger = loggerF.CreateLogger("Program");

                logger.LogInformation("Version out of date or old, performing updating!");

                IServiceScopeFactory serviceScopeFactory = host.Services.GetService<IServiceScopeFactory>();

                using (var scope = serviceScopeFactory.CreateScope())
                {
                    MonitorDBContext db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>();

                    try
                    {
                        db.Database.Migrate();
                    }
                    catch (Exception err)
                    {
                        logger.LogError(err, "There has been an error while performing the Database upgrade! Err: {0}", err.Message);
                        Environment.Exit(-1);
                        return;
                    }
                }

                // Write the version file
                string[] data = new string[]
                {
                    MonitorConfig.Version.ToString(CultureInfo.InvariantCulture)
                };

                File.WriteAllLines("application.dat", data);
            }
        }
    }
}
