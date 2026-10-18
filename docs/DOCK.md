# Clean11 Dock

The first native dock implementation uses C#, WPF UI 4.3.0, and the shared Clean11 palette. It runs independently of Overview and requires Windows 11 x64 with .NET Framework 4.8.

## Included

- Pinned apps, files, and folders; running apps grouped by executable or application identity.
- Initial import of existing Seelen app pins, preserving launch arguments and working directories in private local settings.
- Click to launch, activate, or minimize a single app window. Multiple windows open a picker with a live DWM preview; hover a row to preview that window.
- Middle-click for a new instance; context menus for window actions, pinning, and reordering.
- Drag pins to reorder; drop files, shortcuts, or folders to pin them. An overflow menu keeps additional apps accessible.
- Smooth neighboring icon magnification, active/running indicators, shared colors and corners, and reduced motion.
- Always-visible, intelligent overlap hiding, and auto-hide modes. Fullscreen apps suppress the dock and edge reveal.
- Downloads and pinned-folder stacks, Recycle Bin, Overview, and background-app access.
- Separate settings window for display, icon size, magnification, visibility, utility buttons, takeover, and per-user startup.
- Icons load on a background STA thread. App enumeration happens off the UI thread. Animation rendering stops when settled.

## Integration and recovery

Clean11 temporarily hides the existing bottom dock/taskbar on the selected display. It does not edit Seelen's configuration or change the top bar. Original window visibility is recorded locally before hiding; normal exit and a separate watchdog restore it. A restarted shell is rediscovered automatically.

When Seelen is running, its existing work-area reservation remains in place. A standalone always-visible Clean11 dock registers its own appbar reservation. This compatibility stage can therefore retain an empty bottom margin with Seelen, even in auto-hide mode.

The background-app button uses the existing local Seelen tray widget when available. Otherwise it reveals the Windows notification-area taskbar for 15 seconds. This is an integration adapter, not a new notification-icon host.

Launches use the user's home directory unless a pin explicitly specifies a valid working directory. User data lives under `%LOCALAPPDATA%/Clean11/Dock`; install binaries live under `%LOCALAPPDATA%/Programs/Clean11/Dock`. Neither is stored in the public source tree.

## Build and install

Restore/build the Overview dependencies as described in [BUILDING.md](BUILDING.md), then run:

```powershell
./src/Clean11.Dock/Build.ps1
./src/Clean11.Dock/Install.ps1
```

`Build.ps1 -DependenciesRoot <directory>` can reuse a directory containing the six restored runtime DLLs. Builds include the shared Overview theme sources and copy the theme JSON. The installer carries third-party licenses alongside the executable.

Right-click the dock or its notification icon to open settings or **Exit dock and restore previous bars**. For rollback with automatic startup disabled:

```powershell
./src/Clean11.Dock/RestorePreviousDock.ps1
```

## Scope and remaining work

This is a first working dock, not complete desktop-shell parity. Native notification-icon hosting, Jump Lists, app progress/badge integration, application-specific actions, and simultaneous docks on multiple displays remain future work. One display can be selected at a time. Some hosted applications may share an executable identity. Protected or minimized windows may not supply live DWM content. Folder stacks show up to 60 entries from a bounded enumeration; Open folder gives full access.

The current dock uses a translucent WPF surface, not a live blurred desktop backdrop. It uses the shared WPF UI controls and theme rather than copying platform branding or proprietary assets.

Build compilation is performed during development. Interactive behavior, mixed-DPI transitions, game switching, and recovery still need user-authorized acceptance testing before a public release. EXE/MSI packaging remains part of the full Clean11 roadmap.

## Visual refinement and interactive checks

The dock now uses a compact 40-pixel default icon size, restrained 1.22x magnification, a divider before utilities, and borderless hover surfaces. Magnification allocates room between neighboring icons. High-resolution shell images load on the background STA thread; packaged apps can retain the executable icon learned from a running window. Settings remain available from the context menu and notification icon.

A deferred preview placement could run after its window closed, yielding an invalid DPI/position and terminating the dock. Placement now checks window lifetime, handle validity, DPI, and finite coordinates before moving the preview.

Interactive computer-use checks covered settled dock appearance, hover enlargement, and a multi-window preview. Compilation succeeded. Mixed-DPI and game scenarios still need broader acceptance coverage. The `--inspect-ui` argument temporarily exposes shell windows to accessibility tools; ordinary launches keep their normal shell behavior.

Icon extraction follows Microsoft's [IShellItemImageFactory guidance](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellitemimagefactory-getimage), including background extraction and releasing returned bitmap handles.

Icon layout refinement: app and utility icons share the same frame and vertical baseline. Transparent shell-image padding is cropped on the icon worker, artwork fits a consistent inner box, running indicators use one size, and the dock has additional vertical breathing room. Dock hover uses magnification without an extra button-shaped background. The updated surface was inspected through computer use.

Icon-only refinement: individual icon surfaces and borders are removed. All icons are centered on the dock's vertical midpoint. Running indicators are 3-pixel overlays beneath the icon bounds, not a dedicated layout row, allowing the dock height to shrink without shifting icon alignment. Existing magnification and normalized icon spacing remain in place.
