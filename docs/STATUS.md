# Status

Last updated: 2026-06-26

## Current Phase

Phase 1: Read-only foundation slice

## Summary

The repository now contains a buildable read-only app slice. It can load the bundled catalog, validate Recipes, switch profiles, select apps, scan installed apps with read-only checks, detect its own portable/installed/unknown run mode, save latest scan results, remember profile/app choices, remember portable destination, write simple operation logs, show recent logs, show summary counts, produce a dependency/conflict-aware dry-run review plan, create and save a read-only queue plan, save review session summaries, prune old review/queue sessions, and show the saved-review count. Startup services are composed through dependency injection.

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
- Added read-only configured file path/version detection fallback.
- Added read-only portable folder detection.
- Added read-only Pantry run-mode detection:
  - `pantry.portable` beside the app means portable mode.
  - Program Files location means installed mode.
  - development or unrecognized paths mean unknown mode.
- Wired portable mode to use an app-local `data` folder for SQLite state.
- Fed detection state into the dry-run plan and UI.
- Added dependency-aware dry-run planning:
  - known dependencies are included in review
  - dependencies are ordered before dependents
  - dependency cycles do not duplicate review items
- Added conflict warnings to the dry-run review when selected apps conflict.
- Added `src/Pantry.Queue` for read-only queue session/job planning.
- Queue planning now:
  - includes install/update items
  - skips skip items
  - preserves dry-run order
  - marks non-`VerifiedUnattended` or conflicting jobs as needing review
- Added SQLite storage for queue sessions and queue jobs.
- UI refresh now stores the read-only queue plan and shows queue job counts in the plan summary.
- Added queue-session pruning. Default keeps latest 100 queue sessions.
- Added a status summary band for catalog, selection, plan, detection counts, and run mode.
- Added SQLite initialization with Windows SQLite provider.
- Added operation log storage.
- Added saved scan result storage.
- Added app settings storage.
- Added saved profile/app selections.
- Wired the UI to restore the last profile, app choices, and portable destination.
- Added a basic recent log viewer in the UI.
- Added a small dependency injection composition root for startup service wiring.
- Added `Microsoft.Extensions.DependencyInjection` to the UI project.
- Added SQLite storage for dry-run review session summaries.
- Added a saved-review count to the summary band.
- Added review-session pruning. Default keeps latest 100 reviews.
- Added xUnit tests for Recipe validation, catalog loading, profile defaults, and dry-run planning.
- Added xUnit tests for Winget output parsing, Winget command safety, and portable folder detection.
- Added xUnit tests for registry detection and Winget-to-registry fallback.
- Added xUnit tests for file path detection and Winget/registry-to-file fallback.
- Added xUnit tests for portable/installed/unknown run-mode detection.
- Added xUnit tests for SQLite initialization, operation logs, and scan result persistence.
- Added xUnit tests for saved settings and per-profile app selections.
- Added xUnit tests for dependency ordering and dependency-cycle handling.
- Added xUnit tests for symmetric conflict warnings.
- Added xUnit tests for read-only queue planning.
- Added xUnit tests for queue session storage.
- Added xUnit tests for queue-session pruning.
- Added xUnit tests for review session storage.
- Added xUnit tests for review-session pruning.
- Built the full solution successfully.
- Ran all tests successfully.

## Not Started

- Rich detection engine beyond Winget list, uninstall registry reads, configured file paths, portable folder checks, and app run-mode detection.
- Real queue execution.
- Elevated helper.
- Providers.
- Rich structured logs beyond the current basic log viewer.
- Installer, update, and uninstall execution.

## Current Recommendation

Do not begin real installation or elevation yet.

Next, add a tiny queue summary count in the UI.

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
| File path detection can miss custom installs | File fallback only checks configured paths and uses low/medium confidence. |
| Portable mode depends on marker file | This is intentional for safety; a packaged portable build should create `pantry.portable`. |
| Dependencies can surprise users | Dependencies are visible in the dry-run reason before any execution exists. |
| Conflicts are warnings only | Current conflict handling warns in review; later queue work should decide whether conflicts block execution. |
| DI startup wiring is not xUnit-covered | A direct WinUI project reference crashed the test host, so the app uses container startup validation and normal build coverage for now. |
| Logs are minimal | Operation logs and a basic viewer exist, but no filtering or detailed log screen yet. |

## Next Milestone

Recommended next phase: show queue-session count, still with no real installs.
