PROJECT: THE PANTRY

ROLE:
Act as senior Windows app team. Build production-quality app. Plan deep before code. Work in phases. Verify own work. No fake success. No placeholder core logic. No giant rewrite when small fix works.

MISSION:
Build “The Pantry.”

Windows app. Portable + installed editions.

Main job:

1. Browse highly curated app catalog.
2. Pick profile/apps.
3. Install many apps in one queue.
4. Detect and install updates.
5. Basic uninstall for known apps.

Primary user: owner/power user.
Future goal: reliable everyday-carry USB/PC setup tool.

MAIN VALUES, ORDER:

1. Correct
2. Safe
3. Reliable
4. Quiet
5. System-wide
6. Clear
7. Fast
8. Pretty

TECH DEFAULT:

* C#
* .NET 10 LTS
* WinUI 3
* Windows App SDK
* MVVM Toolkit
* SQLite
* async all slow work
* dependency injection
* structured logging
* unit + integration tests

Keep core engine UI-independent.
UI thin.
Providers modular.
Allow future WPF shell if needed.

APP MODES:

1. Installed mode
2. Portable mode

Portable:

* run from folder/USB
* profiles beside app
* user choose portable tool destination:

  * USB
  * C:
  * other drive/folder
* remember last choice
* modes:

  * clean
  * cached
  * fully portable

Installed:

* local cache
* logs
* scan history
* update history
* same profile format as portable

V1 SCOPE:
IN:

* curated catalog only
* app profiles
* manual app selection
* install queue
* update detection
* automated updates when trusted
* guided/manual updates otherwise
* basic uninstall
* portable tool deployment
* dependency/runtime detection
* signed catalog updates
* bundled offline catalog fallback
* local recipe overrides
* detailed logs
* dry-run/review screen
* cancellation/retry
* machine-wide install preference

OUT OF V1:

* broad Winget search
* automatic driver install
* BIOS/firmware update
* Windows tweaks
* debloat
* full uninstall manager
* scheduled background updates
* enterprise remote management
* automatic rollback of all apps
* graphical recipe editor
* community recipe submissions
* deep vulnerability scanner
* hardware-based auto profile choice

TERM:
“Recipe” = Pantry term for app automation definition.

Recipe includes:

* app identity
* catalog metadata
* source
* package ID or official URL
* install command
* silent args
* machine/all-users args
* admin requirement
* detection rules
* version detection
* update method
* uninstall method
* dependencies
* conflicts
* expected exit codes
* reboot behavior
* fallback method
* trust level
* verification date
* test evidence

Can use formal internal schema name “AppRecipeManifest.”
User-facing word = Recipe.

TRUST LEVELS:

1. VerifiedUnattended

   * safe auto install/update
2. VerifiedGuided

   * trusted, known interaction
3. ManualOfficial

   * official link/download, user completes
4. Experimental

   * advanced view only
5. Blocked

   * unsafe, broken, obsolete, or misleading

Only VerifiedUnattended runs unattended.

If verified recipe changes behavior:

* stop blind automation
* pause item
* keep rest safe
* mark recipe degraded
* downgrade trust
* log evidence
* explain to user

Never automate unknown dialog with keyboard clicks.

INSTALL GOAL:
Minimum interaction.
Prefer:

1. verified silent
2. per-machine/all-users
3. clean uninstall/update
4. official source
5. no bundle offers
6. no surprise restart

Always prefer machine scope when safe.
Never choose per-user when verified machine option exists.
Use machine PATH when appropriate.
Disable bundles/ads only with verified switches.

PRIVILEGE MODEL:

* main UI unelevated
* one elevated helper handles privileged queue work
* one UAC approval per privileged batch when possible
* strict IPC
* validate every elevated command
* helper accepts structured approved jobs, not arbitrary shell text
* never bypass UAC
* never run whole UI elevated by default

SOURCE RULE:
No universal source order.

Each app has tested preferred recipe and optional fallback.

Possible sources:

* Winget
* Microsoft Store
* official MSI
* official EXE
* official GitHub release
* official vendor download
* portable archive
* manual official link

Choose source by:

1. reliability
2. silent support
3. machine scope
4. update support
5. uninstall support
6. source trust
7. low interaction

CATALOG:
Highly curated.
About 100–300 apps eventually.
Start smaller.
Each app intentionally approved.
No raw package dump.

Each category:

