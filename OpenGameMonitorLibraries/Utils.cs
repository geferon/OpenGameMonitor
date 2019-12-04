using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

        public static void TestConnection(this IWebHost host)
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
    }
}
