# Technical documentation — T59

## 1. Overview

Courier Service Management and Tracking System is a staff-facing ASP.NET MVC 5 application for registering, storing, tracking and collecting parcels at the NWU F20 courier point. The repository targets **.NET Framework 4.8** and **SQL Server Express/LocalDB**. Its API contract is maintained in [`API_CONTRACT.md`](API_CONTRACT.md); authoritative scope decisions are in [`../DECISIONS.md`](../DECISIONS.md).

## 2. Architecture

```text
Browser / QR scanner
        |
        v
Web (ASP.NET MVC controllers, Razor views, JS)
        |
        v
Services (validation, workflows, state transitions, notifications)
        |                               |
        v                               v
Domain (entities and interfaces)     Data (ADO.NET repositories)
                                        |
                                        v
                                    SQL Server
```

The dependency rules are documented in [`../README.md`](../README.md): Web uses Services and Data; Services and Data use Domain; SQL access is restricted to Data. `Tests` contains MSTest unit and integration coverage. The source-of-truth solution is `CourierService.sln`.

## 3. Repository guide

| Location | Purpose |
| --- | --- |
| `Domain/Entities` | Package, recipient, user, storage and notification models |
| `Services/Packages` | Registration, scanning, status changes, collection, payments and CSV parsing |
| `Services/Notifications` | Notification queueing, composition, sending and resend logic |
| `Services/Auth`, `Services/Users` | Authentication, roles and account administration |
| `Data/Repositories` | SQL persistence and queries |
| `Web/Controllers`, `Web/Controllers/Api` | MVC and HTTP endpoints |
| `Web/Views` | Razor UI |
| `db/schema.sql` | Database creation and schema |
| `db/seed.sql` | Demo data (development only) |
| `db/seed-50k.sql` | Larger performance-test data set |
| `Tests/` | MSTest suite |
| `docs/` | API, operations and test documentation |

## 4. Setup (Windows)

1. Install Visual Studio 2022 with **ASP.NET and web development** and the **.NET Framework 4.8 targeting pack**. Install SQL Server LocalDB or SQL Server Express.
2. Clone the repository; open `CourierService.sln` and allow NuGet restore.
3. Connect SSMS or Visual Studio SQL Server Object Explorer to `(localdb)\MSSQLLocalDB`. Run `db/schema.sql`.
4. Check the `CourierServiceDb` connection string in `Web/Web.config` matches your local instance. Do not commit credentials. Use the ignored local override approach described in the README where applicable.
5. Set the `Web` project as startup and run with IIS Express (F5). The documented development HTTPS address is `https://localhost:44301/`.
6. For demo records only, run `db/seed.sql` against your **development** database. Do not load demo users into production.
7. Open Test Explorer and run the MSTest suite. Database integration tests need an accessible test database; inspect their setup before running them against any shared instance.

See the README for phone camera/HTTPS certificate setup. Do not copy development certificate private keys into this repository.

## 5. Configuration

`Web/Web.config` provides the local database connection string and application settings. Relevant keys currently include:

| Key | Purpose |
| --- | --- |
| `Smtp.Host`, `Smtp.Port` | SMTP server location |
| `Smtp.FromAddress`, `Smtp.UseSsl` | Sender identity and TLS setting |
| `Sms.Enabled`, `Sms.Provider` | SMS integration feature toggle/provider |
| `Session.InactivityTimeoutMinutes` | Staff session inactivity limit |
| `Https.RedirectEnabled`, `Https.Port` | HTTPS redirect behavior |

Never commit production SMTP credentials, database passwords, recipient data or local certificate keys. Use local ignored files/environment-specific deployment configuration as appropriate. The `dbo.AppConfig` table provides database-backed operational configuration; review `Data/Repositories/AppConfigRepository.cs` and the relevant service before changing fee values.

## 6. Database / ERD orientation

The schema in `db/schema.sql` is the authoritative source for columns, types, constraints and foreign keys. Principal tables are:

| Table | Role |
| --- | --- |
| `Roles`, `Users` | Staff identity and permissions |
| `Recipients` | Recipient contact information |
| `StorageLocations` | Physical storage locations |
| `Packages` | Parcel identity, status, fees and recipient/storage references |
| `PackageStatusHistory` | Status transition history |
| `NotificationQueue`, `NotificationLog` | Pending notification work and delivery results |
| `AuditLog` | Security and business audit events |
| `AppConfig` | Runtime operational configuration |

For a diagram, import `db/schema.sql` into a SQL Server diagramming tool or use the existing project ERD if supplied by the team. **This document is a schema orientation, not a replacement for a validated graphical ERD.**

## 7. API and operational flows

Use [`API_CONTRACT.md`](API_CONTRACT.md) for authoritative routes, request/response shapes and role permissions. Typical flows are:

- **Registration:** authorized staff submits package and recipient information; the service validates and persists it; the identifier can be encoded as a QR code.
- **Scan:** a scanned F20 identifier is normalized and looked up; invalid/unknown codes are handled without disclosing unrelated parcel data.
- **Status lifecycle:** `Registered -> InStorage -> ReadyForCollection -> Collected`. Transitions must follow the service's rules; collection is a distinct authorized workflow.
- **Notifications:** qualifying status changes enqueue messages; a processor uses configured senders and records delivery outcomes.
- **Audit:** sensitive state changes are recorded for review.

## 8. Testing and troubleshooting

- **Unit tests:** `Tests/Packages`, `Tests/Auth`, `Tests/Users`, `Tests/Notifications`, `Tests/Security` and other test folders.
- **Integration tests:** `Tests/Integration` exercises SQL-backed behavior; configure an isolated database before running.
- **Negative scenarios:** [`NEGATIVE_TESTS.md`](NEGATIVE_TESTS.md) documents unauthorized actions, invalid transitions, bad scans, database outages and SMTP failures.
- **Release checks:** [`RELEASE_CHECKLIST.md`](RELEASE_CHECKLIST.md) covers final readiness tasks.

Common problems:

| Symptom | Check |
| --- | --- |
| SQL connection fails | SQL instance name, LocalDB installation, database existence, connection string |
| NuGet/compile failure | Restore packages, .NET Framework 4.8 targeting pack, Visual Studio web workload |
| HTTPS or camera failure | Correct IIS Express SSL binding, trusted local certificate and browser camera permission |
| No notification email | SMTP host/port, development inbox availability, queue/log entries, feature settings |
| Unexpected 401/403 | Login/session and role restrictions in API contract |

## 9. Security and deployment notes

Run over HTTPS, limit database privileges, avoid secrets in source control, and verify role restrictions at the server rather than relying on hidden UI controls. Seed data and local SMTP inboxes are for development. Deployment and backup/restore validation must be completed by the project team for the demo environment; this document does not claim production readiness.

## 10. Maintainer checklist

- Confirm the deployed schema matches `db/schema.sql`.
- Confirm settings and role assignments for the target environment.
- Run automated and manual negative tests.
- Verify collection, notification, audit and import workflows.
- Check the release checklist and rehearse database recovery.

_Last updated: 2026-10-09. Based on the user-supplied repository snapshot; verify against the latest main before merging._
