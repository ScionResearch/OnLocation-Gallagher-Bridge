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

    private static byte[] Protect(byte[] data)
    {
        return ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
    }

    private static byte[] Unprotect(byte[] data)
    {
        return ProtectedData.Unprotect(data, null, DataProtectionScope.LocalMachine);
    }
}
