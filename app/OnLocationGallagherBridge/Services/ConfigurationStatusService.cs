using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;
using System.Text.Json;

namespace OnLocationGallagherBridge.Services;

public enum ConfigurationStatus
{
    NotConfigured,
    Incomplete,
    Faulty,
    Complete
}

public record ConfigurationState(
    ConfigurationStatus ConnectorSettings,
    ConfigurationStatus FieldMapping,
    ConfigurationStatus InitialMatch,
    ConfigurationStatus Overall);

public record StatusBannerModel(string Title, ConfigurationStatus Status, string? Detail = null);

public interface IConfigurationStatusService
{
    ConfigurationStatus GetConnectorSettingsStatus(BridgeConfig config);
    Task<ConfigurationStatus> GetConnectorSettingsStatusAsync(BridgeConfig config, bool testConnections, CancellationToken ct = default);
    ConfigurationStatus GetFieldMappingStatus(SyncProfile? profile);
    ConfigurationStatus GetInitialMatchStatus(SyncProfile? profile);
    Task<ConfigurationState> GetStateAsync(SyncProfile? profile, bool testConnections, CancellationToken ct = default);
    Task<ConfigurationState> GetOverallStateAsync(bool testConnections, CancellationToken ct = default);
    Task<ConfigurationStatus> GetInitialMatchStatusForAllAsync(CancellationToken ct = default);
}

public class ConfigurationStatusService : IConfigurationStatusService
{
    private readonly ConfigService _config;
    private readonly IOnLocationConnector _onLocation;
    private readonly IGallagherConnector _gallagher;
    private readonly BridgeDbContext _db;

    public ConfigurationStatusService(ConfigService config, IOnLocationConnector onLocation, IGallagherConnector gallagher, BridgeDbContext db)
    {
        _config = config;
        _onLocation = onLocation;
        _gallagher = gallagher;
        _db = db;
    }

    public ConfigurationStatus GetConnectorSettingsStatus(BridgeConfig config)
    {
        if (config == null) return ConfigurationStatus.NotConfigured;

        bool onLocationHasCredentials = !string.IsNullOrWhiteSpace(config.OnLocation?.ClientId)
            || !string.IsNullOrWhiteSpace(config.OnLocation?.ApiKey)
            || !string.IsNullOrWhiteSpace(config.OnLocation?.Password);
        bool gallagherHasCredentials = !string.IsNullOrWhiteSpace(config.Gallagher?.ApiKey);

        if (!onLocationHasCredentials && !gallagherHasCredentials)
            return ConfigurationStatus.NotConfigured;

        if (!IsOnLocationComplete(config.OnLocation) || !IsGallagherComplete(config.Gallagher))
            return ConfigurationStatus.Incomplete;

        return ConfigurationStatus.Complete;
    }

    public async Task<ConfigurationStatus> GetConnectorSettingsStatusAsync(BridgeConfig config, bool testConnections, CancellationToken ct = default)
    {
        var status = GetConnectorSettingsStatus(config);
        if (status != ConfigurationStatus.Complete || !testConnections)
            return status;

        var onLocationOk = await _onLocation.TestConnectionAsync(ct);
        var gallagherOk = await _gallagher.TestConnectionAsync(ct);
        return onLocationOk && gallagherOk ? ConfigurationStatus.Complete : ConfigurationStatus.Faulty;
    }

