# OnLocation-Gallagher Bridge Release Notes

## v1.1.0.0 — 25 August 2026

Security and reliability hardening release, based on feedback from a security review of v1.0.0.0.

### Fixed

- **Personal data no longer written to the log file at the default log level.** Full request/response bodies (staff names, emails, employment status, location) are now only logged at `Debug`, including a leftover `Information`-level log of the full cardholder response body on the Exceptions page that bypassed the intended `Information`/`Debug` split.
- **Web sessions are now invalidated immediately** when a user is disabled or deleted, or when the service restarts — previously a signed-in session remained valid until the cookie expired regardless of account state.
- **Exceptions page no longer crashes** when a previously suggested Gallagher cardholder match no longer exists (e.g. deleted in Command Centre); it now shows "Unknown cardholder" instead of a 500 error.
- **Empty password on login no longer crashes the app** (`BCrypt.Verify` throws on an empty, not just null, input — both the login and change-password checks now guard against this).

### Added

- **Account lockout** — configurable failed-login threshold and lockout duration (Users page), with immediate admin unlock.
- **Break-glass password recovery** — `OnLocationGallagherBridge.exe --reset-password <username>`, run locally on the server (elevated prompt, service stopped), for when every admin is locked out or has forgotten their password. Generates a random password meeting the configured complexity rules; no SMTP or web-facing attack surface required.
- **Runtime log level control** — switch between `Information` and `Debug` from **Connector Settings → Logging** without restarting the service, for temporary troubleshooting.

### Asset

- `OnLocationGallagherBridge-v1.1.0.0.msi` — per-machine installer.

### Install / Upgrade

```powershell
msiexec /i "OnLocationGallagherBridge-v1.1.0.0.msi" /qn /norestart
```

The installer performs an in-place major upgrade over any existing v1.0.x install; existing configuration, database, and users are preserved.

See [README.md](README.md) for details, including the new [Authentication and Access Control](README.md#authentication-and-access-control) section.

---

## v1.0.0.0 — 4 August 2026

Initial stable release for testing of the OnLocation-Gallagher Bridge.

### What’s included

- **Windows service + ASP.NET Core web UI** for synchronising OnLocation records with Gallagher Command Centre.
- **Sync profiles** for Staff and Contractors with fast/full sync scheduling and incremental record fetching.
- **Field mapping UI** to map OnLocation fields to Gallagher cardholder fields, including induction/competency mapping and division/access-group defaults.
- **Initial record matching** with candidate review and manual approve/reject/create/exclude actions.
- **OnLocation REST API connector** with OAuth2/API Key/Basic auth and outbound IPv4 preference.
- **Gallagher Command Centre REST API connector** with automatic API discovery, cardholder create/update, and competency sync.
- **Background sync engine** with identity matching, transform engine, audit logging, and exception handling.
- **Email alerts** via SMTP with configurable recipients, groups, and test-email support.
- **Encrypted configuration** stored under `%ProgramData%` using Windows DPAPI.
- **System tray status widget** that autostarts for all users and shows service health and live sync activity.
- **WiX v4 MSI installer** that installs the service, tray widget, Start Menu shortcut, firewall exception, and Windows autostart.

### Asset

- `OnLocationGallagherBridge-v1.0.0.0.msi` — per-machine installer.

### Install

```powershell
msiexec /i "OnLocationGallagherBridge-v1.0.0.0.msi" /qn /norestart
```

See [README.md](README.md) for first-run setup and configuration details.
