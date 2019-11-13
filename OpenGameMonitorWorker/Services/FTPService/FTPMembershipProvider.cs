using FubarDev.FtpServer.AccountManagement;
using Microsoft.AspNetCore.Identity;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker
{
    class FTPMembershipProvider : IMembershipProvider
    {
        UserManager<MonitorUser> _userManager;
        UserClaimsPrincipalFactory<MonitorUser> _userClaimsPrincipalFactory;
        public FTPMembershipProvider(UserManager<MonitorUser> userManager,
            UserClaimsPrincipalFactory<MonitorUser> userClaimsPrincipalFactory)
        {
            _userManager = userManager;
            _userClaimsPrincipalFactory = userClaimsPrincipalFactory;
        }

        public async Task<MemberValidationResult> ValidateUserAsync(string username, string password)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (await _userManager.CheckPasswordAsync(user, password))
            {
                return new MemberValidationResult(MemberValidationStatus.AuthenticatedUser,
                    await _userClaimsPrincipalFactory.CreateAsync(user));
            }
            return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
        }
    }
}
