# Architecture

Last updated: 2026-06-26

## Goal

The Pantry should be split into small parts so the user interface stays simple and the risky work is isolated.

Plainly: the screen the user clicks should not be the same code that runs administrator installs.

## Proposed Solution Structure

```text
ThePantry.slnx
src/
  Pantry.UI/
  Pantry.Core/
  Pantry.Domain/
  Pantry.Catalog/
  Pantry.Detection/
  Pantry.Infrastructure/
tests/
  Pantry.Tests/
catalog/
  bundled/
```

More projects such as `Pantry.Providers`, `Pantry.Queue`, `Pantry.Elevation`, `Pantry.Portable`, and `Pantry.Logging` should be added when those features begin.

## Module Responsibilities

| Module | Responsibility |
| --- | --- |
| `Pantry.UI` | WinUI 3 screens, navigation, view models, and user-facing state. |
| `Pantry.Core` | Application workflows that coordinate catalog, detection, queue planning, and execution. |
| `Pantry.Domain` | Core types such as Recipe, app identity, trust level, install action, detection result, queue job, and provider result. |
| `Pantry.Infrastructure` | SQLite database initialization, operation logs, saved scan results, saved settings, profile selections, and future platform adapters. |
| `Pantry.Catalog` | Load bundled catalog and validate Recipe schema. Later it will apply local overrides and handle signed catalog updates. |
| `Pantry.Detection` | Runs read-only detection. Current checks are Winget list parsing and portable folder existence. |
| Future `Pantry.Providers` | Provider implementations such as Winget, MSI, EXE, Microsoft Store, GitHub release, and portable archive. |
| Future richer detection | Registry, AppX, file versions, services, portable managed folders, and Pantry history. |
| Future `Pantry.Queue` | Plan executable jobs, order dependencies, handle retries, cancellation, failure isolation, and final job states. |
| Future `Pantry.Elevation` | Broker communication with the elevated helper and validate privileged job requests. |
| Future `Pantry.Portable` | Portable mode detection, portable destination choices, managed folders, and portable tool deployment. |
| Future `Pantry.Logging` | Structured operation logging and log index records. |
| `Pantry.Tests` | Unit tests for planning, Recipes, trust, detection mapping, queue behavior, and validation. |
| Future `Pantry.IntegrationTests` | Tests for provider behavior, database persistence, helper IPC, and controlled install flows. |

## Main Runtime Flow

```text
User chooses profile/apps
        |
        v
Catalog service resolves Recipes
        |
        v
Detection service checks current machine state with read-only checks
        |
        v
Dry-run planner creates a review plan
        |
        v
Review screen shows actions, scope, trust, admin needs, risks
        |
        v
STOP in current Phase 1 slice
```

Later phases will continue:

```text
Queue executor runs safe work
        |
        v
Elevation broker sends approved admin jobs to elevated helper when needed
        |
        v
Providers install/update/uninstall
        |
        v
Detection service verifies result
        |
        v
Logs and SQLite state are updated
```

## Elevation Boundary

The main UI runs as a normal user process.

When administrator work is needed:

1. The queue planner builds structured jobs.
2. The review screen shows the user what will happen.
3. The user approves the batch.
4. The app starts the elevated helper through UAC.
5. The helper validates each job against allowed providers and Recipe rules.
6. The helper runs only approved actions.
7. The helper returns structured results.
8. The main app performs post-action detection before calling anything successful.

The helper must not accept raw shell text from the UI.

## Recipe Model

The current v1 Recipe model includes:

- app ID
- catalog display name
- short description
- category
- homepage
- portable flag
- trust level
- source type
- source identifier or URL
- install scope support
- administrator requirement
- detection rules
- update method
- uninstall method
- dependencies
- conflicts
- expected exit codes
- reboot behavior
- verification date
- test evidence

Install commands, silent arguments, hashes, signatures, fallbacks, and richer version rules must be added before real execution.

## Data Storage

Use SQLite for local state:

| Table area | Purpose |
| --- | --- |
| apps and recipes | Track known catalog identity and selected Recipe versions. |
| profiles and selections | Save profile defaults and user customization. |
| detection evidence | Store what was found and how confident the app is. |
| queue sessions and jobs | Record each planned and executed job. |
| catalog versions | Track bundled, active, previous, and updated catalog versions. |
| logs index | Make logs searchable without loading raw log files into the UI. |
| portable locations | Remember managed portable destinations. |

Current tables:

- `operation_logs`
- `scan_results`
- `app_settings`
- `profile_selections`

## Trust Model

Trust levels:

1. `VerifiedUnattended`
2. `VerifiedGuided`
3. `ManualOfficial`
4. `Experimental`
5. `Blocked`

Only `VerifiedUnattended` can run unattended.

If behavior changes unexpectedly, The Pantry should downgrade or pause the Recipe instead of continuing blindly.

## Provider Strategy

Providers should return structured results:

- `Success`
- `Failed`
- `Cancelled`
- `RebootRequired`
- `UserActionRequired`
- `NotApplicable`
- `Unknown`

The first real provider should be Winget because it gives us a known integration point. MSI, official EXE, Microsoft Store, GitHub release, portable archive, and manual official providers can follow.

The current detection slice executes `winget list` for Winget-backed Recipes. It does not run `winget install`, `winget upgrade`, or `winget uninstall`.

## UI Shape

V1 should use a quiet, practical desktop UI:

- profile selection
- basic catalog list
- read-only installed-app scan button
- review screen
- portable destination field

Future UI work should add:

- dashboard
- cards and compact rows
- app detail panel
- queue progress
- failure details
- logs view
- settings for portable/installed mode

The UI should explain risks in normal language and keep technical details expandable.

## Testability

Most logic should be testable without WinUI:

- Recipe validation
- trust decisions
- detection merging
- queue planning
- retry handling
- failure isolation
- catalog rollback
- provider result mapping
- elevation job validation

This is why the engine should not live inside button-click handlers.
