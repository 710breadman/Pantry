# Status

Last updated: 2026-06-26

## Current Phase

Phase 1: Read-only foundation slice

## Summary

The repository now contains a buildable read-only app slice. It can load the bundled catalog, validate Recipes, switch profiles, select apps, scan installed apps with read-only checks, save latest scan results, remember profile/app choices, remember portable destination, write simple operation logs, show recent logs, show summary counts, and produce a dry-run review plan.

The intended upstream repository is `https://github.com/710breadman/Pantry.git`. It is public and has the initial read-only slice pushed.

It does not install, update, uninstall, elevate, or change installed apps.

## Completed

- Read `AGENTS.md`.
- Read `docs/PRODUCT_SPEC.md`.
- Inspected repository files.
- Initialized this folder as a Git repository.
- Added `origin` remote for `https://github.com/710breadman/Pantry.git`.
- Added `.gitignore`.
- Created and pushed the first commit.
- Confirmed the intended GitHub repository: `710breadman/Pantry`.
- Created Phase 0 documentation:
  - `docs/CURRENT_STATE.md`
  - `docs/DECISIONS.md`
  - `docs/ARCHITECTURE.md`
  - `docs/V1_BACKLOG.md`
  - `docs/TEST_PLAN.md`
  - `docs/THREAT_MODEL.md`
  - `docs/STATUS.md`
- Created `ThePantry.slnx`.
- Created projects:
  - `src/Pantry.UI`
  - `src/Pantry.Domain`
  - `src/Pantry.Catalog`
  - `src/Pantry.Core`
  - `src/Pantry.Detection`
  - `src/Pantry.Infrastructure`
  - `tests/Pantry.Tests`
- Added JSON Recipe schema validation.
- Added bundled Recipes for 7-Zip, VLC, Steam, Firefox, and Microsoft Sysinternals Autoruns.
- Added profiles:
  - Gaming Setup
  - Living-Room Media PC
  - Repair Toolkit — Safe
- Added a basic WinUI shell for profile selection, app selection, and dry-run review.
- Added read-only Winget detection using `winget list`.
- Added read-only uninstall registry detection fallback.
- Added read-only portable folder detection.
- Fed detection state into the dry-run plan and UI.
- Added a status summary band for catalog, selection, plan, and detection counts.
- Added SQLite initialization with Windows SQLite provider.
- Added operation log storage.
- Added saved scan result storage.
- Added app settings storage.
- Added saved profile/app selections.
- Wired the UI to restore the last profile, app choices, and portable destination.
- Added a basic recent log viewer in the UI.
- Added xUnit tests for Recipe validation, catalog loading, profile defaults, and dry-run planning.
- Added xUnit tests for Winget output parsing, Winget command safety, and portable folder detection.
- Added xUnit tests for registry detection and Winget-to-registry fallback.
- Added xUnit tests for SQLite initialization, operation logs, and scan result persistence.
- Added xUnit tests for saved settings and per-profile app selections.
- Built the full solution successfully.
- Ran all tests successfully.

## Not Started

- Rich detection engine beyond Winget list, uninstall registry reads, and portable folder checks.
- Real queue execution.
- Elevated helper.
- Providers.
- Rich structured logs beyond the current basic log viewer.
- Installer, update, and uninstall execution.

## Current Recommendation

Do not begin real installation or elevation yet.

Next, add read-only file/version detection for Recipes that need evidence beyond Winget and registry.

## Approval Needed

Current approved choices:

| Choice | Recommended default |
| --- | --- |
| Recipe file format | JSON |
| First provider focus | Winget |
| First apps | 7-Zip, VLC, Steam, Firefox, Microsoft Sysinternals Autoruns |
| IPC direction | Named pipes for the future elevated helper |
| First working feature | Read-only catalog/profile/review slice with no installs yet |
| Test framework | xUnit |
| Local database | SQLite with the `Microsoft.Data.Sqlite` package family |

## Known Risks

| Risk | Current handling |
| --- | --- |
| App could become too broad too early | Keep catalog tiny until one full path works. |
| Admin helper could become unsafe | Design strict structured IPC before implementation. |
| Installer behavior can change | Require Recipe trust, verification date, fallback, and post-action detection. |
| Detection could be wrong | Use multiple evidence sources and confidence levels. |
| Catalog update could be tampered with | Require signed catalog updates and atomic rollback. |
| User may see too much technical detail | Keep UI plain by default and make details expandable. |
| Recipes are still `Experimental` | Do not execute them until real provider tests prove safe behavior. |
| Winget output format may vary | Parser is covered by tests, but more real-machine samples are needed. |
| Registry detection can be fuzzy | Registry fallback uses display-name matching and medium confidence only. |
| Logs are minimal | Operation logs and a basic viewer exist, but no filtering or detailed log screen yet. |

## Next Milestone

Recommended next phase: Phase 2F, read-only file/version detection.
