using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenGameMonitorLibraries;
using OpenGameMonitorWorker.Services;
using Pty.Net;
using SmartFormat;
using SmartFormat.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker.Handlers
{
	public class CustomDataReceivedEventArgs : EventArgs
	{
		private readonly string _data;
		public CustomDataReceivedEventArgs(string data)
		{
			_data = data;
		}

		public string Data { get => _data; }
	}

	abstract class SteamCMDBaseHandler : IGameHandlerBase
	{
		protected readonly IServiceProvider _serviceProvider;
		private readonly ILogger<SteamCMDBaseHandler> _logger;
		protected readonly IServiceScopeFactory _serviceScopeFactory;
		protected readonly SteamCMDService _steamCMDService;
		protected readonly SteamAPIService _steamAPIService;
		private readonly IConfiguration _configuration;

		public abstract string GetServerExecutable(Server server);

		public event EventHandler<ConsoleEventArgs> UpdateMessage;
		public abstract event EventHandler<ConsoleEventArgs> ConsoleMessage;
		public abstract event EventHandler ServerClosed;
		public abstract event EventHandler ServerOpened;
		public event EventHandler ServerUpdateStart;
		public event EventHandler<ServerUpdateEventArgs> ServerUpdated;
		public event EventHandler<ServerUpdateProgressEventArgs> UpdateProgress;

		public abstract string Engine { get; }
		string IGameHandlerBase.Game => throw new NotImplementedException();

		public SteamCMDBaseHandler(IServiceProvider serviceProvider,
			IServiceScopeFactory serviceScopeFactory,
			SteamCMDService steamCMDServ,
			SteamAPIService steamAPIService,
			IConfiguration configuration)
		{
			_serviceProvider = serviceProvider;
			_serviceScopeFactory = serviceScopeFactory;
			_steamCMDService = steamCMDServ;
			_steamAPIService = steamAPIService;
			_configuration = configuration;

			_logger = _serviceProvider.GetRequiredService<ILogger<SteamCMDBaseHandler>>();
		}

		private static Task RunAsyncProcess(Process process, bool shouldStart = true)
		{
			var tcs = new TaskCompletionSource<object>();
			process.Exited += (s, e) => tcs.TrySetResult(null);
			if (shouldStart && !process.Start()) tcs.SetException(new Exception("Failed to start process."));
			return tcs.Task;
		}



		public async Task<bool> InitialInstall(Server server)
		{
			if (String.IsNullOrEmpty(server.Path))
			{
				var pathConfig = _configuration["MonitorDBConfig:DefaultInstallDir"];
				var serverFolder = _configuration["MonitorDBConfig:DefaultServerDir"];
				if (String.IsNullOrEmpty(pathConfig) || String.IsNullOrEmpty(serverFolder))
				{
					throw new Exception("The configuration is missing the installation path!");
				}

				var serverRootPath = Path.Join(pathConfig, Smart.Format(serverFolder, (this as IGameHandlerBase).GetParameters(server)));
				/*if (_configuration.GetValue<bool>("MonitorDBConfig:InstallSeparateGameDirs", true))
				{
					serverRootPath = Path.Join(serverRootPath, server.Game.Id);
				}*/

				server.Path = serverRootPath;
				server.Executable = this.GetServerExecutable(server);
			}

			return await this.UpdateServer(server);
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
			using (var db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>())
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
					server.Game.SteamID?.ToString(CultureInfo.InvariantCulture)
				};
				if (!String.IsNullOrEmpty(server.Branch) && server.Branch != "public")
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

				// app_update param builder
				StringBuilder updateParams = new StringBuilder();
				if (updateText.Count > 1) updateParams.Append("\"");
				updateParams.Append(String.Join(" ", updateText));
				if (updateText.Count > 1) updateParams.Append("\"");

				parameterBuilder.Add(new string[] { "+app_update", updateParams.ToString() });

				parameterBuilder.Add(new string[] { "+quit" });

				string steamCMDArguments = String.Join(" ", parameterBuilder.Select(param => String.Join(" ", param)).ToArray());


				_logger.LogInformation("Starting SteamCMD with arguments: {0}", steamCMDArguments);


				// Init proccess listeners

				IPtyConnection steamCMDProc = null;

				var downloadProgressRegex = @"Update state \((.+?)\) downloading, progress: ([\d.]+) \((\d*) \/ (\d*)\)";
				CultureInfo ci = (CultureInfo) CultureInfo.CurrentCulture.Clone();
				ci.NumberFormat.CurrencyDecimalSeparator = ".";

				//DataReceivedEventHandler outputEventHandler = (sender, e) =>
				EventHandler outputEventHandler = (sender, e2) =>
				{
					var e = (CustomDataReceivedEventArgs)e2;
					_logger.LogDebug("SteamCMD Log {0} - {1}: {2}", server.Id, steamCMDProc.Pid, e.Data);

					UpdateMessage?.Invoke(server, new ConsoleEventArgs()
					{
						NewLine = e.Data
					});

					if (!string.IsNullOrEmpty(e.Data))
					{
						Match match = Regex.Match(e.Data, downloadProgressRegex, RegexOptions.IgnoreCase);
						if (match.Success)
						{
							UpdateProgress?.Invoke(server, new ServerUpdateProgressEventArgs()
							{
								Progress = float.Parse(match.Groups[2].Value, NumberStyles.Any, ci)
							});
						}
					}
				};

				//DataReceivedEventHandler errorEventHandler = (sender, e) =>
				EventHandler errorEventHandler = (sender, e2) =>
				{
					var e = (CustomDataReceivedEventArgs)e2;
					_logger.LogWarning("SteamCMD emitted {0} - {1}: {2}", server.Id, steamCMDProc.Pid, e.Data);

					UpdateMessage?.Invoke(server, new ConsoleEventArgs()
					{
						NewLine = e.Data,
						IsError = true
					});
				};

				// Start process
				var hasErrored = false;
				var processExitedCTS = new CancellationTokenSource();
				var processExitedCToken = processExitedCTS.Token;
				try
				{
					steamCMDProc = await _steamCMDService.CreateSteamCMD(steamCMDArguments);
					//steamCMDProc.OutputDataReceived += outputEventHandler;
					//steamCMDProc.ErrorDataReceived += errorEventHandler;

					//steamCMDProc.Start();
					//steamCMDProc.BeginOutputReadLine();
					//steamCMDProc.BeginErrorReadLine();

					// Exit event
					var processExitedTcs = new TaskCompletionSource<uint>();
					steamCMDProc.ProcessExited += (sender, e) =>
					{
						processExitedTcs.TrySetResult((uint)steamCMDProc.ExitCode);
						processExitedCTS.Cancel();
						//steamCMDProc.ReaderStream.Close();
						//steamCMDProc.ReaderStream.Dispose();
					};

					server.UpdatePID = steamCMDProc.Pid;

					db.Update(server);
					await db.SaveChangesAsync();

					ServerUpdateStart?.Invoke(server, new EventArgs()); // Event


					// Reading console
					var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
					var decoder = encoding.GetDecoder();
					var sb = new StringBuilder();

					var byteBuffer = new byte[1024];
					var maxCharsPerBuffer = encoding.GetMaxCharCount(1024);
					var charBuffer = new char[maxCharsPerBuffer];

					int currentLinePos = 0;
					bool bLastCarriageReturn = false;
					while (!processExitedTcs.Task.IsCompleted)
					{
						try
						{
							var bytesRead = await steamCMDProc.ReaderStream.ReadAsync(byteBuffer, 0, byteBuffer.Length).WithCancellation(processExitedCToken);
							if (bytesRead == 0)
								break;

							int charLen = decoder.GetChars(byteBuffer, 0, bytesRead, charBuffer, 0);
							sb!.Append(charBuffer, 0, charLen);

							MonitorUtils.MoveLinesFromStringBuilderToMessageQueue(ref currentLinePos, ref bLastCarriageReturn, sb,
								(line) => outputEventHandler?.Invoke(steamCMDProc, new CustomDataReceivedEventArgs(line)));
						}
						catch (OperationCanceledException)
						{
							break;
						}
					}

					if (!processExitedTcs.Task.IsCompleted) await processExitedTcs.Task;


					/*
					var readStdOut = Task.Run(async delegate
					{
						string? line = null;
						//while ((line = await steamCMDProc.StandardOutput.ReadLineAsync()) != null)
						while (steamCMDProc.StandardOutput.Peek() > -1)
						{
							line = steamCMDProc.StandardOutput.ReadLine();
							_logger.LogWarning("Read line! {0}", line);
							outputEventHandler.Invoke(steamCMDProc, new CustomDataReceivedEventArgs(line));
						}
					});
					var readErrOut = Task.Run(async delegate
					{
						string? line = null;
						//while ((line = await steamCMDProc.StandardError.ReadLineAsync()) != null)
						while (steamCMDProc.StandardError.Peek() > -1)
						{
							line = steamCMDProc.StandardError.ReadLine();
							errorEventHandler.Invoke(steamCMDProc, new CustomDataReceivedEventArgs(line));
						}
					});

					await Task.WhenAll(readStdOut, readErrOut);
					*/

					//await steamCMDProc.WaitForExitAsync();
					//steamCMDProc.WaitForExit();

					//steamCMDProc.CancelOutputRead();
					//steamCMDProc.CancelErrorRead();

					try
					{
						if (steamCMDProc.ExitCode != 0)
						{
							hasErrored = true;
							throw new Exception();
						}
					}
					catch (InvalidOperationException err)
					{
						// Do nothing, for some fucking reason it seems it can't get the exit code
					}
				}
				catch (Exception err)
				{
					_logger.LogError(err, "There has been an error while updating the server via SteamCMD");
					return false;
				}
				finally
				{
					_logger.LogInformation("Update finished for server {0}!", server.Id);


					ServerUpdated?.Invoke(server, new ServerUpdateEventArgs() { Error = hasErrored });

					server.UpdatePID = null;
					db.Update(server);

					await db.SaveChangesAsync();

					if (steamCMDProc != null)
					{
						//steamCMDProc.OutputDataReceived -= outputEventHandler;
						//steamCMDProc.ErrorDataReceived -= errorEventHandler;
						steamCMDProc.Dispose();
					}
					processExitedCTS.Dispose();
				}
			}

			return true;
		}

		public async Task<bool> CheckUpdate(Server server)
		{
			if (!server.Game.SteamID.HasValue)
				throw new Exception("Server game with SteamCMD Handler has no SteamID assigned!");

			uint appID = server.Game.SteamID.GetValueOrDefault();

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

