# Third-party notices

## WPF UI

- Project: https://github.com/lepoco/wpfui
- Authors: Leszek Pomianowski and WPF UI Contributors
- License: MIT
- Full license: [licenses/WPF-UI-LICENSE.txt](licenses/WPF-UI-LICENSE.txt)
- Upstream license: https://github.com/lepoco/wpfui/blob/main/LICENSE
- Status: integrated into the overview/search prototype through NuGet.
- Version: WPF-UI 4.3.0 and WPF-UI.Abstractions 4.3.0.
- License retrieved: 2026-10-18; full text copied from the restored package.

Copyright (c) 2021-2025 Leszek Pomianowski and WPF UI Contributors. https://lepo.co/

The MIT License permits use, modification, and distribution, including commercial use, subject to retaining its copyright and permission notice. The full upstream license is preserved in this repository.

Include this notice and the full license with distributed application packages. The prototype exposes notices through Settings > Third-party licenses. Retain the package's [third-party notices](licenses/WPF-UI-ThirdPartyNotices.txt), including its incorporated components and icon resources.

The library's license does not automatically cover every font, icon, or sample asset referenced by its gallery. Review those assets separately. In particular, do not bundle the Segoe Fluent Icons font merely because it is referenced by a sample; use the installed Windows font where appropriate. See the [upstream asset notes](https://github.com/lepoco/wpfui#segoe-fluent-icons).

## .NET support libraries

NuGet resolves System.Memory 4.6.3, System.Buffers 4.6.1, System.Numerics.Vectors 4.6.1, and System.Runtime.CompilerServices.Unsafe 6.1.2 for the .NET Framework build. Retain the [.NET Foundation and Contributors MIT license](licenses/DOTNET-RUNTIME-LICENSE.txt).

## Existing adapted source

- Search scoring: Flow Launcher / Wox, MIT. See [FLOW-LAUNCHER-LICENSE.txt](licenses/FLOW-LAUNCHER-LICENSE.txt).
- Native wallpaper/preview presentation: WindowScatter, MIT. See [WINDOWSCATTER-LICENSE.txt](licenses/WINDOWSCATTER-LICENSE.txt).

The optional Everything SDK DLL is not committed or redistributed by this repository. Existing installations can retain their configured file-index integration; new distributions require a separate SDK redistribution review.

## DeskWidgets desktop behavior

- Project: https://github.com/VaticUI/DeskWidgets
- Copyright (c) 2026 VaticUI
- License: MIT; full text: [licenses/DESKWIDGETS-LICENSE.txt](licenses/DESKWIDGETS-LICENSE.txt).
- Reviewed source: `core.py`, desktop ownership / bottom-order behavior (`WidgetWindow.pin` and `nativeEvent`).
- Upstream revision: e37bf4eb3427e2dc5c76937f3c6bf1bd27255ad3
- Modifications: selectively adapted into C#/WPF in `src/Clean11.Dock/DesktopWidgets.cs`; replaced cross-process Progman ownership with positioning above the desktop to avoid implicit input-queue attachment; added `SWP_NOOWNERZORDER`, Clean11 lifecycle, persistence, controls, and shared material integration. The four widget implementations and gallery are implemented for Clean11. Python, PySide6/Qt, and upstream binaries/assets are not bundled.
- Reviewed: 2026-10-21. Retain this notice and the full MIT license in source and installer distributions.