# T52 corrected-flow verification

PR #125, code checkout `3d2300d7ecaf573065dfb12759eb3d80a42d94b8`, including main `c270390`. Documentation changes correct the authorized Scan role, run outage UI checks before logout, restore only changed settings and restore the original phone using the same SQL-session backup.

Environment: Windows, .NET Framework 4.8, Visual Studio MSBuild, SQL Server LocalDB and IIS Express at `http://localhost:48181`. A new `CourierService_TaskVerification_T52` database was created from schema/seed for this rerun; no existing recipient database was used. Mock mode was disabled. Loopback HTTP and local SMTP are test-only configuration; deployment settings are unchanged.

| Check | Observed result |
| --- | --- |
| Sections 1–6 APIs | Local Postman v3: 35 requests, 59 assertions, zero failures; error codes and statuses checked. Forbidden user creation leaves no account. |
| Unknown-code Scan UI | Storage Staff signs in before Scan; unknown identifier displays friendly feedback. |
| Authenticated database outage | 503 ServiceUnavailable with Retry-After 60; Scan displays outage feedback before logout; AuditLog returns the generic Service Unavailable HTML page. |
| Logout/login during outage | Logout 204; login 503 without creating a new signed-in session. |
| Database recovery | Database returned ONLINE; fresh login and lookup both return 200. |
| SMTP connection refused | F20-0005 reaches ReadyForCollection in 32.50 ms; queue fails after three attempts, each with a failed log and reason. |
| SMS missing phone / email fallback | F20-0009 reaches ReadyForCollection in 30.31 ms; email Sent, SMS permanently Failed after one attempt, both logged. Local inbox receives synthetic email. |
| Phone cleanup | Executed the actual 9b/9h SQL snippets in the same connection; original phone restored. |
| Settings cleanup | Original Sms.Enabled restored; all other appSettings and connection settings remain equal to their pre-SMS-test values. |
| Diagnostics | Exception type and stack captured; no seeded recipient email addresses or raw SQL outage messages appear in the trace. |
| Notification unit tests | 63 passed, zero failed/skipped. Release/Razor build passes with inherited dependency/assembly warnings. |

The automated worker uses a 1-second poll and retry delay with three attempts. This verifies failure isolation, attempts and logging, not the manual default 60-second retry timings. No full test-suite or physical-device rerun is claimed.

Evidence: [Postman summary](t52-postman-results.txt), [browser/outage results](t52-browser-results.json), [notification/cleanup results](t52-notification-results.json), [final safety checks](t52-final-checks.json). Runtime configuration, session cookies, SMTP captures and full traces remain under ignored artifacts. After verification, the temporary services were stopped and only the test checkout's original configuration was restored.
