# End-to-end test script by role (T51)

Status: live API and browser paths executed on 2026-10-12; observations and remaining physical checks are recorded in [TEST_RESULTS.md](TEST_RESULTS.md). Reproduction commands are in [verification/README.md](verification/README.md). Use an isolated synthetic database. Never run these acceptance steps against live recipient data.

Before starting, deploy all intended PRs, create the database with the updated schema and seed, disable mock mode, configure a visible test SMTP inbox, and prepare a synthetic CSV from the site's template. Record the original configured fees and restore them after fee checks. Use separate browser sessions or log out between roles.

| Role | Seeded username |
| --- | --- |
| Intake Clerk | intake.demo |
| Storage Staff | storage.demo |
| Collection Staff | collection.demo |
| Supervisor | supervisor.demo |
| System Admin | admin.demo |

The seeded test password is documented in README; use a private credential for any non-demo environment.

## Unauthenticated access

- Open a staff page: redirected to login.
- Request protected package/users/report/audit APIs without a session: 401, no recipient data.
- Invalid credentials give a generic failure; successful login shows the expected role.

## Intake Clerk

- Register a Personal package with valid synthetic recipient fields. Record its F20 identifier; status is Registered and its saved fee matches configuration.
- Verify required fields, malformed email and missing classification/location stop invalid registration.
- With #113 deployed, change Fee.Personal to 12.50 through authorized SQL tools, reload and register another package. Both displayed/saved fee are R12.50; restore the original setting. The saved package keeps R12.50.
- Print a label; decode its QR and verify it contains only the identifier.
- Search and open the package; Generate Label is available. Storage/collection actions, user administration, reports and audit access are refused for this role.

## Storage Staff

- Search/open the recorded package; Generate Label is absent and direct label access is denied.
- Scan it using USB/Enter, then a phone over HTTPS; results identify the same package.
- Move Registered -> In Storage, assigning a valid location. Verify persisted status/location/history after reload.
- Attempt to skip Registered -> Collected directly through the API: rejected with no state change.
- Move In Storage -> Ready for Collection; check the queued notification and test inbox.
- Unknown code shows a friendly error. Registration, collection, import, reports/audit and user administration are refused.

## Collection Staff

- A Registered/In Storage package cannot be collected.
- Look up the Ready package, verify synthetic identification, cancel confirmation: no status/history change.
- Confirm collection with the required verifier: status Collected and timestamp/history persist; repeat collection is rejected.
- Record payment through the allowed operation and check status/time/actor after reload.
- Registration, storage transitions, user administration, import and reports/audit are refused.

## Supervisor

- Confirm intake, storage and collection operations on a second synthetic package.
- Import the sample CSV with at least one invalid row: successful rows persist; errors identify failed rows; duplicate clicks do not submit twice.
- Filter Reports by date/status/classification/payment; compare totals with [REPORT_VERIFICATION.md](REPORT_VERIFICATION.md). Outstanding identifiers open details.
- Open Audit log; apply individual/combined filters and traverse Previous/Next. Records and filters persist correctly. Malicious audit values appear as text, never executable markup.
- User management and admin-only storage-location mutations are refused.

## System Admin

- Verify supervisor operations and user management.
- Create a synthetic user; required/invalid fields fail safely, and a successful account can log in.
- Change another account's role: cancel leaves it unchanged; confirm persists it. Test both activation directions, cancellation and an API failure.
- Own-role changes and self-deactivation are blocked. Duplicate names/emails and weak passwords fail without creating an account.
- Create an allowed storage location; verify duplicate/invalid inputs, list visibility and audit entry.

## Failure and device checks

Run [NEGATIVE_TESTS.md](NEGATIVE_TESTS.md) for database outage, authorization, unknown QR, invalid transitions and notification failure. Record SMTP/SMS behavior, browser console errors, Chrome/Firefox at 1366x768, narrow/mobile screens, two real phones, USB scanner and printer results. These have not been verified by unit tests.

Each failed step must have a defect issue, owner and retest result. #51 stays open until all five live-role results are recorded; #56 and #57 require actual hardware/deployment evidence.
