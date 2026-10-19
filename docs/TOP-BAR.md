# Clean11 Top Bar

The first implementation runs inside the dock host and starts and exits with it. No additional runtime or background service is required. This development version is newer than the published v0.1.0 installer.

## Controls

- Clean11 menu: Settings, overview, Task Manager, Windows Settings, lock, power options, and exit the Clean11 bars.
- Active app: minimize, maximize/restore, close, and open overview. These are Windows window actions, not simulated app menus.
- Overview/search and File Explorer shortcuts.
- Network availability indicator and quick controls.
- Master output volume and mute through Windows Core Audio, plus links to sound/output devices, volume mixer, microphone, network, Bluetooth, display, notifications, and power settings.
- Battery/charging status on devices with a battery.
- Local clock and a month calendar with date/time settings access.

Restart, shutdown, sleep, and sign-out require confirmation in the app. Menus close on Escape or when focus moves outside. The bar itself does not activate when clicked; opening a menu deliberately activates its contents for keyboard navigation.

## Settings and appearance

Clean11 Settings has a **Top Bar** page for enabling the bar, display selection, hiding in fullscreen, reserving screen space, active-app and status modules, and clock seconds. Apply changes to save them.

The bar and popups use the existing shared material, glass visibility, blur radius, colors, and corner resources. Appearance settings are not duplicated. Disabling the bar releases its Windows appbar reservation. Exiting the host releases the reservation; Explorer also removes it when the process exits unexpectedly. The reservation is retained while the bar is temporarily hidden in fullscreen to avoid resizing other windows when a game gains focus.

## Current limits

One chosen display is supported. Network availability indicates an active network connection, not verified internet reachability. Network, Bluetooth, output-device selection, microphone, and notifications open the corresponding Windows settings pages; they are not custom replacements for those system panels. The calendar has no account or event integration. Media controls, multiple simultaneous bars, and module reordering remain future work.

Other desktop bars must be disabled in their own settings to avoid competing screen reservations. Clean11 does not silently stop third-party desktop software on other users' computers.

## Development checks

The framework build was checked along with live visual inspection of the top bar, quick controls, calendar, and Settings page. Master volume was read successfully without changing the user's volume. Escape dismissal and the 48-pixel appbar work-area reservation were checked. Fullscreen games, multiple monitors with different DPI, and sleep/resume still need broader real-device validation. Power commands were not executed during inspection.
