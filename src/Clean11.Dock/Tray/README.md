# Background apps appearance

Compact Clean11 styling for the existing Seelen `@local/system-tray` compatibility widget. This adapter styles the web-based popup; it does not embed WPF controls or replace tray functionality.

Run `Install.ps1` after installing the compatibility widget. Colors and radii come from the shared `Clean11.Overview/ActivitiesTheme.json`. Rerun after changing that theme, then reload the compatibility host.

The panel uses 34px rows, transparent buttons, small icons, subtle row hover, and quiet pin actions. Clean11 positions it just above the dock's Background Apps button, keeping it clear of the dock. Tray clicks, context menus, drag ordering, and pinning remain owned by the existing widget. A right-click context menu keeps the panel open while the menu is in use; Escape and the next arrow click still close it. Long lists scroll at 420px. Keyboard focus and reduced motion are supported.

Installation saves the original CSS once in the user's private LocalAppData/Clean11/Dock directory. Restore that file to the widget's `style.css` to undo the appearance change. No private widget data or upstream JavaScript is included here.
