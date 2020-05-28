using CoreRCON.PacketFormats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker.Handlers
{
	public class ServerUpdateEventArgs : EventArgs
	{
		public bool Error { get; set; } = false;
	}
	public class ConsoleEventArgs : EventArgs
	{
		public string NewLine { get; set; }
		public bool IsError { get; set; } = false;
	}
	public class ServerUpdateProgressEventArgs : EventArgs
	{
		public float Progress { get; set; } // From 0 to 100
	}
	public class ServerParameters
	{
		public string GameID { get; set; }
		public string GameName { get; set; }
		public uint? GameSteamID { get; set; }
		public int ServerID { get; set; }
		public string ServerName { get; set; }
		public string ServerIP { get; set; }
		public int ServerPort { get; set; }
	}
	public class ServerInformation
	{
		public string Name { get; set; }
		public string Map { get; set; }
		public byte Players { get; set; }
		public byte MaxPlayers { get; set; }
	}
	public interface IGameHandlerBase
	{
		bool CanUpdate(Server server);
		Task InitServer(Server server);
		Task<bool> CheckUpdate(Server server);
		Task<bool> UpdateServer(Server server);
		Task<bool> InitialInstall(Server server);
		Task<bool> IsOpen(Server server);
		Task CloseServer(Server server);
		Task OpenServer(Server server);

		Task<ServerInformation> GetServerInfo(Server server);
		Task<ServerQueryPlayer[]> GetServerPlayers(Server server);

		event EventHandler<ConsoleEventArgs> ConsoleMessage;
		event EventHandler<ConsoleEventArgs> UpdateMessage;
		event EventHandler<ServerUpdateProgressEventArgs> UpdateProgress;
		event EventHandler ServerClosed;
		event EventHandler ServerOpened;
		event EventHandler ServerUpdateStart;
		event EventHandler<ServerUpdateEventArgs> ServerUpdated;

		private static string GetLocalIP()
		{
			var hostname = Dns.GetHostName();
			var ipEntry = Dns.GetHostEntry(hostname);
			var addr = ipEntry.AddressList;
			return addr.First().ToString();
		}

		ServerParameters GetParameters(Server server)
		{
			return new ServerParameters()
			{
				GameID = server.Game.Id,
				GameName = server.Game.Name,
				GameSteamID = server.Game.SteamID,
				ServerID = server.Id,
				ServerName = server.Name,
				ServerIP = server?.IP ?? GetLocalIP(),
				ServerPort = server.Port
			};
		}

		List<string> ServerStartParameters(Server server)
		{
			var parms = GetParameters(server);
			List<string> startParameters = new List<string>();
			if (!String.IsNullOrWhiteSpace(server.StartParamsHidden)) startParameters.Add(SmartFormat.Smart.Format(server.StartParamsHidden, parms));
			if (!String.IsNullOrWhiteSpace(server.StartParams)) startParameters.Add(SmartFormat.Smart.Format(server.StartParams, parms));

			return startParameters;
		}

		string ServerStartParametersFormed(Server server)
		{
			return String.Join(" ", ServerStartParameters(server));
		}
	}

	public class GameHandlerAttribute : System.Attribute
	{
		public string engine;
		public string game = null;

		public GameHandlerAttribute(string engine)
		{
			this.engine = engine;
		}

		public GameHandlerAttribute(string engine, string game) : this(engine)
		{
			this.game = game;
		}
	}


	public class GameHandlerService
	{
		private readonly Dictionary<string, IGameHandlerBase> gameHandlers = new Dictionary<string, IGameHandlerBase>();
		private readonly ILogger _logger;
		private readonly IServiceProvider _serviceProvider;
		private readonly IServiceScopeFactory _serviceScope;
		private readonly EventHandlerService _eventHandlerService;

		public GameHandlerService(ILogger<GameHandlerService> logger,
			IServiceProvider serviceProvider,
			IServiceScopeFactory serviceScope,
			EventHandlerService eventHandlerService)
		{
			_logger = logger;
			_serviceProvider = serviceProvider;
			_serviceScope = serviceScope;
			_eventHandlerService = eventHandlerService;

			RegisterGameHandlers();
			InitGameHandlerBase();
		}


		public void RegisterGameHandlers()
		{
			RegisterGameHandler<SourceHandler>();
		}

		public void InitGameHandlerBase()
		{
			// Listen to Server:Closed and check if the server should be restarted upon close
			_eventHandlerService.ListenForEvent("Server:Closed", async (Object serverObj, EventArgs e) =>
			{
				Server serverOrig = (Server)serverObj;

				using (var scope = _serviceScope.CreateScope())
				using (var db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>())
				{
					var currentServer = db.Servers
						.Include(s => s.Game)
						.Include(s => s.Group)
						.Include(s => s.Owner)
						.FirstOrDefault(s => s.Id == serverOrig.Id);

					if (currentServer.RestartOnClose && currentServer.PID != null) // If server has to restart and it crashed
					{
						await Task.Delay(1000);

						IGameHandlerBase handler = GetServerHandler(currentServer);
						await handler.OpenServer(currentServer);
					}
				}
			});

			// Init every server and it's config, and check if the server is open and assign the PID and process to it
			using (var scope = _serviceScope.CreateScope())
			using (var db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>())
			{
				List<Server> servers = db.Servers
					.Where(r => r.PID != null && r.PID != default)
					.Include(s => s.Game)
					.Include(s => s.Owner)
					.Include(s => s.Group)
					.ToList();


				foreach (Server server in servers)
				{
					IGameHandlerBase handler = GetServerHandler(server);
					handler.InitServer(server);
				}

				// Check updates
				List<Server> serversUpdates = db.Servers
					.Where(r => r.UpdatePID != null && r.UpdatePID != default)
					.Include(s => s.Game)
					.Include(s => s.Owner)
					.Include(s => s.Group)
					.ToList();

				// TODO: Attach it if it exists (for events and such)!
				foreach (Server server in serversUpdates)
				{
					
					if (!Process.GetProcesses().Any(x => x.Id == server.UpdatePID))
					{
						server.UpdatePID = null;
					}
				}

				db.SaveChanges();
			}
		}

		public void RegisterGameHandler<TGameHandler>() where TGameHandler : IGameHandlerBase
		{
			var gameHandlerProperties = Attribute.GetCustomAttributes(typeof(TGameHandler)).First(a => a is GameHandlerAttribute) as GameHandlerAttribute;
			if (gameHandlerProperties == null)
			{
				throw new ArgumentException("The GameHandler that was supplied has no GameHandler attribute assigned to it!");
			}

			IGameHandlerBase gameHandler = (IGameHandlerBase)ActivatorUtilities.CreateInstance<TGameHandler>(_serviceProvider);

			string identifier;

			if (!String.IsNullOrEmpty(gameHandlerProperties.game))
			{
				try
				{
					identifier = string.Format(CultureInfo.CurrentCulture, "{0}:{1}", gameHandlerProperties.engine, gameHandlerProperties.game);
				}
#pragma warning disable CA1031 // No capture tipos de excepción generales.
				catch
#pragma warning restore CA1031 // No capture tipos de excepción generales.
				{
					identifier = gameHandlerProperties.engine;
				}
			}
			else
			{
				identifier = gameHandlerProperties.engine;
			}

			gameHandlers.Add(identifier, gameHandler);

			// Small quirk, will try to change on later updates...
			_eventHandlerService.RegisterHandler("Server:ConsoleMessage", (handler) => gameHandler.ConsoleMessage += handler.Listener);
			_eventHandlerService.RegisterHandler("Server:UpdateMessage", (handler) => gameHandler.UpdateMessage += handler.Listener);
			_eventHandlerService.RegisterHandler("Server:UpdateProgress", (handler) => gameHandler.UpdateProgress += handler.Listener);
			_eventHandlerService.RegisterHandler("Server:Closed", (handler) => gameHandler.ServerClosed += handler.Listener);
			_eventHandlerService.RegisterHandler("Server:Opened", (handler) => gameHandler.ServerOpened += handler.Listener);
			_eventHandlerService.RegisterHandler("Server:UpdateStart", (handler) => gameHandler.ServerUpdateStart += handler.Listener);
			_eventHandlerService.RegisterHandler("Server:Updated", (handler) => gameHandler.ServerUpdated += handler.Listener);


			_logger.LogInformation("Registered game handler {0}", identifier);
		}

		public IGameHandlerBase GetServerHandler(Server server)
		{
			if (server == null)
				throw new ArgumentNullException(nameof(server));

			string engine = server.Game.Engine;
			string game = server.Game.Id;

			IGameHandlerBase handler;

			gameHandlers.TryGetValue(String.Format(CultureInfo.InvariantCulture, "{0}:{1}", engine, game), out handler);

			if (handler == null)
				gameHandlers.TryGetValue(engine, out handler);

			if (handler == null)
				throw new Exception($"Engine {engine} doesn't have a proper handler!");

			return handler;
		}

		public async Task InstallServer(Server server)
		{
			IGameHandlerBase handler = GetServerHandler(server);

			if (!String.IsNullOrEmpty(server.Path) && File.Exists(server.Path))
			{
				throw new Exception("Can't install the server, it already has been installed!");
			}

			await handler.InitialInstall(server);
		}

		public async Task UpdateServer(Server server)
		{
			IGameHandlerBase handler = GetServerHandler(server);

			if (await handler.IsOpen(server))
			{
				throw new Exception("Can't update the server while it's open");
			}

			if (handler.CanUpdate(server))
			{
				await handler.UpdateServer(server);
			}
		}

		public async Task<bool> IsServerOpen(Server server)
		{
			IGameHandlerBase handler = GetServerHandler(server);

			return await handler.IsOpen(server);
		}

		public async Task<List<Server>> CheckServerUpdates()
		{
			List<Server> outOfDateServers = new List<Server>();

			using (var scope = _serviceScope.CreateScope())
			using (var db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>())
			{
				List<Server> servers = await db.Servers
					.Where(r => r.Enabled == true && r.ProcessStatus != ServerProcessStatus.Updating)
					.ToListAsync();


				foreach (Server server in servers)
				{
					try
					{
						IGameHandlerBase handler = GetServerHandler(server);

						if (handler.CanUpdate(server))
						{
							//await handler.UpdateServer(server);
							bool needsUpdate = await handler.CheckUpdate(server);

							if (needsUpdate)
							{
								outOfDateServers.Add(server);
							}
						}
					}
					catch (Exception err)
					{
						_logger.LogError("There has been an error while trying to update the server {0}! {1}", server.Id, err.Message);
					}
				}
			}

			return outOfDateServers;
		}

		public async Task StartServer(Server server)
		{
			if (server == null)
				throw new ArgumentNullException(nameof(server));

			bool enabled = server.Enabled;

			if (!enabled)
			{
				throw new Exception("The server can't be started! It's disabled!");
			}

			IGameHandlerBase handler = GetServerHandler(server);

			if (await handler.IsOpen(server))
			{
				throw new Exception("Can't open an already opened server");
			}

			await handler.OpenServer(server);
		}

		public async Task<ServerInformation> GetServerInfo(Server server)
		{
			if (server == null)
				throw new ArgumentNullException(nameof(server));

			IGameHandlerBase handler = GetServerHandler(server);

			if (!await handler.IsOpen(server))
			{
				throw new Exception("Can't get the status of a closed server");
			}

			return await handler.GetServerInfo(server);
		}

		public async Task<ServerQueryPlayer[]> GetServerPlayers(Server server)
		{
			if (server == null)
				throw new ArgumentNullException(nameof(server));

			IGameHandlerBase handler = GetServerHandler(server);

			if (!await handler.IsOpen(server))
			{
				throw new Exception("Can't get the status of a closed server");
			}

			return await handler.GetServerPlayers(server);
		}

		public async Task CloseServer(Server server)
		{
			// This is stupid, it should be able to be closed even if it's disabled
			/*
			if (!server.Enabled)
			{
				throw new Exception("The server can't be stopped! It's disabled!");
			}
			*/

			IGameHandlerBase handler = GetServerHandler(server);

			await handler.CloseServer(server);
		}

		public static string GetServerIP(Server server)
		{
			if (server == null)
				throw new ArgumentNullException(nameof(server));

			if (!String.IsNullOrEmpty(server.IP))
			{
				return server.IP;
			}

			return IPAddress.Loopback.ToString();
		}
	}
}
