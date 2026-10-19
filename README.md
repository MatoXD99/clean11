# Clean11

A cohesive desktop experience for Windows 11: fast search, window overview, a floating dock, a top bar, a customizable desktop, and one Settings app.

**Status: overview/search and dock prototypes.** The repository contains the overview/search application and a native bottom dock using WPF UI, alongside the full desktop-suite blueprint. Top Bar, Desktop, and EXE/MSI packaging remain planned work.

## The experience

Clean11 combines softly rounded glass surfaces, clear typography, responsive motion, and familiar Windows capabilities. All surfaces share the same design system. Each module can be enabled independently.

The existing overview layout is the starting point. Typing transforms its search field into a centered search panel. The dock, top bar, desktop, menus, and Settings use the same materials and interaction rules.

## Read the plan

1. [Product plan](docs/PRODUCT-PLAN.md): features and everyday behavior.
2. [Design system](docs/DESIGN-SYSTEM.md): materials, corners, motion, and accessibility.
3. [Architecture](docs/ARCHITECTURE.md): application structure, Windows integration, and technical decisions.
4. [Delivery roadmap](docs/ROADMAP.md): implementation order and acceptance gates.
5. [Privacy and release](docs/PRIVACY-AND-RELEASE.md): repository hygiene, installation, updates, and distribution.

## Principles

- Fast local search and reliable keyboard interaction.
- A consistent visual language across the entire product.
- Real Windows actions, understandable controls, and an obvious power menu.
- Recoverable integration with the Windows desktop.
- Local data by default; no account required.
- No dependency on another desktop customization suite.

## Distribution intent

Provide an EXE installer for individual users and an MSI distribution for managed installations. Public releases require the compatibility, privacy, license, and recovery gates in the plan.

The project license is a decision for the project owner before code publication. Adopted third-party code and dependencies retain their own licenses and notices.

## Current implementation

See [building the overview prototype](docs/BUILDING.md), [WPF UI redesign notes](docs/WPF-UI-REDESIGN.md), and [Dock features, installation, recovery, and limitations](docs/DOCK.md).

The current prototype uses C# and WPF UI 4.3.0 with the existing .NET Framework backend. Search, favorites, window previews, shortcuts, and launch behavior are retained. It is not yet the complete desktop suite described by the roadmap.

## Unified settings

Open **Clean11 Settings** from Start, the overview gear, or a dock app's context menu.
The rebuilt WPF UI window has Appearance, Dock, Overview, Apps & search, and System pages.
Use the fixed **Apply changes** button to save preferences across all pages. Appearance includes an illustrative material preview; pinning and index actions apply immediately as labeled.

**Live game previews** On enables live thumbnails for detected games; Off keeps game cards without live capture. Existing preferences are preserved. Overview changes apply on the next overview opening. Dock changes apply immediately.

The Settings launcher reuses the dock's settings host; if the dock is stopped, opening Settings starts it. Overview preferences live separately from history and favorites in the current user's LocalAppData/Clean11/Overview directory. No user paths or preferences are committed. The dock installer adds the Start-menu shortcut. Build the dock and overview together when updating this feature.

## Credits

Clean11 uses **[WPF UI](https://github.com/lepoco/wpfui)** as its UI library. WPF UI is created by **Leszek Pomianowski and the WPF UI Contributors** and is licensed under the **MIT License**.

Thank you to the WPF UI contributors for their open-source controls, themes, and navigation components. Version 4.3.0 is restored from NuGet during development; compiled dependencies are not committed to source control.

See [third-party notices](THIRD-PARTY-NOTICES.md) and the [full WPF UI license](licenses/WPF-UI-LICENSE.txt). Distributions containing WPF UI must retain its copyright and license notice.

The Settings redesign uses a 210px navigation sidebar, shared setting cards, wrapping descriptions, numeric slider readouts, independently scrolling content, and a fixed save footer. Closing with pending edits offers Apply, Discard (No), or Cancel. Material controls now live under Appearance; startup and bottom-bar integration live under System.

### Shared appearance

Appearance is stored once in the overview preferences: material, glass visibility, and blur radius. Dock and popup composition use the same tint gradient as the Settings material preview. The radius is expressed as a visual blur radius; the composition renderer converts it to Gaussian standard deviation instead of treating radius and sigma as interchangeable.

The existing background-apps widget retains its tray actions. Clean11 supplies its compact theme and a separate native glass backdrop, with the widget above it so labels and icons remain sharp. Saving Appearance reloads the local tray resource without restarting the whole compatibility host. The adapter targets only the identified local tray widget and does not alter other widgets.

## Performance implementation

WPF UI remains the controls, icons, and theme library. Custom native preview hosts and glass composition complement it; changing those layers does not remove the WPF UI dependency or its license obligations.

Appearance reads now use an immutable in-memory snapshot. A debounced file watcher reloads changes outside animation callbacks. Unchanged palettes, edge brushes, and dock activity indicators are reused. Search prepares its composition material while hidden. Overview exit keeps captions and minimized cards visible through their fade, and drives the top search bar's exit with the same progress.

Appearance includes **Compositor glass animation (experimental)**, off by default. This submits a glass reveal animation to Windows Composition. It is a limited prototype: window previews and search geometry still use the established renderer. Turn the toggle off and apply to restore the standard reveal. Reduced motion takes precedence.

### Measurements and diagnostics

From the repository root, run `tools/Measure-Performance.ps1 -Executable <overview-exe> -LogPath <native-log>`. Leave the desktop idle during the run. The script warms one cycle, then records numeric preparation and update timings under ignored `artifacts/performance/`. It rejects incomplete runs. `-AlreadyHidden` supports older executables without `--hide`, provided the caller first establishes a hidden overview.

One local four-cycle before/after comparison measured average preparation of 95.25 ms versus 54.03 ms. Mean per-transition median update gap was 7.28 ms versus 6.80 ms; mean p95 gap was 16.48 ms versus 16.44 ms, and worst gap was 25.4 ms versus 27.2 ms. These are application update timings under a changing desktop workload, not displayed FPS or a guarantee of improvement on other machines. Tail latency remains a target for further work.

For presentation analysis, run `tools/Record-Performance.ps1 Start` from a terminal permitted to profile the system, reproduce transitions, then run `tools/Record-Performance.ps1 Stop`. The WPR profile combines CPU, GPU, desktop composition, and Clean11 timing events. Clean11 events contain only action/area identifiers and numeric timings; system traces may include process metadata and should remain local. Windows denied profiling during the implementation session, so no displayed-FPS result is claimed.

`--self-test` checks geometry, game classification, and settings serialization. `--graphics-self-test` checks palette reuse and repeated hidden composition creation, adjustable blur, opacity animation, and disposal. These checks passed on the current runtime and the .NET 10 candidate. They do not replace visual or presentation testing.

### Separate .NET 10 candidate

`src/Clean11.Modern/Build.ps1 -Dotnet <dotnet-executable>` builds Overview, Dock, and the Settings launcher using .NET 10 and WPF UI 4.3.0. Build outputs are separated per project. The candidate preserves field-based settings JSON and supplies the updated Windows Runtime interop needed by adjustable blur. The Settings launcher must be packaged beside the dock executable; build output directories alone are not an installer.

The installed app continues to use the established .NET Framework build. Promoting the modern candidate requires startup, installer/upgrade, multi-monitor DPI, graphics-device recovery, and full visual parity checks. A full Composition preview renderer is also a future stage, beyond the optional glass prototype. No process-priority escalation, new search provider, or native-language rewrite is included without trace evidence that it would help.
