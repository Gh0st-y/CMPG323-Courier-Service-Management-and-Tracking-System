# How to use this kit

1. Copy `CLAUDE.md` (from our earlier message) into the repo root, if not already there.
2. Copy everything in this `m0-kit/` folder into the repo:
   - `DECISIONS.md` → repo root
   - `API_CONTRACT.md` → `docs/API_CONTRACT.md`
   - `schema.sql` → `db/schema.sql`
   - `Web.config.sample.xml` → keep as a reference, don't rename over the real Web.config
   - `mock-fixtures.json` → `frontend-mocks/mock-fixtures.json` (or wherever your JS lives)
3. Commit these on a branch, e.g. `feature/T01-T04-T03-foundations`, push, open a PR,
   get a fast review from any teammate, merge to `main`.
4. Open Claude Code in the repo (`claude`) and paste the prompt below to do the rest
   (T02 solution skeleton, T05 data access, T07 frontend scaffold, T10 scanning spike).

---

## Prompt to paste into Claude Code

```
Read CLAUDE.md, DECISIONS.md, docs/API_CONTRACT.md, and db/schema.sql — they're
already committed to this repo and are the source of truth. Don't re-derive any
of these decisions; just build against them.

Work through these GitHub issues in order. For each one, find its real issue
number with `gh issue list --search "<T-code> in:title"`, read the full
acceptance criteria with `gh issue view <number>`, implement it, then stop
and show me a summary before opening the PR so I can sanity-check it.

1. T02 — Repo/solution skeleton. Create the .NET Framework 4.8 solution with
   projects: Web (ASP.NET MVC), Services, Data, Domain, and a Tests project.
   Wire up the connection string in Web.config using the sample I've placed
   at Web.config.sample.xml. Add a README with build/run instructions. Confirm
   .gitignore covers bin/obj/packages and any local config with secrets.

2. T05 — Data access foundation. In Domain, define repository interfaces for
   Package, User, StorageLocation, AuditLog and Notification based on the
   entities in db/schema.sql. In Data, implement them with ADO.NET —
   parameterized queries only, no string concatenation. Add a connection
   factory and a transaction helper usable across multiple repository calls
   in one commit (needed later for status-change + history + audit-log writes).
   Include one working example (e.g. GetPackageByF20Identifier) so teammates
   have a pattern to copy.

3. T07 — Frontend scaffold. Master layout with nav, an NWU-inspired colour
   scheme (no official branding assets — see DECISIONS.md), usable down to
   1366x768. Add a shared JS fetch wrapper that reads from
   frontend-mocks/mock-fixtures.json when a mock-mode flag is on, and hits the
   real API (per docs/API_CONTRACT.md) when it's off. Include toast/error
   display and a loading-state helper others can reuse.

4. T10 — Scanning spike. A page demonstrating: (a) a USB HID-mode scanner
   filling a focused input and triggering a lookup on Enter, and (b) a phone
   camera reading a QR via html5-qrcode and POSTing the decoded value to
   the same lookup endpoint. This needs HTTPS for the camera case — flag
   if the dev environment doesn't have it, don't skip the check silently.
   Write findings (what worked, what didn't) into docs/SCANNING_SPIKE.md.

After each task's PR is opened, wait for me before starting the next one.
```
