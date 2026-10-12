# Frontend mock mode

Mock mode is a development tool, disabled by default through `Frontend.MockEnabled=false` in `Web/Web.config`. The dashboard switch is hidden and `/mock/fixtures` returns 404 when it is disabled. A saved browser preference cannot switch requests to fixtures while the server-provided flag is disabled.

For local frontend demonstrations only, explicitly set `Frontend.MockEnabled` to `true` in the site's appSettings, then enable Mock mode on the Dashboard. If #113's optional `Web.config.Local` support is deployed, that file can provide the local override and must have an `<appSettings>` root. Turn mock mode off to exercise the database. Keep the setting false on deployed/demo acceptance sites.

`frontend-mocks/mock-fixtures.json` contains static data. In particular, `/api/packages/fees` returns Personal R10.00 and WorkRelated R0.00 regardless of database AppConfig values. Mock actions do not prove persistence, fee reloads, role enforcement, SMTP delivery or database connectivity. CSV import requires real mode.

Use [ROLE_TEST_SCRIPT.md](ROLE_TEST_SCRIPT.md) for live acceptance checks and [USER_MANUAL.md](USER_MANUAL.md) for staff workflows.
