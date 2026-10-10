# API Contract (T03)

Base path: `/api`. All responses are JSON. All endpoints except `/api/auth/login` require an
authenticated session (cookie). `Roles` lists who may call it — enforced server-side (SR-02),
not just hidden in the UI.

## Standard error shape
```json
{ "error": { "code": "InvalidTransition", "message": "Package cannot move to Collected from Registered." } }
```
No stack traces, no personal data, no SQL text in any error message (SR-03, OR-04).

Standard status codes: `400` validation, `401` not authenticated, `403` wrong role,
`404` not found, `409` conflict (e.g. duplicate identifier, invalid transition), `500` unexpected.

---

## Auth (FR-01, SR-01)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| POST | `/api/auth/login` | anyone | `{username, password}` → `{userId, role, displayName}` sets session cookie |
| POST | `/api/auth/logout` | any authenticated | — → `204` |

## Packages — registration (FR-03, FR-15, FR-17)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| POST | `/api/packages` | IntakeClerk, Supervisor, SystemAdmin | `{recipient:{fullName,identifierNo,email,phone,department}, senderName, packageType, classification, storageLocationId, notes}` → `{packageId, f20Identifier, fee, status:"Registered"}` |
| GET | `/api/packages/{f20Identifier}/qr` | any authenticated | → PNG image (QR payload = f20Identifier only, CON-008) |

## Packages — scan / status / collection (FR-04, FR-07, FR-13)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| GET | `/api/packages/scan/{f20Identifier}` | any authenticated | → full package record, or `404` friendly error for unknown/malformed ID (IR-006) |
| POST | `/api/packages/{f20Identifier}/status` | StorageStaff, Supervisor, SystemAdmin | `{newStatus, storageLocationId?}` → updated record, or `409` if transition invalid (DR-009) |
| POST | `/api/packages/{f20Identifier}/collect` | CollectionStaff, Supervisor, SystemAdmin | `{verifiedByUserId}` → `{status:"Collected", collectedAtUtc}`, only allowed from `ReadyForCollection` (FR-07) |
| PATCH | `/api/packages/{f20Identifier}/payment` | IntakeClerk, CollectionStaff, Supervisor, SystemAdmin | `{paymentStatus}` (Paid/Unpaid/Exempt) → `{packageId, f20Identifier, paymentStatus, paymentStatusUpdatedAtUtc, paymentStatusUpdatedBy, changed}` (FR-15, FR-17) |

## Packages — search / detail (FR-05)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| GET | `/api/packages?query=&status=&dateFrom=&dateTo=&page=&pageSize=` | any authenticated | → `{items:[...], totalCount}`, ≤3s (NFR-003) |
| GET | `/api/packages/{f20Identifier}/detail` | any authenticated | → `{package, statusHistory:[...], notifications:[...]}` (DR-012) |

## Storage locations (FR-03, FR-04)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| GET | `/api/storage-locations` | any authenticated | → `[{storageLocationId, code, description}]` |
| POST | `/api/storage-locations` | SystemAdmin | `{code, description}` → created location |

## Notifications (FR-06, FR-09, FR-10, IR-001..IR-004)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| POST | `/api/packages/{f20Identifier}/notifications/resend` | Supervisor, SystemAdmin | `{channel}` → re-queues the last failed notification |

Notes for resend (T49):
- `channel` is `Email` or `SMS` (any case); left out, it means `Email`. Anything else is 400 `ValidationError`.
- It looks at the package's **latest** notification on that channel and re-queues it only if it failed. The worker sends it within about 5 s with a fresh 3 attempts, and each attempt appears in the detail page's `notifications` (T28).
- 200 → `{channel, status: "Queued", queuedAtUtc, templateKey, notificationQueueId}`
- 404 `NotFound` → unknown package, or the package has no notification on that channel
- 409 `NotFailed` → the latest notification on that channel wasn't a failure (it was sent or is still waiting), so nothing was re-queued. This also stops an old "ready for collection" email going out after a newer "collected" one.
- Each resend writes a `NotificationResent` audit entry (SR-04).

