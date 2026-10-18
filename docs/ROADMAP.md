# Delivery roadmap and acceptance gates

Phases are dependency-ordered. Dates should be estimated after the platform prototypes; desktop hosting and tray compatibility can materially change effort. Do not describe an early Search-only preview as the completed suite.

## Phase 0 — Foundation and feasibility

Deliver:

- Clean repository, dependency/attribution inventory, supported OS/build matrix, architecture decisions.
- Shared theme prototype and a single rounded material surface exercised at different DPIs.
- Focus/keyboard/transition prototype, native-preview rounding prototype, appbar/fullscreen prototype.
- Desktop host/file-operation prototype, tray adapter spike, notification packaging decision.
- Recovery watchdog and shell state restoration proof before enabling shell hiding.

Exit gate:

- Material corners contain no square backing layer, including resizing and active/inactive states.
- Typing immediately after invocation is never lost; input composition survives transitions.
- Real foreground apps remain correctly positioned and activated after overview dismissal.
- Native shell can be restored after host, UI, and desktop-process termination.
- Document supported and unsupported tray/desktop integrations. If a core visual target cannot be met, revise the renderer decision before scaling implementation.

## Phase 1 — Search, Overview, and Settings

Deliver the shared design system, separate Settings window, provider architecture, central launch policy, dynamic Settings discovery, centered Search transition, polished overview cards, and local privacy controls.

Exit gate:

- All Clean11-owned menus and dialogs use the same design resources.
- App-name queries, including PowerShell and its aliases, rank the intended installed app correctly.
- Rapid query edits cannot show stale results or activate a different row than the selected one.
- Hovering results produces no cursor stutter; no synchronous provider or icon work occurs on pointer movement.
- Search-to-overview and reverse transitions are interruptible and keep the caret/input.
- Launch working directories match the centralized policy from every entry point.

## Phase 2 — Dock and Top Bar

Deliver pins/running apps, window groups, sizing/magnification, stacks, background-app access, system controls, power menu, per-monitor placement, and shared fullscreen behavior.

Exit gate:

- Every flyout opens/closes predictably under repeated click, outside click, Escape, and nested-menu use.
- No bar or dock remains over a fullscreen game; Alt+Tab, monitor changes, and game exit recover correctly.
- Existing taskbar/appbar conflicts are detected during onboarding; the user chooses the active setup.
- Tray actions either work for the supported adapter or clearly use the native fallback.
- Shut down, Restart, Sleep, Lock, and Restore Windows are easy to locate.

## Phase 3 — Desktop

Deliver own desktop surface, files/folders, selection, drag/drop, themed context menus, grid/free layouts, per-monitor persistence, wallpaper controls, and native extension fallback.

Exit gate:

- File operations preserve normal access checks, conflicts, cancellation, recycle behavior, and supported undo.
- User/shared/redirected desktop folders and cloud placeholders behave correctly.
- Disabling Clean11 reveals the same underlying files; no migration is required to recover them.
- Explorer restart, display removal, logoff/login, and crash recovery do not strand desktop items.
- Native extension menus remain available where a themed equivalent is unsupported.

## Phase 4 — Installation and public beta

Deliver reproducible builds, EXE installer, MSI package, upgrade/uninstall behavior, onboarding, sanitized diagnostics, release notes, compatibility reports, dependency notices, and signed update validation.

Exit gate:

- Install on a clean Windows account with no development tools or existing customization suite.
- Exercise repair, upgrade, failed upgrade rollback, startup opt-out, standard-user use, and uninstall.
- Native shell state is restored on uninstall and recovery; settings retention/removal is explicit.
- Scan tracked files, commit metadata, generated assets, archives, and binaries for personal data and private paths.
- All bundled code/assets have reviewed redistribution terms and required notices.
- Real desktop regression review completed on the supported matrix, with known limitations published.

## Phase 5 — Full 1.0 and extensions

Ship the full six-module suite after reliability and performance gates. Add widgets, optional clipboard history, richer actions, icon packs, and ARM64 support as separately gated extensions. Third-party executable plugins need an explicit trust/update model; theme files alone remain declarative.

## Performance targets

These are proposed budgets, not measured claims. Establish a documented reference PC and dataset in Phase 0 and publish p50/p95 results.

| Scenario | Proposed target |
| --- | --- |
| Warm invoke to accepted typing | p95 under 100 ms |
| Warm indexed app query to initial results | p95 under 100 ms |
| Cached file/settings query to initial results | p95 under 150 ms; slow providers continue separately |
| Animation | Meet display frame budget: 16.7 ms at 60 Hz, 8.3 ms at 120 Hz where supported |
| Hidden idle | Combined background CPU below 0.5% averaged over a quiet minute on reference hardware |
| Hidden rendering | No continuous animation/render loop; indexing work separately bounded |
| Memory | Provisional resident working-set budget of 250 MB idle across core processes, measured with dataset/context |

Performance reductions follow a documented quality ladder: pause unused live previews, reduce effect cost, then use opaque material. Never sacrifice input handling to maintain blur.

## Regression matrix

- Input: keyboard-only, mouse, high-rate pointer, IME, pasted text, shortcut conflicts, modifier combinations.
- Display: one/multiple monitors, mixed DPI, scaling 100–200%, negative coordinates, HDR/SDR, 60/120/144+ Hz, unplug/replug.
- Apps: Win32, packaged apps, multiple windows, minimized/maximized windows, elevated apps, protected content, rapid process exits.
- Games: CS2 and other fullscreen/borderless workloads; no injection; deliberate invocation versus accidental overlay.
- Lifecycle: Explorer restart, host crash, shell-broker hang, sleep/resume, lock/unlock, remote session, fast user switching.
- Files: shared/redirected folders, cloud placeholders, removable media, unavailable destinations, long/unicode names, permissions, conflicts.
- Accessibility: high contrast, reduced transparency, reduced motion, large text, screen reader, keyboard focus, localization.
- Installation: clean install, repair, update, interrupted update, rollback, uninstall, standard user, optional machine-wide install.

Automated unit/integration checks and offscreen visual snapshots cover reproducible logic. Fullscreen, focus, tray, material composition, and desktop interactions additionally require scheduled live testing. Do not run UI automation on an actively used desktop without a separate agreed test session.
