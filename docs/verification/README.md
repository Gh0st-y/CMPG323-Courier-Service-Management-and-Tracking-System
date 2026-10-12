# Reproduce local acceptance

These scripts write synthetic packages/users. Use a disposable checkout and a **new** database named `CourierService_TaskVerification_*`. Loopback is necessary but is not proof a database is disposable: inspect configuration before running. Never point the scripts at a production database.

Prerequisites: .NET Framework 4.8, Visual Studio web build tools, SQL Server LocalDB/sqlcmd, Node, Chrome, Playwright with Firefox, and Postman CLI. The public seeded `Demo@2026!` password is synthetic; no production password or session cookie is committed. Runtime logs, cookies and SMTP captures remain under ignored `artifacts/`.

1. Create a disposable worktree of the reviewed candidate and build `Web/CourierService.Web.csproj` in Release.
2. Run `scripts/verification/Prepare-AcceptanceEnvironment.ps1 -SourceDirectory <checkout> -OutputDirectory <runtime> -DatabaseName <new isolated name>`. It refuses an existing database and changes only that checkout's configuration. Defaults: HTTP 48131, local SMTP 48132, mock mode disabled. HTTP is confined to this local harness; deployed sites still use HTTPS.
3. Start `node scripts/verification/smtp-inbox.cjs 48132 <runtime>/smtp-messages.jsonl`. The inbox binds loopback and writes synthetic deliveries locally.
4. From the repository root run:

```powershell
postman collection lint 'postman/collections/Remaining acceptance'
postman collection run 'postman/collections/Remaining acceptance' --working-dir . --no-report-events
```

The ordered v3 collection initializes a unique run ID, switches among all five demo roles, and captures session cookies only in memory. Expected: 37 requests, 59 assertions. Use the collection's `base_url` variable if the isolated port changes. Local runs do not require Postman cloud login; CLI output-file/cloud reports do.

5. Install Playwright in an ignored tooling directory, download its Firefox binary, set `PLAYWRIGHT_BROWSERS_PATH`, then run:

```powershell
node scripts/verification/browser-acceptance.cjs <playwright-tooling-directory> <screenshot-output> http://localhost:48131
node scripts/verification/browser-workflow.cjs http://localhost:48131 <workflow-output> <playwright-tooling-directory>
powershell -File scripts/verification/Verify-SmsFallback.ps1 -EnvironmentFile <runtime>/environment.json
dotnet test Tests/CourierService.Tests.csproj --filter 'FullyQualifiedName!~Integration'
```

Chrome path is Windows-specific. Both browser runners disable proxy use for their own loopback tests; they do not alter Windows settings. The SMS check changes only the isolated config and one newly created synthetic recipient, and restores `Sms.Enabled=false` afterward.

Performance: seed another isolated database with `db/seed-50k.sql` after replacing its database name; run `performance-acceptance.cjs <loopback-url> <output> <playwright-tooling-directory>`. It changes 20 seeded Registered packages and imports 500 rows. Repeated runs require unused seeded Registered IDs (`PERF_STATUS_OFFSET`) and the actual initial row count (`PERF_SEEDED_PACKAGES`). It records ten page loads, retaining the first sample, and five independent sessions for one minute. Preserve failed results too. See [PERFORMANCE.md](PERFORMANCE.md) for targets and CLI limitations.

Stop only the IIS/SMTP processes started for the harness after recording results. No script drops an existing database. Physical phones, USB scanners, printing and the demo rehearsal require separate human evidence.
