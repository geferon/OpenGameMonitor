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
using OpenGameMonitorWorker.Services;
using OpenGameMonitorWorker.Tasks;
using OpenGameMonitorWorker.Handlers;

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
                // If it's an OperationCanceled that means it was cancelled by us, so don't throw this error
                if (!(err is OperationCanceledException))
                {
                    Console.WriteLine("An error has occurred while starting the application!");
                    Console.WriteLine("Note: This is NOT a normal error, if you see this, please report this error!");
                    Console.WriteLine("{0}", err.Message);
                    Console.WriteLine("{0}", err.StackTrace);
                    Console.WriteLine(err.GetType());
                }
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

                    var cfg = config.Build();

                    config.AddMonitorDBConfiguration(options => options.UseMySql(cfg.GetConnectionString("MonitorDatabase")));
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

                    // Identity is only for ASP.NET :/
                    /*
                    services.AddDefaultIdentity<MonitorUser>()
                        .AddRoles<MonitorRole>()
                        .AddRoleManager<Microsoft.AspNetCore.Identity.RoleManager<MonitorRole>>()
                        .AddEntityFrameworkStores<MonitorDBContext>();

                    services.AddIdentityServer()
                        .AddApiAuthorization<MonitorUser, MonitorDBContext>();
                        */


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

                    services.Configure<DotNetFileSystemOptions>(opt => {
                        opt.RootPath = Path.Combine(Path.GetTempPath(), "OpenGameMonitorFTP");
                        opt.AllowNonEmptyDirectoryDelete = true;
                    });

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

        public static void CheckForUpdate(this IHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            /*
            // Migrate by default on development
#if DEBUG
            bool shouldMigrateDatabase = true;
#else
            bool shouldMigrateDatabase = false;
#endif
            // */
            IServiceScopeFactory serviceScopeFactory = host.Services.GetService<IServiceScopeFactory>();

            using (var scope = serviceScopeFactory.CreateScope())
            {
                MonitorDBContext db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>();

                bool shouldMigrateDatabase = db.Database.GetPendingMigrations().Any();

                ILoggerFactory loggerF = host.Services.GetService<ILoggerFactory>();
                ILogger logger = loggerF.CreateLogger("Program");

                // Might not need this anymore after above
                /*
                if (!shouldMigrateDatabase)
                {
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
                }
                */

                if (shouldMigrateDatabase)
                {
                    logger.LogInformation("Version out of date or old, performing updating!");

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


                if (shouldMigrateDatabase)
                {
                    // After the upgrade, restart
                    logger.LogInformation("Application upgraded succesfully! Restarting...");
                    host.Services.GetService<IApplicationLifetime>().StopApplication();
                }
            }
        }
    }
}
