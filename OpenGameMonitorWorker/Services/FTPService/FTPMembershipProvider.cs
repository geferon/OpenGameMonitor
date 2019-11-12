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
        public FTPMembershipProvider(UserManager<MonitorUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<MemberValidationResult> ValidateUserAsync(string username, string password)
        {

            return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
        }
    }
}
