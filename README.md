# Clean11

A cohesive desktop experience for Windows 11: fast search, window overview, a floating dock, a top bar, a customizable desktop, and one Settings app.

**Status: planning only.** This repository contains the proposed product and engineering blueprint. It contains no application implementation or installer yet.

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

The project license is a decision for the project owner before code publication. No third-party code or assets are bundled in this planning repository.
