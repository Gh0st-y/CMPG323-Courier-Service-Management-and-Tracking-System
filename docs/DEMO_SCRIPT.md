# Nine-minute demo storyline (T60)

Status: script prepared; full rehearsal on demo hardware is pending. Do not close #60 until that run is recorded. This uses synthetic data and depends on a verified T57 environment.

## Proposed responsibilities

| Person | Responsibility |
| --- | --- |
| Gh0st-y | Narration and desktop driver; intake, collection, payment and supervisor screens |
| LwandiNxumalo | Phone driver; HTTPS camera scan and storage transitions |
| MrTMak | Local SMTP inbox and notification troubleshooting |
| Visagi1411 | Backup laptop/recording and timing |

Confirm these assignments before rehearsal; they are proposed operating roles, not a record of attendance. Use the same deployed URL everywhere. Log out before changing roles on the desktop: tabs in one browser share the same session.

## Before the audience arrives

- [ ] Agreed commit/tag, URL and synthetic database recorded in DEMO_REHEARSAL.md.
- [ ] Required fixes merged; especially seed #110, CSV #111, identifier encoding #112, configuration #113 and reports #114. Deployment preparation is #116.
- [ ] Mock mode OFF; local SMTP inbox reachable; SMS disabled unless its stub is explicitly being demonstrated.
- [ ] All five demo logins tested. Passwords are in the development README; do not project account passwords or configuration secrets.
- [ ] Printer, USB scanner and two phone cameras checked over trusted HTTPS.
- [ ] External internet outage test completed with a fresh browser; scanner library served locally.
- [ ] Backup laptop, recording and synthetic database backup ready; restore validated separately.
- [ ] Start from a known synthetic data set. Record dashboard baseline counts and today's date in SAST. Do not reset a database during the presentation.

Use one synthetic personal package with recipient `Demo Recipient`, identifier `DEMO-20261010`, email `demo.recipient@courier.test`, phone `0820000001`, sender `Demo Sender`, type `Box`, classification `Personal`, and an active seeded shelf. Choose a fresh demonstration identifier/date suffix for later rehearsals. The server assigns the F20 code: write it down after registration.

## Run order

| Time | Driver / account | Action | What the audience should see / narration |
| --- | --- | --- | --- |
| 0:00–0:40 | Gh0st-y | Introduce the problem | “This replaces F20's manual intake and communication with a staff-only package record, internal QR identifier and digital history.” |
| 0:40–1:20 | Desktop / intake.demo | Log in; show dashboard and allowed navigation | “Staff permissions follow their responsibilities. Recipients receive messages rather than accessing a portal.” |
| 1:20–2:30 | Desktop / intake.demo | Register the synthetic personal package; check displayed configured fee and save | Note returned F20 ID. “The server generates the identifier and calculates the fee; this package starts Registered and Unpaid.” |
| 2:30–3:10 | Desktop / intake.demo | Open label and print | Show QR identifier and readable print. “The encoded QR payload is only the internal identifier.” Keep the label for both scanners. |
| 3:10–4:25 | Phone / storage.demo | Camera-scan the printed label; change Registered to In Storage, then Ready for Collection | Show package/location and immediate feedback. “Both scanners call the same application endpoint. The server enforces the order.” |
| 4:25–5:00 | MrTMak / SMTP inbox | Show Ready for Collection message | Correct F20 ID and shelf, synthetic recipient, send result. “Messages are queued; staff do not wait on SMTP.” Do not claim actual SMS delivery. |
| 5:00–6:10 | Desktop / collection.demo | Log out intake; log in collection. USB-scan or look up the same F20 ID; verify identity checkbox and confirm collection | Show confirmation dialog and Collected success. “The transaction records time and the staff member who processed collection.” |
| 6:10–6:45 | SMTP inbox | Show collection confirmation | Same ID and collection time. The package now has both notification events. |
| 6:45–7:35 | Desktop / collection.demo | Open detail; mark Paid; reload | Show persisted Paid status, staff/time metadata and Registered → InStorage → ReadyForCollection → Collected timeline. “Payment status supports reconciliation; this system does not charge a payment terminal.” |
| 7:35–8:20 | Desktop / supervisor.demo | Log out collection; log in supervisor; open reports for today's received cohort | Show package volumes and fee/status summary. Explain that paid totals are current statuses of packages received in the range, not payments made during that range. |
| 8:20–9:00 | Desktop / supervisor.demo | Upload verified sample CSV; show result, then locate an imported record in search | “CSV uses the same registration rules. These counts come from saved server results.” Finish with scope: live integrations and real SMS gateway are future/conditional work. |

If CSV or reports are not accepted in the release, omit those segments explicitly from the release/demo scope and use the time to show package history and role restrictions. Do not demonstrate known broken screens as completed functionality.

## Failure responses

| Failure | Action and wording |
| --- | --- |
| Camera permission/certificate problem | Switch to tested USB input or manual F20 lookup. “The input method is unavailable on this device; the same lookup is available at the workstation.” Log the device failure afterwards. |
| Damaged print/scanner fails | Enter the displayed F20 ID manually. Avoid claiming that scan timing passed. |
| SMTP message delayed | Continue the lifecycle and show the notification state. “Sending is asynchronous; staff processing continues.” After the agreed wait, use the rehearsed recording and record the failure. Do not repeatedly resend or claim delivery. |
| Session expired / wrong role | Log in with the intended role, then resume. Never change permissions to bypass the demonstration. |
| Collection returns conflict | Reload once and inspect the actual status. If already Collected, show history; otherwise stop and explain the conflict. |
| CSV completion uncertain | Check package records before retrying; partial rows may already be saved. Skip the segment if uncertain. |
| Database / server unavailable | Stop live writes, switch to the verified backup environment or recording and state that it is fallback evidence. Do not restart/reset the database in front of the audience. |

A backup recording is a fallback, not a replacement for recording failed live acceptance checks.

## Rehearsal and completion

Run once from start to finish on the selected demo machine with the physical printer, USB scanner and phones. Use [DEMO_REHEARSAL.md](DEMO_REHEARSAL.md) to record durations, participants, failures and evidence links. Assign each defect to an issue and repeat affected steps after a fix. Save a complete recording and store its link with #60/#61. Close #60 only after the script and full rehearsal criteria both pass.
