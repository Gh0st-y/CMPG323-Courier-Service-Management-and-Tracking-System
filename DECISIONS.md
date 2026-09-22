# Project Decisions (T01)

Resolves the conflicts between the Business Case, Feasibility Study, Functional
Specifications and Technical Specifications. Record any future decision here too,
with a date and who made the call.

| # | Decision | Chosen | Rejected alternative | Reason |
|---|---|---|---|---|
| 1 | Database platform | SQL Server Express / LocalDB | MySQL / Oracle (Tech Spec stack matrix) | Matches FR-12 and CON-004; free tier; native to Visual Studio; no separate server install for teammates |
| 2 | Backend framework | .NET Framework 4.8, ASP.NET MVC | ASP.NET Core / Blazor (FR-12 mention) | Matches the rest of the Technical Specifications (IIS, System.Net.Mail, ADO.NET); team already scoped around it |
| 3 | Concurrent users target | 5 concurrent users (NFR-006) | 20 (PR-02) | Stricter, realistic figure kept; design isn't rearchitected to hit 20 for a demo |
| 4 | Page/form load target | 2 seconds (PR-01) | 3 seconds (NFR-001) | Stricter figure used everywhere |
| 5 | QR scan response target | 1.5 seconds (PR-01) | 2 seconds (NFR-002) | Stricter figure used everywhere |
| 6 | SMS gateway | Adapter interface + stub provider, email as guaranteed channel | Committing to the NWU SMS gateway | Gateway availability unconfirmed (ISS-01); IR-002 requires a fallback path with no code change |
| 7 | "Out for Delivery" status | Excluded from demo scope | Included (mentioned in FR-04 example list) | Not required by any FR acceptance criteria; state machine stays to 4 states: Registered → In Storage → Ready for Collection → Collected |
| 8 | Personal data in dev/demo | Synthetic data only, no real recipients | Using real staff/student data | CON-006, DR-003 — no live personal data until NWU Information Governance sign-off |
| 9 | Data retention/anonymisation job | Documented as an open item, not built | Building a best-guess retention job | Retention period unconfirmed by NWU (CON-009, ISS-02); would be built on a guess |
| 10 | Branch protection on main | 1 required approval, no force-push | No protection during M0 | Approval requirement is temporarily relaxed only for the M0 foundation PRs (see below), then re-enabled |

## M0 branch protection note
For T01–T10, PRs may be merged with a fast review from any teammate (even a quick
read) rather than a full review, to avoid blocking the team's start date. Full PR
review process (1 approval, checklist in pull_request_template.md) is mandatory
from T11 onward. Re-enable strict protection once M0 is merged.

## Still open (not decided yet — do not assume)
- Real SMS gateway integration (ISS-01) — pending sponsor/IT confirmation.
- Data retention period (ISS-02) — pending sponsor/IT confirmation.
- NWU branding assets (CON-010) — interface is NWU-colour-inspired only until sign-off.
- Future live API integration scope (IR-015) — out of scope this release.
