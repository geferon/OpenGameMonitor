using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGameMonitorLibraries
{
	public static class MonitorUtils
	{
		private static Task RunAsyncProcess(Process process, bool shouldStart = true)
		{
			var tcs = new TaskCompletionSource<object>();
			process.Exited += (s, e) => tcs.TrySetResult(null);
			if (shouldStart && !process.Start()) tcs.SetException(new Exception("Failed to start process."));
			return tcs.Task;
		}

		public static Task WaitForExitAsync(this Process process,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			var tcs = new TaskCompletionSource<object>();
			process.EnableRaisingEvents = true;
			process.Exited += (s, e) => tcs.TrySetResult(null);

			if (cancellationToken != default(CancellationToken))
				cancellationToken.Register(tcs.SetCanceled);

			return tcs.Task;
		}

		static byte[] Decompress(byte[] gzip)
		{
			// Create a GZIP stream with decompression mode.
			// ... Then create a buffer and write into while reading from the GZIP stream.
			using (GZipStream stream = new GZipStream(new MemoryStream(gzip),
				CompressionMode.Decompress))
			{
				const int size = 4096;
				byte[] buffer = new byte[size];
				using (MemoryStream memory = new MemoryStream())
				{
					int count = 0;
					do
					{
						count = stream.Read(buffer, 0, size);
						if (count > 0)
						{
							memory.Write(buffer, 0, count);
						}
					}
					while (count > 0);
					return memory.ToArray();
				}
			}
		}

		private static void InternalTestConnection(IServiceProvider services)
		{
			ILoggerFactory loggerF = services.GetService<ILoggerFactory>();

			ILogger logger = loggerF.CreateLogger("Program");

			IServiceScopeFactory serviceScopeFactory = services.GetService<IServiceScopeFactory>();

			using (var scope = serviceScopeFactory.CreateScope())
			{
				MonitorDBContext db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>();

				try
				{
					if (!db.Database.CanConnect())
					{
						logger.LogError("The database connection settings is invalid, or the server can't connect to the database.");
						Environment.Exit(-1);
					}
				}
				catch (Exception err)
				{
					if (err is MySqlException || err is InvalidOperationException)
					{
						string errorText = "The database connection settings is invalid, or the server can't connect to the database.\nError: {0}\nStack trace: {1}";
						logger.LogError(err, string.Format(errorText, err.Message, err.StackTrace));

						Environment.Exit(-1);
						return;
					}
					throw;
				}
			}
		}

		public static void TestConnection(this IHost host)
		{
			if (host == null) throw new ArgumentNullException(nameof(host));

			InternalTestConnection(host.Services);
		}

		public static void TestConnection(this IWebHost host)
		{
			if (host == null) throw new ArgumentNullException(nameof(host));

			InternalTestConnection(host.Services);
		}

		private static void InternalCheckForUpdate(IServiceProvider services)
		{
			IServiceScopeFactory serviceScopeFactory = services.GetService<IServiceScopeFactory>();

			using (var scope = serviceScopeFactory.CreateScope())
			{
				MonitorDBContext db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>();

				bool shouldMigrateDatabase = db.Database.GetPendingMigrations().Any();

				ILoggerFactory loggerF = services.GetService<ILoggerFactory>();
				ILogger logger = loggerF.CreateLogger("Program");


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
					logger.LogInformation("Application upgraded succesfully!");
					//host.Services.GetService<Microsoft.AspNetCore.Hosting.IApplicationLifetime>().StopApplication();
					ReloadInternalConfigAfterUpgrade(services);
				}
			}
		}

		private static void ReloadInternalConfigAfterUpgrade(IServiceProvider services)
		{
			HostBuilderContext hostBuilderContext = services.GetService<HostBuilderContext>();

			IConfigurationRoot rootCfg = (IConfigurationRoot) hostBuilderContext.Configuration;
			MonitorDBConfigurationProvider dbProv = null;
			foreach (IConfigurationProvider provider in rootCfg.Providers)
			{
				if (provider is MonitorDBConfigurationProvider)
				{
					dbProv = (MonitorDBConfigurationProvider)provider;
					break;
				}
			}

			if (dbProv != null)
			{
				dbProv.ForceReload();
			}
		}

		public static void CheckForUpdate(this IHost host)
		{
			if (host == null) throw new ArgumentNullException(nameof(host));

			InternalCheckForUpdate(host.Services);
		}

		public static void CheckForUpdate(this IWebHost host)
		{
			if (host == null) throw new ArgumentNullException(nameof(host));

			InternalCheckForUpdate(host.Services);
		}

		public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
		{
			var tcs = new TaskCompletionSource<bool>();
			using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
			{
				if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
				{
					cancellationToken.ThrowIfCancellationRequested();
				}
			}

			// Rethrow any fault/cancellation exception, even if we awaited above.
			// But if we skipped the above if branch, this will actually yield
			// on an incompleted task.
			return await task.ConfigureAwait(false);
		}

		public static void MoveLinesFromStringBuilderToMessageQueue(ref int currentLinePos, ref bool bLastCarriageReturn, StringBuilder sb, Action<string?> callback)
		{
			int currentIndex = currentLinePos;
			int lineStart = 0;
			int len = sb!.Length;

			// skip a beginning '\n' character of new block if last block ended
			// with '\r'
			if (bLastCarriageReturn && (len > 0) && sb[0] == '\n')
			{
				currentIndex = 1;
				lineStart = 1;
				bLastCarriageReturn = false;
			}

			while (currentIndex < len)
			{
				char ch = sb[currentIndex];
				// Note the following common line feed chars:
				// \n - UNIX   \r\n - DOS   \r - Mac
				if (ch == '\r' || ch == '\n')
				{
					string line = sb.ToString(lineStart, currentIndex - lineStart);
					lineStart = currentIndex + 1;
					// skip the "\n" character following "\r" character
					if ((ch == '\r') && (lineStart < len) && (sb[lineStart] == '\n'))
					{
						lineStart++;
						currentIndex++;
					}

					callback(line);
				}
				currentIndex++;
			}
			if ((len > 0) && sb[len - 1] == '\r')
			{
				bLastCarriageReturn = true;
			}
			// Keep the rest characters which can't form a new line in string builder.
			if (lineStart < len)
			{
				if (lineStart == 0)
				{
					// we found no breaklines, in this case we cache the position
					// so next time we don't have to restart from the beginning
					currentLinePos = currentIndex;
				}
				else
				{
					sb.Remove(0, lineStart);
					currentLinePos = 0;
				}
			}
			else
			{
				sb.Length = 0;
				currentLinePos = 0;
			}
		}

		public static async Task<T[]> FilterAsync<T>(this IEnumerable<T> sourceEnumerable, Func<T, Task<bool>> predicateAsync)
		{
			return (await Task.WhenAll(
				sourceEnumerable.Select(
					v => predicateAsync(v)
					.ContinueWith(task => new { Predicate = task.Result, Value = v })))
				).Where(a => a.Predicate).Select(a => a.Value).ToArray();
		}

		public static IServiceCollection AddHostedSingleton<THostedService>(this IServiceCollection services) where THostedService : class, IHostedService
		{
			services.AddSingleton<THostedService>();
			services.AddSingleton<IHostedService, THostedService>(serviceProvider => serviceProvider.GetService<THostedService>());
			return services;
		}
	}
}
