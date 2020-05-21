using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreRCON;
using CoreRCON.PacketFormats;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenGameMonitorLibraries;
using OpenGameMonitorWorker.Services;
using OpenGameMonitorWorker.Utils;
using Pty.Net;
using SmartFormat;

namespace OpenGameMonitorWorker.Handlers
{
	internal class SourceHandler : SteamCMDBaseHandler
	{
		public override string Engine => "Source";

		public override event EventHandler ServerClosed;
		public override event EventHandler ServerOpened;
		public override event EventHandler<ConsoleEventArgs> ConsoleMessage;

		private readonly ILogger<SourceHandler> _logger;

		private readonly Dictionary<int, Process> serverProcess = new Dictionary<int, Process>();
		//private readonly Dictionary<int, Tuple<Process, IPtyConnection>> serverProcess = new Dictionary<int, Tuple<Process, IPtyConnection>>();

		public SourceHandler(
			IServiceProvider serviceProvider,
			IServiceScopeFactory serviceScopeFactory,
			SteamCMDService steamCMDServ,
			SteamAPIService steamAPIService,
			IConfiguration configuration,
			ILogger<SourceHandler> logger) :
			base(serviceProvider, serviceScopeFactory, steamCMDServ, steamAPIService, configuration)
		{
			_logger = logger;
		}

		private IPEndPoint GetServerEndpoint(Server server)
		{
			var endpoint = new IPEndPoint(
				IPAddress.Parse(GameHandler.GetServerIP(server)),
				server.Port
			);

			return endpoint;
		}

		public override async Task InitServer(Server server)
		{
			if (server.PID != null && server.PID != default)
			{
				try
				{
					Process ps = Process.GetProcessById((int) server.PID);
					/*
					ps.OutputDataReceived += (sender, e) =>
					{
						ConsoleMessage?.Invoke(server, new ConsoleEventArgs()
						{
							NewLine = e.Data
						});
					};
					ps.ErrorDataReceived += (sender, e) =>
					{
						ConsoleMessage?.Invoke(server, new ConsoleEventArgs()
						{
							NewLine = e.Data,
							IsError = true
						});
					};
					*/
					ps.Exited += (sender, e) =>
					{
						//serverProcess[server.Id].Close();
						serverProcess[server.Id].Dispose();
						serverProcess.Remove(server.Id);

						ServerClosed?.Invoke(server, e);
					};
					ps.EnableRaisingEvents = true;

					//ps.BeginOutputReadLine();
					//ps.BeginErrorReadLine();

					//serverProcess[server.Id] = new Tuple<Process, IPtyConnection>(ps, null);
					serverProcess[server.Id] = ps;
				}
				catch
				{
					server.PID = default;
					using (var scope = _serviceScopeFactory.CreateScope())
					using (var db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>())
					{
						db.Update(server);
						await db.SaveChangesAsync();
					}

					await OpenServer(server);
				}
			}
		}

		public override string GetServerExecutable(Server server)
		{
			var postFix = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? ".exe" : "";
			return $"srcds{postFix}";
		}

		public override async Task<bool> IsOpen(Server server)
		{
			return serverProcess.ContainsKey(server.Id) && !serverProcess[server.Id].HasExited;
		}

