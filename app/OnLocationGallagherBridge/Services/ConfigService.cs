using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

[SupportedOSPlatform("windows")]
public class ConfigService
{
    private readonly string _configDir;
    private readonly string _configFile;
    private BridgeConfig _config = new();
    private readonly object _lock = new();

    public ConfigService()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        _configDir = Path.Combine(baseDir, "OnLocation-Gallagher-Bridge", "config");
        _configFile = Path.Combine(_configDir, "config.json.crypt");
    }

    public BridgeConfig GetConfig()
    {
        lock (_lock)
        {
            return _config;
        }
    }

    public void SetConfig(BridgeConfig config)
    {
        lock (_lock)
        {
            _config = config;
        }
    }

    public bool Exists => File.Exists(_configFile);

    public async Task LoadAsync()
    {
        if (!File.Exists(_configFile))
        {
            Directory.CreateDirectory(_configDir);
            _config = new BridgeConfig();
            return;
        }

        var encrypted = await File.ReadAllBytesAsync(_configFile);
        var json = Unprotect(encrypted);
        var cfg = JsonSerializer.Deserialize(json, typeof(BridgeConfig), SourceGenerationContext.Default) as BridgeConfig ?? new BridgeConfig();
        MigrateLegacyConfig(cfg);
        lock (_lock) { _config = cfg; }
    }

    public async Task SaveAsync(BridgeConfig? config = null)
    {
        config ??= _config;
        var json = JsonSerializer.Serialize(config, typeof(BridgeConfig), SourceGenerationContext.Default);
        var encrypted = Protect(Encoding.UTF8.GetBytes(json));

        Directory.CreateDirectory(_configDir);
        await File.WriteAllBytesAsync(_configFile, encrypted);
        lock (_lock) { _config = config; }
    }

    // Testing from scratch needs the stored credentials gone, not just blanked in memory.
    public void Delete()
    {
        if (File.Exists(_configFile)) File.Delete(_configFile);
        lock (_lock) { _config = new BridgeConfig(); }
    }

    public string ConfigFilePath => _configFile;
    public string ConfigDirectory => _configDir;

    private static void MigrateLegacyConfig(BridgeConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.WebHost.Urls))
        {
            var urls = cfg.WebHost.Urls.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(urls)
                && TryParseUrlWithExplicitPort(urls, out var scheme, out _, out var webPort)
                && webPort.HasValue)
            {
                cfg.WebHost.Https.Enabled = string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase);
                cfg.WebHost.Port = webPort.Value;
            }
            cfg.WebHost.Urls = null;
        }

        if (!string.IsNullOrWhiteSpace(cfg.Gallagher.BaseUrl)
            && TryParseUrlWithExplicitPort(cfg.Gallagher.BaseUrl, out var gallScheme, out var gallHost, out var gallPort))
        {
            // Only overwrite the port when the legacy URL actually specified one.
            // Otherwise leave the configured/default port (8904) alone.
            if (gallPort.HasValue)
                cfg.Gallagher.Port = gallPort.Value;
            cfg.Gallagher.BaseUrl = $"{gallScheme}://{gallHost}";
        }
    }

    private static bool TryParseUrlWithExplicitPort(string url, out string scheme, out string host, out int? port)
    {
        scheme = string.Empty;
        host = string.Empty;
        port = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        // Parses scheme://host[:port] optionally followed by a path. IPv6 hosts must be bracketed.
        var match = System.Text.RegularExpressions.Regex.Match(url.Trim(), @"^(https?)://([^/:]+|\[[^\]]+\])(?::(\d+))?(/.*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        scheme = match.Groups[1].Value.ToLowerInvariant();
        host = match.Groups[2].Value;
        if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var parsedPort))
            port = parsedPort;
        return true;
    }

    private static byte[] Protect(byte[] data)
    {
        return ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
    }

    private static byte[] Unprotect(byte[] data)
    {
        return ProtectedData.Unprotect(data, null, DataProtectionScope.LocalMachine);
    }
}
