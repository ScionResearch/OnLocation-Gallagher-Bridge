using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Data;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Pages;

public class AuditModel : PageModel
{
    private readonly BridgeDbContext _db;

    public AuditModel(BridgeDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<AuditLog> Events { get; set; } = new List<AuditLog>();

    public async Task OnGetAsync(CancellationToken ct)
    {
        var rows = await _db.AuditLogs.AsNoTracking().ToListAsync(ct);
        Events = rows.OrderByDescending(a => a.Timestamp).Take(200).ToList();
    }
}
