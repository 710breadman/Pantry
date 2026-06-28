# DevKit Project Status

Last verified: 2026-06-28 (America/Denver)

## Purpose

This file is the handoff point for future work. Read it before changing the
project. `ROADMAP.md` owns planned improvements. Update the verification date,
check results, issue states, and recent changes after each iteration.

## Executive Status

DevKit is a functional Windows 11 developer-environment curator with two
frontends:

- Native .NET 10 WPF app (`src/DevToolsCurator.App`)
- Legacy PowerShell CLI (`setup-devtools.ps1`)

The native app builds after a NuGet restore. Its 24-test console regression
suite, smoke test, UI self-check, contract self-check, and packaged-EXE checks
all pass. The legacy CLI self-test and 9 Pester tests also pass.

Local Git, ignore/attribute rules, SDK pinning, CI config, artifact policy,
catalog contracts, versioned release metadata, SHA-256 output, signing support,
test isolation, and async-command error reporting now exist. Remaining main
risks: no initial commit/remote/hosted CI result, catalog migration incomplete,
native tests still custom, large controller files, and no signing certificate.

## Current Verification

Environment:

- OS/shell: Windows, PowerShell
- SDK: .NET SDK `10.0.300`
- Solution: `src\DevToolsCurator.slnx`
- Native target: `net10.0-windows`
- Core/tests target: `net10.0`

Results on 2026-06-28:

| Check | Result | Notes |
| --- | --- | --- |
| `dotnet restore .\src\DevToolsCurator.slnx` | PASS | All three projects restored |
| `dotnet build .\src\DevToolsCurator.slnx -c Release --no-restore` | PASS | 0 warnings, 0 errors |
| Native console regression suite | PASS | 24/24 |
| Native app `--smoke-test` | PASS | Exit 0 |
| Native app `--ui-self-check` | PASS | Exit 0 |
| Native app `--contract-self-check` | PASS | Exit 0 |
| Packaged `release\DevKit\DevKit.exe --smoke-test` | PASS | Exit 0 |
| Packaged `release\DevKit\DevKit.exe --contract-self-check` | PASS | Exit 0 |
| `setup-devtools.ps1 -SelfTest` | PASS | `Self-test passed.` |
| Legacy Pester suite | PASS | 9/9 using installed Pester 3.4.0 |
| `build-release.ps1 -Version 0.1.0` | PASS | Versioned EXE, metadata, checksum, smoke/contract checks |

Important build detail: an initial `--no-restore` build failed with
`NETSDK1064` because `Microsoft.NET.ILLink.Tasks 10.0.8` was absent from the
local NuGet cache. A normal `dotnet restore` fixed it. Treat restore as required
on a fresh or cleaned machine.

## Architecture Map

### Native application

- `src\DevToolsCurator.App`: WPF views, view models, dialogs, commands, startup
  checks
- `src\DevToolsCurator.Core`: catalog loading, detection, scanning, goal
  planning, winget access, operations, PATH repair, reports, runtime paths
- `src\DevToolsCurator.Tests`: custom executable regression suite; no external
  test framework
- `tool_catalog.json`: 51-tool native catalog; embedded into Core and copied
  beside release EXE as an override

Primary native flow:

1. Resolve AppData/portable paths.
2. Load loose `tool_catalog.json`, falling back to embedded catalog.
3. Inspect system, registry, PATH, environment variables, winget, and versions.
4. Calculate goal-specific recommendations and dashboard summary.
5. Run explicit install/update/repair actions.
6. Rescan and write reports.

### Legacy application

- `setup-devtools.ps1`: preview/apply/repair/update/self-test backend
- `gui\DevToolsDashboard.ps1`: retired PowerShell GUI retained in tree
- `gui\DevTools.GuiModel.psm1`: legacy GUI model helpers
- `tool-catalog.json`: separate 102-tool legacy catalog
- `tests\DevTools.Tests.ps1`: Pester-style helper tests

`setup-devtools.ps1 -Gui` is contract-tested to launch the native EXE rather
than the legacy dashboard.

### Release

- Build entry: `build-release.ps1`
- Output: `release\DevKit\DevKit.exe`
- Shape: self-contained, compressed, single-file `win-x64` WPF executable
- Current file version/product version: `0.1.0.0` / `0.1.0`
- Current EXE size: about 61.8 MiB
- Current EXE signature: not signed
- Current checksum matches `build-metadata.json` and `SHA256SUMS`
- Current provenance: `source_commit=uncommitted`, `source_dirty=true`

## Problems Needing Attention

### P1-01: Source-control baseline needs external completion

Status: Mitigated locally; external setup open

Local Git repository now exists on `main`. `.gitignore`, `.gitattributes`,
`global.json`, `Directory.Build.props`, contribution guidance, artifact policy,
and GitHub Actions CI config now exist. Generated output is ignored.

Remaining:

- Review and create initial commit.
- Select project license; this requires owner intent.
- Connect intended remote.
- Confirm hosted CI, then add branch protection.

### P1-02: Native and legacy catalogs have diverged

Status: Mitigated; schema migration open

`tool_catalog.json` contains 51 native-tool definitions. `tool-catalog.json`
contains 102 legacy-tool definitions. Schemas and filenames differ. Native
build/release code uses the underscore file; legacy CLI and legacy GUI use the
hyphen file.

Impact:

- Tool coverage and recommendations differ by frontend.
- Fixes can land in one catalog only.
- README wording about a shared catalog/engine is easy to misread.

