using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Hubs
{
    public class ServerHub : Hub
    {
        Dictionary<string, ClaimsPrincipal> CurrentConnections = new Dictionary<string, ClaimsPrincipal>();

        private readonly HubConnectionManager _connectionManager;
        public ServerHub(HubConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        public override async Task OnConnectedAsync()
        {
            var id = Context.ConnectionId;
            var user = Context.User;

            _connectionManager.UserConnected(id, user);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var conId = Context.ConnectionId;
            var user = Context.User;

            _connectionManager.UserDisconnected(conId, user);

            await base.OnDisconnectedAsync(exception);
        }
    }
}
