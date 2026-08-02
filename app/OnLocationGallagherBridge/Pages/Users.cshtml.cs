using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;

namespace OnLocationGallagherBridge.Pages;

[Authorize(Roles = "Admin")]
public class UsersModel : PageModel
{
    private readonly IWebAuthService _auth;
    private readonly ConfigService _config;

    public List<WebUser> Users { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Message { get; set; }

    [BindProperty]
    public string NewUsername { get; set; } = "";

    [BindProperty]
    public string NewPassword { get; set; } = "";

    [BindProperty]
    public bool NewIsAdmin { get; set; }

    [BindProperty]
    public AuthConfig Auth { get; set; } = new();

    public UsersModel(IWebAuthService auth, ConfigService config)
    {
        _auth = auth;
        _config = config;
    }

    public void OnGet()
    {
        Users = _auth.GetUsers();
        Auth = _config.GetConfig().WebHost.Auth;
    }

    public async Task<IActionResult> OnPostSaveAuthAsync()
    {
        var cfg = _config.GetConfig();
        Auth.Users = cfg.WebHost.Auth.Users;
        cfg.WebHost.Auth = Auth;
        await _config.SaveAsync(cfg);
        Message = "Authentication settings saved.";
        return RedirectToPage(new { Message });
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        var cfg = _config.GetConfig();
        if (!_auth.AddUser(NewUsername, NewPassword, NewIsAdmin, out var error))
        {
            Message = error;
            return Page();
        }

        await _config.SaveAsync();
        return RedirectToPage(new { Message = $"User '{NewUsername}' added." });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string username)
    {
        if (!_auth.DeleteUser(username, out var error))
        {
            return RedirectToPage(new { Message = error });
        }

        await _config.SaveAsync();
        return RedirectToPage(new { Message = $"User '{username}' deleted." });
    }

    public async Task<IActionResult> OnPostToggleEnabledAsync(string username)
    {
        var user = _auth.FindUser(username);
        if (user is null) return RedirectToPage();

        if (!_auth.ToggleUserEnabled(username, !user.IsEnabled, out var error))
        {
            return RedirectToPage(new { Message = error });
        }

        await _config.SaveAsync();
        return RedirectToPage(new { Message = $"User '{username}' enabled state updated." });
    }

    public async Task<IActionResult> OnPostToggleAdminAsync(string username)
    {
        var user = _auth.FindUser(username);
        if (user is null) return RedirectToPage();

        if (!_auth.SetUserAdmin(username, !user.IsAdmin, out var error))
        {
            return RedirectToPage(new { Message = error });
        }

        await _config.SaveAsync();
        return RedirectToPage(new { Message = $"User '{username}' admin state updated." });
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string username, string newPassword)
    {
        var cfg = _config.GetConfig();
        var user = _auth.FindUser(username);
        if (user is null) return RedirectToPage();

        if (!_auth.ValidatePassword(newPassword, cfg.WebHost.Auth, out var error))
        {
            return RedirectToPage(new { Message = error });
        }

        _auth.SetPassword(user, newPassword);
        user.RequirePasswordChange = true;
        await _config.SaveAsync();
        return RedirectToPage(new { Message = $"Password reset for '{username}'." });
    }
}
