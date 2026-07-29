namespace OnLocationGallagherBridge.Services;

public record SyncActivitySnapshot
{
    public bool Running { get; init; }
    public string? ProfileId { get; init; }
    public string Phase { get; init; } = "Idle";
    public string? Detail { get; init; }
    public int RecordsChecked { get; init; }
    public int RecordsMatched { get; init; }
    public int Processed { get; init; }
    public int Total { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? LastRunSummary { get; init; }
    public DateTimeOffset? LastRunFinishedAt { get; init; }
    public string? LastRunProfileId { get; init; }

    public string Elapsed => StartedAt.HasValue
        ? FormatDuration((UpdatedAt ?? DateTimeOffset.UtcNow) - StartedAt.Value)
        : "—";

    private static string FormatDuration(TimeSpan span) => span.TotalHours >= 1
        ? $"{(int)span.TotalHours}h {span.Minutes}m"
        : span.TotalMinutes >= 1
            ? $"{(int)span.TotalMinutes}m {span.Seconds}s"
            : $"{span.Seconds}s";
}

public interface ISyncActivity
{
    SyncActivitySnapshot Snapshot { get; }
    void Begin(string profileId, string phase);
    void SetPhase(string phase, string? detail = null);
    void AddChecked(int count, string? detail = null);
    void AddMatched(int count);
    void SetProgress(int processed, int total, string? detail = null);
    void Complete(string summary);
}

// A run is reported through a single shared snapshot rather than per profile, because the sync engine
// processes profiles one at a time. Updates from UI-driven fetches are ignored so that browsing the
// mapping or match pages does not masquerade as a sync.
public class SyncActivityService : ISyncActivity
{
    private readonly object _gate = new();
    private SyncActivitySnapshot _snapshot = new();

    public SyncActivitySnapshot Snapshot
    {
        get { lock (_gate) return _snapshot; }
    }

    public void Begin(string profileId, string phase)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            _snapshot = _snapshot with
            {
                Running = true,
                ProfileId = profileId,
                Phase = phase,
                Detail = null,
                RecordsChecked = 0,
                RecordsMatched = 0,
                Processed = 0,
                Total = 0,
                StartedAt = now,
                UpdatedAt = now
            };
        }
    }

    public void SetPhase(string phase, string? detail = null)
    {
        lock (_gate)
        {
            if (!_snapshot.Running) return;
            _snapshot = _snapshot with { Phase = phase, Detail = detail, UpdatedAt = DateTimeOffset.UtcNow };
        }
    }

    public void AddChecked(int count, string? detail = null)
    {
        lock (_gate)
        {
            if (!_snapshot.Running) return;
            _snapshot = _snapshot with
            {
                RecordsChecked = _snapshot.RecordsChecked + count,
                Detail = detail ?? _snapshot.Detail,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
    }

    public void AddMatched(int count)
    {
        lock (_gate)
        {
            if (!_snapshot.Running) return;
            _snapshot = _snapshot with { RecordsMatched = _snapshot.RecordsMatched + count, UpdatedAt = DateTimeOffset.UtcNow };
        }
    }

    public void SetProgress(int processed, int total, string? detail = null)
    {
        lock (_gate)
        {
            if (!_snapshot.Running) return;
            _snapshot = _snapshot with
            {
                Processed = processed,
                Total = total,
                Detail = detail ?? _snapshot.Detail,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
    }

    public void Complete(string summary)
    {
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                Running = false,
                Phase = "Idle",
                Detail = null,
                UpdatedAt = DateTimeOffset.UtcNow,
                LastRunSummary = summary,
                LastRunFinishedAt = DateTimeOffset.UtcNow,
                LastRunProfileId = _snapshot.ProfileId
            };
        }
    }
}
