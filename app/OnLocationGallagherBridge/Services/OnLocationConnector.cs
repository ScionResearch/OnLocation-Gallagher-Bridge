using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

public interface IOnLocationConnector
{
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JsonElement>> GetStaffAsync(SyncBookmark bookmark, CancellationToken ct = default, int limit = OnLocationConnector.DefaultPageSize);
    Task<IReadOnlyList<JsonElement>> GetContractorMembersAsync(SyncBookmark bookmark, CancellationToken ct = default, int limit = OnLocationConnector.DefaultPageSize);
    Task<IReadOnlyList<JsonElement>> GetInductionsAsync(SyncBookmark bookmark, CancellationToken ct = default);
    Task<IReadOnlyList<JsonElement>> GetInductionHoldersAsync(string inductionId, SyncBookmark bookmark, CancellationToken ct = default, int limit = OnLocationConnector.DefaultPageSize);
    Task<IReadOnlyList<JsonElement>> GetInductionHoldersCompletedSinceAsync(string inductionId, DateTimeOffset since, IProgress<OnLocationFetchProgress>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyList<JsonElement>> GetRecentInductionHoldersAsync(string inductionId, int limit, CancellationToken ct = default);
    Task<InductionHolderScan> GetNewInductionHoldersAsync(string inductionId, string? afterId, DateTimeOffset completedSince, CancellationToken ct = default);
    Task<IReadOnlyList<JsonElement>> GetRecordsByIdAsync(string endpoint, IReadOnlyCollection<string> ids, CancellationToken ct = default);
    Task<string?> GetLastErrorAsync();
}

// Records is what fell inside the window; HighestId is the highest id seen whether it was in the window or
// not, so the bookmark still advances past records that were skipped.
public record InductionHolderScan(IReadOnlyList<JsonElement> Records, string? HighestId, int Scanned);

public class OnLocationConnector : IOnLocationConnector
{
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 1000;
    private const int MaxPagesPerRequest = 200;
    // Measured against the live tenant: holder response time scales with `limit`, roughly 0.3s per
    // record (10 records ~5s, 50 ~15s, 100 ~30s). Larger pages exceed the request timeout, so this
    // stays small and pages instead.
    private const int HolderPageSize = 100;
    private const int PeriodicHolderPageSize = 10;
    private const int MaxHolderPages = 200;
    // Holder ids are assigned when the induction is issued, not when it is completed, so a learner who
    // takes a year to finish leaves a low id with a recent `completed` date. Scanning stops this many
    // pages after the first page with no in-window records, which tolerates that skew without walking
    // the entire history. Raise this if records are being missed.
    private const int HolderLookaheadPages = 1;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(3);

    private readonly IHttpClientFactory _httpFactory;
    private readonly ConfigService _config;
    private readonly Serilog.ILogger _logger;
    private readonly ISyncActivity _activity;
    private string? _lastError;

    public OnLocationConnector(IHttpClientFactory httpFactory, ConfigService config, ISyncActivity activity, Serilog.ILogger? logger = null)
    {
        _httpFactory = httpFactory;
        _config = config;
        _activity = activity;
        _logger = logger ?? Serilog.Log.Logger.ForContext<OnLocationConnector>();
    }

    private OnLocationConfig Cfg => _config.GetConfig().OnLocation;

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var mode = Cfg.AuthMode.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(Cfg.BaseUrl)) { _lastError = "OnLocation BaseUrl not configured"; return false; }
            if (mode == "oauth2" && (string.IsNullOrWhiteSpace(Cfg.ClientId) || string.IsNullOrWhiteSpace(Cfg.ClientSecret)))
                { _lastError = "OAuth2 Client Id and Secret are required"; return false; }
            if ((mode == "apikey" || mode == "basic") && string.IsNullOrWhiteSpace(Cfg.ApiKey))
                { _lastError = "API Key is required for API key / Basic auth"; return false; }

