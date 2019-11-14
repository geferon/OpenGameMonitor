using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker
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
    public interface IGameHandlerBase
    {
        string Engine { get; }
        string Game { get; }
		bool CanUpdate(Server server);
        Task InitServer(Server server);
		Task<bool> CheckUpdate(Server server);
		Task<bool> UpdateServer(Server server);
		Task<bool> IsOpen(Server server);
		Task CloseServer(Server server);
		Task OpenServer(Server server);

        Task<object> GetServerInfo(Server server);
        Task<object> GetServerPlayers(Server server);

        event EventHandler<ConsoleEventArgs> ConsoleMessage;
        event EventHandler<ConsoleEventArgs> UpdateMessage;
        event EventHandler ServerClosed;
        event EventHandler ServerOpened;
        event EventHandler<ServerUpdateEventArgs> ServerUpdated;
    }

	public class GameHandler
	{
        private readonly Dictionary<string, IGameHandlerBase> gameHandlers = new Dictionary<string, IGameHandlerBase>();
		private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _serviceScope;
        private readonly EventHandlerService _eventHandlerService;

		public GameHandler(ILogger<GameHandler> logger,
            IServiceProvider serviceProvider,
            IServiceScopeFactory serviceScope,
            EventHandlerService eventHandlerService)
		{
			_logger = logger;
            //_serviceProvider = serviceProvider;
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
                Server server = (Server)serverObj;

                if (server.RestartOnClose)
                {
                    await Task.Delay(1000);

                    IGameHandlerBase handler = GetServerHandler(server);
                    await handler.OpenServer(server).ConfigureAwait(false);
                }
            });

            // Init every server and it's config, and check if the server is open and assign the PID and process to it
            using (var scope = _serviceScope.CreateScope())
            using (var db = scope.ServiceProvider.GetRequiredService<MonitorDBContext>())
            {
                List<Server> servers = db.Servers
                    .Where(r => r.PID != null && r.PID != default)
                    .ToList();


                foreach (Server server in servers)
                {
                    IGameHandlerBase handler = GetServerHandler(server);
                    handler.InitServer(server);
                }
            }
        }

        public void RegisterGameHandler<TGameHandler>() where TGameHandler : IGameHandlerBase
        {
            IGameHandlerBase gameHandler = (IGameHandlerBase)ActivatorUtilities.CreateInstance<TGameHandler>(_serviceProvider);

            string identifier;
            try
            {
                identifier = string.Format(CultureInfo.CurrentCulture, "{0}:{1}", gameHandler.Engine, gameHandler.Game);
            }
#pragma warning disable CA1031 // No capture tipos de excepción generales.
            catch
#pragma warning restore CA1031 // No capture tipos de excepción generales.
            {
                identifier = gameHandler.Engine;
            }

            gameHandlers.Add(identifier, gameHandler);

            // Small quirk, will try to change on later updates...
            _eventHandlerService.RegisterHandler("Server:ConsoleMessage", (handler) => gameHandler.ConsoleMessage += handler.Listener);
            _eventHandlerService.RegisterHandler("Server:UpdateMessage", (handler) => gameHandler.UpdateMessage += handler.Listener);
            _eventHandlerService.RegisterHandler("Server:Closed", (handler) => gameHandler.ServerClosed += handler.Listener);
            _eventHandlerService.RegisterHandler("Server:Opened", (handler) => gameHandler.ServerOpened += handler.Listener);


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

            bool enabled = server.Enabled ?? false;

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
