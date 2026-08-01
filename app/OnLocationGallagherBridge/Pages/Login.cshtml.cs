using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OnLocationGallagherBridge.Services;

namespace OnLocationGallagherBridge.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IWebAuthService _auth;
    private readonly ConfigService _config;

    [BindProperty]
    public string Username { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public string? ErrorMessage { get; set; }
    public bool AuthEnabled { get; set; }
    public string? ReturnUrl { get; set; }

    public LoginModel(IWebAuthService auth, ConfigService config)
    {
        _auth = auth;
        _config = config;
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        AuthEnabled = _config.GetConfig().WebHost.Auth.Enabled;
        if (!AuthEnabled) return RedirectToPage("/Index");

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        AuthEnabled = _config.GetConfig().WebHost.Auth.Enabled;
        if (!AuthEnabled) return RedirectToPage("/Index");

        if (_auth.ValidateCredentials(Username, Password, out var user) && user is not null)
        {
            var identity = _auth.CreateClaimsIdentity(user);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            if (user.RequirePasswordChange)
                return RedirectToPage("/ChangePassword");

            return RedirectToLocal(returnUrl);
        }

        ErrorMessage = "Invalid username or password.";
        ReturnUrl = returnUrl;
        return Page();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToPage("/Index");
    }
}
