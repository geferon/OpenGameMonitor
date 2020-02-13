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
        private readonly IServiceProvider _serviceProvider;
        //private readonly IAuthorizationService _authorizationService;
        private readonly EventHandlerService _eventHandlerService;
        public ServersListener(IHubContext<ServersHub> hub,
            HubConnectionManager hubConnectionManager,
            IServiceProvider serviceProvider,
            //IAuthorizationService authorizationService,
            EventHandlerService eventHandlerService)
        {
            _hub = hub;
            _hubConnectionManager = hubConnectionManager;
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
            using (var db = _serviceProvider.GetService<MonitorDBContext>())
            {
                await NotifyServerUpdated(await db.Servers.FindAsync(args.ServerID), null);
            }
        }

        private async void ServerOpened(object sender, ServerEventArgs args)
        {
            using (var db = _serviceProvider.GetService<MonitorDBContext>())
            {
                await NotifyServerUpdated(await db.Servers.FindAsync(args.ServerID), null);
            }
        }

        private async void ServerMonitorUpdated(object sender, ServerEventArgs args)
        {
            using (var db = _serviceProvider.GetService<MonitorDBContext>())
            {
                await NotifyServerUpdated(await db.Servers.FindAsync(args.ServerID), null);
            }
        }

        private void ServerMessageConsole(object sender, ServerMessageEventArgs e)
        {
            _hub.Clients.Group($"Server:{e.ServerID}").SendAsync("ServerConsoleMessage", e.Message);
        }

        public async void ServerInserted(IInsertedEntry<Server> server)
        {
            var authService = _serviceProvider.GetService<IAuthorizationService>();

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

        public async void ServerUpdated(IUpdatingEntry<Server> server)
        {
            var authService = _serviceProvider.GetService<IAuthorizationService>();

            Dictionary<string, bool[]> connections = new Dictionary<string, bool[]>();
            foreach (var connection in _hubConnectionManager.GetConnectedUsers<ServersHub>())
            {
                if (!connections.ContainsKey(connection.Key))
                    connections.Add(connection.Key, new bool[2]);

                connections[connection.Key][0] = (await authService.AuthorizeAsync(connection.Value, server.Original, "ServerPolicy")).Succeeded;
                connections[connection.Key][1] = (await authService.AuthorizeAsync(connection.Value, server.Entity, "ServerPolicy")).Succeeded;
            }

            var allowedConnections = connections.Where(v => v.Value[0] && v.Value[1]).Select(v => v.Key).ToArray();
            NotifyServerUpdated(server.Entity, allowedConnections);

            var disallowedConnections = connections.Where(v => v.Value[0] && !v.Value[1]).Select(v => v.Key).ToArray();
            NotifyServerDeleted(server.Entity, disallowedConnections);

            var newConnections = connections.Where(v => !v.Value[0] && v.Value[1]).Select(v => v.Key).ToArray();
            NotifyServerInserted(server.Entity, disallowedConnections);
        }

        public async void ServerDeleted(IDeletedEntry<Server> server)
        {
            var authService = _serviceProvider.GetService<IAuthorizationService>();

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

            NotifyServerDeleted(server.Entity, connections.ToArray());
        }

        public async Task NotifyServerUpdated(Server server, string[] connections)
        {
            if (connections == null)
                connections = await GetDefaultConnections(server);

            if (connections.Length > 0)
                await _hub.Clients.Clients(connections).SendAsync("ServerUpdated", server);
        }

        public async Task NotifyServerDeleted(Server server, string[] connections)
        {
            if (connections == null)
                connections = await GetDefaultConnections(server);

            if (connections.Length > 0)
                await _hub.Clients.Clients(connections).SendAsync("ServerDeleted", server.Id);
        }

        public async Task NotifyServerInserted(Server server, string[] connections)
        {
            if (connections == null)
                connections = await GetDefaultConnections(server);

            if (connections.Length > 0)
                await _hub.Clients.Clients(connections).SendAsync("ServerInserted", server);
        }

        private async Task<string[]> GetDefaultConnections(Server server)
        {
            var authService = _serviceProvider.GetService<IAuthorizationService>();

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
