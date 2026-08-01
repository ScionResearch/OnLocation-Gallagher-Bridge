using System.Security.Claims;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public interface IWebAuthService
{
    WebUser? FindUser(string username);
    bool ValidateCredentials(string username, string password, out WebUser? user);
    void SetPassword(WebUser user, string password);
    bool ValidatePassword(string password, AuthConfig rules, out string error);
    List<WebUser> GetUsers();
    bool AddUser(string username, string password, bool isAdmin, out string error);
    bool DeleteUser(string username, out string error);
    bool ToggleUserEnabled(string username, bool enabled, out string error);
    bool SetUserAdmin(string username, bool isAdmin, out string error);
    ClaimsIdentity CreateClaimsIdentity(WebUser user);
}

public class WebAuthService : IWebAuthService
{
    private readonly ConfigService _config;

    public WebAuthService(ConfigService config)
    {
        _config = config;
    }

    public WebUser? FindUser(string username)
    {
        var cfg = _config.GetConfig();
        return cfg.WebHost.Auth.Users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
    }

    public bool ValidateCredentials(string username, string password, out WebUser? user)
    {
        user = FindUser(username);
        if (user is null || !user.IsEnabled || string.IsNullOrWhiteSpace(user.PasswordHash))
            return false;

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    public void SetPassword(WebUser user, string password)
    {
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public bool ValidatePassword(string password, AuthConfig rules, out string error)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < rules.PasswordMinimumLength)
        {
            error = $"Password must be at least {rules.PasswordMinimumLength} characters long.";
            return false;
        }

        if (rules.PasswordRequireUppercase && !password.Any(char.IsUpper))
        {
            error = "Password must contain at least one uppercase letter.";
            return false;
        }

        if (rules.PasswordRequireLowercase && !password.Any(char.IsLower))
        {
            error = "Password must contain at least one lowercase letter.";
            return false;
        }

        if (rules.PasswordRequireDigit && !password.Any(char.IsDigit))
        {
            error = "Password must contain at least one digit.";
            return false;
        }

        if (rules.PasswordRequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
        {
            error = "Password must contain at least one non-alphanumeric character.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public List<WebUser> GetUsers()
    {
        return _config.GetConfig().WebHost.Auth.Users.ToList();
    }

    public bool AddUser(string username, string password, bool isAdmin, out string error)
    {
        var cfg = _config.GetConfig();
        if (string.IsNullOrWhiteSpace(username))
        {
            error = "Username is required.";
            return false;
        }

        if (cfg.WebHost.Auth.Users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
        {
            error = "A user with that name already exists.";
            return false;
        }

        if (!ValidatePassword(password, cfg.WebHost.Auth, out error))
            return false;

        var user = new WebUser
        {
            Username = username.Trim(),
            IsAdmin = isAdmin
        };
        SetPassword(user, password);
        cfg.WebHost.Auth.Users.Add(user);
        return true;
    }

    public bool DeleteUser(string username, out string error)
    {
        var cfg = _config.GetConfig();
        var user = cfg.WebHost.Auth.Users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            error = "User not found.";
            return false;
        }

        var enabledCount = cfg.WebHost.Auth.Users.Count(u => u.IsEnabled);
        if (user.IsEnabled && enabledCount <= 1)
        {
            error = "Cannot delete the last enabled user.";
            return false;
        }

        cfg.WebHost.Auth.Users.Remove(user);
        error = string.Empty;
        return true;
    }

    public bool ToggleUserEnabled(string username, bool enabled, out string error)
    {
        var cfg = _config.GetConfig();
        var user = cfg.WebHost.Auth.Users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            error = "User not found.";
            return false;
        }

        if (!enabled && cfg.WebHost.Auth.Users.Count(u => u.IsEnabled) <= 1)
        {
            error = "Cannot disable the last enabled user.";
            return false;
        }

        user.IsEnabled = enabled;
        error = string.Empty;
        return true;
    }

    public bool SetUserAdmin(string username, bool isAdmin, out string error)
    {
        var cfg = _config.GetConfig();
        var user = cfg.WebHost.Auth.Users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            error = "User not found.";
            return false;
        }

        user.IsAdmin = isAdmin;
        error = string.Empty;
        return true;
    }

    public ClaimsIdentity CreateClaimsIdentity(WebUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.NameIdentifier, user.Username)
        };

        if (user.IsAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        if (user.RequirePasswordChange)
            claims.Add(new Claim("RequirePasswordChange", "true"));

        return new ClaimsIdentity(claims, "Cookies");
    }
}
