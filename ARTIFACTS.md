# Artifact Policy

## Source-controlled inputs

- `src/`, `gui/`, `tests/`
- `setup-devtools.ps1`, `build-release.ps1`
- `config.json`
- `tool_catalog.json` (canonical native catalog)
- `tool-catalog.json` (legacy CLI compatibility catalog)
- Project documentation and build/CI configuration

## Generated local output

These paths are disposable and ignored by Git:

- `**/bin/`, `**/obj/`
- `release/`
- `artifacts/`
- `devtools_setup_report/`
- `devtools_validation_projects/`
- Root and GUI `*.log` files
- Generated `gui_backend_runner_*.ps1` files

Do not use stored report output as current machine or project status. Reports
are point-in-time machine snapshots and may contain local usernames or paths.

## Release output

`build-release.ps1` recreates `release\RecipeCard`. Published deliverables should
be attached to a versioned release, not committed to source. Each release must
include:

- `RecipeCard.exe`
- `README_RUN.txt`
- `tool_catalog.json`
- `config.default.json`
- `build-metadata.json`
- `SHA256SUMS`

Distributed public builds should be Authenticode-signed. Unsigned local builds
must be labeled as development artifacts.

## Retention

- Local build/test output: delete any time.
- Runtime reports: retain only for active diagnosis; redact before sharing.
- Release artifacts: retain through release storage, keyed by version and
  source commit.
