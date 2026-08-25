using System.Security.Claims;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public enum LoginResult
{
    Success,
    InvalidCredentials,
    AccountDisabled,
    LockedOut
}

public interface IWebAuthService
{
    WebUser? FindUser(string username);
    bool ValidateCredentials(string username, string password, out WebUser? user);
    LoginResult AttemptLogin(string username, string password, out WebUser? user, out TimeSpan? lockoutRemaining);
    void SetPassword(WebUser user, string password);
    bool ValidatePassword(string password, AuthConfig rules, out string error);
    List<WebUser> GetUsers();
    bool AddUser(string username, string password, bool isAdmin, out string error);
    bool DeleteUser(string username, out string error);
    bool ToggleUserEnabled(string username, bool enabled, out string error);
    bool UnlockUser(string username, out string error);
    bool SetUserAdmin(string username, bool isAdmin, out string error);
    ClaimsIdentity CreateClaimsIdentity(WebUser user);
}

public class WebAuthService : IWebAuthService
{
    private readonly ConfigService _config;
    private readonly IApplicationSessionService _session;

    public WebAuthService(ConfigService config, IApplicationSessionService session)
    {
        _config = config;
        _session = session;
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
        if (user is null || !user.IsEnabled || string.IsNullOrWhiteSpace(user.PasswordHash) || string.IsNullOrEmpty(password))
            return false;

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    // Used only by the unauthenticated Login page. ChangePassword's "confirm current password" check keeps
    // using ValidateCredentials directly since that already requires an active, authenticated session.
    public LoginResult AttemptLogin(string username, string password, out WebUser? user, out TimeSpan? lockoutRemaining)
    {
        lockoutRemaining = null;
        var cfg = _config.GetConfig();
        user = FindUser(username);
        if (user is null)
            return LoginResult.InvalidCredentials;

        if (!user.IsEnabled)
            return LoginResult.AccountDisabled;

        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > DateTimeOffset.UtcNow)
        {
            lockoutRemaining = lockoutEnd - DateTimeOffset.UtcNow;
            return LoginResult.LockedOut;
        }

        if (!string.IsNullOrEmpty(password) && !string.IsNullOrWhiteSpace(user.PasswordHash) && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            if (user.FailedLoginAttempts != 0 || user.LockoutEndUtc != null)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEndUtc = null;
                _config.SaveAsync(cfg).GetAwaiter().GetResult();
            }
            return LoginResult.Success;
        }

        user.FailedLoginAttempts++;
        var maxAttempts = Math.Max(1, cfg.WebHost.Auth.MaxFailedLoginAttempts);
        if (user.FailedLoginAttempts >= maxAttempts)
        {
            var duration = TimeSpan.FromMinutes(Math.Max(1, cfg.WebHost.Auth.LockoutDurationMinutes));
            user.LockoutEndUtc = DateTimeOffset.UtcNow.Add(duration);
            user.FailedLoginAttempts = 0;
            _config.SaveAsync(cfg).GetAwaiter().GetResult();
            lockoutRemaining = duration;
            user = null;
            return LoginResult.LockedOut;
        }

        _config.SaveAsync(cfg).GetAwaiter().GetResult();
        user = null;
        return LoginResult.InvalidCredentials;
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

    public bool UnlockUser(string username, out string error)
    {
        var cfg = _config.GetConfig();
        var user = cfg.WebHost.Auth.Users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            error = "User not found.";
            return false;
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
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
            new(ClaimTypes.NameIdentifier, user.Username),
            new("SessionToken", _session.SessionToken)
        };

        if (user.IsAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        if (user.RequirePasswordChange)
            claims.Add(new Claim("RequirePasswordChange", "true"));

        return new ClaimsIdentity(claims, "Cookies");
    }
}