            var client = await CreateClientAsync(ct);
            var (response, testBody) = await SendLoggedAsync(client, new HttpRequestMessage(HttpMethod.Get, "staff?limit=1"), ct);
            return await HandleResponseAsync(response, testBody, ct);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger.Error(ex, "OnLocation connection test failed");
            return false;
        }
    }

    public Task<IReadOnlyList<JsonElement>> GetStaffAsync(SyncBookmark bookmark, CancellationToken ct = default, int limit = DefaultPageSize)
        => FetchAllAsync("staff", bookmark, ct, limit);

    public Task<IReadOnlyList<JsonElement>> GetContractorMembersAsync(SyncBookmark bookmark, CancellationToken ct = default, int limit = DefaultPageSize)
        => FetchAllAsync("sp/member", bookmark, ct, limit);

    public Task<IReadOnlyList<JsonElement>> GetInductionsAsync(SyncBookmark bookmark, CancellationToken ct = default)
        => FetchAllAsync("induction", bookmark, ct);

    public Task<IReadOnlyList<JsonElement>> GetInductionHoldersAsync(string inductionId, SyncBookmark bookmark, CancellationToken ct = default, int limit = DefaultPageSize)
        => FetchAllAsync($"induction/{inductionId}/holder", bookmark, ct, limit);

    // The routine poll scans holder records by descending id and applies the completion-date window client-side,
    // stopping after a short run of pages with no in-window records. Requesting 10 records at a time keeps the
    // calls light while still catching older holder ids that were completed recently, because holder ids are
    // assigned when the induction is issued, not when it is completed.
    public async Task<InductionHolderScan> GetNewInductionHoldersAsync(string inductionId, string? afterId, DateTimeOffset completedSince, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(afterId))
        {
            // First run: seed from the date window with larger pages.
            var seeded = await GetInductionHoldersCompletedSinceAsync(inductionId, completedSince, null, ct);
            var highestSeeded = HighestId(seeded, null);
            _logger.Information("OnLocation induction {InductionId}: seeding from the {Since:yyyy-MM-dd} window, {Count} holder record(s), highest id {HighestId}",
                inductionId, completedSince, seeded.Count, highestSeeded ?? "(none)");
            return new InductionHolderScan(seeded, highestSeeded, seeded.Count);
        }

        // Subsequent runs: walk the date window with small pages so older holder ids that were completed
        // recently are not missed just because their id is lower than the last one we saw.
        return await ScanInductionHoldersByWindowAsync(inductionId, completedSince, PeriodicHolderPageSize, null, ct);
    }

    public async Task<IReadOnlyList<JsonElement>> GetRecentInductionHoldersAsync(string inductionId, int limit, CancellationToken ct = default)
    {
        var client = await CreateClientAsync(ct);
        var query = $"order=-id&limit={Math.Clamp(limit, 1, MaxPageSize)}";
        var url = $"induction/{inductionId}/holder?{query}";
        var (response, body) = await SendLoggedAsync(client, new HttpRequestMessage(HttpMethod.Get, url), ct);
        if (!await HandleResponseAsync(response, body, ct)) return Array.Empty<JsonElement>();
        return ExtractRecords(body);
    }
    private static string? HighestId(IReadOnlyList<JsonElement> records, string? current)
    {
        var highest = current;
        foreach (var record in records)
        {
            var id = GetId(record);
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (string.IsNullOrWhiteSpace(highest)) { highest = id; continue; }

            if (long.TryParse(id, out var candidate) && long.TryParse(highest, out var best))
            {
                if (candidate > best) highest = id;
            }
            else if (string.Compare(id, highest, StringComparison.OrdinalIgnoreCase) > 0)
            {
                highest = id;
            }
        }
        return highest;
    }

    public async Task<IReadOnlyList<JsonElement>> GetInductionHoldersCompletedSinceAsync(string inductionId, DateTimeOffset since, IProgress<OnLocationFetchProgress>? progress = null, CancellationToken ct = default)
    {
        var scan = await ScanInductionHoldersByWindowAsync(inductionId, since, HolderPageSize, progress, ct);
        return scan.Records;
    }

    private async Task<InductionHolderScan> ScanInductionHoldersByWindowAsync(string inductionId, DateTimeOffset since, int pageSize, IProgress<OnLocationFetchProgress>? progress, CancellationToken ct)
    {
        var records = new List<JsonElement>();
        var client = await CreateClientAsync(ct);
        string? oldestId = null;
        var pages = 0;
        var scanned = 0;
        var stalePages = 0;

        while (!ct.IsCancellationRequested && pages < MaxHolderPages)
        {
            var query = new List<string> { "order=-id", $"limit={Math.Clamp(pageSize, 1, MaxPageSize)}" };
            if (!string.IsNullOrWhiteSpace(oldestId)) query.Add($"q=id<{oldestId}");

            var url = $"induction/{inductionId}/holder?{string.Join("&", query)}";
            var (response, body) = await SendLoggedAsync(client, new HttpRequestMessage(HttpMethod.Get, url), ct);
            if (!await HandleResponseAsync(response, body, ct)) break;

            var page = ExtractRecords(body);
            pages++;
            scanned += page.Count;
            if (page.Count == 0) break;

            var inWindow = page.Where(holder => CompletedOnOrAfter(holder, since)).ToList();
            records.AddRange(inWindow);
            _activity.AddChecked(page.Count, $"Induction {inductionId}: checked {scanned} holder record(s), {records.Count} in the window");
            _logger.Information("OnLocation induction {InductionId} page {Page}: {PageCount} holder(s), {InWindow} completed on or after {Since:yyyy-MM-dd}, running total {Total}",
                inductionId, pages, page.Count, inWindow.Count, since, records.Count);

            var percent = Math.Min(100, pages * 100 / 10);
            progress?.Report(new OnLocationFetchProgress(percent, $"{records.Count} record(s) found ({scanned} searched)..."));

            stalePages = inWindow.Count == 0 ? stalePages + 1 : 0;
            if (stalePages > HolderLookaheadPages)
            {
                _logger.Information("OnLocation induction {InductionId}: stopping after {StalePages} consecutive page(s) with no records in the date window", inductionId, stalePages);
                break;
            }

            var nextId = GetId(page[^1]);
            if (page.Count < pageSize || string.IsNullOrWhiteSpace(nextId) || nextId == oldestId) break;
            oldestId = nextId;
        }

        if (pages >= MaxHolderPages)
            _logger.Warning("OnLocation induction {InductionId}: hit the {MaxHolderPages} page scan limit; results may be incomplete", inductionId, MaxHolderPages);

        _logger.Information("OnLocation induction {InductionId}: scanned {Scanned} holder records over {Pages} page(s), {Count} completed on or after {Since:yyyy-MM-dd}", inductionId, scanned, pages, records.Count, since);
        return new InductionHolderScan(records, HighestId(records, null), scanned);
    }

    public async Task<IReadOnlyList<JsonElement>> GetRecordsByIdAsync(string endpoint, IReadOnlyCollection<string> ids, CancellationToken ct = default)
    {
        // Retrieving a handful of people by id is far cheaper than enumerating the whole collection, and the
        // requests are issued sequentially to stay well inside the 100 requests/minute credential limit.
        var records = new List<JsonElement>();
        if (ids.Count == 0) return records;

        var client = await CreateClientAsync(ct);
        var missing = 0;
        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(id)) continue;

            var url = $"{endpoint.TrimEnd('/')}/{Uri.EscapeDataString(id)}";
            var (response, body) = await SendLoggedAsync(client, new HttpRequestMessage(HttpMethod.Get, url), ct);
            if (!await HandleResponseAsync(response, body, ct))
            {
                missing++;
                continue;
            }

            var record = ExtractSingleRecord(body);
            if (record.HasValue)
            {
                records.Add(record.Value);
            }
            else
            {
                // Logged once: an unrecognised envelope affects every record, and repeating the body 100 times
                // buries the rest of the run.
                if (missing == 0)
                    _logger.Warning("OnLocation {Endpoint}: could not find a record in the by-id response for {Id}. Body starts: {Body}",
                        endpoint, id, body.Length <= 300 ? body : body[..300]);
                missing++;
            }
        }

        _logger.Information("OnLocation {Endpoint}: retrieved {Count} of {Requested} record(s) by id{Missing}", endpoint, records.Count, ids.Count, missing == 0 ? string.Empty : $", {missing} could not be read");
        return records;
    }

    private static JsonElement? ExtractSingleRecord(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("id", out _))
            return doc.RootElement.Clone();

        var records = ExtractRecords(body);
        if (records.Count > 0) return records[0];

        // The by-id response is documented as a bare record but can arrive wrapped in a single-property
        // envelope, so the first nested object or array element carrying an id is taken instead.
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object && property.Value.TryGetProperty("id", out _))
                    return property.Value.Clone();

                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out _))
                            return item.Clone();
                    }
                }
            }
        }

        return null;
    }

    private static bool CompletedOnOrAfter(JsonElement holder, DateTimeOffset since)
    {
        if (!holder.TryGetProperty("completed", out var completed) || completed.ValueKind != JsonValueKind.String) return false;
        return DateTimeOffset.TryParse(completed.GetString(), out var completedAt) && completedAt >= since;
    }

    public Task<string?> GetLastErrorAsync() => Task.FromResult(_lastError);

    private async Task<IReadOnlyList<JsonElement>> FetchAllAsync(string endpoint, SyncBookmark bookmark, CancellationToken ct, int limit = DefaultPageSize)
    {
        var result = new List<JsonElement>();
        var client = await CreateClientAsync(ct);

        var query = new List<string> { "order=id", $"limit={Math.Clamp(limit, 1, MaxPageSize)}" };
        if (!string.IsNullOrEmpty(bookmark.Cursor)) query.Add($"q=id>{Uri.EscapeDataString(bookmark.Cursor)}");

        var requestUrl = $"{endpoint.TrimEnd('/')}?{string.Join("&", query)}";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        if (!string.IsNullOrEmpty(bookmark.LastModified)) request.Headers.TryAddWithoutValidation("If-Modified-Since", bookmark.LastModified);

        var (response, body) = await SendLoggedAsync(client, request, ct);
        if (!await HandleResponseAsync(response, body, ct)) return result;

        result.AddRange(ExtractRecords(body));

        if (result.Count > 0)
        {
            var lastId = result.Last().GetProperty("id");
            bookmark.Cursor = lastId.ValueKind == JsonValueKind.String
                ? (lastId.GetString() ?? bookmark.Cursor)
                : lastId.GetRawText();
        }

        _logger.Information("OnLocation {Endpoint} returned {Count} records", endpoint, result.Count);
        return result;
    }

    private async Task<(HttpResponseMessage Response, string Body)> SendLoggedAsync(HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        var url = client.BaseAddress is null || request.RequestUri is null
            ? request.RequestUri?.ToString() ?? string.Empty
            : new Uri(client.BaseAddress, request.RequestUri).ToString();

        _logger.Information("OnLocation --> {Method} {Url}", request.Method.Method, url);
        var stopwatch = Stopwatch.StartNew();
        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        stopwatch.Stop();
        _logger.Information("OnLocation <-- {Status} {Url} in {ElapsedMs} ms, {Bytes} bytes",
            (int)response.StatusCode, url, stopwatch.ElapsedMilliseconds, body.Length);
        _logger.Debug("OnLocation <-- body {Url}: {Body}", url, body);
        return (response, body);
    }

    private static List<JsonElement> ExtractRecords(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var result = new List<JsonElement>();
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            result.AddRange(doc.RootElement.EnumerateArray().Select(item => item.Clone()));
            return result;
        }
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            result.AddRange(data.EnumerateArray().Select(item => item.Clone()));
            return result;
        }
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array && prop.Name != "links" && prop.Name != "meta")
            {
                result.AddRange(prop.Value.EnumerateArray().Select(item => item.Clone()));
                break;
            }
        }
        return result;
    }

    private static string? GetId(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var id)) return null;
        return id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
    }

    private async Task<HttpClient> CreateClientAsync(CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("OnLocation");
        client.BaseAddress = new Uri(Cfg.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = RequestTimeout;
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var auth = Cfg.AuthMode.ToLowerInvariant();
        if (auth == "oauth2")
        {
            var token = await GetAccessTokenAsync(ct);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else if (auth == "basic")
        {
            var password = Cfg.Password ?? string.Empty;
            var bytes = Encoding.UTF8.GetBytes($"{Cfg.ApiKey}:{password}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
        }
        else
        {
            client.DefaultRequestHeaders.Add("Authorization", $"APIKEY {Cfg.ApiKey}");
        }
        return client;
    }

    private string? _cachedToken;
    private DateTimeOffset? _tokenExpiresAt;
    private string? _tokenCredentialFingerprint;

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var fingerprint = GetCredentialFingerprint();
        if (!string.IsNullOrEmpty(_cachedToken) &&
            _tokenCredentialFingerprint == fingerprint &&
            _tokenExpiresAt.HasValue &&
            DateTimeOffset.UtcNow.AddMinutes(1) < _tokenExpiresAt.Value)
        {
            return _cachedToken;
        }

        _logger.Information("Requesting new OnLocation OAuth2 access token");
        var client = _httpFactory.CreateClient();

        // OnLocation's token endpoint requires the client id/secret as HTTP Basic auth,
        // NOT as form fields. Sending them in the body results in a 403 Forbidden.
        var clientId = Cfg.ClientId ?? string.Empty;
        var clientSecret = Cfg.ClientSecret ?? string.Empty;
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });
        using var response = await client.PostAsync(Cfg.TokenEndpoint, content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _lastError = $"OnLocation token request failed: {(int)response.StatusCode} {response.StatusCode}. Response: {responseBody}";
            _logger.Error(_lastError);
            throw new HttpRequestException(_lastError);
        }

        using var doc = JsonDocument.Parse(responseBody);
        _cachedToken = doc.RootElement.GetProperty("access_token").GetString();
        _tokenCredentialFingerprint = fingerprint;
        if (int.TryParse(doc.RootElement.GetProperty("expires_in").GetRawText(), out var expiresIn))
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        else
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
        return _cachedToken!;
    }

    private string GetCredentialFingerprint()
    {
        return $"{Cfg.ClientId}:{Cfg.ClientSecret}";
    }

    private async Task<bool> HandleResponseAsync(HttpResponseMessage response, string body, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            _lastError = null;
            return true;
        }
        if ((int)response.StatusCode == 429)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 60;
            _logger.Warning("OnLocation rate limit hit. Waiting {Seconds}s", retryAfter);
            await Task.Delay(TimeSpan.FromSeconds(retryAfter), ct);
        }
        _lastError = $"OnLocation returned {(int)response.StatusCode}: {body}";
        _logger.Error("OnLocation request failed: {Status} {Body}", (int)response.StatusCode, body);
        return false;
    }
}
