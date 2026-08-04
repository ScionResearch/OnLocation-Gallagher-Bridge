# OnLocation-Gallagher Bridge Release Notes

## v1.0.0 — 4 August 2026

Initial stable release of the OnLocation-Gallagher Bridge.

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

- `OnLocationGallagherBridge.Installer.msi` — per-machine installer.

### Install

```powershell
msiexec /i "OnLocationGallagherBridge.Installer.msi" /qn /norestart
```

See [README.md](README.md) for first-run setup and configuration details.
