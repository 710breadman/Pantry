# The Pantry

![The Pantry icon](assets/the-pantry-icon.png)

Professional Windows 11 developer-environment setup, detection, updates, and
repair for app creation, automation, scripting, GitHub workflows, Python, Java,
C#/.NET, Node/TypeScript, Android, Linux/WSL, and code-quality tooling.

The CLI is still available. The new GUI is a native .NET WPF dashboard backed
by the shared .NET Core project. The PowerShell CLI currently retains a
separate compatibility catalog; see `CATALOGS.md`.

Project coordination:

- [`PROJECT_STATUS.md`](PROJECT_STATUS.md): current verified state and issues
- [`ROADMAP.md`](ROADMAP.md): ordered future improvements
- [`CONTRIBUTING.md`](CONTRIBUTING.md): change and verification rules
- [`CATALOGS.md`](CATALOGS.md): native/legacy catalog contract
- [`ARTIFACTS.md`](ARTIFACTS.md): generated output and release policy

## Quick Start

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\setup-devtools.ps1 -Preview
.\setup-devtools.ps1 -Apply
```

Launch the GUI:

```powershell
.\setup-devtools.ps1 -Gui
```

Build the double-clickable release EXE:

```powershell
.\build-release.ps1
```

The release is written to `release\ThePantry\ThePantry.exe`. Normal users can double-click that EXE; no command prompt or Visual Studio is required.

Build and run directly:

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build .\src\DevToolsCurator.slnx
& "C:\Program Files\dotnet\dotnet.exe" run --project .\src\DevToolsCurator.App\DevToolsCurator.App.csproj
```

## CLI Modes

```powershell
.\setup-devtools.ps1 -Preview
.\setup-devtools.ps1 -Apply
.\setup-devtools.ps1 -Repair
.\setup-devtools.ps1 -Update
.\setup-devtools.ps1 -Apply -Tier Core
.\setup-devtools.ps1 -Apply -Tier Recommended
.\setup-devtools.ps1 -Apply -Tier Full
.\setup-devtools.ps1 -Apply -Unattended
.\setup-devtools.ps1 -SelfTest
```

## GUI

Main dashboard:

- Readiness score
- Selected goal profile
- Critical missing count
- Updates available count
- Broken/PATH/reboot issues count
- Recommended next action

Primary actions:

- Start Wizard
- Install Recommended
- Update Installed
- Repair Issues
- Rescan
- Export Summary

The Tools view has grouped sections, search, filters, concise rows, and a details panel. Row actions are explicit: Install, Update, Repair, Fix PATH, Open, and Info only appear when valid for that tool. The main dashboard intentionally avoids raw logs and giant tables.

The wizard opens as a modal setup assistant with Back, Next, Cancel, Save Plan, and Apply Recommended. It does not install during questions; installs only start from the final review step.

## Release Runtime

The published EXE uses AppData by default:

- config: `%AppData%\ThePantry\config.json`
- reports: `%AppData%\ThePantry\reports`
- cache: `%LocalAppData%\ThePantry\cache`

Portable mode is enabled only when `config.json`, `reports`, `cache`,
`.portable`, or `ThePantry.portable` exists next to `ThePantry.exe`. A loose
`tool_catalog.json` beside the EXE can override the embedded catalog, but if
that file is missing or corrupt the app falls back to its embedded default
catalog and still opens.

## Detection

The .NET engine detects tools through multiple sources:

- PATH executable lookup
- common install paths
- registry uninstall keys in HKLM/HKCU, 32-bit and 64-bit views
- winget list/upgrade data
- app execution alias paths
- environment variables such as `JAVA_HOME`, `ANDROID_HOME`, `ANDROID_SDK_ROOT`, `DOTNET_ROOT`
- version commands
- tool-specific probes such as WSL distro listing and GitHub auth status

Status values are deliberately separate:

- `Installed_Current`
- `Installed_Outdated`
- `Installed_NotOnPath`
- `Missing_Recommended`
- `Missing_Optional`
- `Broken`
- `AuthNeeded`
- `RebootNeeded`

