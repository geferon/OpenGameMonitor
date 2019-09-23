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
using JKang.IpcServiceFramework;
using Microsoft.EntityFrameworkCore;

namespace OpenGameMonitorWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IServiceCollection services = ConfigureServices(new ServiceCollection());

            var builder = CreateHostBuilder(args);
            var host = builder.Build();

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
                    services.Configure<MonitorConfig>(hostContext.Configuration.GetSection("AppConfig"));

                    services.AddHostedService<Worker>();

                    services.AddHostedService<QueuedHostedService>();
                    services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

                    services.AddSingleton<SteamAPIService>();
                    services.AddSingleton<SteamCMDService>();
                    services.AddSingleton<GameHandler>();

                    services.AddEntityFrameworkMySql();
                    services.AddDbContext<MonitorDBContext>(options => options.UseMySql("")); // TODO: Connection String
                });

        private static IServiceCollection ConfigureServices(IServiceCollection services)
        {
            return services
                .AddIpc(builder =>
                {
                    builder
                        .AddNamedPipe(options =>
                        {
                            options.ThreadCount = 2;
                        })
                        .AddService<IMonitorComsInterface, MonitorComsService>();
                });
        }
    }
}
