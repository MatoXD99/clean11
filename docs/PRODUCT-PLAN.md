# Product plan

Status: proposed implementation plan. Scope includes all six modules below. An early preview with fewer modules is not the completed product.

## 1. Product identity and scope

Use **Clean11** as the sole product identity. Module names are Clean11 Search, Overview, Dock, Top Bar, Desktop, and Settings. Create original icons and artwork, use a Windows-available font family, and use installed application icons for application entries. Product text and public materials must not use another platform's branding or imply affiliation.

The design direction is modern translucent glass: floating rounded surfaces, subtle borders, restrained shadows, generous spacing, and smooth movement. Windows capabilities define the commands and behavior.

Initial target: supported Windows 11 desktop releases, x64. Record the exact tested editions and builds before each release. ARM64 follows after native dependencies and installation have been validated. No initial Windows Server or Windows 10 support promise.

## 2. Search

### Opening and transition

- A bare Super key opens the familiar overview and immediately focuses its search input. Existing window layout and overview placement remain the baseline.
- Typing moves the same input into the centered Search panel. Keep text, selection, caret, and input composition intact during the animation.
- Offer a separate configurable shortcut for direct Search, with conflict detection. A proposed default is Alt+Space; onboarding must flag conflicts rather than silently take over another launcher.
- Emptying the query reverses the transition to the overview. Escape closes an active submenu first, then clears a query, then closes the overview. Direct Search closes when its empty state is dismissed.
- Retain the last deliberately selected result during asynchronous updates when it still exists. Pointer hover must not reset keyboard navigation until the pointer actually moves.

### Results and actions

- Compact input with category shortcuts: All, Apps, Files, Settings, and Actions. Clipboard is shown only when enabled.
- Results grouped by meaning, with a clear best match. A detail pane appears when useful and space permits; narrow screens use a single-column layout.
- Every item has a clear name, category, and useful secondary information. Show full paths in details when requested, not as the primary label.
- Keyboard navigation, Enter to activate, context actions, and clear loading/empty/error states.
- App actions: open, switch to running window, pin to Dock, open file location, and explicit Run as administrator where supported.
- No web suggestions or remote queries by default. Web search requires an explicit action or enabled provider.

### Search quality

- Discover Start Menu shortcuts, installed packaged apps, registered applications, configured portable apps, and relevant executable aliases.
- Match exact names and prefixes before fuzzy keywords. Recognize aliases such as PowerShell, pwsh, Command Prompt, cmd, Terminal, and Windows Settings.
- Normalize case, diacritics, word boundaries, and locale-aware input. Deduplicate app aliases without merging different installed versions incorrectly.
- Local launch history can improve rankings, with a cap so it cannot bury an exact app-name match. History can be cleared or disabled.
- Search files through the Windows index with explicit coverage indicators. Offer an optional Everything integration after a dependency/license review. Do not require a privileged full-disk indexer for basic use.
- Calculations, unit conversion, and explicit system actions are local providers. Never execute a typed command automatically.
- Cancel superseded queries and discard stale responses. Decode icons and gather details off the UI thread; cache and virtualize rows.

### Dynamic Windows Settings

Discover available names and keywords from the installed system, and refresh when the OS build, language, settings package, or source catalog changes. Treat undocumented metadata formats as replaceable adapters.

Use validated activation routes for direct pages. Ship a reviewed route baseline, retain the last good cache, and refresh route data independently of application UI releases. Do not rely on downloading and parsing a documentation page for every search or as the only route source.

An unavailable or unrecognized route opens the nearest valid category or the Windows Settings root, with accurate labeling. This provides adaptation to system updates; it cannot guarantee advance compatibility with every future metadata change. [Windows Settings activation reference](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings).

## 3. Overview and window management

- Preserve the initial overview layout, wallpaper experience, and visual movement of windows into the overview.
- Rounded previews use a shared clipping policy. App icon and app name appear above each preview; a secondary window title is optional and belongs to the same card.
- Remove duplicate title badges, stale helper windows, tooltip ghosts, and other disconnected preview artifacts.
- Click or keyboard selection activates the correct original window. Closing a window remains an explicit action.
- Provide monitor filtering, keyboard navigation, window grouping, and settings for spacing and density.
- Keep actual application state intact. The overview transition should visually shrink representations; do not blindly minimize every real app or alter maximized state merely to create the animation.
- Use live previews where reliable. Minimized, protected, elevated, or capture-incompatible windows receive an honest static/icon fallback.
- Include practical Windows window actions: minimize, maximize/restore, snap, move to another monitor, and close. Virtual desktop switching/moving is capability-gated because API coverage differs.

## 4. Dock

