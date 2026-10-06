# Negative and robustness tests (T52)

These checks try to break the system on purpose: wrong roles, moves the status rules don't allow, codes that don't exist, bad input and a database that has gone away. Every request below is meant to be **refused**, so running the script changes no package data. The only things it leaves behind are login entries in the audit log.

Spec references: NFR-022, NFR-023, NRF-016, NRF-011, IR-006, OR-04.

## How to run

1. Load `db/schema.sql` and `db/seed.sql` (the demo users all use the password `Demo@2026!`).
2. Press F5 and open the browser console (F12) on `https://localhost:44301/`.
3. Paste each step on its own, in order, and compare the status code and `code` with the expected result.

Steps 2d, 6a and 6b use `/api/users`, which comes with T43. Before T43 is merged they return a 404 page instead of 403; skip them until then.

The script uses four seeded packages. In `seed.sql` the status depends on the package number: numbers divisible by 4 are Registered, then the pattern repeats as In Storage, Ready for Collection and Collected.

| Package | Expected status |
|---|---|
| F20-0004 | Registered |
| F20-0001 | InStorage |
| F20-0002 | ReadyForCollection |
| F20-0003 | Collected |

If your database has been used for other testing, check them first with step 3a. If one has moved on, use the next package with the same remainder (for example F20-0008 instead of F20-0004).

## Results

| Step | Check | Expected | Result |
|---|---|---|---|
| 1a to 1e | Logged out | 401 NotAuthenticated on all five | Pass |
| 2a to 2c | IntakeClerk on other roles' actions | 403 Forbidden | Pass |
| 2d | IntakeClerk on user management | 403 Forbidden | Skipped, needs T43 |
| 3a | Scan the Registered package | 200, status Registered | Pass |
| 3b | Skip a step (Registered to ReadyForCollection) | 409 InvalidTransition | Pass |
| 3c | Go backwards (Collected to InStorage) | 409 InvalidTransition | Pass |
| 3d | Collect through the status endpoint | 400 ValidationError | Pass |
| 3e | Unknown status name | 400 ValidationError | Pass |
| 3f | Body that isn't JSON | 400 ValidationError | Pass |
| 3g | Package that doesn't exist | 404 NotFound | Pass |
| 3h | Malformed package code | 404 NotFound | Pass |
| 3i | StorageStaff collecting | 403 Forbidden | Pass |
| 4a, 4b | Collect from InStorage, collect twice | 409 InvalidTransition on both | Pass |
| 4c | CollectionStaff changing status | 403 Forbidden | Pass |
| 5a | Unknown QR code (API) | 404 NotFound, friendly message | Pass |
| 5b | Unknown QR code (scan screen) | Friendly "not found" message, no error page | Pass |
| 6a, 6b | Supervisor on admin-only endpoints | 403 Forbidden on both | Skipped, needs T43 |
| 7a to 7d | Database offline | 503 ServiceUnavailable, friendly page and toast | Pass |
| 7e | Database back online | 200 again | Pass |
| 8 | Email/SMS failure | Pending T25/T28, see section 8 | Not run |

Tested by: Hannu (Visagi1411)  Date: 2026-10-06
---

## 1. Logged out

Log out first, so nothing is left over from earlier testing.

```js
fetch("/api/auth/logout", { method: "POST" }).then(r => console.log("logout", r.status));
```

1a. Scan. Expected: 401 NotAuthenticated, and no package data in the response.
```js
fetch("/api/packages/scan/F20-0001").then(r => r.text().then(t => console.log(r.status, t)));
```

