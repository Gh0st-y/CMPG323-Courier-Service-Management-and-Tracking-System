# Security and POPIA Checklist (T54)

**Issue:** #54 â€” Security and POPIA checklist
**Scope:** SR-03, NRF-010, NRF-018, CON-008, CON-006
**Purpose:** Record security review evidence and outstanding verification for the courier service demo.

## 1. Parameterised SQL queries

**Status: Reviewed â€” evidence found**

- `Data/DbCommandExtensions.cs` provides a shared helper for adding SQL parameters.
- `Data/Repositories/PackageRepository.cs` uses parameters for package lookups, inserts, updates and search filters.
- Dynamic search SQL is assembled from fixed SQL fragments; user-supplied search values are passed as parameters.
- `EscapeLike` handles SQL `LIKE` wildcard characters in search input.

**Evidence:** `Data/DbCommandExtensions.cs`; `Data/Repositories/PackageRepository.cs`.

## 2. Output encoding and JavaScript values

**Status: Fix applied â€” testing pending**

- CSV import results use `escapeHtml()` before inserting recipient values and error messages into HTML table cells.
- Package detail and label views now serialize `f20Identifier` using `Newtonsoft.Json.JsonConvert.SerializeObject` before embedding it as a JavaScript value.
- The package views retain Razor's normal HTML encoding when displaying the identifier in page text.

**Evidence:** `Web/Scripts/app/csv-import.js`; `Web/Views/Packages/Detail.cshtml`; `Web/Views/Packages/Label.cshtml`.

## 3. Personal data in logs and error responses

**Status: Pending full verification**

- Review application logs, notification logs, exception handling and API error responses to ensure recipient names, identity numbers, email addresses, phone numbers and other personal data are not unnecessarily exposed.
- Error responses should not disclose stack traces, SQL statements, credentials or sensitive personal data.
- Audit-log details should not contain personal data.

**Relevant references:** `Domain/Entities/AuditLogEntry.cs`; `Services/Notifications/NotificationMessage.cs`; `docs/API_CONTRACT.md`.

## 4. QR payload

**Status: Reviewed â€” documented design**

- The QR code is intended to contain only the package `F20Identifier`.
- The QR endpoint retrieves the package and generates a PNG using the package identifier.

**Evidence:** `Web/Controllers/PackagesController.Registration.cs`; `Services/Packages/QrCodeImage.cs`; `docs/API_CONTRACT.md`.

## 5. Secrets and configuration

**Status: Pending final repository scan**

- Verify that no real passwords, API keys, tokens, certificates or private keys are committed.
- Confirm that environment-specific configuration and real credentials are excluded from version control.
- Do not commit local `Web/Web.config` changes as part of T54.

**Evidence to collect:** tracked-file secret checks and repository status review.

## 6. Synthetic demo data

**Status: Pending final verification**

- Confirm that database seed data uses synthetic identities and contact details.
- Do not commit real recipient, staff or student personal information.
- Confirm that demo data uses clearly fictional contact details where applicable.

**Relevant references:** `DECISIONS.md`; `docs/RELEASE_CHECKLIST.md`; `db/schema.sql`.

## 7. Validation and sign-off

- [ ] Review completed for SQL parameterisation.
- [ ] JavaScript output encoding fix tested.
- [ ] Logs and error responses reviewed.
- [ ] QR payload verified to contain only the package identifier.
- [ ] Repository scanned for secrets.
- [ ] Demo data confirmed synthetic.
- [ ] Relevant tests/build completed successfully.
- [ ] Changes reviewed by a teammate before merge.

**Note:** This checklist records review evidence, not formal legal certification or a guarantee of POPIA compliance. Items marked pending must be verified before final sign-off.
