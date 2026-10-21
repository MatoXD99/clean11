# Clean11 Horizon artwork

One original limestone-and-water landscape in eight lighting states. The eight PNG files are the generated originals, **1672 × 941**, not native 4K and not upscaled. They were created with the built-in `image_gen` tool on 2026-10-21. No vendor wallpaper or reference photograph was used.

`midday.png` is the master. Each other state was generated as a lighting/weather edit referencing that master. Generative relighting keeps the composition closely aligned but is not a pixel-identical physical lighting simulation. `manifest.json` records filenames, local schedule, dimensions, and SHA-256 hashes.

## Master prompt

> Use case: stylized-concept. Asset type: original desktop wallpaper for Clean11, named Horizon. Create ONE image, not a contact sheet. Wide 16:9 landscape, highest available resolution, target 3840x2160. A premium, quiet, physically rendered abstract landscape: broad flowing pale porcelain/limestone ridges sweeping from the lower left toward the distant right, a perfectly calm reflective inlet between the sculptural ridges, and a low distant horizon beneath a spacious clear sky. Natural sculptural landforms, not floating objects. Beautiful subtle material detail without visual noise. Midday lighting state: clear soft daylight, pearl white stone, pale cool blue sky, subtle cyan water and soft restrained shadows, balanced highlights without blown whites. Composition: horizon in upper-middle third, airy negative space across upper half and left for desktop icons; more sculptural interest lower right. Calm, refined, original visual identity, depth and soft atmospheric perspective. Fixed eye-level wide camera, clean smooth silhouettes, no plants or manmade objects. No text, no logo, no watermark, no UI, no people, no vendor branding, no reference wallpaper replicas. This image is the geometric master of a set of eight time-of-day relightings; keep the scene simple enough for faithful lighting-only edits.

The tool returned 1672 × 941 despite the target resolution in the prompt.

## Lighting edit prompt set

Each edit used this common instruction, with the lighting paragraph below inserted after “Lighting state:”:

> Use case: lighting-weather. Edit target: the provided Clean11 Horizon midday master wallpaper. Produce ONE full-width wallpaper, not a grid or comparison. Change ONLY the time-of-day illumination, sky colors, exposure, cast-shadow illumination and corresponding water reflections. Lighting state: [state paragraph] Mandatory invariants: preserve the exact camera, 16:9 framing, horizon height, ALL limestone ridge shapes and positions, distant shoreline, inlet boundary, surface detail and reflection geometry from the input. Do not add or remove objects; no geometry redesign. This is the same scene at another hour, not a similar new scene. Highest available resolution, target 3840x2160. No text, logos, branding, watermark, UI or frames.

- **Pre-dawn:** Pre-dawn before sunrise. Deep indigo-blue sky with a faint lavender glow along the horizon; stone in cool violet-blue shadow, dark still water, restrained atmospheric light. No visible sun or stars. Clearly darker than morning, but retain delicate readable terrain.
- **Sunrise:** Sunrise. Soft peach light along the horizon and pale lavender upper sky, a low warm light from frame left grazing the exact ridges, warm pink-pearl stone highlights, cool violet shadows, peach and lavender reflections in water. Refined, quiet, realistic illumination.
- **Morning:** Morning. Cool pale blue sky, soft warm sunlight from the left, pearl stone gently warmed on illuminated slopes and long blue-gray shadows. Crisp but gentle light and clear blue water; visually between sunrise and midday.
- **Afternoon:** Late afternoon. Warmer honey-white sun grazing stone from frame right with noticeably longer shadows than midday, sky softly blue with a pale apricot horizon; clear blue water reflecting warm highlights. Preserve the same landforms exactly.
- **Sunset:** Sunset. Beautiful amber and dusty rose light, low saturated apricot-peach horizon fading to muted violet above. Limestone ridges glow copper-pink with violet shadows. Still water reflects warm sky and pink land. Rich but restrained, no neon, no visible sun disk.
- **Twilight:** Twilight just after sunset. Deep blue-purple sky, a thin fading dusty pink horizon glow, stone cool lavender and slate blue with subdued highlights, reflective inlet violet and blue. Quiet, elegant blue hour, darker than sunset but lighter than night. No stars or sun disk.
- **Night:** Night. Dark navy sky, subtle cool moonlit illumination from off-frame right, limestone ridges in dark blue with delicate silver edges, very dark blue still water with soft reflected light. No moon disk, stars, artificial lights or added objects. Properly dark nighttime wallpaper while retaining readable sculptural silhouettes.

Project licensing is described in the repository README. Generated artwork does not change third-party dependency licenses.
