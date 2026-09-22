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
| GET | `/api/dashboard/stats` | any authenticated | → `{receivedToday, readyForCollection, collectedToday, outstanding}` |

## Users / admin (FR-02, SR-01)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| GET | `/api/users` | SystemAdmin | → `[{userId, username, email, role, isActive}]` |
| POST | `/api/users` | SystemAdmin | `{username, email, password, role}` → created user |
| PATCH | `/api/users/{id}` | SystemAdmin | `{role?, isActive?}` → updated user |

## Audit log (SR-04)
| Method | Path | Roles | Body → Response |
|---|---|---|---|
| GET | `/api/audit-log?user=&action=&dateFrom=&dateTo=&page=` | Supervisor, SystemAdmin | → `{items:[...], totalCount}` |

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
