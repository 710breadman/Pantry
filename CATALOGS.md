# Catalog Contract

## Authority

`tool_catalog.json` is the canonical catalog for the maintained native app and
all new tool metadata.

`tool-catalog.json` is a legacy CLI compatibility catalog. It remains separate
because the PowerShell installer needs manager-specific fields that are not
yet represented by the native schema, and it contains legacy tools not exposed
by the curated native UI.

This split is intentional compatibility, not two equal sources of truth.

## Required invariants

- IDs are unique within each catalog.
- Both files parse successfully.
- At least 40 IDs overlap.
- Native-only IDs remain explicitly listed in the native regression test.
- Adding or changing a shared tool starts in `tool_catalog.json`.
- Any installer behavior needed by `setup-devtools.ps1` is mirrored into
  `tool-catalog.json` in the same change.
- Release builds embed and ship only `tool_catalog.json`.

## Current compatibility boundary

Known native-only IDs:

- `adb`
- `android-sdk`
- `android-studio`
- `dotnet-format`
- `nodejs-lts`
- `py-launcher`
- `python3`
- `wsl2`

The legacy catalog has broader package/extension coverage. Those entries remain
supported by the CLI but are not automatically promoted into the curated native
UI.

## Migration target

Future schema v2 should carry both native detection/goal metadata and legacy
installer metadata. Migration completes when:

1. One catalog drives both frontends.
2. Legacy-only entries are either migrated or explicitly deprecated.
3. `tool-catalog.json` and compatibility code are removed.
4. Contract tests reject any second catalog.
