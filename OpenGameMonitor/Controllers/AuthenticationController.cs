using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
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
        private readonly IHostEnvironment _environment;

        public AuthenticationController(
                IIdentityServerInteractionService interaction,
                UserManager<MonitorUser> userManager,
                SignInManager<MonitorUser> signInManager,
                IHostEnvironment environment
            )
        {
            _interaction = interaction;
            _userManager = userManager;
            _signInManager = signInManager;
            _environment = environment;
        }

        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string ReturnUrl { get; set; }
            public string TwoFAToken { get; set; }
        }

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody]LoginRequest request)
        {
            var context = await _interaction.GetAuthorizationContextAsync(request.ReturnUrl);
            var user = await _userManager.FindByNameAsync(request.Username) ?? await _userManager.FindByEmailAsync(request.Username);

            if (user != null && context != null)
            {
                /*
                if (String.IsNullOrEmpty(request.TwoFAToken))
                {
                    var result = await _signInManager.PasswordSignInAsync(user, request.Password, true, false);

                    if (result.RequiresTwoFactor)
                    {

                    }
                }
                else
                {
                    var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

                    if (user == null)
                    {

                    }
                    var result = await 
                }
                */
                var passwordRight = await _userManager.CheckPasswordAsync(user, request.Password);

                if (passwordRight)
                {
                    if (await _userManager.GetTwoFactorEnabledAsync(user))
                    {
                        if (!String.IsNullOrEmpty(request.TwoFAToken))
                        {
                            var authorized = await _userManager.VerifyTwoFactorTokenAsync(user, "Login", request.TwoFAToken);

                            if (authorized)
                            {
                                await _signInManager.SignInAsync(user, true);
                                return new JsonResult(new { RedirectUrl = request.ReturnUrl, IsOk = true });
                            }
                        }
                        else
                        {
                            var result = new JsonResult(new { IsOK = false, Error = "2fa_enabled", ErrorDescription = "Two Factor Authentication is enabled for this account." });
                            result.StatusCode = 401;
                            return result;
                        }
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, true);
                        return new JsonResult(new { RedirectUrl = request.ReturnUrl, IsOk = true });
                    }
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