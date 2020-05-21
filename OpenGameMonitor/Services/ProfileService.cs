using IdentityServer4.Models;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Identity;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Services
{
	public class ProfileService : IProfileService
	{
		private readonly UserManager<MonitorUser> _userManager;
		private readonly RoleManager<MonitorRole> _roleManager;

		public ProfileService(UserManager<MonitorUser> userManager, RoleManager<MonitorRole> roleManager)
		{
			_userManager = userManager;
			_roleManager = roleManager;
		}

		public async Task GetProfileDataAsync(ProfileDataRequestContext context)
		{
			var user = await _userManager.GetUserAsync(context.Subject);
			var userRoles = await _userManager.GetRolesAsync(user);

			var claims = new List<Claim>();

			foreach (var role in userRoles)
			{
				claims.Add(
					new Claim(ClaimTypes.Role, role)
				);
			}

			foreach (var claim in await _userManager.GetClaimsAsync(user))
			{
				claims.Add(claim);
			}

			foreach (var role in await _userManager.GetRolesAsync(user))
			{
				var roleObj = await _roleManager.FindByNameAsync(role);
				foreach (var claim in await _roleManager.GetClaimsAsync(roleObj))
				{
					claims.Add(claim);
				}
			}

			context.IssuedClaims.AddRange(claims);
		}

		public async Task IsActiveAsync(IsActiveContext context)
		{
			var user = await _userManager.GetUserAsync(context.Subject);

			context.IsActive = (user != null);
		}
	}
}
