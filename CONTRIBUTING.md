# Contributing

## Before editing

1. Read `PROJECT_STATUS.md`.
2. Read `ROADMAP.md`.
3. Keep native catalog changes in `tool_catalog.json`.
4. Update `tool-catalog.json` only when legacy CLI behavior also needs the
   change.

## Verification

Run:

```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet restore .\src\DevToolsCurator.slnx
& $dotnet build .\src\DevToolsCurator.slnx -c Release --no-restore
& $dotnet run --project .\src\DevToolsCurator.Tests\DevToolsCurator.Tests.csproj -c Release --no-build
& $dotnet run --project .\src\DevToolsCurator.App\DevToolsCurator.App.csproj -c Release --no-build -- --smoke-test
& $dotnet run --project .\src\DevToolsCurator.App\DevToolsCurator.App.csproj -c Release --no-build -- --ui-self-check
& $dotnet run --project .\src\DevToolsCurator.App\DevToolsCurator.App.csproj -c Release --no-build -- --contract-self-check
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\setup-devtools.ps1 -SelfTest
```

If Pester 3 or 4 is installed:

```powershell
$result = Invoke-Pester -Script .\tests\DevTools.Tests.ps1 -PassThru
if ($result.FailedCount -gt 0) { exit $result.FailedCount }
```

## Change rules

- Do not commit generated paths listed in `ARTIFACTS.md`.
- Do not put secrets, tokens, usernames, or machine-specific report data in
  source.
- Add/update tests with behavior changes.
- Update `PROJECT_STATUS.md` after verified work.
- Update `ROADMAP.md` only when scope or completion state changes.
- Public release builds require version, checksum, provenance metadata, and
  Authenticode signature.
