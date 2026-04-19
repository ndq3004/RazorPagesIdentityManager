using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using RazorPageIdentityManager.Entities;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace RazorPageIdentityManager.Controllers
{
    public class AuthorizationController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthorizationController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public string Test()
        {
            return "Test";
        }

        [HttpGet("~/connect/authorize")]
        [HttpPost("~/connect/authorize")]
        public async Task<IActionResult> Authorize()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("The OpenIddict server request cannot be retrieved.");

            var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (result.Succeeded is false)
            {
                var redirectUri = Request.PathBase + Request.Path + Request.QueryString;
                return Challenge(
                    properties: new AuthenticationProperties { RedirectUri = redirectUri },
                    authenticationSchemes: new[] { IdentityConstants.ApplicationScheme });
            }

            var user = await _userManager.GetUserAsync(result.Principal)
                ?? throw new InvalidOperationException("The user cannot be retrieved.");
            if (user == null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var submit = Request.HasFormContentType ? Request.Form["submit"].ToString() : "";

            var consentRequired = (request.Prompt ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains("consent", StringComparer.Ordinal);

            if (consentRequired && !string.Equals(submit, "allow", StringComparison.OrdinalIgnoreCase))
            {
                var returnUrl = Request.PathBase + Request.Path + Request.QueryString;
                return RedirectToPage("/Consent", new
                {
                    returnUrl,
                    clientId = request.ClientId,
                    scope = request.Scope
                });
            }

            if (string.Equals(submit, "deny", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid(new AuthenticationProperties
                {
                    Items =
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user denied the consent."
                    }
                }, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var identity = new ClaimsIdentity(
                TokenValidationParameters.DefaultAuthenticationType,
                Claims.Name,
                Claims.Role);

            identity.SetClaim(Claims.Subject, await _userManager.GetUserIdAsync(user));
            identity.SetClaim(Claims.Email, user.Email);
            identity.SetClaim(Claims.Name, user.UserName);

            identity.SetScopes(request.GetScopes());
            identity.SetResources("resource_server");

            identity.SetDestinations(claim => claim.Type switch
            {
                Claims.Name or Claims.Email => [Destinations.AccessToken, Destinations.IdentityToken],
                Claims.Role => [Destinations.AccessToken],
                _ => [Destinations.AccessToken]
            });

            var principal = new ClaimsPrincipal(identity);
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        [HttpPost("~/connect/token")]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("OpenID Connect request cannot be retrieved.");

            if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
            {
                var authenticateResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                if (!authenticateResult.Succeeded)
                {
                    return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                }

                return SignIn(authenticateResult.Principal!, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new InvalidOperationException("The specified grant type is not supported.");
        }

        [HttpGet("~/connect/userinfo")]
        [HttpPost("~/connect/userinfo")]
        public async Task<IActionResult> UserInfo()
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded)
            {
                return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var principal = authenticateResult.Principal!;
            return Ok(new
            {
                sub = principal.GetClaim(Claims.Subject),
                name = principal.GetClaim(Claims.Name),
                email = principal.GetClaim(Claims.Email)
            });
        }

        [HttpGet("~/connect/logout")]
        [HttpPost("~/connect/logout")]
        public IActionResult Logout()
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                IdentityConstants.ApplicationScheme,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
    }
}
