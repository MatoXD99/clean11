# WPF UI redesign

## Implemented

- WPF UI 4.3.0 theme/control dictionaries shared by all WPF surfaces.
- Library text fields, icon buttons, primary/secondary actions, switches, sliders/styles, context menus, and tooltips.
- Compact overview search and favorites restyled with Fluent spacing, materials, and controls.
- Centered search with rounded result rows, an accent selection indicator, and a distinct detail card.
- Settings reorganized into cards with a proper heading, readable labels, sliders, switches, and action rows.
- Themed rename dialog and third-party license viewer.
- Shared neutral palette and corner sizes for overview badges, preview clipping, close controls, and native fallback surfaces.
- One explicit cyan accent applied through WPF UI's accent manager and the native palette.

The result-row template is a small Clean11 adaptation. Library control templates remain in use for standard controls and menus, including submenu and keyboard behavior.

## Behavior retained

Existing search providers/ranking, Settings discovery, favorites and context actions, keyboard shortcuts, working-directory policy, window previews, overview layout, animation timing, and per-user preferences are retained. The redesign changes the presentation layer; it does not replace the search engine or system integration.

The floating surface continues to own its per-pixel rounded alpha. Its optional frosted material is wallpaper-derived, not live application-background blur. A library theme does not change this renderer limitation.

## Scope and review status

This iteration covers the current custom overview/search app. The existing external dock, top bar, power menu, and Windows-owned file dialogs are outside this presentation layer.

The application compiles and its Search, Settings, and overview-header designs have been rendered offscreen without showing windows or injecting input. The preview generator uses synthetic app entries. Live keyboard, focus, fullscreen/game, and mixed-DPI interactions have not been exercised in this pass. No regression suites were run.

Only reviewed source files, build scripts, theme data, and required notices are committed. Personal preferences, caches, desktop captures, logs, installed binaries, and backup archives remain outside source control.

## Minimal surface refinement

Search uses a single borderless input surface, a compact horizontal favorites row, smaller result rows, and a detail area separated by one subtle rule. Shared theme tokens supply the charcoal palette, low-contrast borders, and rounded corners.

Dismissal sets the closing state before hiding the WPF panel. Previously hiding the panel could reactivate the native owner and reopen the input before closing was marked. Final close and abort also hide the panel; hidden or closing owners cannot reopen it. Returning from search resets the visible query, and Escape is handled at window level.

Interactive checks confirmed immediate typing, PowerShell as the top result, Escape returning to an empty overview, a second Escape dismissing it, and close-button dismissal. Search and dock inspection used `--inspect-ui`; normal background launch does not enable this diagnostic behavior.
