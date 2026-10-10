# Reports backend verification (T47)

## Semantics

`GET /api/reports` is restricted to Supervisor and SystemAdmin. Dates select packages by **date received**, using inclusive South African calendar days (UTC+2). The default is the last 30 days, including today. When only an end date is supplied, the default start is 29 days before that end. Reversed, unrepresentable or malformed dates are rejected.

The report describes a receipt cohort's **current** statuses and payment statuses. Fees paid are the saved fees of packages currently marked Paid; outstanding fees are those currently Unpaid. Exempt fees are excluded from both totals. This is not a cash transaction ledger or a report of payments made during the selected dates. Daily volumes count registrations, including zero days. Outstanding collections exclude Collected; the table returns the 50 oldest matching packages.

## Executed checks

Verified on 2026-10-10 using SQL Server LocalDB and the isolated synthetic `CourierService_TaskVerification_20261010` database, initialized from the corrected schema in #110.

| Check | Expected | Result |
| --- | --- | --- |
| SAST day boundaries | Jan 2 starts at Jan 1 22:00 UTC and ends exclusively at Jan 2 22:00 UTC | Passed |
| Cohort counts | 3 packages; 1 ReadyForCollection; 1 Collected | Passed |
| Fee totals | R12.50 paid, R10.00 unpaid; exempt package excluded | Passed |
| Outstanding collections | 2; collected package excluded | Passed |
| Collection duration | 1.0 day for the collected fixture | Passed |
| Combined payment/classification filter | Only personal Paid package; grouped fee total R12.50 | Passed |
| Status filter | One ReadyForCollection package | Passed |
| Daily series | 3, 1, 0 registrations over three days | Passed |
| Empty cohort | Zero counts/fees, null collection duration, empty outstanding table | Passed |
| Default and historical windows | 30 SAST days, historical start relative to supplied end | Passed |
| Invalid ranges | Reversed and unrepresentable dates rejected | Passed |

Three SQL integration tests and three date-window unit tests passed, with no skips. Fixture packages, recipients, users and roles are removed after each SQL test. No real recipient data was used. Release web build with Razor compilation passed with existing dependency binding and duplicate-using warnings.

## Reproduce

Initialize a separate database from db/schema.sql after merging #110. Use a name beginning `CourierService_TaskVerification_`; these tests refuse other databases because they create synthetic fixture records.

```powershell
$env:COURIER_TEST_CONNECTION_STRING = 'Server=(localdb)\MSSQLLocalDB;Database=CourierService_TaskVerification_20261010;Trusted_Connection=True;'
dotnet test Tests/CourierService.Tests.csproj --filter 'FullyQualifiedName~ReportsRepositoryIntegrationTests|FullyQualifiedName~ReportDateWindowTests'
```

## Remaining operational acceptance

Browser role checks, chart rendering, 50,000-package performance and demo-machine verification belong to T51/T53/T57 and were not executed here. Do not interpret this backend evidence as completion of those tasks.
