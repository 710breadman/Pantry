# Recipe Card Roadmap

Roadmap baseline: 2026-06-28

`PROJECT_STATUS.md` owns current facts and open issues. This file owns planned
improvements. Work top-down unless a production defect or security issue
requires reprioritization.

## Phase 0 — Baseline and reproducibility

Status: In progress

Goal: every change has a reviewable diff and repeatable verification.

- [x] Initialize local Git repository on `main`
- [x] Add `.gitignore` and `.gitattributes`
- [x] Pin .NET SDK with `global.json`
- [x] Add deterministic shared build properties
- [x] Add Windows GitHub Actions workflow
- [x] Define artifact ownership and retention
- [x] Isolate PATH-repair regression test from real user PATH
- [x] Create initial reviewed commit
- [ ] Select and add project license
- [x] Connect intended Git remote (`710breadman/Recipe-Card`)
- [ ] Configure branch protections
- [x] Confirm CI passes on hosted `windows-latest`

Exit criteria: clean clone restores, builds, and passes all checks using only
documented commands.

## Phase 1 — Catalog convergence

Status: In progress

Goal: one schema and source of truth for native app and PowerShell CLI.

- [x] Declare `tool_catalog.json` canonical
- [x] Document legacy compatibility boundary
- [x] Add uniqueness/overlap/native-only contract test
- [x] Design catalog schema v2 with detection, goal, and installer fields
- [x] Add schema validation
- [ ] Convert legacy-only entries or mark each deprecated
- [ ] Make `setup-devtools.ps1` consume schema v2
- [ ] Remove `tool-catalog.json`
- [ ] Remove legacy catalog compatibility tests

Exit criteria: one catalog drives both frontends; CI rejects schema or ID drift.

## Phase 2 — Test and architecture modernization

Status: Planned

Goal: standard test tooling, smaller change surfaces, predictable failures.

- [x] Add centralized async-command exception reporting
- [x] Run legacy Pester tests in CI
- [ ] Convert native console suite to xUnit, NUnit, or MSTest
- [ ] Run native tests with `dotnet test`
- [ ] Add mocks/interfaces for process, registry, filesystem, winget, and env
- [ ] Split `MainViewModel` into scan, operation, navigation, and export units
- [ ] Split `ToolDetector` into source-specific detectors
- [ ] Move or delete retired PowerShell dashboard implementation
- [ ] Add cancellation and timeout tests for long operations
- [ ] Add report redaction tests for usernames, tokens, and local paths

Exit criteria: normal tests do not mutate machine state; core behavior can be
tested without launching processes or reading host registry.

## Phase 3 — Release engineering and trust

Status: In progress

Goal: traceable, verifiable Windows releases.

- [x] Add semantic version input and assembly metadata
- [x] Emit source/build metadata
- [x] Emit SHA-256 checksum
- [x] Add optional Authenticode signing hook
- [ ] Obtain/manage code-signing certificate
- [ ] Require signing for public release jobs
- [ ] Add trusted timestamp and signature verification gate
- [ ] Build releases from clean tagged commits in CI
- [ ] Publish checksums and metadata with release
- [ ] Add upgrade/rollback documentation

Exit criteria: every public EXE maps to a tag/commit, has a published checksum,
and carries a valid timestamped signature.

## Phase 4 — Product reliability

Status: Planned

Goal: safe operations and useful diagnostics across supported Windows setups.

- [ ] Add structured logs with operation IDs and redaction
- [ ] Add resumable install/update queues
- [ ] Persist cancellation-safe operation state
- [ ] Add winget source-health diagnostics
- [ ] Add explicit elevation UX and least-privilege checks
- [ ] Add reboot-required continuation guidance
- [ ] Add offline/degraded-mode behavior
- [ ] Add catalog cache freshness and corruption recovery tests
- [ ] Test Windows 11 standard-user and administrator scenarios

Exit criteria: interrupted or failed operations leave clear state and safe
recovery instructions.

## Phase 5 — Product improvements

Status: Planned

Goal: improve recommendation quality and operator control after foundations
are stable.

- [ ] Add plan diff before apply
- [ ] Add per-tool reason/exclusion display
- [ ] Add import/exportable workstation profiles
- [ ] Add organization-managed policy overlays
- [ ] Add dependency/conflict visualization
- [ ] Add historical readiness trends without storing sensitive host data
- [ ] Add localization/accessibility review
- [ ] Evaluate ARM64 publish target

Exit criteria: users can understand, reproduce, and share setup intent without
blindly applying changes.

## Prioritization Rules

1. Security/data-loss defect
2. Build/release break
3. Detection or operation correctness
4. Catalog convergence
5. Testability/maintainability
6. New features

Each roadmap item requires:

- linked issue/status ID
- acceptance criteria
- relevant automated test
- `PROJECT_STATUS.md` update
