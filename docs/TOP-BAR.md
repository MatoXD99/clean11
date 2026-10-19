# Clean11 Top Bar

The first implementation runs inside the dock host and starts and exits with it. No additional runtime or background service is required. The top bar and integrated controls are included in the v0.2.0 Alpha installer.

## Controls

- Clean11 menu: Settings, overview, Task Manager, Windows Settings, lock, power options, and exit the Clean11 bars.
- Active app: minimize, maximize/restore, close, and open overview. These are Windows window actions, not simulated app menus.
- Overview/search and File Explorer shortcuts.
- Network availability indicator and quick controls.
- Integrated Wi-Fi panel: radio control, nearby networks and signal quality, saved-network connect/disconnect/forget, and one-time open/WPA2-Personal connections.
- Integrated Bluetooth panel: radio control, paired/nearby Classic and LE devices, connection status, and pairing/removal through Windows. Removal requires an inline confirmation.
- Integrated volume mixer: master volume and mute plus individual audio-session sliders and mute, using Windows Core Audio. Refresh discovers newly opened sessions.
- Integrated default input/output selectors for apps and calls, plus Windows settings shortcuts for advanced microphones, advanced networking, display, notifications, and power.
- Battery/charging status on devices with a battery.
- Local clock and a month calendar with date/time settings access.

Restart, shutdown, sleep, and sign-out require confirmation in the app. Menus close on Escape or when focus moves outside. The bar itself does not activate when clicked; opening a menu deliberately activates its contents for keyboard navigation.

## Settings and appearance

Clean11 Settings has a **Top Bar** page for enabling the bar, display selection, hiding in fullscreen, reserving screen space, active-app and status modules, and clock seconds. Apply changes to save them.

The bar and popups use the existing shared material, glass visibility, blur radius, colors, and corner resources. Appearance settings are not duplicated. Disabling the bar releases its Windows appbar reservation. Exiting the host releases the reservation; Explorer also removes it when the process exits unexpectedly. The reservation is retained while the bar is temporarily hidden in fullscreen to avoid resizing other windows when a game gains focus.

## Current limits

One chosen display is supported. Network availability indicates an active network connection, not verified internet reachability. Wi-Fi discovery obeys Windows location permissions and adapter policy. New WPA3, enterprise, hidden networks, and persistent password setup use Windows Settings; saved profiles connect directly regardless of their authentication type. One-time Wi-Fi passwords are passed to Windows in memory and are never written to Clean11 preferences or logs. Bluetooth discovery runs for six seconds when its panel opens or refreshes, and stops when dismissed. Pairing requirements vary by device; PIN/custom flows may need Windows Settings. Windows manages Bluetooth profile connections; paired devices expose Connect in Windows / Connection options rather than pretending service installation is a connection API. Entries with the same Bluetooth address are merged, while distinct devices with the same advertised name remain separate. The mixer covers sessions on the current default multimedia output; default input/output selectors update console, multimedia, and communications roles. Per-app routing, microphone, and notifications remain system settings shortcuts. The calendar has no account or event integration. Media controls, multiple simultaneous bars, and module reordering remain future work.

Other desktop bars must be disabled in their own settings to avoid competing screen reservations. Clean11 does not silently stop third-party desktop software on other users' computers.

## Development checks

The framework build was checked along with live visual inspection of the top bar, quick controls, calendar, and Settings page. Master volume was read successfully without changing the user's volume. Escape dismissal and the 48-pixel appbar work-area reservation were checked. Fullscreen games, multiple monitors with different DPI, and sleep/resume still need broader real-device validation. Power commands were not executed during inspection.

## Integrated controls build status

The Framework and modern builds compile with the integrated control panels. Network connection, radio changes, pairing, and audio-volume changes have not been exercised on live devices for this change. No new third-party dependencies were added; Windows WLAN, device enumeration/radio APIs, and Core Audio provide the controls.

## Navigation and device management

Wi-Fi, Bluetooth, and the mixer have a Back button to quick controls. Lists scroll inside a bounded area, leaving navigation and Refresh accessible. Forget network removes the saved Windows profile; Forget device unpairs the selected Bluetooth endpoint. Both present an inline confirmation before changing saved data. Wi-Fi forgetting is available for saved profiles returned in the nearby-network list.

Input and output selectors enumerate active Core Audio endpoints and show the current multimedia default. Choosing a device does not immediately change routing: use **Use selected device** to apply it for apps and calls. Setting defaults uses the isolated, undocumented Windows `IPolicyConfig` interface; failures leave the Windows Settings fallback available. Its ABI is described in the [audio-switch project's interface declaration](https://github.com/tartakynov/audioswitch/blob/master/IPolicyConfig.h). No external audio-switch executable is bundled.

Computer-use inspection covered the Bluetooth list, removal-button visibility, Back navigation, audio endpoint names/dropdown, and the Wi-Fi unavailable-adapter state. Pairings, network profiles, radio power, and active audio routing were left unchanged. Actual removal, reconnection, and default-device switching still need hardware-level validation.