Example: 7-Zip installed at `C:\Program Files\7-Zip\7z.exe` is detected as installed even when `7z.exe` is not on PATH.

## Wizard Profiles

The goal wizard supports:

- Windows desktop apps
- Windows CLI tools/scripts
- Python automation
- C#/.NET apps
- Java apps
- Web/TypeScript apps
- Android apps
- Linux/cross-platform tools
- GitHub/open-source projects
- AI/Codex-ready workstation
- Everything/dev workstation

Heavy tools such as Android Studio, Docker Desktop, WSL2, and full Visual Studio remain opt-in or goal-driven. The Best Dev Stack excludes Android Studio, Docker Desktop, and WSL2 by default.

## Catalog

`tool_catalog.json` is the GUI/shared-engine catalog. Each tool contains:

- `tool_id`
- `display_name`
- `category`
- `description`
- `why_it_matters`
- `used_for`
- `install_method`
- `install_tier`
- `is_heavy`
- `importance_score`
- `goal_tags`
- `winget_ids`
- `fallback_urls`
- `detection`

Formal schema: `schemas/tool-catalog-v2.schema.json`. Runtime loads also enforce
semantic validation before any catalog is used.

Friendly names are shown in the GUI. Raw package IDs stay in details and install commands.

## Reports

The GUI writes:

- `devtools_setup_report\summary.md`
- `devtools_setup_report\tools.csv`
- `devtools_setup_report\issues.json`
- `devtools_setup_report\install_plan.json`
- `devtools_setup_report\last_scan.json`
- `devtools_setup_report\repair_suggestions.md`
- `devtools_setup_report\ui_audit.json`
- `devtools_setup_report\action_results.json`

The older CLI report files remain supported for CLI runs.

## Safety

- Uses winget first.
- Validates winget IDs before automatic install where practical.
- Uses official fallback URLs only.
- Does not store GitHub tokens.
- Does not force `gh auth login`; it shows the command when needed.
- Does not overwrite Git `user.name` or `user.email`.
- Does not permanently weaken PowerShell execution policy.
- Does not uninstall tools unless future explicit UI is added for that.
- PATH repair deduplicates entries and asks before writing user PATH.

## Tests

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build .\src\DevToolsCurator.slnx
& "C:\Program Files\dotnet\dotnet.exe" run --project .\src\DevToolsCurator.Tests\DevToolsCurator.Tests.csproj
& "C:\Program Files\dotnet\dotnet.exe" run --project .\src\DevToolsCurator.App\DevToolsCurator.App.csproj -- --smoke-test
& "C:\Program Files\dotnet\dotnet.exe" run --project .\src\DevToolsCurator.App\DevToolsCurator.App.csproj -- --ui-self-check
& "C:\Program Files\dotnet\dotnet.exe" run --project .\src\DevToolsCurator.App\DevToolsCurator.App.csproj -- --contract-self-check
.\build-release.ps1
```

Legacy CLI tests:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\setup-devtools.ps1 -SelfTest
```

## Research Notes

The curated stack uses official/vendor documentation and package-manager data as the primary signal:

- [Microsoft WinGet documentation](https://learn.microsoft.com/windows/package-manager/winget/)
- [Microsoft WSL install documentation](https://learn.microsoft.com/windows/wsl/install)
- [Microsoft WPF overview](https://learn.microsoft.com/dotnet/desktop/wpf/overview/)
- [Android Studio](https://developer.android.com/studio)
- [Android SDK platform tools](https://developer.android.com/tools/releases/platform-tools)
- [.NET downloads](https://dotnet.microsoft.com/download)
- [Python downloads for Windows](https://www.python.org/downloads/windows/)
- [Node.js downloads](https://nodejs.org/)
- [Eclipse Temurin](https://adoptium.net/temurin/)
- [uv installation](https://docs.astral.sh/uv/getting-started/installation/)
