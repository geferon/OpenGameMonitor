using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Hubs
{
    public class HubConnectionManager
    {
        private Dictionary<Type, Dictionary<string, ClaimsPrincipal>> CurrentConnections = new Dictionary<Type, Dictionary<string, ClaimsPrincipal>>();
        private readonly object connectionsLock = new object();

        public void UserConnected<THub>(string connectionId, ClaimsPrincipal user) where THub : Hub
        {
            var hub = typeof(THub);
            lock (connectionsLock)
            {
                if (!CurrentConnections.ContainsKey(hub))
                {
                    CurrentConnections.Add(hub, new Dictionary<string, ClaimsPrincipal>());
                }

                CurrentConnections[hub].Add(connectionId, user);
            }
        }

        public void UserDisconnected<THub>(string connectionId, ClaimsPrincipal user) where THub : Hub
        {
            var hub = typeof(THub);
            lock (connectionsLock)
            {
                if (!CurrentConnections.ContainsKey(hub))
                {
                    CurrentConnections.Add(hub, new Dictionary<string, ClaimsPrincipal>());
                }

                if (CurrentConnections[hub].ContainsKey(connectionId))
                    CurrentConnections[hub].Remove(connectionId);
            }
        }

        public List<KeyValuePair<string, ClaimsPrincipal>> GetConnectedUsers<THub>() where THub : Hub
        {
            var hubType = typeof(THub);
            return CurrentConnections.ContainsKey(hubType) ? CurrentConnections[hubType].ToList() : new List<KeyValuePair<string, ClaimsPrincipal>>();
        }
    }
}
