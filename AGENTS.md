# The Pantry — Codex Instructions

Read `docs/PRODUCT_SPEC.md` before planning or coding.

## Product

The Pantry is a Windows 11 app for:

- browsing a highly curated app catalog
- installing selected apps in one queue
- using setup profiles such as Gaming, Media, and Repair
- checking for app updates
- basic uninstall support
- portable USB use and normal installed use

## Priorities

1. Correct
2. Safe
3. Reliable
4. Quiet
5. Install for all users when supported
6. Clear
7. Fast
8. Attractive

## Default technology

- C#
- .NET 10
- WinUI 3
- Windows App SDK
- MVVM Toolkit
- SQLite
- structured logging
- unit and integration tests

Keep the main engine separate from the UI.

## Important terminology

A Recipe is The Pantry's complete set of instructions for installing, detecting, updating, and uninstalling an app.

## Safety rules

- Main interface must not run as administrator.
- Use a separate elevated helper for administrator work.
- Never bypass UAC.
- Prefer per-machine or all-users installation.
- Never silently fall back to per-user installation.
- Never automate unknown installer dialogs with keyboard input.
- Never report success without checking that the app installed.
- Never reboot automatically.
- Never run arbitrary downloaded scripts.
- Never use unsigned catalog updates.
- Never install drivers, BIOS, firmware, Windows tweaks, or debloating tools automatically in v1.

## Work style

Before editing:

1. Inspect the repository.
2. Read the specification.
3. Explain the current state.
4. Plan the smallest useful feature.
5. Then implement.

After meaningful changes:

1. Build the project.
2. Run relevant tests.
3. Review errors and failure paths.
4. Fix serious defects.
5. Update project documentation.

Never claim something was tested unless it was actually tested.

Prefer small changes over giant rewrites.

Do not expand the catalog until one complete test path works.

## First test path

Use only a few apps initially, such as:

- 7-Zip
- VLC
- Steam
- Firefox
- one portable repair utility

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

## First task behavior

Do not immediately build the full application.

First create:

- `docs/CURRENT_STATE.md`
- `docs/DECISIONS.md`
- `docs/ARCHITECTURE.md`
- `docs/V1_BACKLOG.md`
- `docs/TEST_PLAN.md`
- `docs/THREAT_MODEL.md`
- `docs/STATUS.md`

Then propose the first small working version.