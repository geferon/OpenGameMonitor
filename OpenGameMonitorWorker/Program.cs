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
using Microsoft.AspNetCore.Hosting;
using System.Net;

namespace OpenGameMonitorWorker
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = CreateHostBuilder(args);
            var host = builder.Build();

            host.CheckForUpdate();

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false);
                    config.AddEnvironmentVariables();

                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions();
                    services.AddLogging();
                    //services.Configure<MonitorConfig>(hostContext.Configuration.GetSection("AppConfig"));

                    services.AddSingleton<EventHandlerService>();

                    // services.AddHostedService<Worker>();

                    services.AddHostedService<QueuedHostedService>();
                    services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();


                    services.AddSingleton<SteamAPIService>();
                    services.AddSingleton<SteamCMDService>();
                    services.AddSingleton<GameHandler>();

                    //services.AddSingleton<NamedPipeOptions>();

                    services.AddEntityFrameworkMySql();
                    services.AddDbContext<MonitorDBContext>(options => options.UseMySql(hostContext.Configuration.GetConnectionString("MonitorDatabase")));

                    // Todo: implement https://docs.microsoft.com/en-us/aspnet/core/tutorials/grpc/grpc-start?view=aspnetcore-3.0&tabs=visual-studio
                    services.AddGrpc();

                    /*
                    services.AddIpc(builder =>
                    {
                        builder
                            .AddNamedPipe(options =>
                            {
                                options.ThreadCount = 2;
                            })
                            .AddService<IMonitorComsInterface, MonitorComsService>();
                    });
                    //services.AddHostedService<IPCService>();
                    */

                })
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

        public static void CheckForUpdate(this IHost host)
        {
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
