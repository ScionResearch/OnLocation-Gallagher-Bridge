using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Services;

// A positional argument list this long made it easy to record an outcome incorrectly, and outcomes other than
// success were simply never written.
public class AuditEntry
{
    public string CorrelationId { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string? SourceDisplay { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Outcome { get; set; } = "Success";
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? GallagherHref { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public int DurationMs { get; set; }
}

public interface IAuditService
{
    Task LogAsync(AuditEntry entry);
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count = 50);
    Task<IReadOnlyList<AuditLog>> QueryAsync(string? profileId, string? action, string? outcome, string? search, int count = 200);
}

public class AuditService : IAuditService
{
    private readonly BridgeDbContext _db;

    public AuditService(BridgeDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(AuditEntry entry)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            CorrelationId = entry.CorrelationId,
            ProfileId = entry.ProfileId,
            SourceId = entry.SourceId,
            SourceDisplay = entry.SourceDisplay,
            Action = entry.Action,
            Outcome = entry.Outcome,
            BeforeJson = entry.BeforeJson,
            AfterJson = entry.AfterJson,
            GallagherHref = entry.GallagherHref,
            Message = entry.Message,
            Error = entry.Error,
            DurationMs = entry.DurationMs,
            Timestamp = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count = 50)
    {
        return await _db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(string? profileId, string? action, string? outcome, string? search, int count = 200)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(profileId)) query = query.Where(a => a.ProfileId == profileId);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(outcome)) query = query.Where(a => a.Outcome == outcome);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.SourceId.Contains(term)
                || (a.SourceDisplay != null && a.SourceDisplay.Contains(term))
                || (a.Message != null && a.Message.Contains(term))
                || (a.Error != null && a.Error.Contains(term)));
        }

        return await query.OrderByDescending(a => a.Timestamp).Take(count).ToListAsync();
    }
}
