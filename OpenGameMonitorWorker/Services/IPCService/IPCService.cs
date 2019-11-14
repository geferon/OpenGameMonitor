using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
//using JKang.IpcServiceFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenGameMonitorLibraries;
using Xeeny.Api.Server;
using Xeeny.Connections;
using Xeeny.Dispatching;
using Xeeny.Server;

namespace OpenGameMonitorWorker
{
    public class IPCService : BackgroundService
    {
        private readonly ILogger<IPCService> _logger;
        //private readonly IServiceScope _serviceScope;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        private ServiceHost<MonitorComsService> service;
        //public MonitorComsService serviceInstance;
        private MonitorComsService serviceInstance;

        public IPCService(ILogger<IPCService> logger,
            //IServiceScope serviceScope,
            IServiceProvider serviceProvider,
            IConfiguration config)
        {
            _logger = logger;
            //_serviceScope = serviceScope;
            _serviceProvider = serviceProvider;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //await new IpcServiceHostBuilder(_serviceScope.ServiceProvider)
            //    .AddNamedPipeEndpoint<IMonitorComsInterface>(name: "OpenGameMonitor", pipeName: "Server")
            //    .Build()
            //    .RunAsync(stoppingToken);

            //serviceInstance = ActivatorUtilities.CreateInstance<MonitorComsService>(_serviceProvider);

            var ip = _config.GetValue<String>("MonitorSettings:Address", "localhost");
            var port = _config.GetValue<int>("MonitorSettings:Port", 5001);
            var address = $"tcp://{ip}:{port}/opengameserver";

            //service = new ServiceHostBuilder<MonitorComsService>(serviceInstance)
            service = new ServiceHostBuilder<MonitorComsService>(InstanceMode.Single)
                .WithCallback<IMonitorComsCallback>()
                .AddTcpServer("tcp://localhost:5001/opengameserver")
                .CreateHost();

            service.ServiceInstanceCreated += (callback) =>
            {
                callback.InitServices(_serviceProvider);
            };


            await service.Open();


            while (!stoppingToken.IsCancellationRequested)
            {
                // wait till the server stops
                await Task.Delay(60000, stoppingToken);
            }

            await service.Close();
        }
    }

    public class MonitorComsService : IMonitorComsInterface
    {
        private IServiceProvider _serviceProvider;
        private HostBuilderContext _hostBuilderContext;
        private GameHandler _gameHandler;
        private EventHandlerService _eventHandlerService;
        private ILogger<IPCService> _logger;

        private bool parametersInit = false;

        private ConcurrentDictionary<string, IMonitorComsCallback> _clients = new ConcurrentDictionary<string, IMonitorComsCallback>();

        IMonitorComsCallback GetCaller() => OperationContext.Current.GetCallback<IMonitorComsCallback>();

        // Gotta make a constructor with cero parameters... even tho I create the object myself :/
        public MonitorComsService()
        {
            //throw new Exception("Unsupported constructor");
        }

        public MonitorComsService(IServiceProvider serviceProvider,
            HostBuilderContext hostBuilderContext,
            GameHandler gameHandler,
            EventHandlerService eventHandlerService,
            ILogger<IPCService> logger)
        {

            _serviceProvider = serviceProvider;
            _hostBuilderContext = hostBuilderContext;
            _gameHandler = gameHandler;
            _eventHandlerService = eventHandlerService;
            _logger = logger;
            parametersInit = true;

            Init();
        }

        public void InitServices(IServiceProvider services)
        {
            if (parametersInit)
            {
                return;
            }

            _serviceProvider = services;
            _hostBuilderContext = services.GetService<HostBuilderContext>();
            _gameHandler = services.GetService<GameHandler>();
            _eventHandlerService = services.GetService<EventHandlerService>();
            _logger = services.GetService<ILogger<IPCService>>();

            parametersInit = true;

            Init();
        }

