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
   skeleton landing page at `https://localhost:44301/`.

## HTTPS and phone camera scanning (T11)

The app runs on two ports: plain HTTP on `http://localhost:44300/` and HTTPS on
`https://localhost:44301/`. Opening the HTTP address sends you to HTTPS automatically. The
redirect can be changed with `Https.RedirectEnabled` and `Https.Port` in `Web/Web.config`.
Browsers only allow camera access on HTTPS pages, so phone scanning needs the setup below.

If `https://localhost:44301/` does not load, click the `Web` project, press F4 and check that
**SSL URL** says `https://localhost:44301/`. An old `Web/CourierService.Web.csproj.user` file
(private to your machine, never committed) can hold on to the previous port. Open it and change
`IISExpressSSLPort` to `44301`, then restart Visual Studio.

### Reaching the site from a phone (once per PC, nothing here is committed)

You need a Windows PC and a phone on the same Wi-Fi. Replace `192.168.0.11` below with your own
PC's IPv4 address (run `ipconfig`). It is recommended to access the router settings and reserve an IP for the computer you are hosting the app on, or you
will have to redo step 2 whenever your computer is assigned a new IP.

1. Close Visual Studio. In an **Administrator** PowerShell, install mkcert and trust its
   certificate authority on this PC:
   ```powershell
   winget install FiloSottile.mkcert
   ```
   Reopen PowerShell as Administrator, then:
   ```powershell
   mkdir C:\courier-dev; cd C:\courier-dev
   mkcert -install
   ```
2. Make a certificate for your PC's address, import it and attach it to port 44301. Keep these
   files outside the repo.
   ```powershell
   mkcert -pkcs12 -p12-file courier-dev.p12 192.168.0.11 localhost 127.0.0.1
   $pw = ConvertTo-SecureString "changeit" -AsPlainText -Force
   Import-PfxCertificate -FilePath .\courier-dev.p12 -CertStoreLocation Cert:\LocalMachine\My -Password $pw
   Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*mkcert development certificate*" } | Format-List Subject, Thumbprint
   netsh http add sslcert ipport=192.168.0.11:44301 certhash=PASTE_THUMBPRINT appid="{214124cd-d05b-4309-9af9-9caa44b2b74a}"
   ```
3. Allow other devices on your home network in, and let IIS Express listen on all addresses:
   ```powershell
   New-NetFirewallRule -DisplayName "Courier dev 44300-44301" -Direction Inbound -Protocol TCP -LocalPort 44300,44301 -Action Allow -RemoteAddress LocalSubnet
   netsh http add urlacl url=http://*:44300/ user=Everyone
   netsh http add urlacl url=https://*:44301/ user=Everyone
   ```
4. Press F5 once, then stop and close Visual Studio. Open
   `.vs\CourierService\config\applicationhost.config` (the `.vs` folder is hidden). In the
   `CourierService.Web` site, change the two bindings to:
   ```xml
   <binding protocol="http" bindingInformation="*:44300:" />
   <binding protocol="https" bindingInformation="*:44301:" />
   ```
   Reopen Visual Studio normally and press F5. `https://192.168.0.11:44301/` should now open on
   the PC with a padlock and no warning. Visual Studio can rewrite this file when project
   settings change, so check these two lines first if the address stops working.
5. Make the phone trust the certificate. Run `mkcert -CAROOT` and copy `rootCA.pem` from that
   folder (never `rootCA-key.pem`) to your phone, for example by email.
   - **iPhone:** rename the copy to `mkcert-root.crt` first. Open it, tap Allow, then go to
     Settings > General > VPN & Device Management > the downloaded profile > Install. Then go to
     Settings > General > About > Certificate Trust Settings and switch the certificate on.
   - **Android:** Settings > Security (or Security & privacy) > Install a certificate (under More
     security settings or Encryption & credentials) > CA certificate, then pick the file. Menu
     names differ between phones.
6. On the phone, open `https://192.168.0.11:44301/` and go to the Scan page. It should load with
   a padlock and start the camera.

When you are done testing, remove the certificate profile from the phone. Anyone holding the
`rootCA-key.pem` file could create certificates your phone would trust, so keep it private.

### If it does not work

- **The phone times out:** check the phone is on the same Wi-Fi, not a guest network, and that the
  firewall rule from step 3 exists. Some networks block devices from reaching each other.
- **Certificate warning on the PC or phone:** the certificate was made for a different address.
  Redo step 2 with the PC's current address and use the new thumbprint.
- **Works on the PC but not from the phone:** the bindings in step 4 are probably back to
  `localhost`. Check them.

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

  ## Demo logins (development only)

Load them with `db/seed.sql` after `db/schema.sql`. All five demo users share one password: `Demo@2026!`

| Role | Username |
|---|---|
| Intake clerk | intake.demo |
| Storage staff | storage.demo |
| Collection staff | collection.demo |
| Supervisor | supervisor.demo |
| System admin | admin.demo |

Optional: `db/seed-50k.sql` adds 50,000 synthetic packages for the performance test. Don't run it unless you need it.