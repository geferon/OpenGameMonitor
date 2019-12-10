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

        public override async Task OnConnectedAsync()
        {
            var id = Context.ConnectionId;
            var user = Context.User;
            CurrentConnections.Add(id, user);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var conId = Context.ConnectionId;
            var connection = CurrentConnections.ContainsKey(conId);

            if (connection)
            {
                CurrentConnections.Remove(conId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
