using Microsoft.AspNetCore.SignalR;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;

namespace OpenGameMonitorWeb.Hubs
{
    public class ServersHub : Hub
    {
        private readonly HubConnectionManager _connectionManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuthorizationService _authorizationService;
        public ServersHub(HubConnectionManager connectionManager, IServiceProvider serviceProvider, IAuthorizationService authorizationService)
        {
            _connectionManager = connectionManager;
            _serviceProvider = serviceProvider;
        }

        public override async Task OnConnectedAsync()
        {
            var id = Context.ConnectionId;
            var user = Context.User;

            _connectionManager.UserConnected<ServersHub>(id, user);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var conId = Context.ConnectionId;
            var user = Context.User;

            _connectionManager.UserDisconnected<ServersHub>(conId, user);

            await base.OnDisconnectedAsync(exception);
        }

        public async Task ListenServerConsole(int serverId)
        {
            if (Context.Items.ContainsKey("ActiveServer"))
            {
                await StopListenServerConsole();
            }

            Server foundServer;
            using (var db = _serviceProvider.GetService<MonitorDBContext>())
            {
                var server = await db.Servers.FindAsync(serverId);
                if (server == null)
                {
                    throw new KeyNotFoundException();
                }

                foundServer = server;
            }

            var authResult = await _authorizationService.AuthorizeAsync(Context.User, foundServer, "ServerPolicy");
            if (!authResult.Succeeded)
            {
                throw new UnauthorizedAccessException();
            }

            Context.Items["ActiveServer"] = serverId;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Server:{serverId}");
        }

        public async Task StopListenServerConsole()
        {
            if (!Context.Items.ContainsKey("ActiveServer"))
                return;

            var active = Context.Items["ActiveServer"];
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Server:{active}");
            Context.Items.Remove("ActiveServer");
        }
    }
}
