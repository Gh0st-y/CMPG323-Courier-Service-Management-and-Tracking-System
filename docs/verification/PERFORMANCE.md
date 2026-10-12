# T53 performance record

2026-10-12, Windows/.NET Framework 4.8, IIS Express, Release build, SQL Server LocalDB, Chrome 1366x768, mock mode off. The database contains the 50,000 synthetic performance packages plus the 200 baseline packages. The original run imported 500, and the precompiled retest started with 50,700 and ended with 51,200. Every import row succeeded.

| Operation | Samples | Original maximum | Precompiled maximum | Recorded target |
| --- | --- | --- | --- | --- |
| Dashboard usable | 10 navigations | 5,132 ms: failed | 859 ms: passed | 2,000 ms |
| Scan API, complete JSON response | 30 | 7 ms | 8 ms | 1,500 ms |
| Search API, complete JSON response | 30 | 98 ms | 112 ms | 3,000 ms |
| Status update | 20 real writes | 31 ms | 29 ms | No numeric target found in repository |
| CSV import | 500 rows | 16,484 ms | 15,687 ms | No numeric target found in repository |

The first dashboard sample is retained. Each benchmark authenticates and exercises APIs before opening Home; this measures the first dashboard/view visit after site initialization, not IIS process startup or login time. Usable means DOM loaded and the dashboard reports `aria-busy=false`, including its 700 ms spinner minimum. Precompiling the DemoFolder publish profile moves Razor view compilation into the release build; its non-updateable output must be republished when views change. Avoid invoking the separate project-root MvcBuildViews compiler during precompiled publishing, as it includes generated nested config files; the publish compiler already validates views.

Five independent StorageStaff sessions then alternated scan/search for 60 seconds, with 250 ms think time. Retest: 833 successful scans and 833 successful searches, zero HTTP/assertion failures; p95 scan 9 ms, p95 search 134 ms. These are loopback measurements on this workstation, not a production capacity estimate.

Raw [original results](performance-before-precompile.json) and [precompiled results](performance-results.json) preserve the failed baseline and passing documented targets. Status/import targets still require the authoritative specification or owner confirmation; neither is silently assigned a passing target.

Postman CLI 1.73.0 successfully ran the functional v3 collection. Its local performance mode rejected v3; a generated classic collection then failed with `error while parsing createbBy`. The benchmark therefore uses the checked-in Node/Playwright harness. It is **not** reported as a successful Postman performance run. The maintained Postman read collection still lints cleanly.

Use [reproduction instructions](README.md); benchmark writes must target only the isolated synthetic database. Demo hardware, HTTPS/phone scanning and a public network were not part of this measurement.
