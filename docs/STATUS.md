# Status

Last updated: 2026-06-26

## Current Phase

Phase 1: Read-only foundation slice

## Summary

The repository now contains a buildable read-only app slice. It can load the bundled catalog, validate Recipes, switch profiles, select apps, and produce a dry-run review plan.

The intended upstream repository is `https://github.com/710breadman/Pantry.git`. It is public and currently empty.

It does not install, update, uninstall, elevate, or detect installed apps yet.

## Completed

- Read `AGENTS.md`.
- Read `docs/PRODUCT_SPEC.md`.
- Inspected repository files.
- Initialized this folder as a Git repository.
- Added `origin` remote for `https://github.com/710breadman/Pantry.git`.
- Added `.gitignore`.
- Could not create the first commit because local Git identity is not configured.
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
  - `src/Pantry.Infrastructure`
  - `tests/Pantry.Tests`
- Added JSON Recipe schema validation.
- Added bundled Recipes for 7-Zip, VLC, Steam, Firefox, and Microsoft Sysinternals Autoruns.
- Added profiles:
  - Gaming Setup
  - Living-Room Media PC
  - Repair Toolkit — Safe
- Added a basic WinUI shell for profile selection, app selection, and dry-run review.
- Added xUnit tests for Recipe validation, catalog loading, profile defaults, and dry-run planning.
- Built the full solution successfully.
- Ran all tests successfully.

## Not Started

- SQLite database initialization and persistence.
- Detection engine.
- Real queue execution.
- Elevated helper.
- Providers.
- Structured logs.
- Installer, update, and uninstall execution.

## Current Recommendation

Do not begin real installation or elevation yet.

Next, add read-only installed-app detection. That means the app can compare selected Recipes to the current machine and mark items as install, update, skip, or unknown without changing the PC.

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

## Next Milestone

Recommended next phase: Phase 2A, read-only detection.

Before the first commit, set local Git identity for this repo only:

```powershell
git config --local user.name "Your Name"
git config --local user.email "you@example.com"
git add .
git commit -m "Initial Pantry read-only slice"
```
