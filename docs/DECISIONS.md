# Decisions

Last updated: 2026-06-26

This document records product and engineering decisions so the project does not re-argue the same basics later.

## Accepted Decisions

| ID | Decision | Plain-English reason |
| --- | --- | --- |
| D-001 | Build the main app as an unelevated Windows desktop app. | A normal app should not run with administrator power all the time. That reduces damage if there is a bug. |
| D-002 | Use a separate elevated helper for administrator work. | Installing machine-wide apps often needs admin rights, but only the install worker should get those rights. |
| D-003 | Keep the engine independent from the UI. | The app should be testable without clicking through screens, and a future shell should not require rewriting core logic. |
| D-004 | Start with a tiny curated catalog, not a broad app search. | Safety depends on tested Recipes. A huge catalog would create risk before the engine is trustworthy. |
| D-005 | Use Recipes as the central automation unit. | Recipes keep installation, detection, updates, uninstall, trust, and failure handling in one predictable shape. |
| D-006 | Only `VerifiedUnattended` Recipes can run without user interaction. | If a Recipe is not proven safe, the app should guide or pause instead of guessing. |
| D-007 | Post-action detection is required before success. | An installer exit code alone is not enough proof that the app is actually installed. |
| D-008 | Prefer machine-wide installs when verified. | The Pantry's goal is reliable system setup, not hidden per-user installs. |
| D-009 | Never silently fall back to per-user install. | Silent fallback would surprise the user and make detection/update behavior confusing. |
| D-010 | Use SQLite for durable local state. | SQLite is simple, local, reliable, and fits a desktop app without a separate database server. |
| D-011 | Use structured logs for every operation. | Structured logs make failures diagnosable without flooding the normal UI with raw text. |
| D-012 | Build one complete vertical slice before adding more apps. | A narrow working path proves the hard parts before catalog size makes everything harder. |
| D-013 | Use JSON for Recipe files. | JSON is easy for .NET to parse and validate in the first slice. |
| D-014 | Validate Recipes with JSON Schema before loading them. | Bad catalog data must fail before it can influence app behavior. |
| D-015 | Use Winget as the first provider focus. | The first real provider should be common and structured before custom installers are attempted. |
| D-016 | Use named pipes for future UI-to-helper IPC. | Named pipes are a normal Windows local communication choice and can be locked down. |
| D-017 | Use xUnit for tests. | It is simple, common, and already working in this repo. |
| D-018 | Keep the first slice read-only. | The app should prove catalog/profile/review behavior before touching installs or admin work. |
| D-019 | Portable mode is activated by a `pantry.portable` marker beside the app. | A marker is clearer and safer than guessing from drive type or folder name. |
| D-020 | Known dependencies are auto-included in the dry-run review. | If a selected app needs another known app, the review must show that dependency before any real queue work exists. |
| D-021 | Known selected conflicts are shown symmetrically in the dry-run review. | If A says it conflicts with B, both A and B should visibly warn the user when both are selected. |
| D-022 | Use `Microsoft.Extensions.DependencyInjection` for app startup wiring. | A small DI container keeps construction rules in one place and keeps the window thinner. |

## Proposed Technical Defaults

These are the current technical choices.

| ID | Proposal | Plain-English reason |
| --- | --- | --- |
| P-001 | Use C#, .NET 10, WinUI 3, Windows App SDK, and MVVM Toolkit. | This follows the product spec and is a standard Windows desktop stack. |
| P-002 | Use JSON for v1 Recipe files. | JSON has strong built-in support in .NET and is easier to validate early. |
| P-003 | Use JSON Schema for Recipe validation. | A schema catches missing or malformed Recipe fields before unsafe work starts. |
| P-004 | Use named pipes for UI-to-helper IPC. | Named pipes are a common local Windows communication method and can be locked down. |
| P-005 | Use Winget as the first provider. | Winget can install known packages and gives us a safer first integration than custom EXE automation. |
| P-006 | Use the `Microsoft.Data.Sqlite` family for local state. | `Microsoft.Data.Sqlite.Core` plus the Windows SQLite provider are used for the local database. |
| P-007 | Use Serilog or `Microsoft.Extensions.Logging` structured logging. | Both support structured logs; the final choice should match the app host setup in Phase 1. |
| P-008 | Use `Microsoft.WindowsAppSDK` `2.2.0`. | This was the latest stable-looking package found during setup. |
| P-009 | Use `NJsonSchema` for schema validation. | It gives real JSON Schema validation instead of hand-written checks. |
| P-010 | Mark first Recipes as `Experimental`. | The current Recipes are read-only catalog definitions, not verified installer automation. |

## Decisions Not Yet Made

| Topic | Options | Recommendation |
| --- | --- | --- |
| Installer packaging for The Pantry itself | MSIX, loose portable folder, installer | Support both installed and portable app modes; the first detector already supports a loose portable folder with a marker file. |
| Catalog signing method | Authenticode, detached signature, or public-key signature file | Use a detached signature or public-key signature file for catalog data; finalize during catalog update design. |
| Structured logging package | Serilog vs `Microsoft.Extensions.Logging` | Decide when richer queue/provider logs are added. |
| SQLite persistence shape | Direct SQL, repository services, or light data access layer | Decide when the first durable state table is added. |

## Decision Rules

When a choice is unclear, prefer the option that is:

1. safer
2. easier to test
3. easier to explain to a non-expert user
4. smaller in scope
5. easier to replace later