        private void Init()
        {
            _eventHandlerService.ListenForEvent("Server:ConsoleMessage", async (Object serverObj, EventArgs e) =>
            {
                ConsoleEventArgs args = (ConsoleEventArgs)e;
                foreach (KeyValuePair<string, IMonitorComsCallback> client in _clients)
                {
                    await client.Value.ServerMessageConsole(args.NewLine).ConfigureAwait(false);
                }
            });
            _eventHandlerService.ListenForEvent("Server:UpdateMessage", async (Object serverObj, EventArgs e) =>
            {
                ConsoleEventArgs args = (ConsoleEventArgs)e;
                foreach (KeyValuePair<string, IMonitorComsCallback> client in _clients)
                {
                    await client.Value.ServerMessageConsole(args.NewLine).ConfigureAwait(false);
                }
            });
            _eventHandlerService.ListenForEvent("Server:Opened", async (Object serverObj, EventArgs e) =>
            {
                foreach (KeyValuePair<string, IMonitorComsCallback> client in _clients)
                {
                    await client.Value.ServerOpened(((Server) serverObj).Id).ConfigureAwait(false);
                }
            });
            _eventHandlerService.ListenForEvent("Server:Closed", async (Object serverObj, EventArgs e) =>
            {
                foreach (KeyValuePair<string, IMonitorComsCallback> client in _clients)
                {
                    await client.Value.ServerClosed(((Server) serverObj).Id).ConfigureAwait(false);
                }
            });
            _eventHandlerService.ListenForEvent("Server:Updated", async (Object serverObj, EventArgs e) =>
            {
                foreach (KeyValuePair<string, IMonitorComsCallback> client in _clients)
                {
                    await client.Value.ServerUpdated(((Server)serverObj).Id).ConfigureAwait(false);
                }
            });
        }

        public async Task Connected()
        {
            var caller = GetCaller();
            var connection = (IConnection)caller;
            _clients.AddOrUpdate(connection.ConnectionId, caller, (k, v) => caller);

            _logger.LogInformation("Web Monitor ({0}) has connected!", connection.ConnectionName);

            connection.SessionEnded += s =>
            {
                _clients.TryRemove(connection.ConnectionId, out IMonitorComsCallback _);
                _logger.LogInformation("Web Monitor ({0}) has disconnected.", connection.ConnectionName);
            };
        }

        private Server GetServer(int server)
        {
            using (MonitorDBContext db = _serviceProvider.GetService<MonitorDBContext>())
            //using (var db = _serviceProvider.GetService<MonitorDBContext>())
            {
                /*List<OpenGameMonitorLibraries.Server> servers = db.Servers
                    .Where(r => r.PID != null && r.PID != default(int))
                    .ToList();*/

                return db.Servers.Where(r => r.Id == server)
                    .FirstOrDefault<OpenGameMonitorLibraries.Server>();
            }
        }

        private IGameHandlerBase GetServerHandler(OpenGameMonitorLibraries.Server server)
        {
            return _gameHandler.GetServerHandler(server);
        }

        private IGameHandlerBase GetServerHandler(int serverId)
        {
            OpenGameMonitorLibraries.Server server = GetServer(serverId);

            return GetServerHandler(server);
        }

        public async Task ConfigReloaded()
        {
            // Find MonitorDBConfig
            IConfigurationRoot rootCfg = (IConfigurationRoot)_hostBuilderContext.Configuration;
            MonitorDBConfigurationProvider dbProv = null;
            foreach (IConfigurationProvider provider in rootCfg.Providers)
            {
                if (provider is MonitorDBConfigurationProvider)
                {
                    dbProv = (MonitorDBConfigurationProvider) provider;
                    break;
                }
            }

            if (dbProv != null)
            {
                dbProv.ForceReload();
            }
        }

        public async Task<bool> ServerOpen(int serverId)
        {
            var server = GetServer(serverId);
            var svHandler = GetServerHandler(server);

            try
            {
                await svHandler.OpenServer(server);
            }
            catch
            {
                return false;
            }

            return true;
        }
        public async Task<bool> ServerClose(int serverId)
        {
            var server = GetServer(serverId);
            var svHandler = GetServerHandler(server);

            try
            {
                await svHandler.CloseServer(server);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public async Task<bool> ServerUpdate(int serverId)
        {
            var server = GetServer(serverId);
            var svHandler = GetServerHandler(server);

            try
            {
                await svHandler.UpdateServer(server);
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
