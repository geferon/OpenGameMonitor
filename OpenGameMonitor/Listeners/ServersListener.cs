using AutoMapper;
using EntityFrameworkCore.Triggers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenGameMonitor.Services;
using OpenGameMonitorLibraries;
using OpenGameMonitorWeb.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Listeners
{
    public class ServersListener : BackgroundService
    {
        private readonly IHubContext<ServersHub> _hub;
        private readonly HubConnectionManager _hubConnectionManager;
        private readonly IMapper _mapper;
        private readonly IServiceProvider _serviceProvider;
        //private readonly IAuthorizationService _authorizationService;
        private readonly EventHandlerService _eventHandlerService;
        public ServersListener(IHubContext<ServersHub> hub,
            HubConnectionManager hubConnectionManager,
            IMapper mapper,
            IServiceProvider serviceProvider,
            //IAuthorizationService authorizationService,
            EventHandlerService eventHandlerService)
        {
            _hub = hub;
            _hubConnectionManager = hubConnectionManager;
            _mapper = mapper;
            _serviceProvider = serviceProvider;
            //_authorizationService = authorizationService;
            _eventHandlerService = eventHandlerService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _eventHandlerService.ListenForEventType<ServerEventArgs>("Monitor:ServerClosed", ServerClosed);
            _eventHandlerService.ListenForEventType<ServerEventArgs>("Monitor:ServerOpened", ServerOpened);
            _eventHandlerService.ListenForEventType<ServerEventArgs>("Monitor:ServerUpdated", ServerMonitorUpdated);
            _eventHandlerService.ListenForEventType<ServerMessageEventArgs>("Monitor:ServerMessageConsole", ServerMessageConsole);

            Triggers<Server>.Inserted += ServerInserted;
            Triggers<Server>.Updating += ServerUpdated;
            Triggers<Server>.Deleted += ServerDeleted;

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken);
            }
        }

        private async void ServerClosed(object sender, ServerEventArgs args)
        {
            using (var scope = _serviceProvider.CreateScope())
            using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
            {
                await NotifyServerUpdated(await db.Servers.FindAsync(args.ServerID), null);
            }
        }

        private async void ServerOpened(object sender, ServerEventArgs args)
        {
            using (var scope = _serviceProvider.CreateScope())
            using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
            {
                await NotifyServerUpdated(await db.Servers.FindAsync(args.ServerID), null);
            }
        }

        private async void ServerMonitorUpdated(object sender, ServerEventArgs args)
        {
            using (var scope = _serviceProvider.CreateScope())
            using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
            {
                await NotifyServerUpdated(await db.Servers.FindAsync(args.ServerID), null);
            }
        }

        private void ServerMessageConsole(object sender, ServerMessageEventArgs e)
        {
            _hub.Clients.Group($"Server:{e.ServerID}").SendAsync("Server:ConsoleMessage", e.Message);
        }

        public async void ServerInserted(IInsertedEntry<Server> server)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var authService = scope.ServiceProvider.GetService<IAuthorizationService>();

                List<string> connections = new List<string>();
                foreach (var connection in _hubConnectionManager.GetConnectedUsers<ServersHub>())
                {
                    var authResult = await authService.AuthorizeAsync(connection.Value, server.Entity, "ServerPolicy");
                    if (authResult.Succeeded)
                    {
                        if (!connections.Contains(connection.Key))
                            connections.Add(connection.Key);
                    }
                }

                await NotifyServerInserted(server.Entity, connections.ToArray());
            }
        }

        public async void ServerUpdated(IUpdatingEntry<Server> server)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var authService = scope.ServiceProvider.GetService<IAuthorizationService>();

                Dictionary<string, bool[]> connections = new Dictionary<string, bool[]>();
                foreach (var connection in _hubConnectionManager.GetConnectedUsers<ServersHub>())
                {
                    if (!connections.ContainsKey(connection.Key))
                        connections.Add(connection.Key, new bool[2]);

                    connections[connection.Key][0] = (await authService.AuthorizeAsync(connection.Value, server.Original, "ServerPolicy")).Succeeded;
                    connections[connection.Key][1] = (await authService.AuthorizeAsync(connection.Value, server.Entity, "ServerPolicy")).Succeeded;
                }

                var allowedConnections = connections.Where(v => v.Value[0] && v.Value[1]).Select(v => v.Key).ToArray();
                var disallowedConnections = connections.Where(v => v.Value[0] && !v.Value[1]).Select(v => v.Key).ToArray();
                var newConnections = connections.Where(v => !v.Value[0] && v.Value[1]).Select(v => v.Key).ToArray();

                List<Task> tasks = new List<Task>();

                tasks.Add(Task.Run(() => NotifyServerUpdated(server.Entity, allowedConnections)));
                tasks.Add(Task.Run(() => NotifyServerDeleted(server.Entity, disallowedConnections)));
                tasks.Add(Task.Run(() => NotifyServerInserted(server.Entity, disallowedConnections)));
                await Task.WhenAll(tasks);
            }
        }

        public async void ServerDeleted(IDeletedEntry<Server> server)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var authService = scope.ServiceProvider.GetService<IAuthorizationService>();

                List<string> connections = new List<string>();
                foreach (var connection in _hubConnectionManager.GetConnectedUsers<ServersHub>())
                {
                    var authResult = await authService.AuthorizeAsync(connection.Value, server.Entity, "ServerPolicy");
                    if (authResult.Succeeded)
                    {
                        if (!connections.Contains(connection.Key))
                            connections.Add(connection.Key);
                    }
                }

                await NotifyServerDeleted(server.Entity, connections.ToArray());
            }
        }

        public async Task NotifyServerUpdated(Server server, string[] connections)
        {
            if (connections == null)
                connections = await GetDefaultConnections(server);

            var serverToSend = _mapper.Map<DTOServer>(server);
            if (connections.Length > 0)
                await _hub.Clients.Clients(connections).SendAsync("Server:Updated", serverToSend);
        }

        public async Task NotifyServerDeleted(Server server, string[] connections)
        {
            if (connections == null)
                connections = await GetDefaultConnections(server);

            if (connections.Length > 0)
                await _hub.Clients.Clients(connections).SendAsync("Server:Deleted", server.Id);
        }

        public async Task NotifyServerInserted(Server server, string[] connections)
        {
            if (connections == null)
                connections = await GetDefaultConnections(server);

            var serverToSend = _mapper.Map<DTOServer>(server);
            if (connections.Length > 0)
                await _hub.Clients.Clients(connections).SendAsync("Server:Inserted", serverToSend);
        }

        private async Task PopulateServer(Server server)
        {
            using (var scope = _serviceProvider.CreateScope())
            using (var db = scope.ServiceProvider.GetService<MonitorDBContext>())
            {
                //if (server.Owner == null)
                //    server.Owner = db.
            }
        }

        private async Task<string[]> GetDefaultConnections(Server server)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var authService = scope.ServiceProvider.GetService<IAuthorizationService>();

                List<string> connections = new List<string>();
                foreach (var connection in _hubConnectionManager.GetConnectedUsers<ServersHub>())
                {
                    var authResult = await authService.AuthorizeAsync(connection.Value, server, "ServerPolicy");
                    if (authResult.Succeeded)
                    {
                        if (!connections.Contains(connection.Key))
                            connections.Add(connection.Key);
                    }
                }

                return connections.ToArray();
            }
        }
    }
}
