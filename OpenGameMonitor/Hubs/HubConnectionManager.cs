using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Hubs
{
    public class HubConnectionManager
    {
        private Dictionary<string, ClaimsPrincipal> CurrentConnections = new Dictionary<string, ClaimsPrincipal>();
        private readonly object connectionsLock = new object();

        public void UserConnected(string connectionId, ClaimsPrincipal user)
        {
            lock (connectionsLock)
            {
                CurrentConnections.Add(connectionId, user);
            }
        }

        public void UserDisconnected(string connectionId)
        {
            lock (connectionsLock)
            {
                if (CurrentConnections.ContainsKey(connectionId))
                    CurrentConnections.Remove(connectionId);
            }
        }

        public List<KeyValuePair<string, ClaimsPrincipal>> GetConnectedUsers()
        {
            return CurrentConnections.ToList();
        }
    }
}