* one excellent selected default
* 2–3 meaningful alternatives when useful
* reason default wins
* conflicts grouped
* commercial apps labeled
* paid apps not preselected unless personal profile says so

Internal app score:

* usefulness
* maintenance activity
* source safety
* installer quality
* silent quality
* machine-scope quality
* update reliability
* uninstall reliability
* community reputation
* bundle/telemetry concerns
* quality versus alternatives

PROFILES:

1. GAMING SETUP
   Main:

* Steam default
* other launchers optional:

  * Xbox
  * Epic
  * GOG
  * Battle.net
  * EA
  * Ubisoft
* top emulators
* top front ends
* EmuDeck main large-job choice
* controller support
* game streaming clients/hosts
* common gaming runtimes

Do not auto-select every launcher.
Steam default. Others visible.

2. GAMING PERFORMANCE

* hardware monitoring
* FPS/frame-time overlays
* GPU tools
* benchmarks
* fan/temp tools

3. GAME STREAMING + RECORDING

* OBS-class recording/streaming
* capture tools
* audio routing
* capture card utilities
* streaming plugins
* stream control tools

4. LIVING-ROOM MEDIA PC
   Priority:

* TV
* controller
  Also support:
* remote
* keyboard/mouse

Apps:

* media players
* local library clients
* couch UI
* streaming access
* playback tools

5. HOME MEDIA SERVER
   Separate profile.
   Possible:

* Plex
* Jellyfin
* Audiobookshelf
* Kavita
* Sunshine
* admin/support tools

Treat servers differently:

* services
* firewall
* ports
* storage
* accounts
* security warnings

6. REPAIR TOOLKIT SAFE

* hardware info
* disk health
* network diagnosis
* malware scan
* file recovery
* basic cleanup
* basic troubleshooting

7. REPAIR TOOLKIT ADVANCED
   Clearly risk-labeled.

* partition tools
* boot repair
* stress testing
* process analysis
* firmware tools
* destructive-capable tools

8. REPAIR RUNTIMES
   Detect first.
   Install only missing/needed:

* Visual C++ redistributables
* .NET Desktop Runtime
* WebView2
* DirectX legacy runtime
* Java when needed
* other justified dependencies

PROFILE BEHAVIOR:

* profile defaults preselected
* defaults must be excellent
* alternatives visible
* user can modify
* save custom profile
* remember previous choices
* user always chooses profile
* never infer/install profile from hardware

Optional “Match This Profile”:

* show missing apps
* show installed alternatives
* show conflicts
* suggest removals
* never auto-remove unrelated apps

DASHBOARD:
Show:

* large profile shortcuts
* available updates
* machine status
* recent activity
* failed/resumable jobs
* catalog freshness
* installed/recognized count

CATALOG UI:
Two modes:

1. cards
2. compact rows

Default app display:

* icon
* name
* short purpose
* installed state
* update state
* recommended badge
* trust badge
* selection control

Expandable technical detail:

* source
* version
* recipe
* install scope
* admin need
* detection evidence
* command preview
* fallback
* verification date

REVIEW SCREEN:
Before execution show:

* apps
* action: install/update/skip
* selected recipe
* source
* version
* scope
* trust
* admin need
* expected prompts
* dependencies
* conflicts
* reboot risk
* portable destination
* estimated download size when known

Simple by default.
Expandable technical details.

QUEUE:

* engine decides safe parallelism
* MSI/EXE privileged installs mostly sequential
* independent downloads may run parallel
* avoid Windows Installer collisions
* dependencies before dependents
* continue after ordinary failure
* pause affected branch when dependency failure
* unrelated items continue
* cancellation must be safe
* resume where possible

Already installed:

* current = skip
* newer available = update
* pinned = respect pin
* uncertain detection = ask/show evidence
* repair/reinstall optional advanced action

REBOOTS:

* never automatic by default
* defer when installer supports it
* finish safe queue work
* show final reboot-required state
* risky recipe may require pause
* recipe records reboot rules

FAILURE UX:
Show:

* plain-English reason
* failed stage
* retry
* verified fallback if available
* manual official path
* expandable stdout/stderr
* exit code
* log location
* correlation/session ID

Do not spam raw logs in main UI.

UPDATES:

* check on launch
* cache result for current day
* manual refresh
* no always-running background service in v1

Update groups:

1. automatic
2. guided
3. manual official
4. unsupported/unknown

Support apps not originally installed by Pantry when detection and update recipe are reliable.

UNINSTALL:

