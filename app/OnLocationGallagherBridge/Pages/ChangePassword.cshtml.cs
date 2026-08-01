using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OnLocationGallagherBridge.Services;

namespace OnLocationGallagherBridge.Pages;

[Authorize]
public class ChangePasswordModel : PageModel
{
    private readonly IWebAuthService _auth;
    private readonly ConfigService _config;

    [BindProperty]
    public string CurrentPassword { get; set; } = "";

    [BindProperty]
    public string NewPassword { get; set; } = "";

    [BindProperty]
    public string ConfirmPassword { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public ChangePasswordModel(IWebAuthService auth, ConfigService config)
    {
        _auth = auth;
        _config = config;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var cfg = _config.GetConfig();
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return RedirectToPage("/Login");

        var user = _auth.FindUser(username);
        if (user is null)
            return RedirectToPage("/Login");

        if (!_auth.ValidateCredentials(username, CurrentPassword, out _))
        {
            ErrorMessage = "Current password is incorrect.";
            return Page();
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "New password and confirmation do not match.";
            return Page();
        }

        if (!_auth.ValidatePassword(NewPassword, cfg.WebHost.Auth, out var error))
        {
            ErrorMessage = error;
            return Page();
        }

        _auth.SetPassword(user, NewPassword);
        user.RequirePasswordChange = false;
        await _config.SaveAsync();

        // Refresh the authentication cookie so the password-change requirement is removed.
        var identity = _auth.CreateClaimsIdentity(user);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return RedirectToPage("/Index");
    }
}
