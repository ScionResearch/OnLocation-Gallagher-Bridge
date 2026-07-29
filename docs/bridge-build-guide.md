# OnLocation–Gallagher Bridge — Build Guide

> Refined, decision-ready guide based on the user's answers to the design questions.

## 1. Decisions from Q&A

| Topic | Decision |
|-------|----------|
| **Primary use case** | Pull contractor induction records from OnLocation and update the matching Gallagher cardholder's competency expiry and status. |
| **Master-data sync** | Also keep Gallagher cardholders in sync with new/existing OnLocation people (employees and/or contractor members). |
| **Entity selection** | User-configurable: choose which OnLocation entities to pull (employees, contractor members, induction holders, etc.). |
| **Direction** | OnLocation → Gallagher only for writes. Gallagher can be read for comparison/reconciliation. |
| **Data to sync** | Master-data changes only (name, email, department, induction valid-from/valid-to, status). No sign-in/out events. |
| **Matching** | Configurable match fields; prefer separate first/last name fields. If only a single name field is supplied, parse with a name-parser library and allow manual review. |
| **UI** | Web-based ASP.NET Core UI, served by the same host process as the service. Accessible from outside the host machine. |
| **Stack** | C# / .NET 8 (LTS). ASP.NET Core host + BackgroundService + Blazor Server or Razor Pages UI. |
| **Storage** | SQLite single-file database for mappings, state, and audit. Encrypted JSON config files for secrets and site profiles. |
| **Security / TLS** | DPAPI-encrypted secrets. Gallagher TLS verification can be disabled via config to keep initial deployment simple. |
| **Alerts** | Client-configurable SMTP email alerts only for now. |
| **Gallagher competencies** | Discoverable via `GET /api/competencies`; user maps OnLocation induction IDs to Gallagher competency IDs/hrefs. |
| **Auto-create cardholders** | User-configurable per profile (auto-create or queue for manual approval). |
| **Service account** | Run under `Network Service` initially. |
| **App root** | New application code lives in `/app`. Prototype code remains in `gallagher-cardholder-parser/` and `onlocation-member-parser/`. |

## 2. Recommended tech stack

| Layer | Choice | Rationale |
|-------|--------|-----------|
| Runtime | .NET 8 (LTS) | Long-term support, runs as Windows service, good async HTTP client, EF Core. |
| Host | `Microsoft.Extensions.Hosting.WindowsServices` + Kestrel | Single process runs as a service and serves the web UI. |
| UI | Blazor Server or Razor Pages + Bootstrap | Web UI with minimal client complexity. Blazor Server gives rich interactivity without a separate JS build. |
| Database | SQLite + EF Core | Single-file, zero install, easy backup/restore. Good enough for thousands of mappings. |
| Scheduling | `IHostedService` loop + cron-like schedules in config | Simple, no external scheduler dependency. |
| HTTP resilience | `Polly` | Retry, circuit breaker, rate-limit handling. |
| Logging | `Serilog` | Structured rolling logs; sink to file and optional Seq/Splunk later. |
| Email | `MailKit` / `MimeKit` | Reliable, widely used, supports modern auth. |
| Mapping | Custom lightweight transform engine + AutoMapper-style conventions | User-defined field mappings, default values, conditional rules. |
| Name parsing | `Humanizer` or a small name-parser utility | Handle `Greg van der Holden` style names when only one field exists. |

## 3. Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         Windows Server / PC                              │
│  ┌───────────────────────────────────────────────────────────────────┐   │
│  │        OnLocation-Gallagher-Bridge (ASP.NET Core + Kestrel)      │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐  │   │
│  │  │  Web UI      │  │ Sync Engine  │  │  Connector Services    │  │   │
│  │  │  (Blazor)    │  │ (Background) │  │  (OnLocation, Gallagher)│  │   │
│  │  └──────┬───────┘  └──────┬───────┘  └────────────────────────┘  │   │
│  │         │                 │                                      │   │
│  │  ┌──────┴─────────────────┴──────────────────────────────┐      │   │
│  │  │                 Shared Services / Domain                 │      │   │
│  │  │  Config, mapping rules, identity matcher, transform    │      │   │
│  │  │  engine, job queue, audit logger, alert sender           │      │   │
│  │  └───────────────────────┬────────────────────────────────┘      │   │
│  └───────────────────────────┼───────────────────────────────────────┘   │
│                              │                                           │
│  ┌───────────────────────────▼───────────────────────────────────────┐  │
│  │  Data layer                                                         │  │
│  │  - SQLite: mappings, sync cursors, queued jobs, audit history       │  │
│  │  - DPAPI-encrypted JSON: secrets, site profiles, SMTP settings       │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────┬───────────────────────────────────────────┘
                               │ HTTPS / HTTP
         ┌─────────────────────┼─────────────────────┐
         ▼                     ▼                     ▼
   OnLocation Cloud       Gallagher Server         SMTP Relay
