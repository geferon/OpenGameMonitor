using EntityFrameworkCore.Triggers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OpenGameMonitorLibraries;
using OpenGameMonitorWeb.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Listeners
{
    public class DbListener
    {
        private readonly IHubContext<ServerHub> _hub;
        private readonly HubConnectionManager _hubConnectionManager;
        private readonly IAuthorizationService _authorizationService;
        public DbListener(IHubContext<ServerHub> hub, HubConnectionManager hubConnectionManager, IAuthorizationService authorizationService)
        {
            _hub = hub;
            _hubConnectionManager = hubConnectionManager;
            _authorizationService = authorizationService;

            Triggers<Server>.Inserted += ServerInserted;
            Triggers<Server>.Updated += ServerUpdated;
        }

        public async void ServerInserted(IInsertedEntry<Server> server)
        {
            List<string> connections = new List<string>();
            foreach (var connection in _hubConnectionManager.GetConnectedUsers())
            {
                var authResult = await _authorizationService.AuthorizeAsync(connection.Value, server, "ServerPolicy");
                if (authResult.Succeeded)
                {
                    if (!connections.Contains(connection.Key))
                        connections.Add(connection.Key);
                }
            }

            await _hub.Clients.Clients(connections).SendAsync("ServerNew", server.Entity);
        }

        public void ServerUpdated(IUpdatedEntry<Server> server)
        {
            Dictionary<string, bool[]> connections = new List<string, bool[]>();
            foreach (var connection in _hubConnectionManager.GetConnectedUsers())
            {
                if (!connections.ContainsKey(connection.Key))
                    connections.Add(connection.Key, new bool[2]);

                connections[connection.Key][0](await _authorizationService.AuthorizeAsync(connection.Value, server, "ServerPolicy")).Succeeded;
            }

            await _hub.Clients.Clients(connections).SendAsync("ServerNew", server.Entity);

        }
    }
}