`tool_catalog.json` is now declared canonical in `CATALOGS.md`. Regression tests
enforce unique IDs, minimum overlap, and the explicit native-only boundary.
Full fix requires schema v2 and legacy CLI migration; tracked in `ROADMAP.md`.

### P1-03: Release has no trustworthy provenance

Status: Partially resolved; certificate/commit required

Release now carries `0.1.0` semantic/file metadata. Build emits
`build-metadata.json` and `SHA256SUMS`; checksum verification passes. Build
script supports timestamped Authenticode signing by certificate thumbprint.

Current artifact remains unsigned because no signing certificate was supplied.
It reports `source_commit=uncommitted` because initial commit does not exist.

### P2-01: Tests are local and partly custom

Status: Partially resolved

Native tests are a custom console executable, not `dotnet test`; legacy helper
tests are Pester-style, while documented CLI verification runs the script's
internal `-SelfTest`. No CI runs either set.

PATH-repair tests now use an injected in-memory environment store and do not
write real machine/user/process PATH. CI runs native regression/self-checks,
legacy self-test, and Pester. Local Pester: 9/9 pass.

Remaining: move native tests to xUnit/NUnit/MSTest and `dotnet test`.

### P2-02: Stale machine reports can be mistaken for current project state

Status: Mitigated

`devtools_setup_report` contains snapshots from 2026-05-30 through 2026-05-31.
Some files reference user `C:\Users\Media`, not the current environment. The
latest summary reports 59% readiness, but this is historical machine state,
not current project health.

Runtime reports are now ignored and classified as disposable machine output in
`ARTIFACTS.md`. Existing snapshots remain locally for reference. Future work:
add prominent machine/age labels and automated redaction.

### P2-03: Large UI/controller files increase change risk

Status: Partially resolved

Largest maintained files include:

- `src\DevToolsCurator.App\MainViewModel.cs`: 846 lines
- `gui\DevToolsDashboard.ps1`: 712 lines
- `src\DevToolsCurator.Tests\Program.cs`: 506 lines
- `src\DevToolsCurator.App\MainWindow.xaml`: 361 lines
- `src\DevToolsCurator.Core\ToolDetector.cs`: 331 lines

`AsyncRelayCommand` now catches failures, reports them through a centralized
handler, logs the exception, and shows a bounded user-facing error. Large files
still need decomposition.

Recommended action: retire/archive the unused PowerShell dashboard, split
`MainViewModel` by responsibility, and add centralized async command exception
reporting.

### P2-04: Runtime artifacts lack clear retention rules

Status: Resolved

Root contains zero-byte GUI logs, historical operation scripts/logs, scan
results, compiled outputs, and a 61.8 MiB release. No policy says which are
fixtures, distributables, or disposable local outputs.

`ARTIFACTS.md` now defines ownership/retention. `.gitignore` enforces generated
output boundaries. Release builds emit the documented artifact set.

## Historical Runtime Snapshot — Not Current

Latest stored native scan: 2026-05-31 08:24:33 -06:00.

- Goal: AI / Codex-ready Workstation
- Readiness: 59%
- Installed/current: 21
- Outdated: 2
- Missing critical/recommended: 8
- Missing optional: 20
- Broken/PATH/reboot issues: 1
- Auth/action needed: 2

Stored issues included missing Python/Node quality tools, missing Git identity,
Git long paths disabled, pending reboot, broken PATH entries, and Developer
Mode disabled. Re-scan before acting; this data may describe another machine.

## Recommended Next Iteration

Start with remaining Phase 0 and catalog schema v2:

1. Review and create initial commit.
2. Select license and connect intended remote.
3. Confirm hosted CI.
4. Design catalog schema v2; migrate legacy installer fields.
5. Obtain signing certificate before public distribution.

Detailed sequencing and acceptance criteria: `ROADMAP.md`.

## Standard Verification Commands

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

Use `build-release.ps1` only when intentionally replacing
`release\DevKit`; it removes and recreates that directory.

## Update Protocol

For each future iteration:

1. Read this file and `README.md`.
2. Confirm whether source control has been added.
3. Re-run checks relevant to changed areas.
4. Update issue states using existing IDs; add new stable IDs when needed.
5. Add a brief entry below with date, scope, verification, and remaining risk.
6. Keep historical runtime snapshots clearly separate from current project
   verification.

## Iteration Log

### 2026-06-28 — Baseline remediation and roadmap

- Initialized local Git repository on `main`.
- Added Git hygiene, SDK pin, deterministic build props, CI, contributing
  guide, catalog contract, and artifact policy.
- Added `ROADMAP.md` with six ordered phases.
- Added catalog drift regression contract; suite now 24/24.
- Removed real user PATH writes from regression tests.
- Added centralized async-command failure reporting.
- Added semantic release versioning, provenance metadata, checksums, and
  optional timestamped Authenticode signing.
- Rebuilt release `0.1.0`; checksum and packaged self-checks pass.
- Verified legacy Pester suite 9/9.
- Remaining external decisions: initial commit approval, license, remote,
  hosted CI, signing certificate.

### 2026-06-28 — Initial project scan

- Added this contact file.
- Inventoried native app, legacy CLI, catalogs, tests, reports, and release.
- Verified restore/build and all documented native self-checks.
- Verified 23 native regression tests and legacy CLI self-test.
- Verified packaged EXE smoke and contract checks.
- Identified hygiene, catalog drift, release provenance, test, stale-report,
  maintainability, and artifact-retention risks.
