# Shared design system

## Visual direction

All Clean11-owned UI uses one design system: Search input and results, overview labels and close controls, favorites, Dock, Top Bar, Desktop menus, tooltips, folder stacks, Settings, dialogs, onboarding, and recovery. Native system dialogs remain recognizably Windows when the shell owns them.

Glass should convey depth without reducing readability. Avoid opaque gray rectangles behind rounded panels, multiple unrelated corner radii, excessive outlines, and decorative motion that delays input.

## Token model

Create a versioned `theme.tokens.json` as the source of truth and generate typed resources for every renderer. User themes override semantic tokens through validation; no arbitrary code in themes.

Proposed starting values are design defaults, subject to the composition prototype:

| Token | Initial value / role |
| --- | --- |
| Radius.Surface | 24 device-independent pixels: Search, cards, large flyouts |
| Radius.Preview | 24: overview previews, sharing the surface family |
| Radius.Menu | 16: menus and compact flyouts |
| Radius.Item | 12: selection rows and large buttons |
| Radius.Control | 10: smaller controls |
| Radius.Capsule | Half height: compact input/category controls |
| Space | 4, 8, 12, 16, 24, 32 |
| Dock.Icon | 48 default, configurable approximately 32–72 |
| TopBar.Height | 32 default, scales for text/accessibility |
| Motion.Hover | 100 ms, colors/opacity only |
| Motion.Popup | 160 ms, opacity and small translation |
| Motion.Transform | 280 ms, interruptible overview/search transition |
| Surface / SurfaceRaised | Separate light/dark semantic materials |
| Text.Primary / Secondary | Contrast-tested colors, independent of wallpaper |
| Accent / Selection / Focus | Separate semantic roles, visible in both themes |
| Stroke / Shadow | One restrained edge and elevation system |

Consistency means using a defined family for each role. It does not mean every tiny control has the same radius as a large panel. Inner clips are derived from the outer radius and border inset.

## Material and corner ownership

1. Prototype a single composited surface with a single owner for outer clipping, shadow, material, and hit testing.
2. Confirm transparent pixels outside the intended curve on contrasting backgrounds, including during resizing and transitions.
3. Do not combine a rectangular native backdrop with an independently rounded foreground and assume the foreground clips the backdrop.
4. Separate actual background blur, wallpaper-derived material, and opaque fallback as explicit material modes. A wallpaper preview is not live blur.
5. Prefer supported system composition/material APIs. If a chosen renderer cannot achieve the specified shape and material together, settle that limitation before building every surface on it.
6. Honor system transparency, high contrast, remote-session restrictions, battery mode, and reduced effects. Fallback retains layout and contrast.

Windows offers distinct backdrop materials with different behavior; selecting one is part of the prototype, not proof that arbitrary window shapes are supported. [System backdrop documentation](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops).

## Interaction and motion

- Keep one live text-input control throughout overview/search transitions. Animate its enclosing composition surface; preserve IME and caret.
- Animate transform and opacity where possible. Avoid relaying out every result row on every frame.
- Reverse interrupted animations from their current position. No queued animations or repeated resize jumps.
- Hide overview-only content as the search panel expands. Reveal results after their layout is stable; first input is accepted immediately.
- Use a shared popup state machine for buttons, outside clicks, focus loss, Escape, nested menus, and app shutdown.
- Use cached icons and stable row heights. Hover affects only visual state, never provider queries or synchronous process inspection.
- Dock magnification has bounded influence on neighboring items and a stable interaction region so targets do not chase the pointer.
- Overview labels, previews, and controls form one logical card, including movement, clipping, and dismissal.

## Accessibility

System text scaling, accessible names and roles, screen-reader navigation, visible focus, keyboard equivalents, contrast-aware materials, and complete reduced-motion paths are required. Information cannot depend only on color, animation, or translucency. Localization includes long strings and right-to-left layout.

## Required visual review set

Review compact overview input, favorites, empty Search, populated Search, details, no-results, provider failure, Settings, rename dialog, context/submenus, dock magnification, tray flyout, power menu, desktop selection, and window previews.

Each must be inspected in light/dark/high-contrast modes, at 100/125/150/200% scaling, on mixed-DPI displays, and against both very light and very dark backgrounds. Hidden/offscreen renders can check assets and clipping, but actual desktop composition and input behavior require a later scheduled interactive review.

## Clean11 glass materials

Midnight (default) is cool blue-gray smoked glass; Pearl is an icy light frost with dark ink. Both share a mild vertical tint gradient and a restrained cool edge highlight. At maximum visibility a small tint remains to support text contrast. Settings > Overview & search selects the material; existing blur radius and glass visibility remain independent. Search ink and result resources update with the preset; preview labels and dock utility glyphs follow it. This is a curated two-preset material, not adaptive per-pixel vibrancy or simulated optical refraction. Dock uses the live compositor; search and preview panels retain the cached wallpaper source. External Seelen components are unchanged.
