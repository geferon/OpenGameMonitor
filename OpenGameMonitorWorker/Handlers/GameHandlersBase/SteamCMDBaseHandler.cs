using Microsoft.Extensions.DependencyInjection;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker
{
	abstract class SteamCMDBaseHandler : IGameHandlerBase
	{
		protected readonly IServiceProvider _serviceProvider;
        protected readonly IServiceScopeFactory _serviceScopeFactory;
        protected readonly SteamCMDService _steamCMDService;
        protected readonly SteamAPIService _steamAPIService;

		public event EventHandler<ConsoleEventArgs> UpdateMessage;
        public event EventHandler<ServerUpdateEventArgs> ServerUpdated;
		public override event EventHandler<ConsoleEventArgs> ConsoleMessage;
        public override event EventHandler ServerClosed;
        public override event EventHandler ServerOpened;

        public abstract string Engine { get; }
        string IGameHandlerBase.Game => throw new NotImplementedException();

        public SteamCMDBaseHandler(IServiceProvider serviceProvider,
			IServiceScopeFactory serviceScopeFactory,
			SteamCMDService steamCMDServ,
			SteamAPIService steamAPIService)
		{
			_serviceProvider = serviceProvider;
			_serviceScopeFactory = serviceScopeFactory;
			_steamCMDService = steamCMDServ;
			_steamAPIService = steamAPIService;
		}

		private static Task RunAsyncProcess(Process process, bool shouldStart = true)
		{
			var tcs = new TaskCompletionSource<object>();
			process.Exited += (s, e) => tcs.TrySetResult(null);
			if (shouldStart && !process.Start()) tcs.SetException(new Exception("Failed to start process."));
			return tcs.Task;
		}

		public bool CanUpdate(Server server)
		{
			return _steamCMDService.SteamCMDInstalled;
		}

		public async Task<bool> UpdateServer(Server server)
		{
			var serverPath = new DirectoryInfo(server.Path);

			if (!serverPath.Exists)
			{
				serverPath.Create();
			}

			using (var scope = _serviceScopeFactory.CreateScope())
			using (var db = _serviceProvider.GetService<MonitorDBContext>())
			{
				var scopedServices = scope.ServiceProvider;

				StringBuilder outputStringBuilder = new StringBuilder();

                // Params stuff
                List<string[]> parameterBuilder = new List<string[]>
                {
                    new string[] { "+login", "anonymous" }, // TODO: Allow user logins
                    new string[] { "+force_install_dir", $"\"{serverPath.FullName}\"" }
                };

                // Fucking dynamic shit param builder what the fuck
                List<string> updateText = new List<string>
                {
                    server.Game.SteamID.ToString(CultureInfo.InvariantCulture)
                };
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
					var steamCMDProc = _steamCMDService.StartSteamCMD(steamCMDArguments);
					steamCMDProc.OutputDataReceived += (sender, e) =>
					{
                        UpdateMessage?.Invoke(server, new ConsoleEventArgs()
						{
							NewLine = e.Data
						});
					};
					steamCMDProc.ErrorDataReceived += (sender, e) =>
					{
                        UpdateMessage?.Invoke(server, new ConsoleEventArgs()
						{
							NewLine = e.Data,
							IsError = true
						});
					};

					server.UpdatePID = steamCMDProc.Id;

					db.Update(server);

					await steamCMDProc.WaitForExitAsync();

                    server.UpdatePID = null;
                    db.Update(server);

                    ServerUpdated?.Invoke(server, new ServerUpdateEventArgs() { Error = steamCMDProc.ExitCode != 0 });

                    if (steamCMDProc.ExitCode != 0)
                    {
                        throw new Exception();
                    }
				}
				catch
				{
					return false;
				}
			}

			return true;
		}

		public async Task<bool> CheckUpdate(Server server)
		{
			uint appID = server.Game.SteamID;

			string appManifestFile = Path.Combine(server.Path, "SteamApps", string.Format(CultureInfo.InvariantCulture, "appmanifest_{0}.acf", appID));
			if (File.Exists(appManifestFile))
			{
				KeyValues manifestKv;
				using (Stream s = File.OpenRead(appManifestFile))
				{
					manifestKv = new KeyValues(s);
				}

				Dictionary<string, object> appState = (Dictionary<string, object>)manifestKv.Items["AppState"];

				var serverData = await _steamAPIService.GetAppInfo(appID);

				if (appState["buildid"] != serverData.KeyValues["depots"]["branches"][server.Branch]["buildid"])
				{
					return true;
				}
			}

			return false;
		}

		public abstract Task CloseServer(Server server);
		public abstract Task OpenServer(Server server);
		public abstract Task<bool> IsOpen(Server server);
        public abstract Task InitServer(Server server);
        public abstract Task<object> GetServerInfo(Server server);
        public abstract Task<object> GetServerPlayers(Server server);
    }
}

