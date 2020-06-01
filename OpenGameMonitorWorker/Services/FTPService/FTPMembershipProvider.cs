using FubarDev.FtpServer.AccountManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker.Services
{
	class FTPMembershipProvider : IMembershipProvider
	{
		private readonly IServiceProvider _serviceProvider;
		public FTPMembershipProvider(
			IServiceProvider serviceProvider
		) {
			_serviceProvider = serviceProvider;
		}

		public async Task<MemberValidationResult> ValidateUserAsync(string username, string password)
		{
			using (var scope = _serviceProvider.CreateScope())
			using (var userManager = scope.ServiceProvider.GetRequiredService<UserManager<MonitorUser>>())
			{
				var userClaimsPrincipalFactory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<MonitorUser>>();

				var user = await userManager.FindByNameAsync(username);

				if (user == null)
				{
					user = await userManager.FindByEmailAsync(username);
				}

				if (await userManager.CheckPasswordAsync(user, password))
				{
					return new MemberValidationResult(MemberValidationStatus.AuthenticatedUser,
						await userClaimsPrincipalFactory.CreateAsync(user));
				}
				return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
			}
		}
	}
}
