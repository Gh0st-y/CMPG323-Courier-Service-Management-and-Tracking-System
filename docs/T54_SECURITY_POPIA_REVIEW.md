# T54 — Security and POPIA review

**Review date:** 2026-10-09  
**Basis:** Source-code review of the uploaded `main (1).zip` snapshot.  
**Status:** **Review prepared; outstanding findings require remediation and verification.** This is not a certification of POPIA compliance.

## Evidence checklist

| Check | Evidence in repository | Assessment / follow-up |
|---|---|---|
| Parameterised SQL | `Data/DbCommandExtensions.cs` provides `AddParameter`; `Data/Repositories/PackageRegistrationRepository.cs` uses named parameters for recipient fields. | **Partially verified.** Spot checks support safe query parameters. Review all dynamic queries, including `PackageRepository.cs` filtering, before claiming complete coverage. |
| Password storage and login | `Services/Auth/AuthService.cs` calls `BCrypt.Net.BCrypt.Verify`; `Web/Controllers/AccountController.cs` clears the password from the view model after unsuccessful login. | **Implemented in inspected paths.** Verify password creation/hashing and rate limiting separately. |
| Server-side role enforcement | `Web/Infrastructure/RoleAuthorizeAttribute.cs` returns 401 for unauthenticated and 403 for unauthorized roles; user management controller and import controller have role attributes. | **Implemented in inspected paths.** Audit every sensitive endpoint and verify role tests. |
| Secrets and configuration | `.gitignore` ignores `Web.config.Local`, `*.local.config`, and `.env*`; `Web/Web.config` contains local placeholder SMTP values and an integrated-security LocalDB string. | **No live secret identified in these inspected settings.** Run repository-wide secret scanning, including Git history, before closure. |
| QR payload minimisation | `Services/Packages/QrCodeImage.cs` encodes the trimmed F20 identifier, not a recipient profile. | **Implemented in inspected path.** Verify QR image endpoints do not disclose sensitive information to unauthorized users. |
| Personal-data masking | `Services/Packages/PersonalData.cs` provides phone masking; `Web/Controllers/PackageActionsController.cs` and `Web/Controllers/PackagesController.cs` use it in some responses. | **Partially verified.** Check other endpoints, CSV import previews, exports, logs and UI for unmasked data. |
| Synthetic demo data | `db/seed.sql` exists and is described as demo data in documentation. | **Not verified.** Manually confirm every seed record is synthetic; never copy real recipient details into screenshots or presentations. |
| Output encoding | Razor normally HTML-encodes `@` expressions, but `Web/Views/Packages/Label.cshtml` and `Web/Views/Packages/Detail.cshtml` embed `f20Identifier` using `@Html.Raw(f20Identifier)` inside JavaScript strings. | **Finding — requires review/fix.** Raw insertion into a JavaScript string can be unsafe if the value is not tightly constrained. Use context-appropriate JavaScript string serialization and verify the identifier validation at the controller boundary. |
| CSRF | `Web/Controllers/AccountController.cs` has `[ValidateAntiForgeryToken]` on inspected POST actions. | **Partially verified.** Review all state-changing cookie-authenticated MVC and API routes for CSRF protection. |
| Privacy governance | Technical documentation covers some security considerations. | **Not verified.** Team must define retention/deletion, lawful purpose, access requests, incident handling and appropriate notices; code alone cannot establish POPIA compliance. |

## Recommended actions before marking T54 complete

1. **Address the JavaScript output-encoding finding.** Replace raw interpolation of the package identifier in both Razor views with safe JSON/JavaScript-string encoding; add regression coverage for quotes, backslashes and script-breaking characters.
2. Review all controllers/actions for server-side authorization and CSRF defenses, especially state-changing requests.
3. Review dynamic SQL query construction to ensure every user-provided value is passed as a parameter.
4. Scan tracked files **and commit history** for passwords, tokens, keys and recipient information. Revoke any exposed secrets; deleting a file alone does not revoke them.
5. Verify that QR payloads contain only the package identifier and that QR/scan endpoints enforce appropriate access.
6. Use synthetic recipients for demos and screenshots; verify logging and exports avoid unnecessary personal information.
7. Record the team's data-retention, access-control, and breach-response decisions with the project manager.
8. Run the application and negative tests locally; record reviewer, date, environment and test results.

## Verification record (fill in after testing)

| Test | Result | Reviewer/date |
|---|---|---|
| SQL injection and dynamic filter tests | Not run | — |
| Unauthorized and cross-role API requests | Not run | — |
| CSRF checks on state-changing endpoints | Not run | — |
| QR data and endpoint access | Not run | — |
| XSS regression on package identifier | Not run | — |
| Secret scan (files and history) | Not run | — |
| Synthetic demo data review | Not run | — |

## Scope and handoff

This document is a **source-review contribution**, not a completed security audit or legal determination. T54 should remain open until the team resolves or accepts documented findings and records test evidence. Use a separate branch such as `feature/T54-security-review` and open a draft PR for review. Do not use `Closes #54` until the acceptance criteria are satisfied.
