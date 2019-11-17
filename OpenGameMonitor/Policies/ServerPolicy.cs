using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenGameMonitorLibraries;
using Microsoft.AspNetCore.Identity;

namespace OpenGameMonitorWeb.Policies
{
    public class ServerPolicyRequirement : IAuthorizationRequirement { }

    public class ServerPolicyHandler : AuthorizationHandler<ServerPolicyRequirement, Server>
    {
        private readonly UserManager<MonitorUser> _userManager;
        public ServerPolicyHandler(UserManager<MonitorUser> userManager)
        {
            _userManager = userManager;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
            ServerPolicyRequirement requirement,
            Server server)
        {
            //var user = (MonitorUser)context.User.Identity;
            var user = await _userManager.GetUserAsync(context.User);

            if (context.User.IsInRole("Admin") ||
                user == server.Owner ||
                //(server.Group?.Members.Contains(user) ?? false))
                (server.Group?.Members.Any((group) => group.User == user) ?? false))
            {
                context.Succeed(requirement);
            }
        }
    }
}
