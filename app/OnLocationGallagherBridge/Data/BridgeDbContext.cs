using Microsoft.EntityFrameworkCore;
using OnLocationGallagherBridge.Models;

namespace OnLocationGallagherBridge.Data;

public class BridgeDbContext : DbContext
{
    public BridgeDbContext(DbContextOptions<BridgeDbContext> options) : base(options) { }

    public DbSet<SyncProfile> SyncProfiles { get; set; } = null!;
    public DbSet<EntityMapping> EntityMappings { get; set; } = null!;
    public DbSet<SyncBookmark> SyncBookmarks { get; set; } = null!;
    public DbSet<SyncJob> SyncJobs { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<ManualMatchQueue> ManualMatchQueues { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncProfile>().HasKey(e => e.Id);
        modelBuilder.Entity<EntityMapping>().HasIndex(e => new { e.ProfileId, e.SourceType, e.SourceId }).IsUnique();
        modelBuilder.Entity<SyncBookmark>().HasKey(e => e.ProfileId);
        modelBuilder.Entity<SyncJob>().HasIndex(e => e.Status);
        modelBuilder.Entity<AuditLog>().HasIndex(e => e.Timestamp);
        modelBuilder.Entity<ManualMatchQueue>().HasIndex(e => e.Status);
    }
}