- Floating glass container with consistent icon sizing and optional gentle pointer magnification. Pointer position must never trigger expensive discovery or image extraction.
- Pinned apps, running indicators, grouped windows, drag-to-reorder, and predictable launch/switch/minimize behavior. A grouped app opens a window chooser when necessary.
- Windows actions where supported: Jump Lists, recent documents, progress, and attention indicators. Do not invent unread counts for apps that expose none.
- Separate optional area for folder stacks, Downloads, Recycle Bin, and recently used items.
- A clearly labeled background-apps button opens a themed tray panel when the compatibility adapter supports it; otherwise it exposes a reliable route to the native notification area.
- Tray entries represent notification-area applications, not all Windows services. Include a separate Task Manager action for process inspection.
- Auto-hide, always-visible, size, magnification strength, alignment, monitor assignment, and reduced-motion settings.
- Hide for fullscreen applications; restore reliably after exiting a game. Reserve work area only when the selected visibility mode requires it.
- Preserve original application icons by default. Optional icon packs must have verified redistribution rights and a consistent fallback.

## 5. Top Bar

- Thin glass bar with Clean11 menu, active app identity, optional workspace/monitor status, and configurable system modules.
- Clean11 menu: About, Settings, Task Manager, Windows Settings, Lock, Sleep, Restart, Shut down, Sign out, and Exit Clean11 / Restore Windows desktop. Shut down must be directly discoverable.
- Quick Controls: volume and output device, microphone controls where available, network status, Bluetooth, battery/power status, and supported media controls. Unsupported actions lead to the correct Windows page.
- Clock/calendar, Search, and notification access. A custom notification view is optional and requires supported capabilities and permission; native notification center remains accessible.
- Active application commands are exposed only where there is a real integration. No universal File/Edit menu is promised for arbitrary Windows apps.
- Shared popup behavior: click opens; a second click closes; outside click and Escape close; selecting a command closes once. Submenus remain within the same focus session.
- Fullscreen and borderless-game policy is shared with the Dock. No always-on-top bar over a game. Optional deliberate edge reveal must not accidentally activate during gameplay.

## 6. Desktop

### Core desktop

- Render actual desktop items from Windows known folders, including redirected/cloud-backed locations and shared desktop items. Preserve filenames, locations, access rules, and cloud placeholders.
- Selection rectangle, multiselect, keyboard navigation, open, drag/drop, rename, copy/cut/paste, new folder, delete to Recycle Bin, and Properties.
- Consistent themed context menu: Open, Open with where available, Rename, Copy/Cut/Paste, New, Sort, View, Refresh, Open in Terminal, Properties, and Clean11 Settings.
- Background context menu also exposes wallpaper, display settings, desktop icon size, grid/free placement, and alignment.
- Preserve Windows conflict dialogs, progress, cancellation, recycle semantics, and supported undo. Advanced extension commands can use an explicit native context-menu fallback.
- Per-monitor layout survives DPI changes, reconnection, resolution changes, and docking. No item becomes permanently unreachable on a missing monitor.
- Folder stacks are a presentation feature; opening a folder can use File Explorer. A complete replacement for File Explorer is a separate future project.

### Personalization

- Respect the current wallpaper and support standard wallpaper changes. Animated wallpaper integrations must be optional and versioned.
- Layout presets and configurable icon/label appearance.
- Later: original clock, calendar, media, and weather widgets. Weather requires a configured provider and user-chosen location; calendar access needs separate consent. Widgets do not block the full desktop foundation.

## 7. Separate Settings app

One normal application window with a searchable sidebar:

| Page | Controls |
| --- | --- |
| General | Startup, modules, presets, update channel, restore Windows |
| Appearance | Light/dark/system, glass strength, contrast, accent, density, shared corners |
| Search | Providers, indexing coverage, exclusions, ranking history, shortcuts |
| Overview | Layout, grouping, previews, labels, monitor behavior |
| Dock | Pins, icon size, magnification, visibility, stacks, tray access |
| Top Bar | Modules, order, clock, fullscreen rules, monitor placement |
| Desktop | Layout, folders, labels, context menu, wallpaper, widgets |
| Accessibility | Reduced motion, transparency, text scaling, keyboard access |
| Privacy | History, clipboard, provider permissions, retention, clear local data |
| Recovery | Disable modules, restore shell state, reset appearance, diagnostics export |
| About | Version, licenses, release notes, update controls |

Settings previews are contained inside the app. Applying a preset explains which modules change. Export appearance separately from app pins, file locations, history, and device-specific layouts.

## 8. Deliberate boundaries

The complete suite is the goal; implementation proceeds in gated increments. Initial releases do not modify other applications' title bars, replace the Windows login shell, require a driver, inject into games, or offer a universal third-party application menu. Those mechanisms would undermine the reliability required for the desktop itself.
