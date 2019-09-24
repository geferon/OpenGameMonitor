using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenGameMonitorLibraries;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker
{
    public class SteamCMDService
    {
        private readonly ILogger _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

		public event EventHandler SteamCMDInstalledEvent;

        public IBackgroundTaskQueue Queue { get; }

        //private Dictionary<int, Process> _processes = new Dictionary<int, Process>();
        private Process activeSteamCMD;


        public SteamCMDService(ILogger<SteamCMDService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;

            CheckSteamCMD();
        }

        private string TempLocation
        {
            get
            {
                string programFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                return Path.Combine(programFolder, "Temp");
            }
        }

        public string SteamCMDLocation {
            get
            {
                string programFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                return Path.Combine(programFolder, "SteamCMD");
            }
        }

        private DirectoryInfo _steamCMDFolder { get; set; }
        public DirectoryInfo SteamCMDFolder
        {
            get
            {
                if (_steamCMDFolder == null)
                {
                    _steamCMDFolder = new DirectoryInfo(SteamCMDLocation);
                }

                return _steamCMDFolder;
            }
        }

        private FileInfo _steamCMDExe { get; set; }
        public FileInfo SteamCMDExe
        {
            get
            {
                if (_steamCMDExe == null)
                {
                    _steamCMDExe = new FileInfo(Path.Combine(SteamCMDFolder.FullName, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "steamcmd.exe" : "steamcmd"));
                }
                return _steamCMDExe;
            }
        }

        public bool SteamCMDInstalled
        {
            get
            {
                return SteamCMDExe.Exists;
            }
        }

        // https://docs.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/background-tasks-with-ihostedservice
        // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-2.2&tabs=visual-studio

        private void CheckSteamCMD()
        {
            if (!SteamCMDFolder.Exists)
            {
                SteamCMDFolder.Create();
            }

            if (!SteamCMDInstalled)
            {
                _logger.LogInformation("SteamCMD isn't installed, installing");
                using (var client = new WebClient())
                {
                    client.DownloadFileAsync(
                        new Uri(
                            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                            "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip" : "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz"
                        ),
                        Path.Combine(TempLocation, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "SteamCMD.zip" : "SteamCMD.tar.gz")
                    );

                    client.DownloadFileCompleted += (sender, e) =>
                    {
                        string fileLocation = Path.Combine(TempLocation, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "SteamCMD.zip" : "SteamCMD.tar.gz");

                        using (Stream stream = File.OpenRead(fileLocation))
                        using (var reader = ReaderFactory.Open(stream))
                        {
                            while (reader.MoveToNextEntry())
                            {
                                if (!reader.Entry.IsDirectory)
                                {
                                    Console.WriteLine(reader.Entry.Key);
                                    reader.WriteEntryToDirectory(SteamCMDFolder.FullName, new ExtractionOptions()
                                    {
                                        ExtractFullPath = true,
                                        Overwrite = true
                                    });
                                }
                            }
                        }

						EventHandler handler = SteamCMDInstalledEvent;
						handler?.Invoke(this, new EventArgs());

					};
                }
            }
        }


		public Process StartSteamCMD(string strparams)
		{
			if (activeSteamCMD != null && !activeSteamCMD.HasExited)
			{
				throw new Exception("Can't start a SteamCMD when one is already open!");
			}

			var steamCMDProc = new Process();
			steamCMDProc.StartInfo.FileName = SteamCMDExe.FullName;
			steamCMDProc.StartInfo.WorkingDirectory = SteamCMDFolder.FullName;
			steamCMDProc.StartInfo.Arguments = strparams;

			steamCMDProc.StartInfo.RedirectStandardError = true;
			steamCMDProc.StartInfo.RedirectStandardOutput = true;
			steamCMDProc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			steamCMDProc.StartInfo.CreateNoWindow = true;
			steamCMDProc.StartInfo.UseShellExecute = true;
			steamCMDProc.EnableRaisingEvents = true; // Investigate?
													 //activeSteamCMD.EnableRaisingEvents = false;
			// activeSteamCMD.OutputDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
			// activeSteamCMD.ErrorDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);

			steamCMDProc.Exited += (sender, e) =>
			{
				activeSteamCMD.Close();
				activeSteamCMD = null;
			};

			steamCMDProc.Start();

			activeSteamCMD = steamCMDProc;

			return activeSteamCMD;
		}

        
    }
}
