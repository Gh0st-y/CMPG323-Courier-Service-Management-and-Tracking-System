# T54 - Security and POPIA review

Issue: #54. Updated: 2026-10-12. This is a source review and verification record, not a certification of POPIA compliance. Keep #54 open until the remaining acceptance checks have evidence.

## Implemented safeguards and evidence

| Check | Evidence | Status |
| --- | --- | --- |
| Parameterised SQL | Repositories use `Data/DbCommandExtensions.cs` for parameters; package search assembles fixed SQL fragments and parameterises input. | Inspected paths use parameters; repository-wide dynamic SQL review remains pending. |
| Password storage | `AuthService` verifies BCrypt hashes; the login controller clears failed passwords from its view model. | Implemented; rate-limit review remains pending. |
| Server-side roles | `RoleAuthorizeAttribute` uses the session login and returns 401/403; role services have automated tests. | Implemented; complete live endpoint/role matrix remains pending. |
| Package view output encoding | #112 validates identifiers on the server and reads the identifier from HTML-encoded text using `textContent` in Detail and Label. This branch retains main's versions of both views. | Node regression covers quotes, script markup and escape characters. |
| QR contents | `QrCodeImage` encodes only the F20 identifier. | Automated QR tests exist; physical scanner/phone verification remains pending. |
| Personal-data masking | `PersonalData.MaskPhone` exposes at most three characters; `MaskEmail` masks the local part. Package notification responses choose masking by channel. | Automated phone/email cases cover blank and malformed values. |
| Error diagnostics | `ExceptionDiagnostics.Format` includes exception type and stack trace, excluding messages and inner exception data. Controllers, database-outage filter and notification worker use it. User responses remain generic. | Regression checks preserve the failure location without leaking sensitive exception messages. |
| Configuration secrets | Local appSettings and environment files are ignored; tracked SMTP settings are placeholders. | Full tracked-file and Git-history secret review remains pending. |
| Synthetic demo data | Demo seed scripts are present. | Team must confirm data and screenshots contain no real recipient information. |
| CSRF | Inspected Account POST actions use anti-forgery validation. | All state-changing session-authenticated routes still need review. |
| Privacy governance | Retention, access requests, incident handling and notices require team decisions. | Pending; cannot be established by code tests alone. |

## Verification

Run from the repository root:

```powershell
dotnet test Tests/CourierService.Tests.csproj --no-restore --filter 'FullyQualifiedName!~Integration'
node Tests/Frontend/package-page-identifiers.test.js
```

Record test totals, skipped tests and environment in the PR. SQL integration checks and manual HTTP tests need an isolated synthetic database and a running site. Skipped SQL checks do not establish that database setup works.

## Remaining acceptance work

- Run SQL injection/dynamic-filter cases and the complete unauthorized/cross-role HTTP matrix.
- Review CSRF on every state-changing endpoint and record results.
- Scan tracked files and Git history for secrets; revoke any exposed credential.
- Review notification logs, exports, previews and screenshots for unnecessary personal data.
- Verify QR access, synthetic recipients, retention and incident-handling decisions.
- Run the negative tests and record reviewer, date, environment and observed results.

This document consolidates the previous `SECURITY_POPIA_CHECKLIST.md` and `T54_SECURITY_POPIA_REVIEW.md`. It preserves open findings instead of treating an implementation change as completed operational verification.
