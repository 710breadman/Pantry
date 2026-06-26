# Threat Model

Last updated: 2026-06-26

## Security Goal

The Pantry will download, install, update, and uninstall software. That is powerful and risky.

The security goal is to make sure The Pantry only does what the user approved, only from trusted Recipes, and only with the minimum privileges needed.

## Main Assets To Protect

| Asset | Why it matters |
| --- | --- |
| User's Windows installation | Bad installs, wrong uninstall actions, or unwanted reboots can damage the machine. |
| Administrator privileges | If misused, admin rights can change almost anything on the PC. |
| Catalog and Recipes | If tampered with, they could point to malicious downloads or commands. |
| Local database | Stores history, choices, evidence, and queue state. |
| Logs | May include paths, command output, or system details. |
| Portable USB data | Portable profiles and tools can be lost, corrupted, or run on different machines. |

## Trust Boundaries

| Boundary | Risk |
| --- | --- |
| Main app to elevated helper | A bug could send unsafe privileged work. |
| Catalog update source to local catalog | A malicious or broken catalog could change install behavior. |
| Recipe to provider command | Bad data could become command injection. |
| Provider to external installer | Installer behavior may change or show unexpected prompts. |
| Detection to success reporting | Weak detection could create false success. |
| Portable USB to local machine | Paths and state may change between machines. |

## Major Threats And Mitigations

| Threat | Mitigation |
| --- | --- |
| Unsigned or tampered catalog update | Require signature validation before use; keep bundled fallback and last-known-good catalog. |
| Command injection through Recipe data | Use structured provider arguments, strict schema validation, escaping, and allowlists. |
| Main UI running as admin | Keep UI unelevated; isolate admin work in helper. |
| Elevated helper becomes arbitrary command runner | Helper accepts only structured approved jobs and rejects unknown actions/providers. |
| Unknown installer dialog automated incorrectly | Never automate unknown dialogs with keyboard input; pause and ask user. |
| False install success | Always run post-action detection before reporting success. |
| Silent per-user fallback | Treat unexpected scope as failure or user-action-required, not success. |
| Malicious download URL | Allow only approved HTTPS sources in trusted Recipes; verify hashes/signatures when available. |
| Installer surprise reboot | Use verified no-reboot arguments when available; never reboot automatically. |
| Secrets in logs | Sanitize command output and never log tokens/passwords. |
| Broken update corrupts queue state | Persist queue state per job and use final states for resume/retry. |
| USB removed during portable deployment | Use staged writes, verify final files, and record incomplete deployment. |
| Local override escalates trust silently | Mark overrides visibly and never inherit official trust automatically. |

## Elevated Helper Rules

The elevated helper must:

- run only when UAC approved
- validate every job
- reject raw shell commands
- reject unknown Recipe/provider/action combinations
- reject unsigned or untrusted Recipe data
- return structured results
- log what it did with sanitized details
- avoid storing long-lived secrets

## Catalog Rules

Catalog updates must:

- use HTTPS
- be signature checked before use
- be schema validated before use
- be applied atomically
- keep the current catalog if update fails
- keep a last-known-good catalog
- show catalog version/date in the app

## Installer Rules

Installers must:

- come from an approved source
- match the selected Recipe
- use verified silent arguments for unattended installs
- prefer machine-wide scope when verified
- avoid surprise restart
- produce logs
- be followed by detection

## Highest-Risk Areas

1. Elevated helper IPC and validation.
2. Recipe-to-command conversion.
3. Catalog update signing and rollback.
4. Post-install detection accuracy.
5. Handling installers that change behavior over time.

## Security Acceptance For First Full Slice

Before the first real install slice is considered complete:

- no raw arbitrary command reaches the helper
- helper rejects invalid jobs
- only `VerifiedUnattended` Recipes run unattended
- post-install detection is required
- unexpected prompts pause safely
- no automatic reboot is possible
- logs are written without secrets
- failed app does not corrupt unrelated jobs

