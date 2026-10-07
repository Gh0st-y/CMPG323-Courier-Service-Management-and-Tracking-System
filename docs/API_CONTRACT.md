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

## CSV import (FR-08, IR-009..IR-013)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| POST | `/api/import/csv` | Supervisor, SystemAdmin | multipart file → `{importedCount, failedRows:[{row, reason}]}`; whole file rejected on structural error (IR-010) |

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
