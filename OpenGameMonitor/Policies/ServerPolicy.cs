using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenGameMonitorLibraries;
using Microsoft.AspNetCore.Identity;

namespace OpenGameMonitorWeb.Policies
{
    public class ServerPolicyRequirement : IAuthorizationRequirement
    {
        public ServerPolicyRequirement() : this(Array.Empty<string>()) { }

        //public ServerPolicyRequirement(params string[] permissions) : this(permissions) { }

        public ServerPolicyRequirement(params string[] permissions)
        {
            this.Permissions = permissions;
        }

        public string[] Permissions;
    }

    public class ServerPolicyHandler : AuthorizationHandler<ServerPolicyRequirement, Server>
    {
        private readonly UserManager<MonitorUser> _userManager;
        public ServerPolicyHandler(UserManager<MonitorUser> userManager)
        {
            _userManager = userManager;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ServerPolicyRequirement requirement,
            Server server
        ) {
            // Has permissions or next
            if (requirement.Permissions.All(perm => context.User.HasClaim("Permission", perm)))
            {
                context.Succeed(requirement);
                return;
            }

            //var user = (MonitorUser)context.User.Identity;
            //var user = await _userManager.GetUserAsync(context.User);
            var userId = _userManager.GetUserId(context.User);

            if (userId == null || server == null)
                return;

            if (userId == server.Owner.Id ||
                //(server.Group?.Members.Contains(user) ?? false))
                (server.Group?.Members.Any((group) => group.User.Id == userId) ?? false))
            {
                context.Succeed(requirement);
            }
        }
    }
}
