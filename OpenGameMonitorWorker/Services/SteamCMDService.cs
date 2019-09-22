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
        private readonly SteamAPIService _steamAPIService;

        public IBackgroundTaskQueue Queue { get; }

        //private Dictionary<int, Process> _processes = new Dictionary<int, Process>();
        private Process activeSteamCMD;
        private List<Server> serversToUpdate = new List<Server>();

        public SteamCMDService(ILogger<SteamCMDService> logger,
            IBackgroundTaskQueue queue,
            IServiceScopeFactory serviceScopeFactory,
            SteamAPIService steamAPIService)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            Queue = queue;
            _steamAPIService = steamAPIService;

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
                    };
                }
            }
        }

        private static Task RunAsyncProcess(Process process, bool shouldStart = true)
        {
            var tcs = new TaskCompletionSource<object>();
            process.Exited += (s, e) => tcs.TrySetResult(null);
            if (shouldStart && !process.Start()) tcs.SetException(new Exception("Failed to start process."));
            return tcs.Task;
        }

        public void UpdateServer(Server server)
        {
            if (!SteamCMDInstalled || (activeSteamCMD != null || !activeSteamCMD.HasExited))
            {
                serversToUpdate.Add(server);
                return;
            }

            var serverPath = new DirectoryInfo(server.Path);

            if (!serverPath.Exists)
            {
                serverPath.Create();
            }

            Queue.QueueBackgroundWorkItem(async token =>
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;

                    activeSteamCMD = new Process();
                    StringBuilder outputStringBuilder = new StringBuilder();

                    // Params stuff
                    List<string[]> parameterBuilder = new List<string[]>();
                    parameterBuilder.Add(new string[] { "+login", "anonymous" }); // TODO: Allow user logins
                    parameterBuilder.Add(new string[] { "+force_install_dir", $"\"{serverPath.FullName}\"" });

                    // Fucking dynamic shit param builder what the fuck
                    List<string> updateText = new List<string>();
                    updateText.Add(server.Game.SteamID.ToString());
                    if (!String.IsNullOrEmpty(server.Branch))
                    {
                        updateText.Add("-beta");
                        updateText.Add(server.Branch);
                        if (!String.IsNullOrEmpty(server.BranchPassword))
                        {
                            updateText.Add("-betapassword");
                            updateText.Add(server.BranchPassword);
                        }

                        // TODO: Should add 'validate' param?
                    }

                    StringBuilder updateParams = new StringBuilder();
                    if (updateText.Count > 1) updateParams.Append("\"");
                    updateParams.Append(String.Join(" ", updateParams));
                    if (updateText.Count > 1) updateParams.Append("\"");

                    parameterBuilder.Add(new string[] { "+app_update", updateParams.ToString() });

                    parameterBuilder.Add(new string[] { "+quit" });

                    string steamCMDArguments = String.Join(" ", parameterBuilder.Select(param => String.Join(" ", param)).ToArray());

                    // Start process
                    try
                    {
                        activeSteamCMD.StartInfo.FileName = SteamCMDExe.FullName;
                        activeSteamCMD.StartInfo.WorkingDirectory = SteamCMDFolder.FullName;
                        activeSteamCMD.StartInfo.Arguments = steamCMDArguments;

                        activeSteamCMD.StartInfo.RedirectStandardError = true;
                        activeSteamCMD.StartInfo.RedirectStandardOutput = true;
                        activeSteamCMD.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        activeSteamCMD.StartInfo.CreateNoWindow = true;
                        activeSteamCMD.StartInfo.UseShellExecute = true;
                        activeSteamCMD.EnableRaisingEvents = true; // Investigate?
                        //activeSteamCMD.EnableRaisingEvents = false;
                        activeSteamCMD.OutputDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
                        activeSteamCMD.ErrorDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);

                        activeSteamCMD.Start();

                        server.UpdatePID = activeSteamCMD.Id;

                        //activeSteamCMD.WaitForExit();
                        await RunAsyncProcess(activeSteamCMD, false);

                        if (activeSteamCMD.ExitCode != 0)
                        {
                            // TODO: Error on update?
                        }

                    }
                    finally
                    {
                        activeSteamCMD.Close();

                        activeSteamCMD = null;

                        // Move on and start the next one?
                        serversToUpdate.Remove(server);

                        if (!token.IsCancellationRequested)
                        {
                            if (serversToUpdate.Count > 0)
                            {
                                UpdateServer(serversToUpdate[0]);
                            }
                        }
                    }
                }
            });
        }

        public async Task<bool> CheckUpdate(Server server)
        {
            uint appID = server.Game.SteamID;

            string appManifestFile = Path.Combine(server.Path, "SteamApps", String.Format("appmanifest_{0}.acf", appID));
            if (File.Exists(appManifestFile))
            {
                KeyValues manifestKv;
                using (Stream s = File.OpenRead(appManifestFile))
                {
                    manifestKv = new KeyValues(s);
                }

                Dictionary<string, object> appState = (Dictionary<string, object>) manifestKv.Items["AppState"];

                var serverData = await _steamAPIService.GetAppInfo(appID);

                if (appState["buildid"] != serverData.KeyValues["depots"]["branches"][server.Branch]["buildid"])
                {
                    return true;
                }
            }

            return false;
        }
    }
}
