# Courier Service Management and Tracking System — Claude Code context

North-West University F20 courier intake/tracking system. Staff-only. Demo: Sun 4 Oct 2026.
Full plan: see `PROJECT_PLAN.md`. Full specs: see `/docs` (add Functional_Specifications.docx,
Technical_Specifications.docx, Business_Case.pdf, Feasibility_Study.pdf there if not already).

## Decisions (from T01 — do not relitigate these without a team discussion)
- Backend: C#, .NET Framework 4.8, ASP.NET MVC, hosted on IIS.
- Database: SQL Server Express / LocalDB. All data access goes through repository
  interfaces (see FR-11, IR-014) so the platform can change later without touching callers.
- Frontend: HTML5, CSS3, vanilla JavaScript (no SPA framework). Shared layout, nav, and a
  JS fetch wrapper live in the frontend scaffold (T07) — reuse them, don't reinvent per page.
- Auth: BCrypt-hashed passwords, session cookies (HttpOnly), 30-min inactivity expiry.
- Notifications: async queue + background worker. Email via SMTP (System.Net.Mail) is the
  guaranteed channel. SMS is a stub adapter with email fallback (gateway unconfirmed).
- Status state machine: Registered → In Storage → Ready for Collection → Collected.
  Invalid transitions must be rejected in the service layer, not just the UI (DR-009).
- No real personal data anywhere in dev/demo — synthetic seed data only (CON-006, DR-003).

## Architecture (three-tier, per Technical Specifications)
- `Web` — MVC controllers, Razor views, static JS/CSS. No business logic here.
- `Services` — business logic: package lifecycle, state machine, fee calc, notifications.
- `Data` — ADO.NET repositories, parameterized queries only, no raw SQL string concatenation.
- `Domain` — POCOs and repository interfaces shared across layers.
- Scanning devices never touch the database directly — everything goes through the backend
  over HTTP (FR-13). QR payload contains ONLY the internal package identifier (CON-008).

## Conventions
- Branch per issue: `feature/T<id>-short-name`, off `main`. Never push to `main` directly.
- PR description must include `Closes #<issue number>`.
- Every multi-step DB operation (status change + history + audit entry) is one transaction.
- Every significant action (login, package create, status change, collection, CSV import,
  config change) writes an append-only AuditLog entry (SR-04, NFR-019) — never update/delete
  existing audit rows.
- Roles enforced at the API/controller level, not just hidden in the UI (SR-02).
- Config (fees, SMTP, SMS on/off, notification templates) lives outside code (OR-01).

## Working an issue with Claude Code
1. Pull the issue for full acceptance criteria and spec refs: `gh issue view <number>`
2. Check its "Depends on" list in the issue body — confirm those are merged to `main` first.
3. Branch: `git checkout -b feature/T<id>-short-name`
4. Implement against the acceptance criteria checklist in the issue body.
5. Open a PR with `Closes #<number>`; keep it under ~400 changed lines where possible.

## Useful commands
- `gh issue list --milestone "M1 Core lifecycle"` — see what's left in the current milestone.
- `gh issue view <number> --json body -q .body` — just the acceptance criteria/spec refs.
