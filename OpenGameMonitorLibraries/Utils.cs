using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
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

		public static async Task WaitForExitAsync(this Process proc)
		{
			await RunAsyncProcess(proc, false);

			if (proc.ExitCode != 0)
			{
				throw new Exception($"Process exited with exit code {proc.ExitCode}");
			}
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
    }
}
