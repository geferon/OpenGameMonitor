using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Policies
{
    public static class PoliciesBaseExtensions
    {
        public static async Task CreateRoles(this IServiceProvider serviceProvider)
        {
            using (var services = serviceProvider.CreateScope())
            {
                //initializing custom roles
                var RoleManager = services.ServiceProvider.GetRequiredService<RoleManager<MonitorRole>>();
                var UserManager = services.ServiceProvider.GetRequiredService<UserManager<MonitorUser>>();
                string[] roleNames = { "Admin", "Developer", "Moderator" };
                IdentityResult roleResult;

                Dictionary<string, bool> rolesCreated = new Dictionary<string, bool>();

                foreach (var roleName in roleNames)
                {
                    var roleExist = await RoleManager.RoleExistsAsync(roleName);
                    rolesCreated[roleName] = !roleExist;
                    if (!roleExist)
                    {
                        //create the roles and seed them to the database: Question 1
                        var role = new MonitorRole(roleName)
                        {
                            Id = Guid.NewGuid().ToString()
                        };

                        roleResult = await RoleManager.CreateAsync(role);

                        //RoleManager.AddClaimAsync(role, new System.Security.Claims.Claim(ClaimTypes.AuthorizationDecision, ))
                    }
                }

                var Configuration = services.ServiceProvider.GetRequiredService<IConfiguration>();

                if (rolesCreated["Admin"])
                {
                    //Here you could create a super user who will maintain the web app
                    var poweruser = new MonitorUser
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserName = Configuration["UserSystem:AdminUserName"],
                        Email = Configuration["UserSystem:AdminUserEmail"],
                        EmailConfirmed = true
                    };
                    //Ensure you have these values in your appsettings.json file
                    //string userPWD = Configuration["UserSystem:AdminUserPassword"];
                    string userPWD = "P@ssword1234"; // Password should be changed manually
                    var _user = await UserManager.FindByEmailAsync(Configuration["UserSystem:AdminUserEmail"]);

                    if (_user == null)
                    {
                        var createPowerUser = await UserManager.CreateAsync(poweruser, userPWD);
                        if (createPowerUser.Succeeded)
                        {
                            //here we tie the new user to the role
                            await UserManager.AddToRoleAsync(poweruser, "Admin");

                        }
                    }
                    // */
                }
            }
        }
    }
}
