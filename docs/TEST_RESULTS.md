# Verification results

Date: 2026-10-12. Environment: Windows workspace, .NET Framework 4.8, local Visual Studio MSBuild, Node. These are automated local checks; they do not establish a live end-to-end pass.

| Branch/change | Check | Observed result |
| --- | --- | --- |
| Audit viewer #119, d3f8345 | Non-integration .NET suite | 343 passed, 0 failed, 0 skipped. |
| Audit viewer #119 | Malicious suggestion markup/selection regression | Passed; markup is text, selection preserves the value, empty/no-match clears suggestions. |
| Audit viewer #119 | Release build with Razor view compilation | Passed; existing assembly-version warning remains. |
| POPIA #118, 75e17ef | Non-integration .NET suite excluding the known main HTTPS port test | 355 passed, 0 failed, 0 skipped among selected tests. HTTPS mismatch handled separately in the UI/verification fix branch. |
| POPIA #118 | Identifier regression and Release/Razor build | Passed; existing assembly-version and duplicate-using warnings recorded at that point. |
| UI/verification fix branch | Full non-integration .NET suite | 343 passed, 0 failed, 0 skipped, including HTTPS configuration test. |
| UI/verification fix branch | CSV import regression | Passed: multipart, saved/error rows, duplicate submit, auth/network/malformed response, mock mode and stale mock preference with mocks disabled. |
| UI/verification fix branch | User management regression | Passed: cancel/confirm, duplicate clicks, failed requests, role reset and self-protection. |
| UI/verification fix branch | Report links regression | Passed: app-relative detail path, encoded identifiers and safe recipient text. Charts intentionally absent from the Node DOM test. |
| UI/verification fix branch | Package identifier regression | Passed: normal identifiers, quotes, script markup and escape characters. |
| UI/verification fix branch | Release/Razor build | Passed; existing assembly-version warning remains. Duplicate using removed. |

SQL integration tests were not executed during these changes. No live HTTP/role acceptance pass, performance benchmark, camera/USB/printer check, screenshots, deployment or rehearsal was performed. Earlier PR evidence should be read separately; this record does not claim it was repeated.

## Live acceptance record

Use [ROLE_TEST_SCRIPT.md](ROLE_TEST_SCRIPT.md) and replace Pending only with observed evidence.

| Area | Result | Tester/environment/evidence |
| --- | --- | --- |
| Unauthenticated and cross-role HTTP access | Pending | Pending |
| Intake Clerk lifecycle and fee behavior | Pending | Pending |
| Storage Staff scan/status/notifications | Pending | Pending |
| Collection Staff identity/collection/payment | Pending | Pending |
| Supervisor import/reports/audit | Pending | Pending |
| System Admin users/storage locations | Pending | Pending |
| SQL integration/fresh database | Pending | Pending |
| SMTP/SMS/database failure acceptance | Pending | Pending |
| Chrome/Firefox, 1366x768 and mobile | Pending | Pending |
| 50,000-row performance targets (#53) | Pending | Pending |
| Two phones, USB scanner and printer (#56/#57) | Pending | Pending |

Do not close #51, #53, #56 or deployment/rehearsal issues from automated service tests alone.
