using Microsoft.Extensions.DependencyInjection;
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

	class GameHandler
	{
		private Dictionary<string, GameHandlerBase> gameHandlers = new Dictionary<string, GameHandlerBase>();
		private readonly IServiceProvider _serviceProvider;

		public GameHandler(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
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
					var gameHandler = (GameHandlerBase) ActivatorUtilities.CreateInstance(_serviceProvider, type);
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

		public async Task CheckServerUpdates()
		{
			using (var db = _serviceProvider.GetService<MonitorDBContext>())
			{
				List<Server> servers = db.Servers
					.Where(r => r.Enabled == true)
					.ToList();

				foreach (Server server in servers)
				{

				}
			}
		}
	}
}
