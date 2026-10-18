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
