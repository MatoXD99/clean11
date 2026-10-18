# Clean11

A cohesive desktop experience for Windows 11: fast search, window overview, a floating dock, a top bar, a customizable desktop, and one Settings app.

**Status: overview/search prototype.** The repository now contains the existing overview/search application redesigned with WPF UI, alongside the full desktop-suite blueprint. The custom Dock, Top Bar, Desktop, and installer remain planned work.

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

See [building the overview prototype](docs/BUILDING.md) and [WPF UI redesign notes](docs/WPF-UI-REDESIGN.md).

The current prototype uses C# and WPF UI 4.3.0 with the existing .NET Framework backend. Search, favorites, window previews, shortcuts, and launch behavior are retained. It is not yet the complete desktop suite described by the roadmap.

## Credits

Clean11 uses **[WPF UI](https://github.com/lepoco/wpfui)** as its UI library. WPF UI is created by **Leszek Pomianowski and the WPF UI Contributors** and is licensed under the **MIT License**.

Thank you to the WPF UI contributors for their open-source controls, themes, and navigation components. Version 4.3.0 is restored from NuGet during development; compiled dependencies are not committed to source control.

See [third-party notices](THIRD-PARTY-NOTICES.md) and the [full WPF UI license](licenses/WPF-UI-LICENSE.txt). Distributions containing WPF UI must retain its copyright and license notice.
