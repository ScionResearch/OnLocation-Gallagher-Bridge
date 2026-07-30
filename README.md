# OnLocation–Gallagher Bridge

A Windows service that synchronises people and induction data from **MRI OnLocation** (formerly WhosOnLocation) into **Gallagher Command Centre** cardholder records. The bridge polls OnLocation for new and updated records, reconciles each person against an existing Gallagher cardholder, transforms the source fields according to a configurable mapping, and writes the result to Command Centre via its REST API.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Core Components](#core-components)
- [Data Model](#data-model)
- [Identity Reconciliation](#identity-reconciliation)
- [Incremental Fetch Strategy](#incremental-fetch-strategy)
- [Field Mapping and Transformation](#field-mapping-and-transformation)
- [Competency Synchronisation](#competency-synchronisation)
- [Resilience and Error Handling](#resilience-and-error-handling)
- [Logging and Diagnostics](#logging-and-diagnostics)
- [Web UI](#web-ui)
- [Configuration](#configuration)
- [Data Locations](#data-locations)
- [Build and Deploy](#build-and-deploy)
- [First-Run Setup](#first-run-setup)
- [Testing and Reset](#testing-and-reset)
- [Project Structure](#project-structure)
- [Technology Stack](#technology-stack)

---

## Overview

The bridge runs as a background Windows service with an ASP.NET Core web UI on port 5000. It maintains one or more **sync profiles**, each defining a source entity type (Staff or Contractor Members), the inductions to track, a field mapping, match rules, and a polling schedule. On each poll the bridge:

1. Fetches new or updated records from OnLocation (incrementally, using bookmarks).
2. Reconciles each record against existing Gallagher cardholders using configurable match rules.
3. Transforms source fields into the Gallagher cardholder schema.
4. Creates or updates the cardholder in Command Centre, including competencies.
5. Logs every action to the audit trail and surfaces failures on the Exceptions page.

Unmatched records are queued for manual review on the **Initial Record Match** page, where an operator links each OnLocation person to an existing Gallagher cardholder (or excludes them, or creates a new one). Once the initial match is complete, the profile enters routine incremental sync.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Windows Service Host                      │
│                                                               │
│  ┌──────────────┐   ┌───────────────┐   ┌────────────────┐  │
│  │  SyncEngine   │──▶│ OnLocationSource │──▶│ OnLocationConnector │  │
│  │ (Background   │   │    Service       │   │    (REST API)       │  │
│  │  Service)     │   └───────────────┘   └────────────────┘  │
│  │               │                                           │
│  │               │   ┌───────────────┐   ┌────────────────┐  │
│  │               │──▶│  JobProcessor   │──▶│ GallagherConnector  │  │
│  │               │   │                 │   │    (REST API)       │  │
│  │               │   │ TransformEngine │   └────────────────┘  │
│  │               │   │ IdentityMatcher │                       │
│  │               │   └───────────────┘                       │
│  │               │                                           │
│  │               │   ┌───────────────┐                       │
│  │               │──▶│  AuditService   │                       │
│  │               │   └───────────────┘                       │
│  └──────────────┘                                           │
│                                                               │
│  ┌──────────────────────────────────────────────────────┐    │
│  │               ASP.NET Core Razor Pages UI              │    │
│  │  Dashboard · Settings · Mapping · MatchReview ·        │    │
│  │  ManualSync · Exceptions · Audit                       │    │
│  └──────────────────────────────────────────────────────┘    │
│                                                               │
│  ┌──────────┐  ┌──────────────┐  ┌────────────────────┐     │
│  │ SQLite    │  │ ConfigService  │  │  Serilog File Logs  │     │
│  │ bridge.db │  │ config.json    │  │  bridge-YYYYMMDD.log│     │
│  │           │  │ .crypt (DPAPI) │  │                     │     │
│  └──────────┘  └──────────────┘  └────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

All components run in a single process. The sync engine is a `BackgroundService` that wakes every minute, checks which profiles are due, and processes them sequentially. The web UI is served by the same process, so operators can configure, monitor, and trigger syncs without a separate tool.

---

## Core Components

### Sync Engine

`SyncEngine` is a .NET `BackgroundService` that runs a continuous loop:

1. Every minute, queries the database for enabled profiles whose `NextRun` has passed.
2. For each due profile, calls `OnLocationSourceService.GetRecordsAsync` to fetch new records.
3. Queues each record as a `SyncJob` (or updates an existing pending job for the same source ID).
4. Processes each pending job through `JobProcessor`, which transforms and writes to Gallagher.
5. Updates the profile's `LastRun` and `NextRun` timestamps.
6. Reports progress through `ISyncActivity` so the dashboard can show live status.

Profiles that have not completed their initial record match are skipped — the engine logs this and moves on.

### OnLocation Connector

`OnLocationConnector` wraps the OnLocation REST API (`https://api.whosonlocation.com/v1`). It supports three authentication modes:

- **OAuth2** (default) — client credentials flow with token caching and automatic refresh.
- **API Key** — `Authorization: APIKEY <key>` header.
- **Basic** — `Authorization: Basic <base64(apikey:password)>` header.

Key operations:

| Method | Description |
|--------|-------------|
| `GetStaffAsync` | Paginated staff list with id-based cursor bookmark. |
| `GetContractorMembersAsync` | Paginated contractor member list with cursor. |
| `GetInductionHoldersAsync` | All holders for a given induction, paginated. |
| `GetInductionHoldersCompletedSinceAsync` | Holders completed on or after a date (client-side filter). |
| `GetNewInductionHoldersAsync` | Incremental scan: pages through holders with `id > afterId`, filtering by completion date window. Returns the highest id seen so the bookmark advances. |
| `GetRecordsByIdAsync` | Fetches individual staff or contractor records by ID (cheaper than enumerating the whole collection when only a few people are affected). |

Rate limiting is handled automatically: a 429 response triggers a retry after the server's `Retry-After` interval.

### Gallagher Connector

`GallagherConnector` wraps the Gallagher Command Centre REST API. It discovers the API root from the `/api` endpoint and follows hypermedia links to reach `cardholders`, `competencies`, `divisions`, `accessGroups`, and `personalDataFields`.

Key operations:

| Method | Description |
|--------|-------------|
| `CreateCardholderAsync` | POST a new cardholder. Handles 201-with-empty-body by reading the `Location` header. |
| `UpdateCardholderAsync` | PATCH an existing cardholder. Returns a `GallagherWriteResult` with status code so the caller can distinguish 404 (stale link) from other failures. |
| `FindCardholderByEmailAsync` | Search cardholders by email filter. |
| `GetAllCardholdersAsync` | Full cardholder enumeration with pagination (used for initial match). |
| `GetCompetenciesAsync` | All competency definitions (paginated). |
| `GetDivisionsAsync` | All divisions. |
| `GetAccessGroupsAsync` | All access groups. |
| `GetPersonalDataFieldsAsync` | All personal data field definitions. |
| `GetCardholderAsync` | Fetch a single cardholder with optional `expand` (e.g. `competencies`). |

TLS verification can be disabled for self-signed Command Centre certificates via the Gallagher config.

### Identity Matcher

`IdentityMatcher` reconciles an OnLocation record against a pool of Gallagher cardholder candidates. Match rules are defined per profile in `MatchRulesJson` and evaluated in order:

1. **Exact match** — field values match exactly (e.g. email = email). Confidence: 1.0.
2. **Fuzzy match** — Levenshtein distance ≤ 2 on name fields. Confidence: 0.8.
3. **No match** — record is queued for manual review.

A `MatchSession` is created per initial-match run to index candidates and prevent the same cardholder from being assigned to two different OnLocation records in the same pass.

### Transform Engine

`TransformEngine` applies the profile's field map to each source record, producing a dictionary of Gallagher cardholder field values. Supported transforms:

| Transform | Description |
|-----------|-------------|
| `copy` | Direct field copy (default). |
| `static` | A fixed value from options. |
| `date` | Parse and reformat a date. |
| `lookup` | Map a value through a lookup table. |
| `condition` | Conditional value mapping (`eq`/`else` rules). |
| `concat` | Concatenate multiple source fields with a separator. |
| `first-name` | Extract the first name from a full name field. |
| `last-name` | Extract the last name from a full name field. |
| `gallagher-expiry` | Convert a date to a Gallagher competency expiry timestamp (end-of-day NZ timezone, UTC ISO 8601). |

Personal data fields are addressed in the mapping UI as `personalDataFields.<name>` and rewritten to `@<name>` for the Command Centre API.

On cardholder creation, the transform engine also applies profile defaults:

- **Division** — new cardholders are placed in the profile's `DefaultDivisionHref`.
- **Access groups** — new cardholders are added to the profile's `DefaultAccessGroupsJson` list.

These defaults are applied on create only, so subsequent updates never drag a cardholder back into the default division or access groups after an operator has moved it.

### Job Processor

`JobProcessor` takes a `SyncJob` through its full lifecycle:

1. Loads the profile and the source record payload.
2. Checks the `EntityMapping` for this source ID:
   - **Excluded** → marks the job complete, logs "Excluded" to audit, does nothing else.
   - **No mapping or not manually confirmed** → queues the record for manual match review, marks the job `ManualReview`.
   - **Manually confirmed mapping with an href** → transforms the record, builds the cardholder payload, and PATCHes the existing cardholder.
   - **Manually confirmed mapping with no href** → transforms, applies create defaults, and POSTs a new cardholder.
3. Handles stale links: if a PATCH returns 404, the mapping is cleared and the record goes back to manual review.
4. Logs every outcome to the audit trail with before/after JSON, duration, and error details.

The manual match queue is deduplicated: if a pending queue entry already exists for the same profile and source ID, it is updated rather than duplicated.

### Audit Service

`AuditService` persists every sync action to the `AuditLogs` table. Each entry includes:

- **Correlation ID** — links all jobs from a single sync run.
- **Profile ID**, **Source ID**, **Source Display** (person name or email for readability).
- **Action** — Create, Update, NoChange, ManualReview, Failed, StaleLink, Excluded.
- **Outcome** — Success, Failed, Pending.
- **Before/After JSON** — the cardholder state before and after the write.
- **Gallagher Href** — the cardholder link.
- **Error** — the error message on failure.
- **Duration** — milliseconds spent processing.
- **Timestamp**.

The audit page supports filtering by profile, action, outcome, and free-text search across source ID, display name, message, and error.

### Alert Service

`AlertService` sends email alerts via SMTP (using MailKit) when configured. Intended for failure notifications and test alerts from the Settings page.

### Config Service

`ConfigService` stores all credentials and settings in an encrypted JSON file using Windows DPAPI (`ProtectedData` with `LocalMachine` scope). The file is unreadable without access to the machine account. The service provides:

- `LoadAsync` — decrypt and load the config at startup.
- `SaveAsync` — encrypt and persist the config.
- `Delete` — remove the config file entirely (for testing from a clean state).
- `GetConfig` / `SetConfig` — thread-safe in-memory access.

### Sync Activity Service

`SyncActivityService` is a singleton that holds a single `SyncActivitySnapshot` record. The sync engine and manual sync handler update it as a run progresses, and the dashboard polls it every 2 seconds via a JSON endpoint to show:

- Whether a sync is running and which profile.
- The current phase (Scanning, Fetching, Queueing, Writing).
- Progress (processed / total records).
- Elapsed time.
- The last run summary and when it finished.

---

## Data Model

All state is stored in a SQLite database (`bridge.db`). EF Core code-first with additive column migrations (no EF migrations framework — new columns are added via `ALTER TABLE` on startup).

| Entity | Purpose |
|--------|---------|
| `SyncProfile` | A sync configuration: entity type, endpoint, field map, match rules, inductions, schedule, enable/disable, create defaults. |
| `EntityMapping` | The link between an OnLocation source ID and a Gallagher cardholder href. Tracks confidence, manual override, and exclusion. |
| `SyncBookmark` | Per-profile cursor: `LastModified`/`Cursor` for staff/contractor pagination, `InductionCursorsJson` for per-induction highest-id bookmarks. |
| `SyncJob` | A queued record to process. Status: Pending → Running → Complete / Failed / ManualReview / DeadLetter. |
| `AuditLog` | Immutable record of every sync action with before/after JSON and error details. |
| `ManualMatchQueue` | Records awaiting operator match decisions. Status: Pending → Approved / Rejected. |
| `InductionCompetencyMap` | Maps an OnLocation induction ID to a Gallagher competency href. |
| `FieldMap` | Persisted field mapping rows (used by the Mapping page UI). |

Two default profiles are seeded on first run:

- **employees** — Staff entity type, polls the `staff` endpoint.
- **contractor-members** — SpMember entity type, polls the `sp/member` endpoint.

Both are disabled by default. Induction-holder profiles are created from the Mapping page when an operator configures induction-based field maps.

---

## Identity Reconciliation

The bridge uses a two-phase identity strategy:

### Phase 1: Initial Record Match

Before a profile can sync, an operator runs the initial match from the **Initial Record Match** page. This:

1. Fetches all relevant people from OnLocation (staff or contractor members).
2. For induction-driven profiles, fetches induction holders completed within a configurable window and merges the induction data into each person record.
3. Loads all existing Gallagher cardholders as candidates.
4. Runs the `IdentityMatcher` against each source record.
5. Presents results for operator review: each person is shown with the best-match candidate, confidence score, and resolution options (approve match, choose a different cardholder, create new, or exclude).
6. Approved matches create `EntityMapping` rows with `ManualOverride = true`.
7. Excluded records create mappings with `Excluded = true` and are never sent to Gallagher.

Before any changes are written to Command Centre, the operator must confirm a summary of the changes: how many existing cardholders will be linked, how many new cardholders will be created, and how many records will be excluded. A checkbox requires confirmation that Command Centre has been backed up, because creating cardholders and linking existing records is not easily reversed.

While the approval is applied, a live progress bar shows how many records have been processed. The result card then reports the number matched, created, excluded, and any failures with their error messages, so the operator knows exactly what changed before routine incremental sync begins.

Once all records are resolved, the profile's `InitialMatchCompleted` flag is set and the sync engine begins routine polling.

### Phase 2: Routine Sync

On each poll, the sync engine fetches only new/updated records. For each record:

- If a confirmed `EntityMapping` exists → transform and write to Gallagher.
- If no mapping exists, or the mapping is not manually confirmed → queue for manual review.
- If the mapped cardholder returns 404 on update → the link is cleared (stale link) and the record goes back for re-matching.

---

## Incremental Fetch Strategy

The bridge avoids re-reading the entire OnLocation dataset on every poll. Two strategies are used depending on the profile type:

### Staff / Contractor Member Profiles (no inductions)

Uses an id-based cursor bookmark: each poll requests records with `id > <last cursor>` and advances the cursor to the last record seen.

### Induction-Driven Profiles

Uses a per-induction highest-id bookmark stored in `SyncBookmark.InductionCursorsJson` as `{"inductionId": "highestHolderId"}`. On each poll:

1. For each selected induction, calls `GetNewInductionHoldersAsync` with `afterId = <highest id seen>` and `completedSince = now - SyncWindowDays`.
2. The connector pages through holder records with `id > afterId`, keeping only those whose `completed` date falls within the window.
3. The bookmark advances to the **highest id seen** (including records outside the window), so skipped records are never re-read.
4. The affected person records are fetched by ID from the staff or contractor endpoint.
5. Induction data is merged into each person record and the result is queued for sync.

The `SyncWindowDays` setting (configurable per profile, default 7) bounds the first scan, which would otherwise read the entire induction history. Holder IDs are assigned when an induction is issued, not when it is completed, so a learner who takes a long time to finish has a low ID with a recent completion date. The scan tolerates this skew with a small lookahead past the first page with no in-window records.

---

## Field Mapping and Transformation

Field maps are configured per profile on the **Field Mapping** page. Each mapping row specifies:

- **Source field** — a dot-path into the OnLocation record (e.g. `email`, `first_name`, `inductions.123.completed`). Optional when the transform is *Rule based*.
- **Target field** — the Gallagher cardholder field name (e.g. `firstName`, `lastName`, `@EmployeeId` for a personal data field).
- **Transform** — one of the transforms listed in the [Transform Engine](#transform-engine) section.
- **Options** — transform-specific parameters (lookup table, date format, static value, condition rules, concat fields/separator).

### Rule-based transforms

A field map can use a **Rule based** transform to decide whether a value is written to the target field. For each rule-based map you can:

- Set the overall logic to **AND** (every rule must match) or **OR** (any rule can match).
- Add up to 10 rules. Each rule evaluates an OnLocation source field with an operator:
  - `equals`, `does not equal`
  - `contains`, `does not contain`
  - `greater than`, `less than` (numeric comparison)
  - `exists` (source field is present and not null)
- Compare the source field against either a constant or another OnLocation source field.
- Choose the output as either a constant value (string or number) or another OnLocation source field.

If no rule evaluates true, the target field is omitted from the Gallagher payload. This is useful for defaulting values such as a personal data field "Cardholder Type" to `Contractor`, or bucketing IDs into ranges.

The mapping UI discovers available Gallagher fields (cardholder fields, personal data fields, competencies) and OnLocation fields dynamically, so the operator builds the map from real field names rather than typing them.

---

## Competency Synchronisation

When a field map targets `competencies.<competencyName>.<field>` (e.g. `competencies.Induction.expires`), the transform engine:

1. Collects all competency-related values from the transformed output.
2. Fetches the competency definitions from Gallagher to resolve names to hrefs.
3. For updates, fetches the cardholder's existing competencies to determine which are new (add) vs. existing (update).
4. Builds the correct payload shape:
   - **POST (create)** — a plain `competencies` array with competency hrefs.
   - **PATCH (update)** — a `competencies` object with `add` and `update` sub-arrays, each containing the competency link and field values.

Expiry dates are converted to Gallagher's expected format: end-of-day in the New Zealand timezone, expressed as UTC ISO 8601 (`yyyy-MM-ddTHH:mm:ssZ`).

---

## Resilience and Error Handling

- **Stale link repair** — a 404 on cardholder update clears the mapping and sends the record back for re-matching, rather than retrying forever.
- **Manual match deduplication** — the manual match queue never accumulates duplicate entries for the same person.
- **Rate limit handling** — OnLocation 429 responses trigger an automatic retry after the server's `Retry-After` interval.
- **Excluded records** — records marked as excluded during initial match are silently skipped on every subsequent sync.
- **Dead-letter jobs** — failed jobs remain in the `Failed` status and appear on the Exceptions page for retry, rematch, or dismissal.
- **SQLite DateTimeOffset ordering** — SQLite cannot order by `DateTimeOffset` in SQL, so audit and exception queries materialise results first and sort in-memory.
- **Form value limit** — the form value count limit is raised to 65,536 to support the initial match review form, which posts ~10 values per record.
- **Create with empty body** — Gallagher's cardholder create returns a 201 with an empty body; the connector falls back to the `Location` header to obtain the new cardholder's href.
- **Connection testing** — both connectors provide `TestConnectionAsync` methods, callable from the Settings page or automatically before Initial Record Match, that validate credentials and reachability.

---

## Logging and Diagnostics

**Serilog** writes to two sinks:

- **Console** — `Information` level and above (useful when running interactively).
- **Rolling file** — `Debug` level and above, daily rotation, 30-day retention, at `%ProgramData%\OnLocation-Gallagher-Bridge\logs\bridge-YYYYMMDD.log`.

Both connectors log every HTTP request and response with method, URL, status code, elapsed milliseconds, and byte count. Request and response bodies are logged at `Debug` level.

The sync engine logs when each profile runs, how many records were fetched, and when profiles are skipped (disabled, not due, or pending initial match). When nothing is due, a `Debug` message explains why.

The **Audit** page provides a filterable, searchable view of every sync action with expandable before/after JSON payloads. The **Exceptions** page groups failed jobs by error message and offers retry, rematch, and dismiss actions.

---

## Web UI

The web UI is served at `http://localhost:5000` and provides the following pages:

| Page | Purpose |
|------|---------|
| **Dashboard** (`/Index`) | Profile list with enable/disable toggles, schedule controls (interval + sync window), live sync activity indicator, and recent audit feed. |
| **Connector Settings** (`/Settings`) | OnLocation and Gallagher connection configuration, SMTP alert settings, connection tests, and testing reset controls. |
| **Field Mapping** (`/Mapping`) | Configure source-to-target field maps per profile, including rule-based transforms, induction selection, competency mapping, division/access group defaults, and the bridge sync-message target field. |
| **Initial Record Match** (`/MatchReview`) | Run the initial match, review candidate cardholders, approve/reject/create/exclude each record. |
| **Manual Sync** (`/ManualSync`) | Trigger an immediate sync of one or all profiles outside the normal schedule. Reports progress to the same live activity indicator. |
| **Exceptions** (`/Exceptions`) | View failed sync jobs grouped by error, with retry, rematch, and dismiss actions. |
| **Audit** (`/Audit`) | Filterable audit log with outcome/action badges, source display names, durations, and expandable JSON detail. |

The dashboard's live activity card polls a JSON endpoint every 2 seconds and auto-refreshes the page when a run completes.

---

## Configuration

All credentials and settings are stored in an encrypted JSON file (see [Config Service](#config-service)). The Settings page provides forms for:

### OnLocation

- **Base URL** — defaults to `https://api.whosonlocation.com/v1`.
- **Auth Mode** — OAuth2, API Key, or Basic.
- **OAuth2** — Client ID, Client Secret, Token Endpoint.
  - The token endpoint expects the client ID and secret as **HTTP Basic auth** (`Authorization: Basic <base64(clientId:clientSecret)>`), with `grant_type=client_credentials` in the form body.
- **API Key / Basic** — API Key, Password (Basic only).

### Gallagher

- **Base URL** — the Command Centre server URL.
- **API Key** — the operator's API key, used as the Basic auth password with an empty username.
- **Disable TLS Verification** — for self-signed certificates.

### SMTP (Alerts)

- Host, Port, SSL, Username, Password, From address, Alert Recipients, Enable/disable.

### Web Host

- **URLs** — the bind address (default `http://*:5000`).

### Logging

- **Minimum Level** — defaults to `Information`.
- **Retention Days** — defaults to 30.

### Per-Profile (Dashboard)

- **Enabled** — toggle sync on/off.
- **Polling Interval** — 1 minute to 1 month (configurable from the dashboard).
- **Sync Window Days** — how far back the induction holder scan looks for completed inductions (default 7).
- **Bridge Sync Message Target** — the Gallagher cardholder field (description or a personal data field) to write timestamped `Created by OnLocation Bridge` / `Updated by OnLocation Bridge` messages into. Leave empty to leave cardholder fields untouched.

---

## Data Locations

All persistent data is stored under `%ProgramData%\OnLocation-Gallagher-Bridge\`:

| Path | Description |
|------|-------------|
| `bridge.db` | SQLite database (profiles, mappings, jobs, audit logs, match queue). |
| `config\config.json.crypt` | Encrypted configuration file (DPAPI LocalMachine scope). |
| `logs\bridge-YYYYMMDD.log` | Rolling daily log files (30-day retention). |

The install directory is `C:\Program Files\OnLocation-Gallagher-Bridge\`.

---

## Build and Deploy

### Prerequisites

- **.NET 9 SDK** — to build and publish.
- **Windows** — the bridge targets `win-x64` and uses Windows-specific features (DPAPI, Windows Service hosting).
- **PowerShell 5.1+** — for the install/uninstall scripts.
- **Administrator privileges** — required to install the service and set ACLs.

### Publish

```powershell
cd "app"
dotnet publish -c Release
```

The self-contained publish output is placed at:
```
app\OnLocationGallagherBridge\bin\Release\net9.0\win-x64\publish\
```

### Install as a Windows Service

Run PowerShell as Administrator:

```powershell
cd "app"
.\install-service.ps1
```

The script:

1. Copies the publish output to `C:\Program Files\OnLocation-Gallagher-Bridge\`.
2. Sets ACLs for Administrators and Network Service.
3. Registers the service as `OnLocation-Gallagher-Bridge` (Automatic startup, runs as `NT AUTHORITY\NETWORK SERVICE`).
4. Creates the `%ProgramData%\OnLocation-Gallagher-Bridge\` data directory with appropriate ACLs.
5. Opens firewall port 5000.
6. Starts the service.

Optional parameters:

```powershell
.\install-service.ps1 -ServiceName "CustomName" -Port 6000
```

### Uninstall

```powershell
.\uninstall-service.ps1
```

Stops and removes the service, deletes the install directory, and removes the firewall rule. Data in `%ProgramData%` is preserved.

### Run from the Command Line

For development or debugging:

```powershell
cd "app\OnLocationGallagherBridge"
dotnet run
```

The web UI is available at `http://localhost:5000`.

---

## First-Run Setup

1. **Open the web UI** at `http://localhost:5000`.
2. **Configure connectors** on the **Connector Settings** page:
   - Enter OnLocation credentials and Gallagher Command Centre credentials.
   - Save settings. (Connection tests are still available but no longer required before Initial Record Match; the match preflight validates both connections automatically.)
3. **Configure field mappings** on the **Field Mapping** page:
   - Select a profile (e.g. `employees` or `contractor-members`).
   - Choose the inductions to track (if applicable).
   - Build the source-to-target field map.
   - Set the default division and access groups for new cardholders.
   - Save the mapping.
4. **Run the initial record match** on the **Initial Record Match** page:
   - Select the profile and set the completion window (how far back to look for completed inductions).
   - Click **Preview** to see candidate matches.
   - Review each record: approve the suggested match, choose a different cardholder, create a new one, or exclude.
   - Complete the match to enable routine sync.
5. **Enable the profile** on the **Dashboard** and set the polling interval and sync window.
6. **Monitor** the dashboard's live activity card and the **Audit** page.

---

## Testing and Reset

The **Connector Settings** page provides three reset actions for testing from a clean state:

- **Clear sync data** — deletes all sync jobs, bookmarks, audit logs, and manual match queue entries from the database. Profiles and mappings are preserved.
- **Clear profiles and mappings** — deletes all sync profiles, entity mappings, field maps, and induction competency maps. The two default profiles are re-seeded.
- **Delete encrypted credentials** — removes the `config.json.crypt` file and resets the in-memory config to defaults.

Each action requires confirmation. These are outside the main settings form so a reset cannot accidentally re-save credentials.

---

## Project Structure

```
OnLocation-Gallagher-Bridge/
├── app/
│   ├── install-service.ps1              # Windows service installation script
│   ├── uninstall-service.ps1            # Windows service removal script
│   ├── OnLocationGallagherBridge.sln     # Visual Studio solution
│   └── OnLocationGallagherBridge/
│       ├── Program.cs                    # App startup, DI, logging, DB init
│       ├── OnLocationGallagherBridge.csproj
│       ├── Data/
│       │   └── BridgeDbContext.cs        # EF Core DbContext (SQLite)
│       ├── Models/
│       │   ├── Entities.cs               # EF Core entities (SyncProfile, SyncJob, etc.)
│       │   └── BridgeConfig.cs           # Configuration model
│       ├── Services/
│       │   ├── SyncEngine.cs             # Background sync loop
│       │   ├── OnLocationConnector.cs    # OnLocation REST API client
│       │   ├── OnLocationSourceService.cs # Source record fetching & merging
│       │   ├── GallagherConnector.cs     # Gallagher REST API client
│       │   ├── IdentityMatcher.cs        # Record matching engine
│       │   ├── TransformEngine.cs        # Field transformation engine
│       │   ├── JobProcessor.cs           # Job lifecycle processor
│       │   ├── AuditService.cs           # Audit log persistence
│       │   ├── AlertService.cs           # SMTP email alerts
│       │   ├── ConfigService.cs          # Encrypted config (DPAPI)
│       │   └── SyncActivityService.cs    # Live sync status tracking
│       ├── Pages/
│       │   ├── Index.cshtml / .cs        # Dashboard
│       │   ├── Settings.cshtml / .cs     # Connector settings & resets
│       │   ├── Mapping.cshtml / .cs      # Field mapping configuration
│       │   ├── MatchReview.cshtml / .cs  # Initial record match
│       │   ├── ManualSync.cshtml / .cs   # Manual sync trigger
│       │   ├── Exceptions.cshtml / .cs   # Failed job management
│       │   ├── Audit.cshtml / .cs        # Audit log viewer
│       │   └── Shared/_Layout.cshtml     # Nav layout
│       ├── wwwroot/                      # Static assets (CSS, JS, Bootstrap)
│       └── appsettings.json
├── docs/
│   ├── bridge-design-plan.md             # Original design document
│   └── bridge-build-guide.md             # Build phase guide
├── ref/                                  # API reference materials
├── gallagher-cardholder-parser/          # Gallagher API exploration tool
└── onlocation-member-parser/             # OnLocation API exploration tool
```

---

## Technology Stack

| Technology | Purpose |
|------------|---------|
| .NET 9 / ASP.NET Core | Application framework and web server |
| EF Core 9 + SQLite | Data persistence |
| Serilog | Structured logging (console + rolling file) |
| Razor Pages | Web UI |
| Bootstrap 5 | UI styling |
| MailKit | SMTP alert emails |
| Windows DPAPI | Credential encryption at rest |
| Windows Services | Background service hosting |
| Polly | HTTP resilience policies |
