using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
//using JKang.IpcServiceFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using OpenGameMonitorLibraries;
using OpenGameMonitorWorker.Handlers;
using OpenGameMonitorWorker.Tasks;
using Xeeny.Api.Server;
using Xeeny.Connections;
using Xeeny.Dispatching;
using Xeeny.Server;

namespace OpenGameMonitorWorker.Services
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
            var port = _config.GetValue<int>("MonitorSettings:Port", 5010);
            var address = $"tcp://{ip}:{port}/opengameserver";

            //service = new ServiceHostBuilder<MonitorComsService>(serviceInstance)
            service = new ServiceHostBuilder<MonitorComsService>(InstanceMode.Single)
                .WithCallback<IMonitorComsCallback>()
                .AddTcpServer(address)
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
        private GameHandlerService _gameHandler;
        private ServerTracker _serverTracker;
        //private EventHandlerService _eventHandlerService;
        private ILogger<IPCService> _logger;

        private bool parametersInit = false;

        private ConcurrentDictionary<string, IMonitorComsCallback> _clients = new ConcurrentDictionary<string, IMonitorComsCallback>();

        IMonitorComsCallback GetCaller() => OperationContext.Current.GetCallback<IMonitorComsCallback>();

        // Gotta make a constructor with zero parameters... even tho I create the object myself :/
        public MonitorComsService()
        {
            //throw new Exception("Unsupported constructor");
        }

        public MonitorComsService(IServiceProvider serviceProvider,
            HostBuilderContext hostBuilderContext,
            GameHandlerService gameHandler,
            ServerTracker serverTracker,
            //EventHandlerService eventHandlerService,
            ILogger<IPCService> logger)
        {

            _serviceProvider = serviceProvider;
            _hostBuilderContext = hostBuilderContext;
            _gameHandler = gameHandler;
            _serverTracker = serverTracker;
            //_eventHandlerService = eventHandlerService;
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
            _gameHandler = services.GetService<GameHandlerService>();
            _serverTracker = services.GetService<ServerTracker>();
            //_eventHandlerService = services.GetService<EventHandlerService>();
            _logger = services.GetService<ILogger<IPCService>>();

            parametersInit = true;

            Init();
        }

        private void Init()
        {
            _gameHandler.ConsoleMessage += async (object serverObj, ConsoleEventArgs args) =>
            {
                await Task.WhenAll(
                    _clients.Select(client =>
                        client.Value.ServerMessageConsole(((Server)serverObj).Id, args.NewLine ?? (args.IsError ? "Error" : ""))
                    )
                );
            };
            _gameHandler.UpdateMessage += async (object serverObj, ConsoleEventArgs args) =>
            {
                await Task.WhenAll(
                    _clients.Select(client =>
                        client.Value.ServerMessageUpdate(((Server)serverObj).Id, args.NewLine ?? (args.IsError ? "Error" : ""))
                    )
                );
            };
            _gameHandler.UpdateProgress += async (object serverObj, ServerUpdateProgressEventArgs args) =>
            {
                await Task.WhenAll(
                    _clients.Select(client =>
                        client.Value.ServerUpdateProgress(((Server)serverObj).Id, args.Progress)
                    )
                );
            };
            _gameHandler.ServerOpened +=  async (object serverObj, EventArgs e) =>
            {
                await Task.WhenAll(
                    _clients.Select(client =>
                        client.Value.ServerOpened(((Server)serverObj).Id)
                    )
                );
            };
            _gameHandler.ServerClosed += async (object serverObj, EventArgs e) =>
            {
                await Task.WhenAll(
                    _clients.Select(client =>
                        client.Value.ServerClosed(((Server)serverObj).Id)
                    )
                );
            };
            _gameHandler.ServerUpdateStart += async (object serverObj, EventArgs e) =>
            {
                await Task.WhenAll(
                    _clients.Select(client =>
                        client.Value.ServerUpdateStart(((Server)serverObj).Id)
                    )
                );
            };
            _gameHandler.ServerUpdated += async (object serverObj, ServerUpdateEventArgs e) =>
            {
                await Task.WhenAll(
                    _clients.Select(client =>
                        client.Value.ServerUpdated(((Server)serverObj).Id)
                    )
                );
            };
            _serverTracker.ServersMonitorRecorded += async (object _, ServerRecordsEventArgs args) =>
            {
                await Task.WhenAll(
                    _clients.Select(client =>
                        client.Value.ServersMonitorRecordAdded(args.RowsInserted)
                    )
                );
            };
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

        private async Task<Server> GetServer(int server)
        {
            using (var scope = _serviceProvider.CreateScope())
            using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
            {
                /*List<OpenGameMonitorLibraries.Server> servers = db.Servers
                    .Where(r => r.PID != null && r.PID != default(int))
                    .ToList();*/

                return await db.Servers.Where(r => r.Id == server)
                    .Include(s => s.Group)
                    .Include(s => s.Owner)
                    .Include(s => s.Game)
                    .FirstOrDefaultAsync<OpenGameMonitorLibraries.Server>();
            }
        }

        private IGameHandlerBase GetServerHandler(OpenGameMonitorLibraries.Server server)
        {
            return _gameHandler.GetServerHandler(server);
        }

        private async Task<IGameHandlerBase> GetServerHandler(int serverId)
        {
            OpenGameMonitorLibraries.Server server = await GetServer(serverId);

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
            var server = await GetServer(serverId);
            var svHandler = GetServerHandler(server);

            try
            {
                await svHandler.OpenServer(server);
            }
            catch (Exception err)
            {
                _logger.LogError(err, "There has been an error while opening the server {0}", serverId);
                return false;
            }

            return true;
        }

        public async Task<bool> ServerClose(int serverId)
        {
            var server = await GetServer(serverId);
            var svHandler = GetServerHandler(server);

            try
            {
                await svHandler.CloseServer(server);
            }
            catch (Exception err)
            {
                _logger.LogError(err, "There has been an error while closing the server {0}", serverId);
                return false;
            }

            return true;
        }

        public async Task<bool> ServerInstall(int serverId)
        {
            var server = await GetServer(serverId);
            var svHandler = GetServerHandler(server);

            try
            {
                return await svHandler.InitialInstall(server);
            }
            catch (Exception err)
            {
                _logger.LogError(err, "There has been an error while installing the server {0}", serverId);
                return false;
            }
        }

        public async Task<bool> ServerUpdate(int serverId)
        {
            var server = await GetServer(serverId);
            var svHandler = GetServerHandler(server);

            try
            {
                return await svHandler.UpdateServer(server);
            }
            catch (Exception err)
            {
                _logger.LogError(err, "There has been an error while updating the server {0}", serverId);
                return false;
            }
        }
    }
}
