# Release checklist: code freeze, v1.0-demo tag, backup and recording (T61)

Run this once T60 (demo script and rehearsal) is done and every P0 fix for the demo is merged. Work through it in order and tick each box. The whole thing takes about half a day, most of it the recording.

Release owner: ________  Freeze date and time: ________

## 1. Before the freeze

- [ ] T60 demo script is final, and the rehearsal found no P0 problems that are still open.
- [ ] Every PR the demo needs is merged into `main`. Check the open pull requests on GitHub: anything still open either gets merged now or is left out of v1.0-demo on purpose.
- [ ] The team has been told the freeze time in the group chat: after it, nothing is merged into `main` unless the release owner agrees it is a demo-breaking fix.

## 2. Freeze `main`

- [ ] Ask the repository owner (Gh0st-y) to lock `main`: GitHub > Settings > Branches > the rule for `main` > tick **Lock branch** > Save. This makes `main` read-only, so nothing can be merged by accident. Only a repository admin can do this.
- [ ] Post in the group chat that `main` is frozen, with the time.

If a demo-breaking bug turns up after the freeze, the owner unlocks `main`, the fix goes through a normal PR with a review, `main` is locked again, and the release is tagged `v1.0-demo.1` instead (section 5).

## 3. Check the frozen `main`

Do this on a fresh copy, so nothing from your own branches or local changes sneaks in.

- [ ] In Visual Studio, switch to `main` and pull. The status bar shows `0 ↓ 0 ↑` and Git Changes is empty.
- [ ] Rebuild the solution (Build > Rebuild Solution): no errors.
- [ ] Run all tests in Test Explorer: everything passes. The integration tests need LocalDB; "inconclusive" means LocalDB wasn't reachable, so fix that and run them again.
- [ ] Run `docs/NEGATIVE_TESTS.md` sections 1 to 7 and the T51 end-to-end script. Everything passes, or any failure is written in the release notes as a known issue.
- [ ] No secrets or certificates in the repository. In PowerShell, in the repo folder:

  ```powershell
  git ls-files | Select-String -Pattern '\.(pfx|p12|pem|key|bak|user)$'
  git grep -n -i "password=" -- "*.config" "*.json" "*.cs"
  ```

  Both should print nothing. The demo logins in README.md and `db/seed.sql` (`Demo@2026!`) are expected and fine: they only exist in seeded test databases.
- [ ] Write down the commit you are releasing: on GitHub, the short hash shown at the top of `main` (for example `7a6abcc`). Commit: ________

## 4. Clean database for the demo

The integration tests and everyone's manual testing leave test users, `TEST-` packages and audit entries behind. Start the demo from a clean, known database.

Run these on the machine and database the demo will use (agree with whoever owns T57, the demo environment). In Visual Studio: View > SQL Server Object Explorer > right-click **master** > New Query.

- [ ] Drop the old database (this deletes it, so be sure you're on the right machine):

  ```sql
  ALTER DATABASE CourierService SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE CourierService;
  ```

- [ ] Open and run `db/schema.sql`, then `db/seed.sql` (it creates the database and the 200 seed packages again).
- [ ] Only if the demo shows search on a large data set: run `db/seed-50k.sql` as well.
- [ ] Log in as each demo user from README.md once to check they work.

## 5. Tag the release

The easiest way is a GitHub release, which creates the tag for you:

- [ ] On GitHub: Releases > **Draft a new release**.
- [ ] Choose a tag: type `v1.0-demo` and pick "Create new tag on publish". Target: `main`.
- [ ] Release title: `v1.0-demo`. In the description, paste the release notes (section 8).
- [ ] Click **Publish release**.

Or from PowerShell in the repo folder, on an up-to-date `main`:

```powershell
git checkout main
git pull
git tag -a v1.0-demo -m "Demo release v1.0 (CMPG323)"
git push origin v1.0-demo
```

- [ ] Check the tag builds on its own: clone it into a new folder, open the solution, rebuild and run the tests.

  ```powershell
  git clone --branch v1.0-demo https://github.com/Gh0st-y/CMPG323-Courier-Service-Management-and-Tracking-System.git courier-v1.0-demo-check
  ```

## 6. Database backup

On the demo machine, after section 4 (so the backup is the clean demo database):

- [ ] Create a folder for it first, for example `C:\courier-backup`.
- [ ] In a query window on **master**:

  ```sql
  BACKUP DATABASE CourierService
      TO DISK = N'C:\courier-backup\CourierService-v1.0-demo.bak'
      WITH INIT, CHECKSUM;

  RESTORE VERIFYONLY
      FROM DISK = N'C:\courier-backup\CourierService-v1.0-demo.bak'
      WITH CHECKSUM;
  ```

  The second command should end with "The backup set on file 1 is valid."
- [ ] If the demo machine fails, this restores it:

  ```sql
  RESTORE DATABASE CourierService
      FROM DISK = N'C:\courier-backup\CourierService-v1.0-demo.bak'
      WITH REPLACE;
  ```

The backup only holds the synthetic seed data (fake names, `.test` email addresses, fake phone numbers), so it contains no real personal data (POPIA).

## 7. Backup screen recording

A recording of the full demo, in case something fails on the day.

- [ ] Follow the T60 demo script from start to finish, on the clean database, with the demo logins.
- [ ] Desktop: record with OBS Studio (free) or the Windows Snipping Tool's record option. 1080p, with a voice-over if the script has one.
- [ ] Phone scanning: record it on the phone itself (iPhone: Control Center > Screen Recording) and keep it as a separate clip.
- [ ] Watch the recording once all the way through: sound works, text is readable, nothing private on screen (no other browser tabs, email or notifications).
- [ ] Name the files `courier-demo-v1.0.mp4` and `courier-demo-v1.0-phone-scan.mp4`.

## 8. Release notes

Paste this into the GitHub release (section 5) and fill in the blanks.

```
v1.0-demo, frozen on ________ at commit ________

Included: login and roles, package registration, search and detail, scan lookup (USB scanner and phone camera over HTTPS), status changes and collection, CSV import, audit log, user management API, Service Unavailable handling. (Adjust to what was actually merged.)

Known limitations:
- Email and SMS notifications: ________ (state whether T25/T28 made it in)
- A deactivated user's open session stays valid until it times out (30 minutes).
- ________

Database backup and recording: OneDrive > ________
```

## 9. Store on OneDrive

- [ ] Create a folder `CMPG323 Courier Demo/v1.0-demo` on the team's OneDrive.
- [ ] Upload `CourierService-v1.0-demo.bak`, both recordings, and a copy of the release notes.
- [ ] **Do not** upload certificates or keys (`.p12`, `rootCA-key.pem`) or any `Web.config` with real passwords.
- [ ] Share the folder with the team and put the link in the release notes and the group chat.

## 10. Done

- [ ] Every box above is ticked.
- [ ] Close issue #61 with a comment linking the release and the OneDrive folder.
- [ ] `main` stays locked until after the demo.
