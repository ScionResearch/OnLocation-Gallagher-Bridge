# OnLocation–Gallagher Bridge — Design Plan

> Compact plan and build outline for `OnLocation-Gallagher-Bridge`.
> Based on the two API skill files (`gallagher-cardholder-api`, `onlocation-api`) and the existing prototypes (`index.html`, `proxy.py`).

## 1. Goal

Build a robust, network-resident integration that keeps Gallagher Command Centre cardholders in sync with people/visitor records held in MRI OnLocation, with a configuration UI and comprehensive operational logging.

## 2. High-level architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Windows Host                              │
│  ┌──────────────────┐      ┌─────────────────────────────┐  │
│  │   Bridge Service │◄────►│  SQLite / SQL Server LocalDB │  │
│  │   (sync engine)  │      │  - mappings                 │  │
│  └────────┬─────────┘      │  - state / cursor / job queue │  │
│           │                 │  - audit log                │  │
│           │                 └─────────────────────────────┘  │
│           │                                                  │
│  ┌────────▼─────────┐                                        │
│  │  Management UI   │                                        │
│  │  (web or WPF)    │                                        │
│  └──────────────────┘                                        │
└───────────────────┬───────────────────────────────────────────┘
                    │
      ┌─────────────┼─────────────┐
      ▼             ▼             ▼