```

## 4. Data layer

### 4.1 Encrypted JSON config files (`%ProgramData%\OnLocation-Gallagher-Bridge\config\`)

Keep **secrets and environment-specific settings** here, encrypted with Windows DPAPI at rest.

```jsonc
{
  "OnLocation": {
    "BaseUrl": "https://api.whosonlocation.com/v1",
    "AuthMode": "OAuth2",      // or "ApiKey" / "Basic"
    "ClientId": "<encrypted>",
    "ClientSecret": "<encrypted>",
    "ApiKey": "<encrypted>",
    "Scope": "domain:read domain:write"
  },
  "Gallagher": {
    "BaseUrl": "https://gallagher.local/api",
    "Username": "<encrypted>",
    "ApiKey": "<encrypted>",
    "DisableTlsVerification": true
  },
  "Smtp": {
    "Host": "smtp.office365.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "<encrypted>",
    "Password": "<encrypted>",
    "From": "bridge@example.com",
    "AlertRecipients": ["admin@example.com"]
  },
  "WebHost": {
    "Urls": "http://*:5000",
    "AdminPasswordHash": "<bcrypt>"
  },
  "Logging": {
    "Path": "%ProgramData%\\OnLocation-Gallagher-Bridge\\logs",
    "RetentionDays": 30
  }
}
```

### 4.2 SQLite database (`bridge.db`)

Use EF Core with the following core tables:

| Table | Purpose |
|-------|---------|
| `SyncProfiles` | One row per configured entity sync (e.g. Employees, ContractorMembers, InductionHolders). |
| `EntityMappings` | Bridge ID + OnLocation entity type/id ↔ Gallagher cardholder id + match confidence + override flag. |
| `SyncBookmarks` | Last cursor / `If-Modified-Since` / `changes` token per profile. |
| `SyncJobs` | Pending/in-progress/failed jobs. |
| `AuditLog` | Every create/update/delete, old/new snapshots, correlation id. |
| `ManualMatchQueue` | Proposed matches waiting for UI approval. |
| `InductionToCompetencyMap` | Maps OnLocation induction course/type IDs to Gallagher competency IDs. |

## 5. Sync engine design

### 5.1 Profiles

Each profile is independently scheduled and configured:

All profiles are user-configurable in the UI. The default supported profiles are:

- **Employees** (`GET /staff`) → create/update Gallagher cardholders.
- **Contractor members** (`GET /sp/member`) → create/update Gallagher cardholders.
- **Induction holders** (`GET /induction/{id}/holder` for configured induction IDs) → update cardholder competency expiry/status.

Each profile has:
- Enabled/disabled flag
- Polling interval (default 5 min; user can set down to 1 min)
- Endpoint URL override if needed
- Field mapping reference
- Match-rule reference

### 5.2 Incremental change detection

| System | Mechanism |
|--------|-----------|
| **OnLocation** | Use `If-Modified-Since` header and/or keyset cursor (`order=id`, `q=id>{lastId}`). Fallback to full list if no bookmark. For induction holders, remember the latest `modified`/`id` per induction. |
| **Gallagher (for comparison only)** | Optionally call `GET /api/cardholders/changes` if available; otherwise fetch by known IDs during reconciliation. |

### 5.3 Job lifecycle

1. **Source poll** — fetch changed records from OnLocation.
2. **Enqueue** — create one `SyncJob` per changed OnLocation record.
3. **Identity resolution** — match OnLocation record to Gallagher cardholder using configured rules.
   - High confidence → auto-link.
   - Medium confidence → queue in `ManualMatchQueue`.
   - No match → optionally create new cardholder or queue for approval.
4. **Transform** — apply field mapping to produce Gallagher cardholder/competency payload.
5. **Write** — call Gallagher create/update/delete.
6. **Commit state** — update mapping, bookmark, audit log.

### 5.4 Name matching strategy

- If OnLocation provides separate `first_name` and `last_name` → use them.
- If only `name` exists → parse into first/last with a simple rules-based parser.
  - Example: `Greg van der Holden` → first=`Greg`, last=`van der Holden`.
- Match rules are configurable ordered list, e.g.:
  1. Email exact
  2. First + Last exact
  3. First + Last fuzzy (Levenshtein ≤ 2)
  4. Employee/contractor number exact

## 6. Field mapping & transformations

Store mappings as JSON per profile:

```jsonc
{
  "TargetEntity": "Cardholder",
  "FieldMappings": [
    { "source": "email", "target": "email", "transform": "copy" },
    { "source": "first_name", "target": "firstName", "transform": "copy" },
    { "source": "last_name", "target": "lastName", "transform": "copy" },
    { "source": "department_id", "target": "division", "transform": "lookup", "lookupTable": "Departments" },
    { "source": "active", "target": "status", "transform": "condition", "rules": [{"eq": true, "out": "active"}, {"else": "inactive"}] }
  ],
  "DefaultValues": {
    "accessGroups": ["/api/access_groups/12"]
  }
}
```

For inductions, a separate mapping profile:

```jsonc
{
  "TargetEntity": "CardholderCompetency",
  "InductionId": 42,
  "CompetencyHref": "/api/competencies/99",
  "FieldMappings": [
    { "source": "validfrom", "target": "issued", "transform": "date" },
    { "source": "validto", "target": "expires", "transform": "date" },
    { "source": "status", "target": "status", "transform": "lookup", "lookupTable": "CompetencyStatus" }
  ]
}
```

## 7. Resilience & monitoring

| Concern | Implementation |
|---------|----------------|
| Retries | Polly retry with exponential backoff on transient HTTP errors. |
| Rate limits | Respect OnLocation 100 req/min; implement token bucket or sleep on `429` with `Retry-After`. |
| Dead letters | Failed jobs move to `SyncJobs` status `Failed` with error detail; UI shows retry action. |
| Circuit breaker | Pause a profile after N consecutive failures; surface in dashboard. |
| Health endpoint | `/health` returns service + connector status. |
| Logs | Serilog with correlation IDs per sync run. Files roll daily, retained 30 days. |
| Alerts | SMTP email on repeated failures, dead-letter queue, credential expiry, or certificate errors. |

## 8. Web UI pages

| Page | Purpose |
|------|---------|
| **Dashboard** | Status of each profile, last sync time, queue depth, errors, alerts. |
| **Connector settings** | OnLocation auth, Gallagher URL + credentials + TLS toggle, test connection buttons. |
| **Profiles** | Enable/disable profiles, set polling interval, choose entity type, choose induction IDs. |
| **Field mapping** | Per-profile visual editor for source → target fields, default values, lookups. |
| **Match review** | Approve/reject/override proposed matches; manually link records. |
| **Manual actions** | Force a sync, run a dry-run preview, reset a cursor, reprocess failed jobs. |
| **Audit log** | Search/filter sync history and changes made to Gallagher. |
| **Logs** | View/download Serilog files. |
| **SMTP alerts** | Configure alert recipients and test email. |

## 9. Security notes

- All secrets stored DPAPI-encrypted in JSON config files.
- UI login protected by a single admin account (hashed password in config).
- Service runs under `Network Service` initially (least privilege within those constraints).
- SQLite DB stored in `%ProgramData%\OnLocation-Gallagher-Bridge\` with ACLs restricted to administrators and `Network Service`.
- Gallagher TLS verification disabled only when `DisableTlsVerification: true`; UI warns that this is not for production.

## 10. Build phases

### Phase 1 — Skeleton & connectors (MVP)
- Create ASP.NET Core solution with Windows service host.
- OnLocation connector: OAuth2 + Basic + API key auth, pagination, `If-Modified-Since`.
- Gallagher connector: Basic auth, root `GET /api` discovery, cardholder CRUD, optional change tracking.
- Encrypted config + SQLite schema.
- Health endpoint and minimal logging.

### Phase 2 — Sync engine
- Background sync service with profiles.
- SQLite job queue and bookmarks.
- Employee/contractor member sync with simple field mapping.
- Retry/dead-letter handling.

### Phase 3 — Identity & inductions
- Matching engine + manual review queue.
- Induction holder sync → update Gallagher competency.
- Mapping UI and match-review UI.

### Phase 4 — UI, deployment, polish
- Full Blazor UI.
- Audit log viewer, log viewer, SMTP alerts.
- MSI installer or `sc.exe` install script.
- Documentation and runbook.

## 11. Configuration / installer notes

- Ship as a folder + `install-service.ps1` that:
  1. Creates `%ProgramData%\OnLocation-Gallagher-Bridge\`.
  2. Copies binaries into the chosen install directory.
  3. Creates initial encrypted config via the first-run web setup wizard (or a manual JSON template).
  4. Registers the service with `sc.exe` or `New-Service`, running as `Network Service`.
  5. Opens the configured Kestrel port in Windows Firewall if requested.
- First-run setup wizard in the web UI can create the encrypted config and test connections.

## 12. Final decisions captured

All previous open items are now decided:

1. **Entity scope** — User-configurable; employees, contractor members, and induction holders supported by default.
2. **Gallagher competencies** — Discovered via `GET /api/competencies`; user maps OnLocation inductions to Gallagher competencies.
3. **Auto-create cardholders** — User-configurable per profile (auto-create or queue for approval).
4. **SMTP auth** — Username/password for now; OAuth SMTP can be added later.
5. **Service account** — `Network Service` for initial deployment.

## 13. Next step

Scaffold the .NET 8 solution under `/app` and implement Phase 1: host, configuration encryption, SQLite store, and the two API connectors with test endpoints.

## 14. Files produced by this phase

- `docs/bridge-design-plan.md` — earlier brainstorm and Q&A.
- `docs/bridge-build-guide.md` — this file.
- `.windsurf/skills/gallagher-cardholder-api/` and `.windsurf/skills/onlocation-api/` — API reference skills.
