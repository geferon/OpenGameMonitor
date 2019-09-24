using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker
{
	public class ConsoleEventArgs : EventArgs
	{
		public string NewLine { get; set; }
		public bool IsError { get; set; } = false;
	}
	public interface IGameHandlerBase
	{
		string Game { get; }
		bool CanUpdate(Server server);
		Task<bool> CheckUpdate(Server server);
		Task<bool> UpdateServer(Server server);
		Task<bool> IsOpen(Server server);
		Task CloseServer(Server server);
		Task OpenServer(Server server);

		EventHandler ConsoleMessage { get; set; }
		EventHandler UpdateMessage { get; set; }
	}

	public class GameHandler
	{
		private Dictionary<string, IGameHandlerBase> gameHandlers = new Dictionary<string, IGameHandlerBase>();
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger _logger;
		private readonly EventHandlerService _eventHandlerService;

		public GameHandler(IServiceProvider serviceProvider,
			ILogger logger,
			EventHandlerService eventHandlerService)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
			_eventHandlerService = eventHandlerService;
		}


		public void RegisterGameHandlers()
		{
			Type[] typeList = Assembly.GetExecutingAssembly().GetTypes()
				.Where(t => String.Equals(t.Namespace, "OpenGameMonitorWorker.GameHandlers", StringComparison.Ordinal))
				.ToArray();

			foreach (Type type in typeList)
			{
				if (type is IGameHandlerBase)
				{
					IGameHandlerBase gameHandler = (IGameHandlerBase) ActivatorUtilities.CreateInstance(_serviceProvider, type);
					gameHandlers.Add(gameHandler.Game, gameHandler);

                    _eventHandlerService.RegisterHandler("Server:ConsoleMessage", gameHandler.ConsoleMessage);


                    _logger.LogInformation("Registered game handler {0}", gameHandler.Game);
				}
			}
		}

		public IGameHandlerBase GetServerHandler(Server server)
		{
            if (server == null)
                throw new ArgumentNullException(nameof(server));

			string engine = server.Game.Engine;

			IGameHandlerBase handler;
			gameHandlers.TryGetValue(engine, out handler);

			if (handler == null)
			{
				throw new Exception($"Engine {engine} doesn't have a proper handler!");
			}

			return handler;
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

		public async Task<List<Server>> CheckServerUpdates()
		{
			List<Server> outOfDateServers = new List<Server>();

			using (var db = _serviceProvider.GetService<MonitorDBContext>())
			{
				List<Server> servers = db.Servers
					.Where(r => r.Enabled == true)
					.ToList();


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

			if (!server.Enabled)
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
	}
}
