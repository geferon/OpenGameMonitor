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
using FubarDev.FtpServer.AccountManagement;

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

#if DEBUG
                    throw;
#endif
                }
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService() // Both Windows and SystemD support
                .UseSystemd()
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

                    config.AddMonitorDBConfiguration(options => options.UseMySql(cfg.GetConnectionString("MonitorDatabase"), x => x.MigrationsAssembly("OpenGameMonitorDBMigrations")));
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


                    services.AddHostedService<IPCService>();

                    // Basic server services
                    services.AddSingleton<EventHandlerService>();

                    // services.AddHostedService<Worker>();

                    services.AddHostedService<QueuedHostedService>();
                    services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

                    services.AddSingleton<SteamAPIService>();
                    services.AddSingleton<SteamCMDService>();
                    services.AddSingleton<GameHandlerService>();

                    services.AddHostedService<ServerTracker>();

                    // Identity
                    services.AddIdentityCore<MonitorUser>(options => options.SignIn.RequireConfirmedAccount = true)
                        .AddRoles<MonitorRole>()
                        .AddEntityFrameworkStores<MonitorDBContext>();

                    // FTP Server
                    services.AddSingleton<IMembershipProvider, FTPMembershipProvider>();

                    services.AddFtpServer(options => options.Services.AddSingleton<FubarDev.FtpServer.FileSystem.IFileSystemClassFactory, ServerManagerFileSystemProvider>());

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
    }
}