* basic uninstall for recognized apps
* confirm action
* show scope
* use trusted uninstall method
* no deep leftover cleaner
* no forced removal
* no bulk destructive cleanup in v1

PORTABLE TOOLS:
When chosen:

* offer installed or portable deployment
* destination chooser:

  * USB
  * local drive
  * custom folder
* managed folder structure
* shortcuts optional
* PATH changes explicit
* removal supported when safe

STATE + DETECTION:
Never rely on one source.

Evidence sources:

* Winget
* uninstall registry keys
* AppX/MS Store packages
* file paths
* executable versions
* services
* portable managed folders
* Pantry install history
* user confirmation

Detection result:

* Installed
* UpdateAvailable
* NotInstalled
* Unknown
* Broken
* Portable
* Manual

Confidence:

* High
* Medium
* Low

Store evidence with result.
Avoid false “not installed.”

DATA:
SQLite:

* apps
* recipes
* profiles
* profile selections
* installed evidence
* versions
* queue sessions
* queue jobs
* logs index
* catalog versions
* trust changes
* local overrides
* portable tool locations

Recipe files:

* human-readable YAML or JSON
* schema validated
* deterministic
* versioned
* support migrations

CATALOG DELIVERY:

* ship bundled catalog
* app usable offline
* online check for signed catalog update
* validate signature before use
* atomic catalog swap
* keep last known good catalog
* rollback broken catalog update
* show catalog version/date
* never execute unsigned catalog data

LOGGING:
For every operation store:

* timestamp
* session ID
* app ID
* recipe ID/version
* action
* source
* sanitized command
* elevation state
* start/end
* exit code
* stdout/stderr
* detection before/after
* retry/fallback
* final state

Never log secrets/tokens/passwords.

LOCAL OVERRIDES:
Advanced user may override recipe locally.

* keep separate from signed official catalog
* mark visually
* validate schema
* show diff
* easy reset
* local override cannot silently become official trust
* no full graphical editor in v1

SECURITY:

* only official/approved sources
* verify hashes when available
* verify Authenticode where applicable
* validate URLs
* HTTPS only unless explicitly justified
* no command injection
* no arbitrary remote scripts
* no unsigned catalog execution
* no piracy
* no license bypass
* no credentials stored insecurely
* least privilege
* elevated helper allowlist
* sanitize logs
* threat-model updater and IPC

ARCHITECTURE MODULES:

* Pantry.UI
* Pantry.Core
* Pantry.Domain
* Pantry.Infrastructure
* Pantry.Catalog
* Pantry.Providers
* Pantry.Detection
* Pantry.Queue
* Pantry.Elevation
* Pantry.Portable
* Pantry.Logging
* Pantry.Tests

Suggested interfaces:

* IAppProvider
* IInstallExecutor
* IUpdateProvider
* IUninstallProvider
* IDetectionProvider
* IRecipeResolver
* ITrustEvaluator
* IQueuePlanner
* IQueueExecutor
* IElevationBroker
* ICatalogService
* IProfileService
* IPortableDeploymentService

Do not over-engineer.
Use interfaces where real substitution exists.

PROVIDER RESULT:
Structured result, not string guessing:

* Success
* Failed
* Cancelled
* RebootRequired
* UserActionRequired
* NotApplicable
* Unknown

PLANNING MODE:
Before coding:

1. Inspect repo.
2. Read all existing docs/code.
3. Summarize current state.
4. Identify reusable work.
5. List conflicts with this spec.
6. Build implementation plan.
7. Create architecture docs.
8. Create backlog.
9. Define acceptance tests.
10. Then code.

Do not delete working code without reason.
Do not rewrite whole app merely for style.

AGENT/REVIEW LOOP:
Simulate these roles or use real subagents when available:

A. PRODUCT PLANNER
Checks:

* v1 scope
* user flow
* no scope creep
* requirements coverage

B. WINDOWS ARCHITECT
Checks:

* WinUI/.NET structure
* portability
* async behavior
* elevation separation
* IPC
* deployment

C. RECIPE ENGINEER
Checks:

* app recipes
* machine scope
* silent args
* detection
* updates
* fallback
* trust status

D. SECURITY REVIEWER
Checks:

* command injection
* updater trust
* signature handling
* elevation boundary
* unsafe sources
* destructive behavior

E. UX REVIEWER
Checks:

* low interaction
* clear defaults
* clear failures
* trust display
* portable destination flow
* accessibility

