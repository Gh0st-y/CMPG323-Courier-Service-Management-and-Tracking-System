# Demo deployment preparation (T57)

Status: preparation only. No IIS deployment, database restore, certificate installation or hardware check has been performed. Keep #57 open until the evidence below is recorded on the selected demo machine.

## Release prerequisites

Merge required fixes before selecting a release commit: database seed #110, CSV persistence #111, package-page injection #112, fee configuration #113 and reports verification #114. Review remaining security findings before using anything beyond synthetic demo data. Confirm the approved demo date and owners; original issue dates are historical.

The package script exports one committed Git snapshot, builds/publishes Release with Razor compilation, runs unit tests, checks required assets and records SHA256 hashes. It never installs IIS, changes a database, or overwrites an existing package. Final packages require a tag; candidate packages explicitly record that they are candidates.

```powershell
.\scripts\Prepare-DemoPackage.ps1 -ReleaseRef v1.0-demo -OutputDirectory C:\courier-packages\v1.0-demo
# Preparation check before a release tag exists:
.\scripts\Prepare-DemoPackage.ps1 -ReleaseRef HEAD -Candidate -OutputDirectory C:\courier-packages\candidate-01
```

Output: source archive/snapshot, `web/`, `web.zip`, and `manifest.json`. No private ignored Web.config.Local is exported. Keep packages outside source control. Existing assembly-binding warnings are not hidden by packaging.

## Target-machine configuration

1. Record machine, Windows/IIS version, release commit/tag, URL, SQL instance, app-pool name and operator.
2. Install IIS ASP.NET 4.x features and the .NET Framework 4.8 runtime. Use a dedicated .NET CLR v4.0 Integrated app pool.
3. Unpack `web.zip` into a new site directory; preserve the previous deployment for rollback. Do not overwrite a running deployment without an agreed outage window.
4. Set the deployed CourierServiceDb connection string to the chosen SQL Express instance. Developer LocalDB is not an IIS deployment strategy. Give the app-pool identity only the needed database permissions; AuditLog needs SELECT/INSERT, not UPDATE/DELETE.
5. Restore an agreed synthetic demo backup or initialize a separate synthetic database from the corrected schema and seed scripts. Never run cleanup/drop commands against an existing shared database without checking its identity and backup.
6. Configure a visible local SMTP inbox. The FromAddress must be valid; use only synthetic `.test` recipients. With #113 merged, Web.config.Local may contain appSettings overrides; it cannot override connectionStrings. Leave SMS disabled unless demonstrating the stub explicitly.
7. Bind HTTPS on the agreed URL with a certificate trusted by both phones. Release defaults redirect HTTP to port 443; set Https.Port to the actual binding if different. Do not export private certificate keys with the package.
8. Verify Release debug is false, secure cookies are enabled and remote errors are generic. Start the site, disable browser mock mode and sign in using the synthetic demo accounts.

The scanner library and logo are included in this preparation change. Camera pages now load the existing repository scanner asset locally, allowing fresh-browser camera loading without an external CDN. Certificate trust and browser permissions still require real-device checks.

## Acceptance evidence (fill after execution)

| Check | Result/evidence | Operator/date |
| --- | --- | --- |
| Tagged build published to intended IIS site | Pending | Pending |
| SQL connection and synthetic database restored | Pending | Pending |
| All five demo roles sign in | Pending | Pending |
| Register, print label, scan, store, ready, email, collect, confirmation email | Pending | Pending |
| USB scanner reads printed F20 QR | Pending | Pending |
| Printer produces readable QR labels | Pending | Pending |
| Two phones scan over trusted HTTPS | Pending | Pending |
| Fresh browser works with external internet disconnected | Pending | Pending |
| Local SMTP inbox displays both messages | Pending | Pending |
| Backup restored into a separate database and queried | Pending | Pending |
| Backup laptop has package, synthetic backup and required runtimes | Pending | Pending |

## Recovery

Keep the previous site folder, package manifest and a verified synthetic database backup. A database backup must be readable by the SQL Server service account. Validate a restore into a separate database with distinct data/log file paths; VERIFYONLY alone is not a demonstrated restoration. On failure, stop the demo, record the error and switch to the rehearsed backup environment or recording. Do not reset a database while presenting.

## Completion

Record actual evidence, resolve demo-breaking defects, then review #57 for closure. This preparation PR deliberately does not close it. Full rehearsal follows under #60; release freeze/tag/backup recording remains #61.

## Preparation verification performed

On 2026-10-10, the candidate package from commit `b1be0d8` was exported, restored, built and published into a new local output directory. All 333 non-integration MSTest tests passed with no skips. Required assets and the Release transform were verified; all manifest hashes matched the published files, and the local scanner asset matched the existing repository copy. Existing NuGet version and assembly-binding warnings were retained.

This candidate deliberately excludes other unmerged branches and is not a final demo release. SQL integration, IIS installation, backup restore, phone/printer checks and rehearsal remain pending.
