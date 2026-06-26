# Test Plan

Last updated: 2026-06-26

## Testing Goal

The Pantry must be tested most heavily where mistakes can damage trust:

- wrong install state
- wrong install scope
- false success
- unsafe elevation
- bad catalog update
- broken queue recovery
- confusing failure handling

Plainly: the app should be boringly reliable before it becomes broad.

## Test Levels

| Level | Purpose | Examples |
| --- | --- | --- |
| Unit tests | Test small logic without Windows UI or real installers. | Recipe validation, trust decisions, queue planning, detection merge rules. |
| Integration tests | Test multiple app parts together with controlled external tools. | SQLite persistence, catalog loading, provider command construction, IPC validation. |
| Manual verification | Test real Windows behavior. | UAC prompt, actual install, actual uninstall, reboot handling. |
| Security tests | Try unsafe input and boundary failures. | Command injection, unsigned catalog, helper rejecting unknown jobs. |
| UX tests | Check whether normal users understand what will happen. | Review screen clarity, failure messages, technical details hidden by default. |

## Phase 1 Foundation Tests

| Area | Test |
| --- | --- |
| Solution | Builds cleanly. |
| Dependency injection | App startup container validates registrations when built. Direct xUnit coverage is deferred because referencing the WinUI project crashed the test host. |
| Settings | Settings can be saved and loaded. Current tests cover last profile, portable destination, and per-profile app choices. |
| SQLite | Database initializes and can write/read basic records. Current tests cover operation logs and scan result storage. |
| Review sessions | Dry-run review summaries are saved and listed. |
| Logging | Operation log records include category, message, details, and timestamp. Session/job IDs come later with real queue sessions. |
| Recipe model | Required Recipe fields are enforced. |
| Recipe schema | Invalid Recipe files fail validation with clear errors. |
| Portable mode | Installed, portable, and unknown/development layouts are detected correctly. Current tests cover marker-file portable mode, Program Files installed mode, unknown mode, and marker priority. |

## Phase 2 Catalog And Profile Tests

| Area | Test |
| --- | --- |
| Bundled catalog | Loads offline. |
| Catalog validation | Invalid catalog does not load. |
| Profile defaults | Defaults are selected and alternatives remain visible. |
| Custom choices | User choices survive restart. |
| Conflicts | Conflicting apps are shown before review. |

## Phase 3 Detection Tests

| Area | Test |
| --- | --- |
| Winget detection | Known package returns installed/update/not-installed evidence. Current tests cover parser behavior and safe `winget list` command construction. |
| Registry detection | Uninstall registry keys are parsed correctly. Current tests cover display-name matching and fallback after Winget misses. |
| File detection | Executable path and version rules work. Current tests cover configured path detection and fallback after Winget and registry miss. |
| Evidence merge | Conflicting evidence produces `Unknown` or lower confidence, not false `NotInstalled`. |
| Installed newer | Newer installed app is not downgraded silently. |
| Portable folder detection | Existing folder returns installed/current; missing folder returns not installed. |

## Phase 4 Queue Tests

| Area | Test |
| --- | --- |
| Dry run | Review plan changes no system state. |
| Dependencies | Dependencies are ordered before dependents. Current tests cover required dependency inclusion, dependency ordering, and cycle handling. |
| Conflicts | Known conflicts are shown before execution. Current tests cover symmetric warnings when selected apps conflict. |
| Failure isolation | Failed app does not block unrelated app. |
| Retry | Failed job can retry without duplicating completed work. |
| Cancellation | Cancelled queue records final states. |
| Reboot | Reboot-required result is shown but never triggers automatic reboot. |

## Phase 5 Elevation Tests

| Area | Test |
| --- | --- |
| Helper launch | Helper starts only through normal UAC flow. |
| IPC validation | Helper rejects malformed messages. |
| Allowlist | Helper rejects unknown provider/action. |
| No raw shell | Helper does not accept arbitrary command text. |
| Batch behavior | Multiple privileged jobs can share one approved batch where safe. |
| Result handling | Main app still verifies installation after helper reports success. |

## Provider Tests

Each provider needs tests for:

- command construction
- expected exit code mapping
- cancellation handling
- retry behavior
- uninstall behavior
- update behavior
- post-action detection requirement
- logs with sanitized command and output

## Catalog Update Tests

| Area | Test |
| --- | --- |
| Signature | Unsigned catalog update is rejected. |
| Tampering | Modified signed catalog is rejected. |
| Schema | Signed but invalid catalog is rejected. |
| Atomic swap | Failed update keeps current catalog. |
| Rollback | Last-known-good catalog can be restored. |
| Offline | Bundled catalog works without network. |

## First Vertical Slice Acceptance Tests

Use only a few apps, such as:

- 7-Zip
- VLC
- Steam
- Firefox
- one safe portable repair utility

The first complete path must prove:

- catalog display
- profile selection
- installed-app detection
- review screen
- administrator elevation
- installation
- post-install verification
- updates
- uninstall
- logs
- retry handling
- portable destination
- Recipe trust

## Test Data Strategy

Use fake Recipes and fake providers for most unit tests.

Use real providers only in integration or manual tests where the machine state can be controlled. This avoids tests that accidentally install software during normal development.

## What Not To Claim

Do not say a feature is tested unless the exact test was run.

Do not treat a successful installer exit code as a passing install test unless detection confirms the app afterward.

Do not weaken a test to make the build pass.