## CSV import (FR-08, IR-009..IR-013)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| POST | `/api/import/csv` | Supervisor, SystemAdmin | multipart file → `{importedCount, failedRows:[{row, reason}]}`; whole file rejected on structural error (IR-010) |

Required columns (case-insensitive, any order): `RecipientFullName, RecipientIdentifierNo, RecipientEmail,
RecipientPhone, SenderName, PackageType, Classification, StorageLocation`. If any of these is missing or renamed,
the whole file is rejected with 400 `ValidationError` and nothing is imported (IR-010). `RecipientDepartment` and
`Notes` are optional columns.

`StorageLocation` is the location's `code` (e.g. `A1-01`), looked up against `dbo.StorageLocations`; a code that
doesn't match an existing location fails just that row, same as any other bad value.

Each row is passed to the same `PackageRegistrationService.Register` the registration form uses (IR-009), so:
- the F20 identifier is always generated by the server — any `TrackingNumber` column in the file is not read;
- every imported package starts at status `Registered` and payment status follows the normal rule (work-related Exempt, personal Unpaid; FR-15,
  FR-17) — a row can't set its own status, date received, or fee;
- a row that fails this service's own validation (bad email, unknown package type, etc.) is reported with that
  service's message and skipped; the rest of the file still imports (IR-011).

One `CsvImported` audit entry is written per import with a summary (imported/failed/total counts), in addition to
the normal `PackageCreated` entry for each package (SR-04).

## Dashboard (UI: Dashboard)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| GET | `/api/dashboard/stats?period=today\|week\|month` | any authenticated | → `{period, receivedToday, readyForCollection, collectedToday, outstanding, recentActivity:[{f20Identifier, fromStatus, toStatus, changedBy, changedAtUtc}]}`; any other `period` → 400 `InvalidPeriod` |

Notes for the Dashboard endpoint:
- `period` is optional and defaults to `today`. "Today" is the current calendar day in South African time (UTC+2), not the UTC day. `week` is the last 7 days including today, `month` the last 30 days including today.
- **`receivedToday` and `collectedToday` hold the totals for the whole period.** With `period=week` or `period=month` they are the week's or month's totals, even though the names say "Today". `readyForCollection` and `outstanding` are current counts and ignore `period`.
- `recentActivity` is the 10 most recent status changes, newest first. `fromStatus` is `null` for a package's first status. `changedAtUtc` is an ISO 8601 UTC timestamp. It includes staff usernames, which is why the endpoint needs a login.

## Users / admin (FR-02, SR-01)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| GET | `/api/users` | SystemAdmin | → `[{userId, username, email, role, isActive}]` |
| POST | `/api/users` | SystemAdmin | `{username, email, password, role}` → created user |
| PATCH | `/api/users/{id}` | SystemAdmin | `{role?, isActive?}` → updated user |

## Audit log (SR-04)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| GET | `/api/audit-log?user=&action=&dateFrom=&dateTo=&page=&pageSize=` | Supervisor, SystemAdmin | → `{items:[...], totalCount, page, pageSize}` |
 `user` is a username (exact match). `dateFrom` and `dateTo` are `yyyy-MM-dd` (UTC) and both days are included. Newest first. `pageSize` defaults to 25, max 100. Each item: `{auditId, userId, username, action, entityType, entityId, detail, context, timestampUtc}`.

## Config (OR-01)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| GET | `/api/config` | SystemAdmin | → `[{key, value, description}]` |
| PATCH | `/api/config/{key}` | SystemAdmin | `{value}` → updated, no redeploy needed (NFR-024) |

---

## Role reference (matches Functional Spec §"User Classes")
`IntakeClerk`, `StorageStaff`, `CollectionStaff`, `Supervisor`, `SystemAdmin`

Changes to this contract go through a PR — don't silently change an endpoint shape once
frontend work has started against it.

---

## Response details: scan, status and collect (T19, T20)

These were only described as "the record" above; this is what the implemented endpoints return.

