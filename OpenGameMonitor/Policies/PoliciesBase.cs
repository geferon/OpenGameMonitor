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
                var roleNames = new (string, string[])[]
                {
                    ( "Moderator", new string[] { "Servers.ViewAll", "Users.View", "Groups.View" } ),
                    ( "Developer", new string[] { "Settings.View", "Servers.InteractAll" } ),
                    ( "Admin", new string[] { "Settings.Edit", "Servers.EditAll", "Servers.Create", "Users.Modify", "Users.Create", "Groups.Modify", "Groups.Create" } )
                };

                IdentityResult roleResult;

                Dictionary<string, bool> rolesCreated = new Dictionary<string, bool>();

                List<Claim> baseClaims = new List<Claim>();

                foreach (var role in roleNames)
                {
                    var roleName = role.Item1;

                    foreach (string claim in role.Item2)
                    {
                        baseClaims.Add(new Claim("Permission", claim));
                    }

                    var roleExist = await RoleManager.RoleExistsAsync(roleName);
                    rolesCreated[roleName] = !roleExist;
                    if (!roleExist)
                    {
                        //create the roles and seed them to the database: Question 1
                        var roleObj = new MonitorRole(roleName)
                        {
                            Id = Guid.NewGuid().ToString()
                        };

                        roleResult = await RoleManager.CreateAsync(roleObj);

                        foreach (var claim in baseClaims) {
                            await RoleManager.AddClaimAsync(roleObj, claim);
                        }
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
