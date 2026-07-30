using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;
using OnLocationGallagherBridge.Services;
using System.Text.Json;

namespace OnLocationGallagherBridge.Pages;

public class AuditModel : PageModel
{
    private readonly BridgeDbContext _db;
    private readonly IAuditService _audit;

    public AuditModel(BridgeDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public IReadOnlyList<AuditLog> Events { get; set; } = new List<AuditLog>();
    public List<string> ProfileIds { get; set; } = new();
    public List<string> Actions { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ProfileId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Action { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Outcome { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public const int PageSize = 50;

    public int TotalCount { get; set; }
    public int PageCount => (TotalCount + PageSize - 1) / PageSize;

    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int PendingCount { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        ProfileIds = await _db.SyncProfiles.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        Actions = await _db.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(ct);

        PageNumber = Math.Max(1, PageNumber);
        var result = await _audit.QueryAsync(ProfileId, Action, Outcome, Search, PageNumber, PageSize);
        if (result.TotalCount > 0 && PageNumber > result.TotalCount / PageSize + (result.TotalCount % PageSize == 0 ? 0 : 1))
        {
            PageNumber = Math.Max(1, (result.TotalCount + PageSize - 1) / PageSize);
            result = await _audit.QueryAsync(ProfileId, Action, Outcome, Search, PageNumber, PageSize);
        }

        Events = result.Items;
        TotalCount = result.TotalCount;

        SuccessCount = result.SuccessCount;
        FailedCount = result.FailedCount;
        PendingCount = result.PendingCount;
    }

    public static string OutcomeColour(string outcome) => outcome switch
    {
        "Failed" => "danger",
        "Pending" => "warning",
        _ => "success"
    };

    public static string ActionColour(string action) => action switch
    {
        "Create" => "primary",
        "Update" => "info",
        "StaleLink" => "danger",
        "ManualReview" => "warning",
        "Excluded" => "secondary",
        _ => "secondary"
    };

    // The payload is stored as compact JSON, which is unreadable in a detail panel.
    public static string Prettify(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "—";
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
