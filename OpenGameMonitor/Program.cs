using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenGameMonitorLibraries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OpenGameMonitorWeb.Policies;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using System.Runtime.InteropServices;

namespace OpenGameMonitor
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = CreateWebHostBuilder(args);
			var host = builder.Build();

			host.TestConnection();

			try
			{
				host.CheckForUpdate();

				host.Services.CreateRoles().Wait();

				//host.Run();
#if !DEBUG
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					host.RunAsService();
				}
				else
				{
#endif
					host.Run();
#if !DEBUG
				}
#endif
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

		public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
			WebHost.CreateDefaultBuilder(args)
				.ConfigureAppConfiguration((hostingContext, config) =>
				{
					var env = hostingContext.HostingEnvironment;

					config.AddJsonFile("appsettings.json", optional: false);
					config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);

					var cfg = config.Build();

					config.AddMonitorDBConfiguration(options => options.UseMySql(cfg.GetConnectionString("MonitorDatabase")));
				})
				.UseStartup<Startup>();
	}
}
