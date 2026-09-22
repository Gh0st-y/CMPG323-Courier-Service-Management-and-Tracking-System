# CMPG323-Courier-Service-Management-and-Tracking-System

Staff-only intake, storage and collection tracking system for North-West University's
F20 courier point. See [DECISIONS.md](DECISIONS.md) for the architecture/decisions log,
[docs/API_CONTRACT.md](docs/API_CONTRACT.md) for the REST contract, and
[db/schema.sql](db/schema.sql) for the database schema.

## Solution layout

```
CourierService.sln
Domain/    CourierService.Domain    POCOs and repository interfaces, no dependencies on the other layers
Services/  CourierService.Services  Business logic: package lifecycle, state machine, fee calc, notifications
Data/      CourierService.Data     ADO.NET repository implementations (parameterized queries only)
Web/       CourierService.Web      ASP.NET MVC controllers, Razor views, static JS/CSS — no business logic
Tests/     CourierService.Tests    Unit tests (MSTest) covering Domain/Services/Data
docs/      API_CONTRACT.md         REST endpoint contract
db/        schema.sql              SQL Server Express / LocalDB schema script
frontend-mocks/  mock-fixtures.json  Fixtures the shared JS fetch wrapper serves in mock mode (T07/T09)
```

`Web` depends on `Services` and `Data`; `Services` and `Data` depend on `Domain`; nothing
depends back on `Web`. Scanning devices and all UI calls go through `Web`'s HTTP API —
no layer talks to the database except `Data`.

## Prerequisites

- Visual Studio 2022+ with the **ASP.NET and web development** workload (includes the
  .NET Framework 4.8 targeting pack and the legacy web project tooling), or the .NET
  Framework 4.8 Developer Pack plus MSBuild for command-line builds.
- SQL Server Express or SQL Server LocalDB (`(localdb)\MSSQLLocalDB` — installed with
  the Visual Studio workload above, or standalone from Microsoft).

## First-time setup

1. Clone the repo and open `CourierService.sln` in Visual Studio. It will restore NuGet
   packages (ASP.NET MVC 5, MSTest) automatically on open/build.
2. Create the database: open `db/schema.sql` in SQL Server Object Explorer / SSMS against
   `(localdb)\MSSQLLocalDB` and execute it. It's idempotent — safe to re-run.
3. `Web/Web.config` already points at `(localdb)\MSSQLLocalDB` / database `CourierService`
   (see `connectionStrings`), matching `Web.config.sample.xml`. If your local SQL Server
   instance is named differently, update the connection string there — never commit real
   credentials; use a local, gitignored override (`Web.config.Local`) if you need one for
   your machine.
4. Set `Web` as the startup project and press F5 (IIS Express). You should get the
   skeleton landing page at `https://localhost:44300/`.

## Building from the command line

```bash
"C:\Program Files\Microsoft Visual Studio\<edition>\<version>\MSBuild\Current\Bin\MSBuild.exe" CourierService.sln -t:restore
"C:\Program Files\Microsoft Visual Studio\<edition>\<version>\MSBuild\Current\Bin\MSBuild.exe" CourierService.sln -p:Configuration=Debug
```

## Running tests

```bash
dotnet test Tests/CourierService.Tests.csproj
```

(or run them from Visual Studio's Test Explorer)

## Frontend style guide (T07)

Everything under `Web/Content` and `Web/Scripts` is shared — reuse it rather than
re-implementing per page (FE2/FE3, this means you).

**Theme** — `Web/Content/site.css` defines CSS custom properties (`--color-primary`,
`--color-accent`, `--color-bg`, `--color-text`, `--color-error`, etc.). The palette is
NWU-inspired (purple/gold) but not an official brand asset (CON-010) — change the tokens,
not individual component rules, if that changes. Layout is plain flexbox, no CSS framework;
`html, body { min-width: 1024px }` and a `--content-max-width` keep it usable at 1366x768
(the acceptance criterion) without being unusable at 1920 either.

**Master layout** — `Web/Views/Shared/_Layout.cshtml` has the header/nav (`.app-header`,
`.app-nav`) and the `.app-main` content container. Give a view a title via
`@@{ ViewBag.Title = "..."; }` and put page-specific `<script>` in `@@section scripts { }`.
Nav links for features that don't have a controller yet point at `#` — wire them up when
that feature's task lands, don't add new top-level nav items without updating this file.

**Shared JS** — `Web/Scripts/app/courier-app.js` exposes one global, `CourierApp`:

```js
// Fetch wrapper — GET/POST/PATCH against /api/*, or against frontend-mocks/mock-fixtures.json
// when mock mode is on. Shows the loading bar automatically and an error toast on failure
// (matching docs/API_CONTRACT.md's { error: { code, message } } shape) unless you pass { silent: true }.
CourierApp.api.get("/packages/scan/F20-0001").then(function (pkg) { ... });
CourierApp.api.post("/packages", { recipient: { ... }, ... });

// Toasts
CourierApp.toast.success("Package registered.");
CourierApp.toast.error("Something went wrong.");

// Confirm dialog (promise-based, not the browser's native confirm())
CourierApp.confirm("Mark this package as collected?").then(function (confirmed) { ... });

// Loading state, if you're not going through CourierApp.api (which already wraps it)
CourierApp.loading.show();
CourierApp.loading.hide();
```

**Mock mode** — toggle from the browser console with
`CourierApp.config.setMockMode(true)` (persists in `localStorage`; see the toggle button on
the home page for a working example). When on, `CourierApp.api` reads
`frontend-mocks/mock-fixtures.json` (served by `Web/Controllers/MockController.cs` — that
controller is dev-only plumbing, not part of the real API) instead of hitting `/api/*`, so
frontend work isn't blocked on a backend endpoint landing.

## Contributing

- Branch per issue: `feature/T<id>-short-name`, off `main`. Never push to `main` directly.
- Open a PR with `Closes #<issue number>` — the template in `.github/pull_request_template.md`
  is applied automatically.
- `main` requires a PR and 1 approval (relaxed to a fast review from any teammate for the
  M0 foundation tasks — see `DECISIONS.md`).
