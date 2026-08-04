using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

// The status code matters to the caller: a 404 means the stored href no longer resolves and the mapping needs
// repairing, which is a different problem from a rejected payload.
public record GallagherWriteResult(bool Success, int StatusCode, string? Error)
{
    public bool NotFound => StatusCode == 404;
}

public interface IGallagherConnector
{
    bool? LastConnectionResult { get; }
    string? LastConnectionError { get; }
    Task<bool> TestConnectionAsync(CancellationToken ct = default, bool raiseNotifications = false);
    Task<JsonElement?> CreateCardholderAsync(object payload, CancellationToken ct = default);
    Task<GallagherWriteResult> UpdateCardholderAsync(string href, object payload, CancellationToken ct = default);
    Task<IReadOnlyList<JsonElement>> GetCompetenciesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JsonElement>> GetDivisionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JsonElement>> GetAccessGroupsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JsonElement>> GetPersonalDataFieldsAsync(int limit = 100, CancellationToken ct = default);
    Task<IReadOnlyList<JsonElement>> GetCardholdersAsync(int limit = 100, CancellationToken ct = default, string? expand = null);
    Task<IReadOnlyList<JsonElement>> GetAllCardholdersAsync(CancellationToken ct = default, string? expand = null);
    Task<JsonElement?> GetCardholderAsync(string href, string? expand = null, CancellationToken ct = default);
    Task<string?> GetLastErrorAsync();
    Task<JsonElement?> GetApiRootAsync(CancellationToken ct = default);
}