F. TEST ENGINEER
Checks:

* unit tests
* integration tests
* failure paths
* cancellation
* reboot paths
* catalog rollback
* installed-state accuracy

G. ADVERSARIAL REVIEWER
Try break design.
Find:

* false installed states
* false success
* hanging installers
* stale recipes
* bad fallback
* wrong install scope
* repeated UAC
* partial queue corruption
* USB removal
* offline failure
* signature failure

LOOP RULE:
For each phase:

1. Implement smallest complete slice.
2. Build.
3. Run tests.
4. Run static analysis.
5. Run role reviews.
6. Record defects.
7. Fix Critical/High.
8. Repeat until:

   * build clean
   * tests pass
   * no Critical
   * no unresolved High
   * acceptance criteria pass
9. Commit checkpoint.
10. Continue next phase.

Never claim tested if not tested.
Never hide failing tests.
Never weaken test to force pass.
Never replace real logic with mock in production path.

PHASES:

PHASE 0: DISCOVERY

* inspect repo
* produce CURRENT_STATE.md
* produce GAP_ANALYSIS.md
* produce ARCHITECTURE.md
* produce V1_BACKLOG.md
* produce TEST_PLAN.md
* produce THREAT_MODEL.md
* no major code yet

PHASE 1: FOUNDATION

* solution structure
* DI
* logging
* SQLite
* settings
* navigation shell
* portable/installed mode detection
* catalog schema
* recipe schema validation

PHASE 2: CATALOG + PROFILES

* bundled catalog
* categories
* cards/rows
* search curated catalog
* profiles
* default selections
* alternatives/conflicts
* save/load custom profiles

PHASE 3: DETECTION

* Winget detection
* registry detection
* AppX detection
* file/version detection
* evidence/confidence model
* dashboard state
* update availability

PHASE 4: QUEUE

* review screen
* dependency graph
* queue planner
* cancellation
* retries
* failure isolation
* session logging
* dry run

PHASE 5: ELEVATED EXECUTION

* secure helper
* IPC
* command allowlist
* per-machine installs
* one elevation batch
* exit/result handling
* reboot handling

PHASE 6: PROVIDERS
Order:

1. Winget
2. MSI
3. official EXE
4. Microsoft Store
5. GitHub release
6. portable archive
7. manual official

Each provider requires:

* tests
* failure mapping
* cancellation behavior
* source verification
* detection verification

PHASE 7: UPDATES + UNINSTALL

* launch update check
* daily cache
* manual refresh
* update groups
* basic uninstall
* post-action detection

PHASE 8: PORTABLE TOOLKIT

* choose destination
* managed folders
* USB/local behavior
* clean/cached/portable modes
* safe USB removal/recovery

PHASE 9: SIGNED CATALOG UPDATE

* signature verification
* download
* schema validation
* atomic apply
* last-known-good rollback
* offline fallback

PHASE 10: HARDEN

* accessibility
* performance
* installer edge cases
* crash recovery
* interrupted queues
* corrupt DB recovery
* bad catalog recovery
* release packaging

FIRST VERTICAL SLICE:
Build one full working path before broad catalog.

Use 3–5 safe apps:

* 7-Zip
* VLC
* Steam
* Firefox or another browser
* one portable repair tool

Slice must prove:

* catalog display
* profile selection
* detection
* review
* elevation
* install
* update
* uninstall
* logs
* retry
* portable destination
* recipe trust

ACCEPTANCE:

* UI never freezes during scan/install/update
* no repeated UAC per item when batching works
* machine-wide install chosen when verified
* false success impossible
* every job has final state
* failed app does not destroy unrelated queue
* unexpected installer prompt pauses safely
* offline bundled catalog works
* bad signed-catalog update rejected
* current catalog remains usable
* profile state survives restart
* portable profiles survive machine change
* technical logs exist
* normal user sees plain language
* no automatic reboot
* no arbitrary elevated commands

OUTPUT STYLE:
Be concise.
Use tables/checklists in docs.
Update status after meaningful milestones.
Show found defects early.
Do not ask repeated questions already answered.
When uncertain:

* choose safest reasonable default
* document assumption
* continue unless truly blocked

FIRST RESPONSE:
Do not start coding immediately.

Return:

1. repo state summary
2. requirement interpretation
3. architecture proposal
4. risk list
5. phased work plan
6. first vertical-slice plan
7. files to create/change
8. tests to add
9. unresolved blockers only

Then begin Phase 0.
