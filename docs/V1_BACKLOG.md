# V1 Backlog

Last updated: 2026-06-26

This backlog is ordered to prove safety and correctness before catalog size.

## Phase 0: Discovery

| Item | Status | Acceptance |
| --- | --- | --- |
| Read local instructions and product spec | Done | `AGENTS.md` and `docs/PRODUCT_SPEC.md` were read. |
| Inspect repository | Done | Current state captured in `docs/CURRENT_STATE.md`. |
| Create architecture docs | Done | Phase 0 docs exist. |
| Propose first small working version | Done | User approved the read-only catalog/profile/review slice. |

## Phase 1: Foundation

| Item | Priority | Acceptance |
| --- | --- | --- |
| Create solution and project structure | Done | Buildable solution with separated UI/core/domain/catalog/infrastructure/test projects. |
| Add dependency injection | Not started | Services are currently composed directly in the UI shell. |
| Add structured logging | Partial | App writes simple operation logs and shows recent logs; session/job correlation comes later. |
| Add settings service | Done | App stores last profile, portable destination, and app choices per profile. |
| Add SQLite persistence | Done | Database initializes and stores operation logs plus latest scan results. |
| Add portable/installed mode detection | Done | App can tell whether it is running from installed, portable, or unknown/development layout. |
| Add Recipe domain models | Done | Core Recipe types compile and are covered by unit tests. |
| Add Recipe schema validation | Done | Invalid Recipe files are rejected with clear errors. |

## Phase 2: Catalog And Profiles

| Item | Priority | Acceptance |
| --- | --- | --- |
| Add bundled offline catalog | Done | App can load a local catalog without internet. |
| Add 3-5 initial apps | Done | Catalog includes 7-Zip, VLC, Steam, Firefox, and Microsoft Sysinternals Autoruns. |
| Add profile definitions | Done | Three profiles can preselect recommended apps. |
| Add catalog display | Done | User can browse apps in a basic list. |
| Add profile selection UI | Done | User can choose a profile and modify selected apps. |
| Save profile choices | Done | Profile choices survive restart through SQLite settings tables. |

## Phase 3: Detection

| Item | Priority | Acceptance |
| --- | --- | --- |
| Winget detection | Done | Known Winget apps can be detected with evidence from `winget list`. |
| Registry detection | Done | Installed desktop apps can be detected from uninstall registry keys with medium confidence. |
| File/version detection | Done | Recipe can check configured executable paths and read file version where available. |
| Detection confidence model | Done | Results include state, confidence, and evidence. |
| Portable folder detection | Done | Portable app path can be checked without changing files. |
| Dashboard installed state | Partial | UI shows catalog, selection, plan, detection, and run-mode summary counts. |

## Phase 4: Queue And Review

| Item | Priority | Acceptance |
| --- | --- | --- |
| Dry-run queue planner | Done | App creates dependency-aware install/update/skip plan without changing system. |
| Review screen | Done | User sees app, action, provider, scope, trust, admin need, dependencies, conflicts, and portable destination. |
| Dependency ordering | Done | Known dependencies are included in dry-run review and ordered before dependents. |
| Conflict warnings | Done | Known selected conflicts are visible before execution. |
| Retry model | High | Failed jobs can be retried safely. |
| Cancellation model | High | User can cancel without corrupting queue state. |
| Failure isolation | High | One failed app does not stop unrelated jobs. |

## Phase 5: Elevated Execution

| Item | Priority | Acceptance |
| --- | --- | --- |
| Elevated helper project | High | Helper can be launched through UAC for approved batches. |
| Strict IPC | High | Helper accepts structured messages only. |
| Job allowlist validation | High | Helper rejects unknown provider/action combinations. |
| One elevation per privileged batch | Medium | Multiple admin installs can run after one UAC approval where safe. |
| Post-install verification | High | Success requires detection after install. |

## Phase 6: Providers

| Item | Priority | Acceptance |
| --- | --- | --- |
| Winget install provider | High | First-slice Winget apps can install with expected scope. |
| Winget update provider | High | Known installed apps can be checked/updated when trusted. |
| Winget uninstall provider | High | Known apps can be uninstalled after confirmation. |
| MSI provider | Medium | Supports trusted MSI Recipes with silent and machine-scope args. |
| Official EXE provider | Medium | Supports only verified silent installers. |
| Portable archive provider | Medium | Can deploy safe portable tools to managed folders. |

## Phase 7: Updates And Uninstall

| Item | Priority | Acceptance |
| --- | --- | --- |
| Launch update check | Medium | App checks once daily and caches result. |
| Manual refresh | Medium | User can request fresh update scan. |
| Update groups | Medium | Updates are grouped as automatic, guided, manual, or unsupported. |
| Basic uninstall | High | Recognized apps can be uninstalled by trusted method after confirmation. |

## Phase 8: Portable Toolkit

| Item | Priority | Acceptance |
| --- | --- | --- |
| Destination chooser | High | User can choose USB, local drive, or custom folder. |
| Managed folder layout | High | Portable tools are placed in predictable folders. |
| Portable profiles | Medium | Profiles can live beside the app in portable mode. |
| Portable removal | Medium | Managed portable tool removal works when safe. |

## Phase 9: Signed Catalog Updates

| Item | Priority | Acceptance |
| --- | --- | --- |
| Catalog signature verification | High | Unsigned or invalid catalog updates are rejected. |
| Atomic catalog swap | High | Broken update cannot corrupt current catalog. |
| Last-known-good rollback | High | App can return to previous valid catalog. |
| Offline fallback | High | Bundled catalog remains usable offline. |

## Phase 10: Hardening

| Item | Priority | Acceptance |
| --- | --- | --- |
| Accessibility pass | Medium | Keyboard navigation, readable labels, and screen reader basics work. |
| Performance pass | Medium | UI remains responsive during scans and queue work. |
| Crash recovery | High | Interrupted sessions can be inspected and resumed where safe. |
| Corrupt DB recovery | Medium | App handles damaged local state gracefully. |
| Release packaging | Medium | Installed and portable editions can be produced repeatably. |

## Recommended First Small Working Feature

Build a read-only catalog and profile review slice. This is now implemented.

The first feature should:

- load a bundled catalog with 5 apps: done
- validate Recipe files: done
- show profiles with default selections: done
- produce a review plan: done
- install nothing yet: done
- run detection in dry-run mode only if available: done for Winget list, uninstall registry reads, configured file paths, portable folder checks, and Pantry run-mode detection
- write logs: done for simple operation logs and recent log display

Next useful slice: dependency injection composition root.