OnLocation      Gallagher      SMTP/Teams
(cloud API)   (local server)   (alerts)
```

- **Service**: background worker that polls/sources changes, executes sync jobs, writes to local DB, logs to rolling files + structured log sink.
- **Database**: local relational store for mappings, sync state (cursors/bookmarks), job queue, and operational audit.
- **Management UI**: separate process or embedded web host for configuration, field mapping, matching review, monitoring, and log inspection.
- **Security**: secrets encrypted at rest (DPAPI/Windows Credential Manager/vault), HTTPS to OnLocation, configurable TLS handling for Gallagher.

## 3. In scope / out of scope

### In scope
- One-way sync from OnLocation to Gallagher cardholders (default; configurable per entity type).
- Entity types: employees, contractor members, visitor events/pre-registrations, optionally inductions/certification holders.
- Initial matching wizard + ongoing identity reconciliation.
- Per-site/tenant configuration and per-entity field mapping.
- Change-driven incremental sync with optional fallback full sync.
- Windows service hosting, installer, and management UI.
- Logging, alerting, retry/dead-letter handling, health checks.

### Out of scope (for first phase)
- Bidirectional writes from Gallagher back to OnLocation.
- Real-time physical access events (alarms/door events) from Gallagher.
- OnLocation webhook ingestion (the API docs currently show only polling/audit-log change detection).

## 4. Core components

| Component | Responsibility |
|-----------|----------------|
| **Connector: OnLocation** | Auth (OAuth client-credentials or API key/Basic), rate-limit handling, pagination, `If-Modified-Since`/audit-log change detection, retry/backoff. |
| **Connector: Gallagher** | Auth (Basic API key, certificate trust if self-signed), root `GET /api` discovery, cardholder CRUD, change tracking (`GET /api/cardholders/changes`), batch/field-specifier efficiency. |
| **Sync Engine** | Schedule jobs, detect changes, queue work, apply transformations, call connectors, update mapping state. |
| **Mapper/Transform engine** | User-defined field mapping rules, default value injection, conditional logic, data-type/format conversions. |
| **Identity Reconciler** | Propose matches, allow manual approve/override, generate stable bridge IDs, detect duplicates. |
| **Store** | Persist mappings (`onlocation_id ↔ gallagher_id`), sync cursor/bookmarks, queued jobs, change history, operational logs. |
| **Management UI** | Site/connector config, field mapping editor, match review, dashboard/status, log viewer, manual actions. |
| **Windows Service host** | Installs/runs the sync engine and embedded UI host as a service. |

## 5. Data flow

### Incremental sync (preferred)

1. OnLocation connector polls each relevant endpoint with a stored `If-Modified-Since` or keyset cursor.
   - Employees: `GET /staff`
   - Contractor members: `GET /sp/member`
   - Visitor events: `GET /visitor/event`
   - Optionally audit log: `GET /audit` for broad change detection.
2. Changed records are queued as sync jobs keyed by OnLocation ID + entity type.
3. Sync engine dequeues jobs, fetches full record(s) if needed.
4. Identity reconciler resolves the OnLocation record to a Gallagher cardholder:
   - Exact match on configured key fields.
   - Fuzzy / candidate proposals for manual review.
   - New cardholder created if unresolved and policy allows.
5. Mapper transforms source record into a Gallagher cardholder request payload.
6. Gallagher connector performs create (`POST`), update (`PATCH`), or delete (`DELETE`).
7. State store updated: mapping, cursor, last-success timestamp, audit log.

### Gallagher-side change detection
- For safety / reconciliation, periodically call `GET /api/cardholders/changes` (if supported/licensed) to detect cardholders modified outside the bridge and update local state only.

## 6. Identity reconciliation & matching

| Capability | Description |
|------------|-------------|
| **Stable bridge ID** | Generate a UUID or deterministic hash per OnLocation record; stored in mapping table. |
| **Match rules** | User selects ordered matching fields (e.g. `email`, `employee_number`, `first_name+last_name+date_of_birth`). |
| **Proposals** | Engine proposes high-confidence matches; low-confidence records appear in UI for manual link/merge/create. |
| **Manual override** | Operator can approve, reject, or manually link a record; override is persisted. |
| **Duplicate guard** | Prevents creating two Gallagher cardholders for the same OnLocation record and warns on multiple OnLocation records matching one Gallagher cardholder. |

## 7. Field mapping & transformations

- Per-entity mapping profiles (e.g. “Employees to Cardholders”, “Contractors to Cardholders”).
- Source fields: standard OnLocation fields + custom fields (`GET /customfield`).
- Target fields: Gallagher cardholder fields + Personal Data Fields (PDFs) by name/ID.
- Transform primitives: copy, static value, concat, substring, date format, case, lookup table, conditional.
- Validation: required target fields, type/format checks, length limits.
- Preview mode: simulate transformation on sample records before enabling sync.

## 8. Efficiency & rate limiting

- **OnLocation**: respect 100 req/min per credential; implement token/credential rotation if needed, adaptive backoff on `429`, `Retry-After` handling.
- **Gallagher**: use `fields=` to avoid fetching unneeded data; use `top`/`sort`/next-link pagination; prefer `changes` endpoint over full searches.
- **Local queuing**: batch changes where possible; avoid re-querying unchanged records; store latest cursors.
- **Adaptive polling**: configurable interval per entity (down to 1 minute), with back-off on error/no-change.
- **Differential updates**: only PATCH changed fields.

## 9. Resilience, logging & monitoring

| Concern | Approach |
|---------|----------|
| **Logging** | Structured logs (Serilog/NLog/loguru) to rolling files + optional Seq/Splunk/ETW. Per-job correlation IDs. |
| **Retries** | Exponential backoff for transient errors; dead-letter queue for permanent failures. |
| **Circuit breaker** | Pause a connector after repeated failures; surface in UI and alert. |
| **Health checks** | Endpoint/UI status: last sync, queue depth, error rate, credential expiry. |
| **Alerts** | Email/Teams/webhook on credential expiry, repeated failures, dead-letter records. |
| **Operational audit** | Every create/update/delete logged with before/after snapshots and operator identity. |

## 10. Security

- Encrypt all secrets (API keys, OAuth client secrets, Gallagher credentials) at rest.
- Use Windows DPAPI or a small local vault; never store keys in plain JSON.
- Run service under a dedicated low-privilege account; UI/admin access restricted to authorized users.
- Gallagher TLS: trust configured root CA or explicit server certificate; support “verify disabled” only in dev/test with clear warnings.
- OnLocation OAuth: store `access_token`/`refresh_token` securely; refresh before expiry.

## 11. Deployment

- MSI installer or portable directory + `sc.exe`/`NSSM` service registration.
- Service executable configurable via UI or config file.
- Database auto-created on first run.
- Upgrade path: preserve DB + config; run DB migrations on startup.

## 12. Suggested build phases

1. **Phase 1 — Core connector + sync loop**
   - OnLocation connector (auth, rate-limit, pagination, `If-Modified-Since`).
   - Gallagher connector (discovery, cardholder CRUD, changes endpoint).
   - SQLite store and simple mapping table.
   - Console-only sync worker for one entity type (e.g. employees).

2. **Phase 2 — Identity & mapping**
   - Matching engine + manual review data model.
   - Field mapping engine.
   - Initial-load wizard.

3. **Phase 3 — UI & service host**
   - Management UI: config, mapping, match review, status.
   - Windows service wrapper.
   - Installer.

4. **Phase 4 — Robustness & features**
   - Full audit logging, alerting, dead-letter handling.
   - Additional entity types (contractors, visitors, inductions).
   - Multi-site/tenant profiles.

## 13. Open questions for you

Please answer these so the build outline can be finalized:

1. **Entity scope** — Which OnLocation records become Gallagher cardholders? Employees only, or also contractors, visitors, pre-registered visitors, induction/certification holders?
2. **Sync direction** — Strictly OnLocation → Gallagher? Any scenarios where Gallagher should write back (e.g. access group assignment/status)?
3. **Trigger events** — Should a sign-in/out event in OnLocation update a Gallagher cardholder state, or only master-data changes (name, email, department, induction status)?
4. **Matching keys** — Which fields do you usually trust for the initial VLOOKUP-style match? Email, employee ID, name+DOB, something else?
5. **UI style** — Web-based UI (served by the service) or native WPF desktop app? Should it be usable from remote machines?
6. **Stack preference** — Continue with Python/Flask/FastAPI + SQLite, or move to .NET/C# for tighter Windows-service integration?
7. **Database** — SQLite okay for production, or do you prefer SQL Server LocalDB/Express?
8. **Credential storage** — Are you happy with DPAPI-encrypted config files, or do you require Windows Credential Manager / a secrets manager?
9. **Gallagher TLS** — Does the test/prod server use a self-signed cert? Should the bridge trust a specific CA file or allow insecure mode for staging?
10. **Notifications** — Email alerts? Teams webhook? Both? Any specific alerting endpoint?

Once you answer these, I’ll turn this into a detailed build-ready guide (and optionally a `.windsurf/workflows/bridge-build.md` workflow).
