# Current State

Last updated: 2026-06-26

## Repository Snapshot

The repository has a first read-only Phase 1 slice.

| Area | Current state |
| --- | --- |
| Source code | Read-only WinUI shell plus separated domain, catalog, core, detection, and infrastructure projects |
| Tests | xUnit tests for Recipe validation, catalog loading, profile defaults, dry-run planning, Winget/registry/file detection, run-mode detection, SQLite state, and saved settings |
| Build system | `.NET 10` solution file: `ThePantry.slnx` |
| Git repository | Initialized locally; `origin` points to `https://github.com/710breadman/Pantry.git` |
| Intended upstream | `https://github.com/710breadman/Pantry.git` |
| Upstream state | Public GitHub repository with the initial read-only slice pushed |
| Existing docs | `AGENTS.md`, `docs/PRODUCT_SPEC.md` |
| Phase | Phase 1: read-only foundation slice |

## What Exists

- `AGENTS.md` defines the local working rules for Codex.
- `docs/PRODUCT_SPEC.md` defines the product vision, safety rules, v1 scope, architecture direction, phases, and first vertical slice.
- The intended upstream repository is `710breadman/Pantry` on GitHub.
- `ThePantry.slnx` contains the current .NET solution.
- `src/Pantry.UI` contains a basic WinUI 3 shell.
- `src/Pantry.Domain` contains the shared models.
- `src/Pantry.Catalog` loads and validates bundled JSON Recipes.
- `src/Pantry.Core` creates the dry-run review plan.
- `src/Pantry.Detection` runs read-only installed-app checks.
- `src/Pantry.Infrastructure` detects installed/portable run mode and initializes SQLite state, operation logs, saved scan results, app settings, and saved profile selections.
- `tests/Pantry.Tests` covers the read-only foundation behavior.
- `catalog/bundled` contains the JSON Schema, five approved Recipe files, and three profiles.

## Product Interpretation

The Pantry should be a Windows 11 app that helps a power user safely install, update, and uninstall a small curated set of known apps.

The key idea is not "search the whole internet for apps." The key idea is "use trusted Recipes for apps we deliberately support."

A Recipe is the complete instruction set for one app, including:

- where it comes from
- how to install it
- whether it needs administrator rights
- how to detect whether it installed correctly
- how to update it
- how to uninstall it
- what failures are expected
- what trust level it has

## Important Constraints

- The main app must not run as administrator.
- Administrator work must happen in a separate elevated helper.
- The elevated helper must accept structured approved jobs, not arbitrary shell commands.
- Only `VerifiedUnattended` Recipes may run unattended.
- The app must never report success until post-install detection confirms the result.
- The app must never silently fall back from machine-wide install to per-user install.
- The app must never automate unknown installer dialogs.
- The app must never reboot automatically.
- Catalog updates must be signed before use.

## Current Working Slice

The app can now:

- load bundled JSON Recipes
- validate each Recipe against `catalog/bundled/recipe.schema.json`
- load profiles
- show the catalog in a basic WinUI interface
- switch profiles
- select and deselect apps
- scan installed apps with read-only checks
- use Winget list, uninstall registry keys, configured file paths, and portable folder checks for read-only detection
- detect whether the app is running in portable, installed, or unknown/development mode
- use local app data for normal/unknown mode state and an app-local `data` folder when a `pantry.portable` marker exists
- show catalog, selection, plan, and detection summary counts
- remember last profile
- remember app choices per profile
- remember portable destination
- save latest scan results locally
- write simple operation logs locally
- show recent operation logs in the UI
- create a dry-run review plan
- show install/update/skip intent, provider, trust level, scope, administrator requirement, detection state, dependencies, and portable destination

The app still cannot install, update, uninstall, elevate, or change installed software.

## Conflicts Or Gaps Found

| Topic | Finding | Recommendation |
| --- | --- | --- |
| Phase 0 file list | `PRODUCT_SPEC.md` mentions `GAP_ANALYSIS.md`; `AGENTS.md` and the user request do not. | Do not create extra files yet. Capture gaps in this document and `STATUS.md`. |
| .NET version | Local SDK is .NET `10.0.301`. | Continue targeting .NET 10. |
| WinUI templates | Local SDK did not have WinUI templates installed. | UI project was hand-authored to avoid changing machine-wide template/workload state. |
| Windows App SDK version | Current project uses `Microsoft.WindowsAppSDK` `2.2.0`. | Keep pinned until there is a reason to upgrade. |
| Recipe format | Approved as JSON. | Continue JSON plus schema validation. |
| SQLite library | `Microsoft.Data.Sqlite.Core` is referenced to establish the boundary without the vulnerable native bundle from `Microsoft.Data.Sqlite`. | Add actual database initialization in a later persistence slice. |
| IPC mechanism | Strict IPC required but mechanism not chosen. | Start with a named pipe protocol between UI/core and elevated helper. Keep messages structured and validated. |
| Installer providers | Many providers listed. | Start with Winget only after detection is stable; real installs are still blocked. |
| Git status | Local Git repository tracks `origin/main`. | Commit and push after each safe slice. |

## Current Readiness

The repository is ready for review of the read-only Phase 1 slice.

- Build passed with 0 warnings and 0 errors.
- Tests passed: 28 total, 0 failed.
- Malformed Recipes are rejected by test.
- UI scan found no installer/elevation execution logic.
- Detection is read-only and limited to `winget list`, uninstall registry reads, configured file paths, and portable folder existence checks.
- Run-mode detection is read-only and uses a deliberate `pantry.portable` marker rather than guessing from drive type.
- Real install, update, uninstall, and elevation are not implemented yet.
