using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenGameMonitorLibraries;

namespace OpenGameMonitorWeb
{
    public class ServerPolicyRequirement : IAuthorizationRequirement { }

    public class ServerPolicyHandler : AuthorizationHandler<ServerPolicyRequirement, Server>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
            ServerPolicyRequirement requirement,
            Server server)
        {
            var user = (MonitorUser)context.User.Identity;
            if (user == server.Owner ||
                (server.Group?.Members.Contains(user) ?? false))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
