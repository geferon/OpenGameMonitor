using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenGameMonitorLibraries;

namespace OpenGameMonitorWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AuthenticationController : ControllerBase
    {
        private readonly IIdentityServerInteractionService _interaction;
        private readonly UserManager<MonitorUser> _userManager;
        private readonly SignInManager<MonitorUser> _signInManager;

        public AuthenticationController(
                IIdentityServerInteractionService interaction,
                UserManager<MonitorUser> userManager,
                SignInManager<MonitorUser> signInManager
            )
        {
            _interaction = interaction;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string ReturnUrl { get; set; }
            public string? TwoFAToken { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody]LoginRequest request)
        {
            var context = await _interaction.GetAuthorizationContextAsync(request.ReturnUrl);
            var user = await _userManager.FindByNameAsync(request.Username) ?? await _userManager.FindByEmailAsync(request.Username);

            if (user != null && context != null)
            {
                var passwordRight = _userManager.CheckPasswordAsync(user, request.Password);

                if (await _userManager.GetTwoFactorEnabledAsync(user))
                {
                    if (request.TwoFAToken.HasValue && !String.IsNullOrEmpty(request.TwoFAToken))
                    {
                        _userManager.VerifyTwoFactorTokenAsync(user, "Login", request.TwoFAToken);
                    }
                    else
                    {
                        return new JsonResult();
                    }
                }
                else
                {
                    await _signInManager.SignInAsync(user, true);
                    return new JsonResult(new { RedirectUrl = request.ReturnUrl, IsOk = true });
                }
            }

            return Unauthorized();
        }

        [HttpGet]
        [Route("Logout")]
        public async Task<IActionResult> Logout(string logoutId)
        {
            var context = await _interaction.GetLogoutContextAsync(logoutId);
            bool showSignoutPrompt = true;

            if (context?.ShowSignoutPrompt == false)
            {
                // it's safe to automatically sign-out
                showSignoutPrompt = false;
            }

            if (User?.Identity.IsAuthenticated == true)
            {
                // delete local authentication cookie
                await HttpContext.SignOutAsync();
            }

            // no external signout supported for now (see \Quickstart\Account\AccountController.cs TriggerExternalSignout)
            return Ok(new
            {
                showSignoutPrompt,
                ClientName = string.IsNullOrEmpty(context?.ClientName) ? context?.ClientId : context?.ClientName,
                context?.PostLogoutRedirectUri,
                context?.SignOutIFrameUrl,
                logoutId
            });
        }

        [HttpGet]
        [Route("Error")]
        public async Task<IActionResult> Error(string errorId)
        {
            // retrieve error details from identityserver
            var message = await _interaction.GetErrorContextAsync(errorId);

            if (message != null)
            {
                if (!_environment.IsDevelopment())
                {
                    // only show in development
                    message.ErrorDescription = null;
                }
            }

            return Ok(message);
        }
    }
}