# Demo candidate verification - 2026-10-12

Source: `9aba7d6328e3318de8629dc2878dd07f74993800` on `feature/T57-deployment-preparation`, including main `e5e6ea5`, the #110 schema fix and the HTTPS port consistency correction.

Command:

```powershell
./scripts/Prepare-DemoPackage.ps1 -ReleaseRef 9aba7d6328e3318de8629dc2878dd07f74993800 -OutputDirectory artifacts/maintenance-20261012-t57 -Candidate
```

Observed results:

- An immutable Git source archive was restored, built in Release with Razor compilation, and published with the DemoFolder profile.
- All 343 selected non-integration .NET tests passed, with no failures or skips.
- The packaging script checked required published assets, Release compilation settings and secure-cookie settings.
- All 104 published file SHA256 hashes were independently recomputed and matched `manifest.json`.
- `source.zip`, `web.zip` and the manifest remain local generated artifacts, excluded from Git.
- Existing NuGet version-constraint/downgrade and assembly-binding warnings, plus the duplicate using warning inherited from main, remain. #121 removes the duplicate using; dependency warnings need separate assessment.

This is a preparation candidate, not a final release or deployed environment. Its manifest records `candidate=true`, `deployed=false`, `hardwareVerified=false`. Pending fee/security/audit/UI PRs are not included. Merge the required reviewed PRs, prepare a tagged final build and rerun verification before deployment.

SQL integration tests, restored database/backups, IIS/certificate setup, SMTP inbox demonstration, phones/USB/printer, backup laptop and rehearsal remain pending. #57 stays open.