1b. Status change. Expected: 401.
```js
fetch("/api/packages/F20-0004/status", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ newStatus: "InStorage" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

1c. Collect. Expected: 401.
```js
fetch("/api/packages/F20-0002/collect", { method: "POST" }).then(r => r.text().then(t => console.log(r.status, t)));
```

1d. Search. Expected: 401.
```js
fetch("/api/packages?query=nkosi").then(r => r.text().then(t => console.log(r.status, t)));
```

1e. Audit log. Expected: 401.
```js
fetch("/api/audit-log").then(r => r.text().then(t => console.log(r.status, t)));
```

## 2. Wrong role: IntakeClerk

```js
fetch("/api/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ username: "intake.demo", password: "Demo@2026!" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

2a. Status change (StorageStaff, Supervisor, SystemAdmin only). Expected: 403 Forbidden.
```js
fetch("/api/packages/F20-0004/status", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ newStatus: "InStorage" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

2b. Collect (CollectionStaff, Supervisor, SystemAdmin only). Expected: 403.
```js
fetch("/api/packages/F20-0002/collect", { method: "POST" }).then(r => r.text().then(t => console.log(r.status, t)));
```

2c. Audit log (Supervisor, SystemAdmin only). Expected: 403.
```js
fetch("/api/audit-log").then(r => r.text().then(t => console.log(r.status, t)));
```

2d. User management (SystemAdmin only). Expected: 403.
```js
fetch("/api/users").then(r => r.text().then(t => console.log(r.status, t)));
```

## 3. Status rules sent straight to the API: StorageStaff

The screens only offer allowed moves. These go around the screens and call the API directly, which must still refuse them (DR-009).

```js
fetch("/api/auth/logout", { method: "POST" }).then(r => console.log("logout", r.status));
```
```js
fetch("/api/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ username: "storage.demo", password: "Demo@2026!" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

3a. Check the Registered package. Expected: 200 with "status":"Registered".
```js
fetch("/api/packages/scan/F20-0004").then(r => r.text().then(t => console.log(r.status, t)));
```

3b. Skip a step, Registered to ReadyForCollection. Expected: 409 InvalidTransition.
```js
fetch("/api/packages/F20-0004/status", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ newStatus: "ReadyForCollection" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

3c. Go backwards, Collected to InStorage. Expected: 409 InvalidTransition.
```js
fetch("/api/packages/F20-0003/status", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ newStatus: "InStorage" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

3d. Mark as Collected through the status endpoint instead of /collect. Expected: 400 ValidationError (collecting has its own endpoint and roles).
```js
fetch("/api/packages/F20-0002/status", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ newStatus: "Collected" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

3e. A status that doesn't exist. Expected: 400 ValidationError listing the real statuses.
```js
fetch("/api/packages/F20-0004/status", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ newStatus: "Lost" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

3f. A body that isn't JSON. Expected: 400 ValidationError, not a 500.
```js
fetch("/api/packages/F20-0004/status", { method: "POST", headers: { "Content-Type": "application/json" }, body: "this is not json" }).then(r => r.text().then(t => console.log(r.status, t)));
```

3g. A package that doesn't exist. Expected: 404 NotFound.
```js
fetch("/api/packages/F20-9999/status", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ newStatus: "InStorage" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

3h. A malformed code (longer than 30 characters). Expected: 404 NotFound, the same answer as an unknown code.
```js
fetch("/api/packages/F20-0004-THIS-CODE-IS-FAR-TOO-LONG/status", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ newStatus: "InStorage" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

3i. StorageStaff trying to collect. Expected: 403 Forbidden.
```js
fetch("/api/packages/F20-0002/collect", { method: "POST" }).then(r => r.text().then(t => console.log(r.status, t)));
```

## 4. Collection rules: CollectionStaff

```js
fetch("/api/auth/logout", { method: "POST" }).then(r => console.log("logout", r.status));
```
```js
fetch("/api/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ username: "collection.demo", password: "Demo@2026!" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

4a. Collect a package that is still In Storage. Expected: 409 InvalidTransition (FR-07, only from Ready for Collection).
```js
fetch("/api/packages/F20-0001/collect", { method: "POST" }).then(r => r.text().then(t => console.log(r.status, t)));
```

4b. Collect a package that was already collected. Expected: 409 InvalidTransition.
```js
fetch("/api/packages/F20-0003/collect", { method: "POST" }).then(r => r.text().then(t => console.log(r.status, t)));
```

4c. CollectionStaff changing a status. Expected: 403 Forbidden.
```js
fetch("/api/packages/F20-0001/status", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ newStatus: "ReadyForCollection" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

## 5. Unknown QR code (IR-006)

5a. Still logged in as collection.demo. Expected: 404 with code NotFound and the message "No package was found for that code." No stack trace or other detail.
```js
fetch("/api/packages/scan/F20-9999").then(r => r.text().then(t => console.log(r.status, t)));
```

5b. Open the Scan page, type `F20-9999` into the scan box and press Enter. Expected: a friendly "not found" message on the screen, not an error page.

## 6. Admin-only endpoints: Supervisor

```js
fetch("/api/auth/logout", { method: "POST" }).then(r => console.log("logout", r.status));
```
```js
fetch("/api/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ username: "supervisor.demo", password: "Demo@2026!" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

6a. List users. Expected: 403.
```js
fetch("/api/users").then(r => r.text().then(t => console.log(r.status, t)));
```

6b. Create a user. Expected: 403, and no user is created.
```js
fetch("/api/users", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ username: "should.not.exist", email: "should.not.exist@courier.test", password: "Welcome123", role: "SystemAdmin" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

## 7. Database unavailable (NFR-022, OR-04)

**Only do this on your own local database, never on the demo machine during a demo.** It takes the database offline and then brings it back.

Stay logged in as supervisor.demo. Your session lives in the web server's memory, so it survives the database going offline.

7a. In Visual Studio, open View > SQL Server Object Explorer, expand `(localdb)\MSSQLLocalDB` > Databases, right-click **master** (not CourierService) and choose New Query. Run:

```sql
ALTER DATABASE CourierService SET OFFLINE WITH ROLLBACK IMMEDIATE;
```

7b. Scan in the console. Expected: 503 with code ServiceUnavailable and the message "The system can't reach its database right now. Please try again in a few minutes." No SQL, server names or stack trace. If the very first call after going offline gives a different error, run it once more: the first call can hit a connection that was already open.
```js
fetch("/api/packages/scan/F20-0001").then(r => r.text().then(t => console.log(r.status, t, "Retry-After:", r.headers.get("Retry-After"))));
```

7c. Log out and try to log in. Expected: the logout is 204, then the login gives 503 ServiceUnavailable.
```js
fetch("/api/auth/logout", { method: "POST" }).then(r => console.log("logout", r.status));
```
```js
fetch("/api/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ username: "supervisor.demo", password: "Demo@2026!" }) }).then(r => r.text().then(t => console.log(r.status, t)));
```

7d. Open the Scan page and scan `F20-0001`. Expected: a toast with the "can't reach its database" message, and the page keeps working. Visual Studio's Output window shows the full error starting with "Database unavailable:" (that is the log, OR-04).

7e. Bring the database back in the same query window:

```sql
ALTER DATABASE CourierService SET ONLINE;
```

Log in again as supervisor.demo and repeat 7b. Expected: 200 with the package.

## 8. Email and SMS failures (NFR-023)

Not testable yet. The notification sender (T25, T28) isn't on main. The design already keeps a failed email or SMS from blocking staff: a status change only **queues** the notification inside its own transaction (`IPackageStatusChangeListener`), and a background worker sends it later and marks failures (`INotificationRepository.MarkFailed`). When T25/T28 land, add a step here that points SMTP at a closed port, changes a status, and checks that the change succeeds and the queue row is marked failed.
