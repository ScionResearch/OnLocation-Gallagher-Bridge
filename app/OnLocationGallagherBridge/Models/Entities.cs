using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnLocationGallagherBridge.Models;

public enum UnmatchedAction
{
    ManualReview,
    CreateNew,
    Ignore
}

public class SyncProfile
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty; // Staff, SpMember, InductionHolder
    public bool Enabled { get; set; } = true;
    public int PollingIntervalMinutes { get; set; } = 60;
    // How far back a poll looks for completed inductions. Bounds the very first scan, which would otherwise
    // read the entire induction history, and keeps every later scan cheap.
    public int SyncWindowDays { get; set; } = 7;
    // Fast sync only inspects the newest holder records and is meant for recently completed inductions.
    public int FastSyncIntervalMinutes { get; set; } = 5;
    public int FastSyncRecordCount { get; set; } = 10;
    // Full sync re-scans a configurable date window to catch old induction invites that were completed late.
    public int FullSyncIntervalDays { get; set; } = 1;
    // Minutes since midnight (0 - 1439) at which the full sync should run.
    public int FullSyncTimeOfDayMinutes { get; set; } = 60;
    // How many months back the full sync looks. Null means all existing records.
    public int? FullSyncLookbackMonths { get; set; }
    public DateTimeOffset? NextFullRun { get; set; }
    public string OnLocationEndpoint { get; set; } = string.Empty;
    public string MatchRulesJson { get; set; } = "[]";
    public string FieldMapJson { get; set; } = "[]";
    // Induction ids the operator wants processed. Empty means every induction referenced by the field map.
    public string SelectedInductionIdsJson { get; set; } = "[]";
    // Gallagher requires a division on every cardholder, so new cardholders are created in this one.
    public string DefaultDivisionHref { get; set; } = string.Empty;
    public string DefaultDivisionName { get; set; } = string.Empty;
    // Access groups new cardholders are added to. Serialised as [{"href":"...","name":"..."}].
    public string DefaultAccessGroupsJson { get; set; } = "[]";
    public bool AutoCreate { get; set; } = false;
    public UnmatchedAction DefaultUnmatchedAction { get; set; } = UnmatchedAction.ManualReview;
    public bool InitialMatchCompleted { get; set; } = false;
    public DateTimeOffset? InitialMatchCompletedAt { get; set; }
    public DateTimeOffset? LastRun { get; set; }
    public DateTimeOffset? LastFullRun { get; set; }
    public DateTimeOffset? NextRun { get; set; }
    // Field on the Gallagher cardholder to write sync-result messages to. Empty means no message is written.
    // "description" writes to the cardholder description. "personalDataFields.<name>" writes to that PDF.
    public string BridgeMessageTarget { get; set; } = string.Empty;
}

public class EntityMapping
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProfileId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string GallagherHref { get; set; } = string.Empty;
    public string? GallagherId { get; set; }
    public double Confidence { get; set; }
    public bool ManualOverride { get; set; } = false;
    public bool Excluded { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class SyncBookmark
{
    [Key]
    public string ProfileId { get; set; } = string.Empty;
    public string? LastModified { get; set; }
    public string? Cursor { get; set; }
    // Highest holder record id already retrieved per induction, as {"inductionId":"holderId"}. Holder records
    // are only ever appended, so the next poll asks for id greater than this instead of rescanning.
    public string InductionCursorsJson { get; set; } = "{}";
    public DateTimeOffset LastRun { get; set; } = DateTimeOffset.UtcNow;
}

public class SyncJob
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProfileId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending"; // Pending, Running, Failed, Complete, DeadLetter
    public int RetryCount { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
}

public class AuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CorrelationId { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // Create, Update, NoChange, ManualReview, Failed, StaleLink, Excluded
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? GallagherHref { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? Message { get; set; }
    // Who the record is, so the log can be read without cross-referencing OnLocation ids.
    public string? SourceDisplay { get; set; }
    public string? Error { get; set; }
    public string Outcome { get; set; } = "Success"; // Success, Failed, Pending
    public int DurationMs { get; set; }
}

public class ManualMatchQueue
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProfileId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceJson { get; set; } = "{}";
    public string? CandidateHref { get; set; }
    public double Confidence { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class InductionCompetencyMap
{
    [Key]
    public int Id { get; set; }
    public string OnLocationInductionId { get; set; } = string.Empty;
    public string GallagherCompetencyHref { get; set; } = string.Empty;
    public string? MappedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class FieldMap
{
    [Key]
    public int Id { get; set; }
    public string ProfileId { get; set; } = string.Empty;
    public string SourceField { get; set; } = string.Empty;
    public string TargetField { get; set; } = string.Empty;
    public string Transform { get; set; } = "copy";
    public string? OptionsJson { get; set; }
    public int SortOrder { get; set; }
}
