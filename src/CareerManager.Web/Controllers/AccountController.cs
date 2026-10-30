using CareerManager.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareerManager.Controllers;

public sealed class AccountController(
    IConfiguration configuration,
    MicrosoftAccountOwnerMapping ownerMapping) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult SignIn(string? returnUrl = null)
    {
        if (!ownerMapping.IsConfigured
            || string.IsNullOrWhiteSpace(configuration["Authentication:Microsoft:ClientId"])
            || string.IsNullOrWhiteSpace(configuration["Authentication:Microsoft:ClientSecret"]))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Microsoft sign-in is not configured. Contact the application owner.");
        }

        if (User.Identity?.IsAuthenticated == true
            && ownerMapping.TryMapValidatedSubject(User.FindFirst("sub")?.Value, out var mappedOwnerId)
            && User.HasClaim(CareerOwnerClaim.Type, mappedOwnerId))
        {
            return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
        }

        var redirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!;
        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUri },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("SignOut")]
    public IActionResult SignOutUser()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = Url.Action(nameof(SignedOut)) },
            CookieAuthenticationDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult SignedOut() => View();

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AuthenticationError()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