    public ConfigurationStatus GetFieldMappingStatus(SyncProfile? profile)
    {
        if (profile == null) return ConfigurationStatus.NotConfigured;

        var maps = JsonSerializer.Deserialize<List<FieldMapDto>>(profile.FieldMapJson) ?? new List<FieldMapDto>();
        var hasMaps = maps.Any(m => !string.IsNullOrWhiteSpace(m.Target)
            && (!string.IsNullOrWhiteSpace(m.Source) || string.Equals(m.Transform, "rule-based", StringComparison.OrdinalIgnoreCase)));
        if (!hasMaps) return ConfigurationStatus.NotConfigured;

        var rules = JsonSerializer.Deserialize<List<MatchRuleDto>>(profile.MatchRulesJson) ?? new List<MatchRuleDto>();
        var hasPrimaryRule = rules.Any(r => r.IsPrimary
            && !string.IsNullOrWhiteSpace(r.SourceFields)
            && !string.IsNullOrWhiteSpace(r.TargetFields));
        return hasPrimaryRule ? ConfigurationStatus.Complete : ConfigurationStatus.Incomplete;
    }

    public ConfigurationStatus GetInitialMatchStatus(SyncProfile? profile)
    {
        if (profile == null) return ConfigurationStatus.NotConfigured;
        if (GetFieldMappingStatus(profile) == ConfigurationStatus.NotConfigured)
            return ConfigurationStatus.NotConfigured;
        if (profile.InitialMatchCompleted) return ConfigurationStatus.Complete;
        return ConfigurationStatus.Incomplete;
    }

    public async Task<ConfigurationState> GetStateAsync(SyncProfile? profile, bool testConnections, CancellationToken ct = default)
    {
        var config = _config.GetConfig();
        var connector = await GetConnectorSettingsStatusAsync(config, testConnections, ct);
        var mapping = GetFieldMappingStatus(profile);
        var initial = GetInitialMatchStatus(profile);
        var overall = MinStatus(connector, mapping, initial);
        return new ConfigurationState(connector, mapping, initial, overall);
    }

    public async Task<ConfigurationState> GetOverallStateAsync(bool testConnections, CancellationToken ct = default)
    {
        var config = _config.GetConfig();
        var connector = await GetConnectorSettingsStatusAsync(config, testConnections, ct);
        var profiles = await _db.SyncProfiles.AsNoTracking().ToListAsync(ct);
        if (profiles.Count == 0)
        {
            return new ConfigurationState(
                connector,
                ConfigurationStatus.NotConfigured,
                ConfigurationStatus.NotConfigured,
                MinStatus(connector, ConfigurationStatus.NotConfigured, ConfigurationStatus.NotConfigured));
        }

        var mapping = profiles.Select(GetFieldMappingStatus).Max();
        var initial = profiles.Select(GetInitialMatchStatus).Max();
        var overall = MinStatus(connector, mapping, initial);
        return new ConfigurationState(connector, mapping, initial, overall);
    }

    public async Task<ConfigurationStatus> GetInitialMatchStatusForAllAsync(CancellationToken ct = default)
    {
        var profiles = await _db.SyncProfiles.AsNoTracking().ToListAsync(ct);
        if (profiles.Count == 0) return ConfigurationStatus.NotConfigured;
        return profiles.Select(GetInitialMatchStatus).Max();
    }

    private static bool IsOnLocationComplete(OnLocationConfig? c)
    {
        if (c == null || string.IsNullOrWhiteSpace(c.BaseUrl))
            return false;

        return c.AuthMode switch
        {
            "OAuth2" => !string.IsNullOrWhiteSpace(c.ClientId) && !string.IsNullOrWhiteSpace(c.ClientSecret),
            "ApiKey" => !string.IsNullOrWhiteSpace(c.ApiKey),
            "Basic" => !string.IsNullOrWhiteSpace(c.ApiKey) && !string.IsNullOrWhiteSpace(c.Password),
            _ => false
        };
    }

    private static bool IsGallagherComplete(GallagherConfig? c) =>
        c != null && !string.IsNullOrWhiteSpace(c.BaseUrl) && !string.IsNullOrWhiteSpace(c.ApiKey);

    private static ConfigurationStatus MinStatus(params ConfigurationStatus[] statuses)
    {
        var result = ConfigurationStatus.Complete;
        foreach (var s in statuses)
            if (s < result) result = s;
        return result;
    }
}
