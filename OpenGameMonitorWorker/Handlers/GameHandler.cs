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
	public interface GameHandlerBase
	{
		string Game { get; }
		bool CanUpdate(Server server);
		Task<bool> CheckUpdate(Server server);
		Task<bool> UpdateServer(Server server);
		Task CloseServer(Server server);
		Task OpenServer(Server server);

		event EventHandler ConsoleMessage;
		event EventHandler UpdateMessage;
	}

	public class GameHandler
	{
		private Dictionary<string, GameHandlerBase> gameHandlers = new Dictionary<string, GameHandlerBase>();
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger _logger;

		public GameHandler(IServiceProvider serviceProvider,
			ILogger logger)
		{
			_serviceProvider = serviceProvider;
			logger = _logger;
		}


		public void RegisterGameHandlers()
		{
			Type[] typeList = Assembly.GetExecutingAssembly().GetTypes()
				.Where(t => String.Equals(t.Namespace, "OpenGameMonitorWorker.GameHandlers", StringComparison.Ordinal))
				.ToArray();

			foreach (Type type in typeList)
			{
				if (type is GameHandlerBase)
				{
					GameHandlerBase gameHandler = (GameHandlerBase) ActivatorUtilities.CreateInstance(_serviceProvider, type);
					gameHandlers.Add(gameHandler.Game, gameHandler);
				}
			}
		}

		public GameHandlerBase GetServerHandler(Server server)
		{
			string engine = server.Game.Engine;

			GameHandlerBase handler;
			gameHandlers.TryGetValue(engine, out handler);

			if (handler == null)
			{
				throw new Exception($"Engine {engine} doesn't have a proper handler!");
			}

			return handler;
		}

		public async Task UpdateServer(Server server)
		{
			try
			{
				GameHandlerBase handler = GetServerHandler(server);

				if (handler.CanUpdate(server))
				{
					await handler.UpdateServer(server);
				}
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
						GameHandlerBase handler = GetServerHandler(server);

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
	}
}
