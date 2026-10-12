# Outstanding acceptance reconciliation

Checked 2026-10-12 after PR #125 merged (`07f9acd`). This record distinguishes committed evidence from remaining acceptance work; it does not claim new device testing or teammate approval.

| Issue | Committed evidence | Remaining work |
| --- | --- | --- |
| #52 | [Corrected negative-test rerun](T52_VERIFICATION.md), merged #125; issue closed. | None identified in that rerun. |
| #53 | [50,000-row performance record](PERFORMANCE.md); page, scan and search meet the recorded targets after precompiled publishing. Status update and 500-row import timings recorded. | Obtain authoritative status-update/import targets or owner confirmation that timing-only acceptance applies; assess the recorded timings against that decision. |
| #55 | [Browser and UI evidence](../TEST_RESULTS.md), [second review](PR_REVIEW_20261012.md): Chrome/Firefox, 1366x768, role visibility, labels, filters, Scan, report links, timeline, cancellation, responsive reports/users and CSS cleanup. | FE2/FE3 sign-off and final visual acceptance. The issue's unchecked comment checklist is older than the merged evidence. |
| #56 | PR #127 records Hannu's iPhone camera Scan/Collection test over trusted LAN HTTPS. | Independent second real phone, workstation USB scanner, review and merge of #127. Keep #56 open. |
| #57 | Deployment packaging and precompiled publish preparation are merged. | Tagged demo-machine deployment, restore, demonstrable SMTP inbox, printer/USB checks, HTTPS camera check on that deployed environment, backup laptop and offline copy. Preparation is not deployment acceptance. |
| #60 | [Nine-minute storyline](../DEMO_SCRIPT.md) and [rehearsal form](../DEMO_REHEARSAL.md) are committed. | Confirm proposed drivers and complete one recorded rehearsal on the verified demo hardware. |
| #61 | Excluded by the user's instruction. | No tag, freeze, release recording or backup action performed here. |

The iPhone evidence is a named tester's report, not an independently rerun hardware test. No outstanding issue is closed solely because preparation or automated browser evidence exists. Numeric performance targets, hardware outcomes and team sign-off must not be invented.

Local housekeeping: main was fast-forwarded to the fetched GitHub main. Earlier acceptance/performance worktree diffs were archived under ignored `artifacts/worktree-cleanup-20261012/` before their tracked files were restored; databases and evidence artifacts were retained. Old merged/superseded branches were retained to avoid discarding history. No new application behaviour or API contract changes are introduced by this reconciliation.