**`GET /api/packages/scan/{f20Identifier}`** returns, for a logged-in user of any role:
`{ packageId, f20Identifier, status, classification, packageType, paymentStatus, fee, storageLocationId, storageLocation, recipientName, recipientIdentifierNo, recipientDepartment, recipientPhone, receivedAtUtc, collectedAtUtc }`.
- `status` is the API name: `Registered`, `InStorage`, `ReadyForCollection` or `Collected`.
- `storageLocation` is the location's code, e.g. `"Shelf A-3"`.
- `recipientPhone` is masked to its last three characters, and no email address is returned (DR-004). Only what is needed to check someone's identity.
- An unknown identifier and a malformed one (anything other than letters, digits, `-` and `_`, up to 30 characters) both give the same `404` with code `NotFound` (IR-006). Leading/trailing whitespace from a scanner is ignored; matching is not case sensitive.

**`POST /api/packages/{f20Identifier}/status`** body `{ "newStatus": "InStorage", "storageLocationId": 3 }` (`storageLocationId` optional; leaving it out keeps the current location). `newStatus` accepts the API name or the staff-facing name (`"In Storage"`), in any letter case.
- `200` returns the updated record (same shape as scan).
- `400 ValidationError`: missing or unknown `newStatus`, a storage location that doesn't exist or is retired, a body that isn't valid JSON, or `newStatus` of `Collected` (collecting is only done through `/collect`, so the `CollectionStaff` role split can't be bypassed).
- `404 NotFound`; `409 InvalidTransition` (message e.g. `Package cannot move to Collected from Registered.`); `409 ConcurrentUpdate` if someone changed the package between it being read and saved (nothing is written; reload and retry).
- The allowed moves are Registered, In Storage, Ready for Collection, Collected, in that order only (DECISIONS.md #7). Status change, history row and audit entry are saved as one transaction.

**`POST /api/packages/{f20Identifier}/collect`** body `{ "verifiedByUserId": 5 }`. The whole body is optional; `verifiedByUserId` defaults to the logged-in user, and if given must be an active user (otherwise `400 ValidationError`).
- `200` returns `{ "status": "Collected", "collectedAtUtc": "..." }`.
- Only works from `ReadyForCollection`; otherwise `409 InvalidTransition`.
- The package records who verified the collector and when; the history row and audit entry record the logged-in user who processed it.

**`GET /api/storage-locations`** (any logged-in user) returns the active locations as `[{ storageLocationId, code, description }]`. Adding a location (`POST`, SystemAdmin) is not built yet (T24).

## Response details: payment status (T41)

**`PATCH /api/packages/{f20Identifier}/payment`** body `{ "paymentStatus": "Paid" }`.
- `paymentStatus` must be `Paid`, `Unpaid` or `Exempt` (any case), otherwise `400 ValidationError`. Unknown package: `404 NotFound`.
- Any status can be set from any other, at any stage of the package, since the fee is often paid at collection and recorded afterwards.
- The status, the time and the staff member are stored on the package (`PaymentStatusUpdatedAtUtc`, `PaymentStatusUpdatedByUserId`), and a `PaymentStatusChanged` audit entry is written, e.g. "Unpaid to Paid (fee R10.00)". The audit log keeps every change for reconciliation (FR-15).
- Setting the status it already has writes nothing and returns `changed: false`.
- `paymentStatusUpdatedAtUtc` is ISO 8601 UTC; `paymentStatusUpdatedBy` is the staff username. Both are `null` until staff first change the status.
- New packages start `Exempt` when work-related and `Unpaid` when personal, whatever fee AppConfig gives (T41).
- `GET /api/packages/{f20Identifier}/detail` includes `paymentStatusUpdatedAtUtc` and `paymentStatusUpdatedBy` in `package`.

## Response details: users (T43)

All three endpoints are SystemAdmin only (`401 NotAuthenticated` when not logged in, `403 Forbidden` for other roles).

**`GET /api/users`** returns every user, active or not, ordered by username:
`[{ userId, username, email, role, isActive, lastLoginUtc }]`. `lastLoginUtc` is `null` for someone who has never logged in. The password hash is never returned.

**`POST /api/users`** body `{ "username": "new.clerk", "email": "new.clerk@courier.test", "password": "Welcome123", "role": "IntakeClerk" }`.
- `201` returns the new user (same shape as one item from `GET`). New users are active.
- `role` is one of the five role names, in any letter case.
- `username`: 3 to 100 characters, letters, numbers, `.`, `-` and `_` only. `email`: up to 200 characters and must look like an address.
- `password`: at least 8 characters with at least one letter and one number (NRF-014), at most 72 bytes because BCrypt ignores anything longer. Stored as a BCrypt hash (work factor 11), never as plain text.
- `400 ValidationError` for a missing or invalid field or a body that isn't valid JSON; `409 UsernameTaken` or `409 EmailTaken` if either is already used by another account (not case sensitive).

**`PATCH /api/users/{id}`** body `{ "role": "Supervisor", "isActive": false }`. Both fields are optional, but at least one must be sent; a field left out keeps its value.
- `200` returns the updated user. Sending values the user already has is fine and changes nothing.
- `400 ValidationError` for an unknown role or an empty body; `400 OwnAccount` if an admin tries to change their own role or deactivate themselves; `404 NotFound` for an unknown id; `409 LastAdministrator` if the change would leave no active SystemAdmin.
- A deactivated user can no longer log in. A session that is already open stays valid until it times out (30 minutes, `Session.InactivityTimeoutMinutes`).
- Each change is written to the audit log as `UserCreated`, `UserRoleChanged` (detail e.g. `Role: IntakeClerk -> Supervisor`), `UserDeactivated` or `UserReactivated`. The entity id is the user's id; no names or email addresses go into the audit detail (SR-03).

## Response details: registration and QR label

**`POST /api/packages`** (IntakeClerk, Supervisor, SystemAdmin) body, as sent by the registration form:
`{ "recipient": { "fullName", "identifierNo", "email", "phone", "department" }, "senderName", "packageType", "classification", "storageLocationId", "notes" }`.
- `201` returns `{ packageId, f20Identifier, fee, paymentStatus, status: "Registered" }`.
- Required: `recipient.fullName`, `recipient.identifierNo`, `recipient.email`, `recipient.phone`, `senderName`, `packageType`, `classification`, `storageLocationId`. Optional: `recipient.department`, `notes`. Text is trimmed and limited to the column sizes in `db/schema.sql`.
- `packageType` is one of `Envelope`, `Box`, `Parcel`, `Other`. `classification` is `Personal` or `WorkRelated` (also accepted: `Work-related`, `work related`, any letter case).
- `phone` may contain spaces, dashes, dots or brackets; it is stored as digits (with an optional leading `+`) and must be 9 to 15 digits.
- `storageLocationId` must be an active location from `GET /api/storage-locations`.
- `f20Identifier` is `F20-` plus the next free number, at least four digits (`F20-0201`, or `F20-60001` with the 50,000-package seed). It is assigned inside the registration's transaction, so two clerks registering at the same moment never get the same one.
- `fee` comes from `dbo.AppConfig` (`Fee.Personal`, `Fee.WorkRelated`), so changing the fee needs no redeploy (FR-15). `paymentStatus` is `Exempt` for a work-related package and `Unpaid` for a personal one, whatever the fee (T41, FR-17).
- The recipient's details are saved with this package as a new recipient row (the details as given at registration).
- The package, recipient, first status history row (`Registered`) and the `PackageCreated` audit entry are saved in one transaction. The audit detail holds only the classification and fee, no names or contact details (SR-03).
- `400 ValidationError` with a message the form can show, for a missing or invalid field or a body that isn't valid JSON.

**`GET /api/packages/{f20Identifier}/qr`** (any logged-in user) returns the label's QR code as `image/png`.
- The QR code holds the F20 identifier only (CON-008), so a lost label shows no personal details and the scan screens can look the package up from it.
- Only drawn for packages that exist: an unknown or malformed identifier gives the same `404 NotFound` as the scan endpoint.
- Sent with `Cache-Control: private`, so only the user's own browser keeps a copy.
## Registration fee lookup (T06)

`GET /api/packages/fees` (IntakeClerk, Supervisor, SystemAdmin) returns `{ "Personal": 10.00, "WorkRelated": 0.00 }` using current `Fee.Personal` and `Fee.WorkRelated` database values. The response is not cached and includes no other configuration. Registration reads those same keys independently when saving.