		public override async Task OpenServer(Server server)
		{
			if (serverProcess.ContainsKey(server.Id) && !serverProcess[server.Id].HasExited)
			{
				return; // Server is open?
			}

			var proc = new Process();
			proc.StartInfo.FileName = Path.Combine(server.Path, server.Executable);
			proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(Path.Combine(server.Path, server.Executable));

			/*var options = new PtyOptions()
			{
				Name = $"srcds Server {server.Id}",
				App = Path.Combine(server.Path, server.Executable),
				CommandLine = (this as IGameHandlerBase).ServerStartParameters(server).ToArray(),
				VerbatimCommandLine = true,
				Cwd = Path.GetDirectoryName(Path.Combine(server.Path, server.Executable)),
				Cols = 100,
				Rows = 80,
				Environment = server.EnvironmentVariables.ToDictionary(v => v.Key, v => v.Value)
			};

			IPtyConnection proc = await PtyProvider.SpawnAsync(options, new System.Threading.CancellationToken());
			var procObj = Process.GetProcessById(proc.Pid);
			procObj.PriorityClass = server.ProcessPriority;
			*/


			proc.StartInfo.Arguments = (this as IGameHandlerBase).ServerStartParametersFormed(server);

			proc.StartInfo.RedirectStandardError = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.UseShellExecute = false;
			proc.EnableRaisingEvents = true; // Investigate?
			//activeSteamCMD.EnableRaisingEvents = false;
			// activeSteamCMD.OutputDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
			// activeSteamCMD.ErrorDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);

			proc.OutputDataReceived += (sender, e) =>
			{
				_logger.LogDebug("Log received from server {0}: {1}", server.Id, e.Data);
				ConsoleMessage?.Invoke(server, new ConsoleEventArgs()
				{
					NewLine = e.Data
				});
			};
			proc.ErrorDataReceived += (sender, e) =>
			{
				_logger.LogError("Error received from server {0}", server.Id);
				ConsoleMessage?.Invoke(server, new ConsoleEventArgs()
				{
					NewLine = e.Data,
					IsError = true
				});
			};

			/*
			var processExitedCTS = new CancellationTokenSource();
			var processExitedCToken = processExitedCTS.Token;
			var processExitedTcs = new TaskCompletionSource<uint>();
			*/

			proc.Exited += (sender, e) =>
			{
				//serverProcess[server.Id].Close();
				serverProcess.Remove(server.Id);

				//processExitedTcs.TrySetResult((uint)proc.ExitCode);
				//processExitedCTS.Cancel();

				ServerClosed?.Invoke(server, e);

				proc.Dispose();
			};

			/*
			var backgroundTask = Task.Run(async () =>
			{
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
						var bytesRead = await proc.ReaderStream.ReadAsync(byteBuffer, 0, byteBuffer.Length).WithCancellation(processExitedCToken);
						if (bytesRead == 0)
							break;

						int charLen = decoder.GetChars(byteBuffer, 0, bytesRead, charBuffer, 0);
						sb!.Append(charBuffer, 0, charLen);

						MonitorUtils.MoveLinesFromStringBuilderToMessageQueue(ref currentLinePos, ref bLastCarriageReturn, sb,
							(line) => ConsoleMessage?.Invoke(server, new ConsoleEventArgs()
							{
								NewLine = line
							}));
					}
					catch (OperationCanceledException)
					{
						break;
					}
				}
			});
			*/

			proc.Start();
			proc.BeginOutputReadLine();
			proc.BeginErrorReadLine();

			// Has to be set AFTER it starts
			proc.PriorityClass = server.ProcessPriority;

			//serverProcess[server.Id] = new Tuple<Process, IPtyConnection>(procObj, proc);
			serverProcess[server.Id] = proc;

			ServerOpened?.Invoke(server, new EventArgs());


			using (var scope = _serviceScopeFactory.CreateScope())
			using (var db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>())
			{
				server.PID = proc.Id;

				db.Update(server);
				await db.SaveChangesAsync();
			}
		}

		public async override Task CloseServer(Server server)
		{
			var endpoint = GetServerEndpoint(server);
			var config = new SourceConfigParser(server);
			Process serverProc = serverProcess[server.Id];

			bool succesfulShutdown = false;

			if (server.Graceful)
			{
				string rconPass = config.Get("rcon_password");

				if (!String.IsNullOrEmpty(rconPass)) {
					try
					{
						using (var rcon = new RCON(endpoint, rconPass))
						{
							await rcon.ConnectAsync();

							await rcon.SendCommandAsync("quit");

							succesfulShutdown = true;
						}
					}
					catch (TimeoutException) { }
					catch (AuthenticationException) { }
				}

				if (succesfulShutdown)
				{
					using (var cts = new CancellationTokenSource())
					{
						cts.CancelAfter(30 * 1000);

						await serverProc.WaitForExitAsync(cts.Token);

						if (!serverProc.HasExited)
						{
							succesfulShutdown = false;
						}
					}
				}
			}

			if (!succesfulShutdown)
			{
				bool success = serverProc.CloseMainWindow();

				if (success)
				{
					using (var cts = new CancellationTokenSource())
					{
						cts.CancelAfter(10 * 1000);

						await serverProc.WaitForExitAsync(cts.Token);
					}
				}

				if (!serverProc.HasExited)
				{
					serverProc.Kill();
				}
			}

			
		}

		public override async Task<object> GetServerInfo(Server server)
		{
			var endpoint = GetServerEndpoint(server);

			var info = await ServerQuery.Info(endpoint, ServerQuery.ServerType.Source) as SourceQueryInfo;

			return info;
		}
		
		public override async Task<object> GetServerPlayers(Server server)
		{
			var endpoint = GetServerEndpoint(server);

			var players = await ServerQuery.Players(endpoint);

			return players;
		}
	}
}