public class GallagherConnector : IGallagherConnector
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ConfigService _config;
    private readonly Serilog.ILogger _logger;
    private readonly INotificationService _notifications;
    private string? _lastError;
    private JsonElement? _apiRoot;
    private bool? _lastConnectionResult;
    private bool? _lastNotifiedResult;
    private readonly object _connectionLock = new();

    public bool? LastConnectionResult
    {
        get { lock (_connectionLock) return _lastConnectionResult; }
    }

    public string? LastConnectionError
    {
        get { lock (_connectionLock) return _lastError; }
    }

    public GallagherConnector(IHttpClientFactory httpFactory, ConfigService config, INotificationService notifications, Serilog.ILogger? logger = null)
    {
        _httpFactory = httpFactory;
        _config = config;
        _notifications = notifications;
        _logger = logger ?? Serilog.Log.Logger.ForContext<GallagherConnector>();
    }

    private GallagherConfig Cfg => _config.GetConfig().Gallagher;

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default, bool raiseNotifications = false)
    {
        try
        {
            _logger.Information("Testing Gallagher connection to {BaseUrl}", Cfg.BaseUrl);
            if (string.IsNullOrWhiteSpace(Cfg.BaseUrl)) { _lastError = "Gallagher BaseUrl not configured"; _logger.Warning("Gallagher BaseUrl is empty"); UpdateConnectionState(false, raiseNotifications); return false; }

            // Perform a fresh request rather than relying on the cached API root.
            var client = CreateClient();
            var response = await client.GetAsync("api", ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _lastError = $"Gallagher connection failed: {(int)response.StatusCode} {body}";
                _logger.Warning("Gallagher connection test failed: {Error}", _lastError);
                _apiRoot = null;
                UpdateConnectionState(false, raiseNotifications);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            _apiRoot = doc.RootElement.Clone();
            _lastError = null;
            _logger.Information("Gallagher connection OK");
            UpdateConnectionState(true, raiseNotifications);
            return true;
        }
        catch (OperationCanceledException)
        {
            _lastError = "Connection test was cancelled";
            _logger.Debug("Gallagher connection test was cancelled");
            UpdateConnectionState(false, raiseNotifications);
            return false;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _apiRoot = null;
            _logger.Error(ex, "Gallagher connection test failed");
            UpdateConnectionState(false, raiseNotifications);
            return false;
        }
    }

    private void UpdateConnectionState(bool ok, bool raiseNotifications)
    {
        lock (_connectionLock)
        {
            _lastConnectionResult = ok;
            if (!raiseNotifications) return;
            if (_lastNotifiedResult == ok) return;
            _lastNotifiedResult = ok;
        }
        try
        {
            if (ok)
                _notifications.RaiseEventAsync(NotificationEventType.ConnectionRestored, "Gallagher connection restored.").GetAwaiter().GetResult();
            else
                _notifications.RaiseEventAsync(NotificationEventType.ConnectionInterrupted, $"Gallagher connection interrupted. {_lastError}").GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to raise Gallagher connection notification");
        }
    }

    public async Task<JsonElement?> GetApiRootAsync(CancellationToken ct = default)
    {
        if (_apiRoot.HasValue) return _apiRoot.Value;
        _logger.Information("Discovering Gallagher API root at {BaseUrl}api", (Cfg.BaseUrl ?? string.Empty).TrimEnd('/') + "/");
        var client = CreateClient();
        _logger.Information("Sending Gallagher GET api");
        var response = await client.GetAsync("api", ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _lastError = $"Gallagher discovery failed: {(int)response.StatusCode} {body}";
            _logger.Error(_lastError);
            return null;
        }
        var json = await response.Content.ReadAsStringAsync(ct);
        _logger.Information("Gallagher discovery returned {StatusCode} ({Length} bytes)", (int)response.StatusCode, json.Length);
        _logger.Debug("Gallagher discovery body: {Json}", json);
        using var doc = JsonDocument.Parse(json);
        _apiRoot = doc.RootElement.Clone();
        _logger.Information("Gallagher API root discovered");
        return _apiRoot.Value;
    }

    public async Task<JsonElement?> CreateCardholderAsync(object payload, CancellationToken ct = default)
    {
        var root = await GetApiRootAsync(ct);
        if (!root.HasValue) return null;
        var client = CreateClient();
        var href = GetHref(root.Value, "cardholders");
        if (string.IsNullOrEmpty(href)) { _lastError = "No cardholders href discovered"; return null; }
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        _logger.Information("Gallagher --> POST {Url} ({Bytes} bytes)", href, json.Length);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.PostAsync(href, content, ct);
        var result = await response.Content.ReadAsStringAsync(ct);
        stopwatch.Stop();
        _logger.Information("Gallagher <-- {Status} POST {Url} in {ElapsedMs} ms, {Bytes} bytes", (int)response.StatusCode, href, stopwatch.ElapsedMilliseconds, result.Length);
        if (!response.IsSuccessStatusCode)
        {
            _lastError = $"Gallagher create failed: {(int)response.StatusCode} {result}";
            _logger.Error(_lastError);
            return null;
        }
        // A successful create is a 201 with an empty body: the new cardholder is identified only by the
        // Location header. Parsing the body unconditionally threw a JsonReaderException on every create.
        var location = response.Headers.Location?.ToString();

        JsonElement? parsed = null;
        if (!string.IsNullOrWhiteSpace(result))
        {
            try
            {
                using var doc = JsonDocument.Parse(result);
                parsed = doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                _logger.Warning(ex, "Gallagher create returned a body that is not JSON: {Body}", result.Length <= 300 ? result : result[..300]);
            }
        }

        if (parsed.HasValue && parsed.Value.ValueKind == JsonValueKind.Object && parsed.Value.TryGetProperty("href", out _))
            return parsed;

        if (!string.IsNullOrWhiteSpace(location))
            return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["href"] = location,
                ["id"] = location.TrimEnd('/').Split('/').LastOrDefault()
            });

        if (parsed.HasValue)
        {
            _logger.Warning("Gallagher accepted the cardholder but returned no href and no Location header");
            return parsed;
        }

        _lastError = $"Gallagher accepted the cardholder ({(int)response.StatusCode}) but returned neither a body nor a Location header, so it cannot be linked to the OnLocation record";
        _logger.Error(_lastError);
        return null;
    }

    public async Task<GallagherWriteResult> UpdateCardholderAsync(string href, object payload, CancellationToken ct = default)
    {
        var client = CreateClient();
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        _logger.Information("Gallagher --> PATCH {Url} ({Bytes} bytes)", href, json.Length);
        _logger.Debug("Gallagher --> PATCH body {Url}: {Body}", href, json);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.PatchAsync(href, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        stopwatch.Stop();
        _logger.Information("Gallagher <-- {Status} PATCH {Url} in {ElapsedMs} ms, {Bytes} bytes", (int)response.StatusCode, href, stopwatch.ElapsedMilliseconds, body.Length);

        if (response.IsSuccessStatusCode) return new GallagherWriteResult(true, (int)response.StatusCode, null);

        var error = response.StatusCode == System.Net.HttpStatusCode.NotFound
            ? $"Cardholder {href} does not exist in Command Centre (404). It was probably deleted, or the REST operator cannot see its division."
            : $"Gallagher update failed: {(int)response.StatusCode} {body}";
        _lastError = error;
        _logger.Error("Gallagher PATCH {Url} failed: {Status} {Body}", href, (int)response.StatusCode, body);
        return new GallagherWriteResult(false, (int)response.StatusCode, error);
    }

    // A single unpaged request only returned the server's default page, so a competency further down the
    // list looked as though it did not exist.
    public Task<IReadOnlyList<JsonElement>> GetCompetenciesAsync(CancellationToken ct = default)
        => GetCollectionAsync("competencies", "api/competencies", ct, "competencies", "results");

    public async Task<IReadOnlyList<JsonElement>> GetPersonalDataFieldsAsync(int limit = 100, CancellationToken ct = default)
    {
        var root = await GetApiRootAsync(ct);
        if (!root.HasValue) return Array.Empty<JsonElement>();
        var client = CreateClient();
        var href = GetHref(root.Value, "personalDataFields");
        if (string.IsNullOrEmpty(href)) { _lastError = "No personalDataFields href discovered"; _logger.Warning(_lastError); return Array.Empty<JsonElement>(); }

        var response = await client.GetAsync($"{href}?limit={limit}", ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _lastError = $"Gallagher personalDataFields request failed: {(int)response.StatusCode} {body}";
            _logger.Error(_lastError);
            return Array.Empty<JsonElement>();
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        _logger.Information("Gallagher personalDataFields returned {StatusCode} ({Length} bytes)", (int)response.StatusCode, json.Length);
        _logger.Debug("Gallagher personalDataFields body: {Json}", json);
        using var doc = JsonDocument.Parse(json);
        return ExtractArray(doc.RootElement, "personalDataFields", "results");
    }

    public async Task<JsonElement?> GetCardholderAsync(string href, string? expand = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        var url = string.IsNullOrWhiteSpace(expand) ? href : $"{href}{(href.Contains('?') ? "&" : "?")}fields={Uri.EscapeDataString(expand)}";
        var (response, body) = await GetLoggedAsync(client, url, ct);
        if (!response.IsSuccessStatusCode)
        {
            _lastError = $"Gallagher cardholder request failed: {(int)response.StatusCode} {body}";
            _logger.Error(_lastError);
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    public async Task<IReadOnlyList<JsonElement>> GetCardholdersAsync(int limit = 100, CancellationToken ct = default, string? expand = null)
    {
        var root = await GetApiRootAsync(ct);
        if (!root.HasValue) return Array.Empty<JsonElement>();
        var client = CreateClient();
        var href = GetHref(root.Value, "cardholders");
        if (string.IsNullOrEmpty(href)) { _lastError = "No cardholders href discovered"; return Array.Empty<JsonElement>(); }

        var query = new List<string> { $"limit={limit}" };
        if (!string.IsNullOrWhiteSpace(expand)) query.Add($"fields={Uri.EscapeDataString(expand)}");
        var results = new List<JsonElement>();
        var response = await client.GetAsync($"{href}?{string.Join("&", query)}", ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _lastError = $"Gallagher cardholders search failed: {(int)response.StatusCode} {body}";
            _logger.Error(_lastError);
            return results;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        _logger.Information("Gallagher cardholders returned {StatusCode} ({Length} bytes): {Json}", (int)response.StatusCode, json.Length, json);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
            foreach (var item in doc.RootElement.EnumerateArray()) results.Add(item.Clone());
        else if (doc.RootElement.TryGetProperty("cardholders", out var ch) && ch.ValueKind == JsonValueKind.Array)
            foreach (var item in ch.EnumerateArray()) results.Add(item.Clone());
        else if (doc.RootElement.TryGetProperty("results", out var r) && r.ValueKind == JsonValueKind.Array)
            foreach (var item in r.EnumerateArray()) results.Add(item.Clone());

        return results;
    }

    public async Task<IReadOnlyList<JsonElement>> GetAllCardholdersAsync(CancellationToken ct = default, string? expand = null)
    {
        var root = await GetApiRootAsync(ct);
        if (!root.HasValue) return Array.Empty<JsonElement>();
        var href = GetHref(root.Value, "cardholders");
        if (string.IsNullOrWhiteSpace(href)) return Array.Empty<JsonElement>();

        var results = new List<JsonElement>();
        var separator = href.Contains('?') ? "&" : "?";
        var next = $"{href}{separator}sort=id&top=1000";
        if (!string.IsNullOrWhiteSpace(expand)) next += $"&fields={Uri.EscapeDataString(expand)}";
        var client = CreateClient();
        var page = 0;
        while (!string.IsNullOrWhiteSpace(next) && !ct.IsCancellationRequested)
        {
            var (response, body) = await GetLoggedAsync(client, next, ct);
            if (!response.IsSuccessStatusCode)
            {
                _lastError = $"Gallagher cardholder search failed: {(int)response.StatusCode} {body}";
                _logger.Error(_lastError);
                break;
            }

            using var doc = JsonDocument.Parse(body);
            var pageResults = ExtractArray(doc.RootElement, "cardholders", "results");
            results.AddRange(pageResults);
            page++;
            _logger.Information("Gallagher cardholders page {Page}: {PageCount} cardholder(s), running total {Total}", page, pageResults.Count, results.Count);
            next = doc.RootElement.TryGetProperty("next", out var nextElement) && nextElement.ValueKind == JsonValueKind.Object && nextElement.TryGetProperty("href", out var nextHref)
                ? nextHref.GetString()
                : null;
        }

        _logger.Information("Gallagher returned {Total} cardholder(s) over {Pages} page(s) with fields '{Fields}'", results.Count, page, expand ?? "(defaults)");
        return results;
    }

    public Task<IReadOnlyList<JsonElement>> GetDivisionsAsync(CancellationToken ct = default)
        => GetCollectionAsync("divisions", "api/divisions", ct, "divisions", "results");

    public Task<IReadOnlyList<JsonElement>> GetAccessGroupsAsync(CancellationToken ct = default)
        => GetCollectionAsync("accessGroups", "api/access_groups", ct, "accessGroups", "results");

    // The href should always come from the /api feature list. Older servers do not advertise every
    // collection, so a documented default path is used rather than failing outright.
    private async Task<IReadOnlyList<JsonElement>> GetCollectionAsync(string feature, string fallbackPath, CancellationToken ct, params string[] keys)
    {
        var root = await GetApiRootAsync(ct);
        if (!root.HasValue) return Array.Empty<JsonElement>();

        var href = GetHref(root.Value, feature);
        if (string.IsNullOrWhiteSpace(href))
        {
            _logger.Warning("Gallagher did not advertise a '{Feature}' link, falling back to {Path}", feature, fallbackPath);
            href = fallbackPath;
        }

        var results = new List<JsonElement>();
        var client = CreateClient();
        var next = $"{href}{(href.Contains('?') ? "&" : "?")}sort=id&top=1000";
        while (!string.IsNullOrWhiteSpace(next) && !ct.IsCancellationRequested)
        {
            var (response, body) = await GetLoggedAsync(client, next, ct);
            if (!response.IsSuccessStatusCode)
            {
                _lastError = $"Gallagher {feature} request failed: {(int)response.StatusCode} {body}";
                _logger.Error(_lastError);
                break;
            }

            using var doc = JsonDocument.Parse(body);
            results.AddRange(ExtractArray(doc.RootElement, keys));
            next = doc.RootElement.TryGetProperty("next", out var nextElement) && nextElement.ValueKind == JsonValueKind.Object && nextElement.TryGetProperty("href", out var nextHref)
                ? nextHref.GetString()
                : null;
        }

        _logger.Information("Gallagher returned {Count} {Feature}", results.Count, feature);
        return results;
    }

    public Task<string?> GetLastErrorAsync() => Task.FromResult(_lastError);

    private async Task<(HttpResponseMessage Response, string Body)> GetLoggedAsync(HttpClient client, string url, CancellationToken ct)
    {
        _logger.Information("Gallagher --> GET {Url}", url);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        stopwatch.Stop();
        _logger.Information("Gallagher <-- {Status} {Url} in {ElapsedMs} ms, {Bytes} bytes",
            (int)response.StatusCode, url, stopwatch.ElapsedMilliseconds, body.Length);
        _logger.Debug("Gallagher <-- body {Url}: {Body}", url, body);
        return (response, body);
    }

    private HttpClient CreateClient()
    {
        if (string.IsNullOrWhiteSpace(Cfg.BaseUrl))
            throw new InvalidOperationException("Gallagher BaseUrl is not configured.");

        var baseUri = new Uri(Cfg.BaseUrl.TrimEnd('/') + "/");
        var builder = new UriBuilder(baseUri.Scheme, baseUri.Host, Cfg.Port, baseUri.AbsolutePath);

        var client = _httpFactory.CreateClient("Gallagher");
        client.BaseAddress = builder.Uri;
        client.Timeout = TimeSpan.FromMinutes(2);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(Cfg.ApiKey))
        {
            var key = Cfg.ApiKey.Trim();
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{key}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            _logger.Debug("Gallagher request will use Basic auth with key prefix {KeyPrefix}", key.Length > 4 ? key[..4] : "(short)");
        }
        else
        {
            _logger.Warning("Gallagher API key is not configured");
        }

        return client;
    }

    private static List<JsonElement> ExtractArray(JsonElement root, params string[] preferredKeys)
    {
        var list = new List<JsonElement>();

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray()) list.Add(item.Clone());
            return list;
        }

        if (root.ValueKind != JsonValueKind.Object) return list;

        foreach (var key in preferredKeys)
        {
            if (root.TryGetProperty(key, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.EnumerateArray()) list.Add(item.Clone());
                    return list;
                }

                if (prop.ValueKind == JsonValueKind.Object)
                {
                    foreach (var child in prop.EnumerateObject())
                    {
                        if (child.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in child.Value.EnumerateArray()) list.Add(item.Clone());
                            return list;
                        }
                    }
                }
            }
        }

        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.Value.EnumerateArray()) list.Add(item.Clone());
                return list;
            }
        }

        return list;
    }

    private static string? GetHref(JsonElement root, string featureName)
    {
        if (root.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Object)
        {
            if (features.TryGetProperty(featureName, out var feature) && feature.ValueKind == JsonValueKind.Object)
            {
                if (feature.TryGetProperty("href", out var href))
                    return href.GetString();

                if (feature.TryGetProperty(featureName, out var innerFeature) && innerFeature.ValueKind == JsonValueKind.Object && innerFeature.TryGetProperty("href", out var innerHref))
                    return innerHref.GetString();

                foreach (var prop in feature.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object && prop.Value.TryGetProperty("href", out var nestedHref))
                        return nestedHref.GetString();
                }
            }
        }

        if (root.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in links.EnumerateArray())
            {
                if (link.ValueKind != JsonValueKind.Object) continue;
                string? rel = null;
                if (link.TryGetProperty("rel", out var relProp) && relProp.ValueKind == JsonValueKind.String) rel = relProp.GetString();
                if (rel == null && link.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String) rel = nameProp.GetString();
                if (string.Equals(rel, featureName, StringComparison.OrdinalIgnoreCase) &&
                    link.TryGetProperty("href", out var linkHref) && linkHref.ValueKind == JsonValueKind.String)
                    return linkHref.GetString();
            }
        }

        if (root.TryGetProperty(featureName, out var direct))
        {
            if (direct.ValueKind == JsonValueKind.Object && direct.TryGetProperty("href", out var dhref))
                return dhref.GetString();
            if (direct.ValueKind == JsonValueKind.String)
                return direct.GetString();
        }

        return null;
    }
}

internal static class JsonExtensions
{
    public static JsonElement? CloneIfNotDefault(this JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Undefined) return null;
        return element.Clone();
    }
}
