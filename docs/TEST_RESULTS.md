# Verification results

Date: 2026-10-12. Environment: Windows workspace, .NET Framework 4.8, Visual Studio MSBuild, Node, IIS Express and SQL Server LocalDB. Earlier unit checks are below; the live acceptance record now includes real HTTP and browser execution.

| Branch/change | Check | Observed result |
| --- | --- | --- |
| Audit viewer #119 after merged #113 | Non-integration .NET suite | 346 passed, 0 failed, 0 skipped. |
| Audit viewer #119 | Malicious suggestion markup/selection regression | Passed; markup is text, selection preserves the value, empty/no-match clears suggestions. |
| Audit viewer #119 | Release build with Razor view compilation | Passed; existing assembly-version warning remains. |
| POPIA #118 after merged #113 | Full non-integration .NET suite after correcting the inherited HTTPS port mismatch | 359 passed, 0 failed, 0 skipped; no individual test excluded. |
| POPIA #118 | Identifier regression and Release/Razor build | Passed; existing assembly-version and duplicate-using warnings recorded at that point. |
| UI/verification fix branch after externally merged #113 | Full non-integration .NET suite | 346 passed, 0 failed, 0 skipped, including HTTPS configuration and live fee tests. |
| UI/verification fix branch | CSV import regression | Passed: multipart, saved/error rows, duplicate submit, auth/network/malformed response, mock mode and stale mock preference with mocks disabled. |
| UI/verification fix branch | User management regression | Passed: cancel/confirm, duplicate clicks, failed requests, role reset and self-protection. |
| UI/verification fix branch | Report links regression | Passed: app-relative detail path, encoded identifiers and safe recipient text. Charts intentionally absent from the Node DOM test. |
| UI/verification fix branch | Package identifier regression | Passed: normal identifiers, quotes, script markup and escape characters. |
| UI/verification fix branch | Registration fee frontend regression after #113 | Passed: R12.50 display and failed-lookup submission protection. |
| UI/verification fix branch | Release/Razor build | Passed; existing assembly-version warning remains. Duplicate using removed. |

The older branch checks above did not execute live acceptance. The separate pass below used new synthetic databases and a real site with mocks disabled. Unit tests do not substitute for the physical/deployment checks still pending.

## Live acceptance record

Candidate: integrated commit `7a991ee` (UI #121, security #118, audit #119 and documentation #115), plus the scan/timeline/report follow-up in this branch. GitHub subsequently merged the earlier implementation PRs; this follow-up still requires review. Acceptance site: `http://localhost:48131`, database `CourierService_TaskVerification_20261012_Remaining`. Performance database: `CourierService_TaskVerification_20261012_Performance`. Both were newly created from schema/seed; no existing recipient database was modified. Tester: Codex automated harnesses.

| Area | Result | Tester/environment/evidence |
| --- | --- | --- |
| Unauthenticated and cross-role HTTP access | Passed sampled role matrix | Local Postman v3 collection: 37 requests, 59 assertions, zero failures. Protected routes return 401/403. This is not an exhaustive security review of every route. |
| Intake Clerk lifecycle and fee behavior | Passed | Real registration saves configured fee; UI form opens saved QR label. Earlier R12.50 regression and user-observed live config result remain recorded separately. |
| Storage Staff scan/status/notifications | Passed browser/API paths | Keyboard Enter lookup and Registered -> InStorage -> ReadyForCollection; label action hidden and direct route forbidden. Physical USB/camera pending. |
| Collection Staff identity/collection/payment | Passed | UI identity checkbox; Cancel leaves ReadyForCollection; Confirm persists Collected; payment saves and survives reload. |
| Supervisor import/reports/audit | Passed sampled paths | Actual multipart import saves two rows/reports one invalid row; report and audit pages render on integrated candidate. |
| System Admin users/storage locations | Passed | Real API create/role/disable/enable/storage create; browser creation cancellation clears fields, deactivation cancellation leaves account active; password hashes absent from response. |
| SQL integration/fresh database | Fresh schema/seed and HTTP persistence passed | Dedicated hard-coded integration suite was not redirected to these databases. Final non-integration suite: 359 passed, zero failed/skipped. |
| SMS fallback (#38) | Passed | Disabled: email Sent, SMS not queued. Stub enabled: both Sent. Missing synthetic phone: SMS Failed with failed log; email Sent with sent log. Status requests 8.76–11.19 ms. 63 notification unit tests pass, including provider-failure isolation. No external SMS provider exercised. |
| Chrome/Firefox, 1366x768 and narrow browser | Passed | 66 checks, zero failures or page exceptions; five roles in both browsers. At 390x844, reports/scan/users/audit have no horizontal overflow. Five additional Chrome lifecycle-form checks pass. Screenshots use synthetic data. |
| 50,000-row performance targets (#53) | Measured; see report | [Performance record](verification/PERFORMANCE.md). Original first page exceeded target; precompiled release retest recorded separately. |
| Phone camera scanning (#56) | Phone 1 passed; phone 2 pending | iPhone 15 Pro Max, iOS 26.6.2, Safari, over trusted HTTPS on the LAN (mkcert): camera opened, label QR scanned on the Scan page, package found, status change saved; Collection page camera lookup worked. Tester: Hannu (Visagi1411), 2026-10-12. Second phone (ideally Android) still needed. |
| USB scanner and printer (#56/#57) | Pending | Pending |

Evidence: [browser results](verification/browser-results.json), [final precompiled release: 66/66](verification/browser-release-results.json), [form workflow](verification/workflow-results.json), [Postman summary](verification/postman-results.txt), [SMS results](verification/sms-results.json), [reproduction](verification/README.md), [user guide screenshots](USER_MANUAL.md). Final branch non-integration tests: 359/359; six frontend regression scripts pass; precompiled Release publish passes with the existing assembly-version warning. The real-phone result above is a tester-reported iPhone pass; a second phone and physical USB/printer checks remain pending. No demo-machine deployment, backup laptop or team rehearsal pass is claimed. T52 was subsequently verified and merged in #125; see [T52 verification](verification/T52_VERIFICATION.md). T61 remains outside this work.

Second pass: [all open PRs and reviewer comments](verification/PR_REVIEW_20261012.md), including a combined Windows PowerShell 5.1 package, current role labels/filters and the restored notification reason in pending #123.

Current outstanding acceptance requirements: [acceptance reconciliation](verification/ACCEPTANCE_RECONCILIATION.md).
