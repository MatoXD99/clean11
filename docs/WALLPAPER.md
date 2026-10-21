# Clean11 Horizon

Open **Clean11 Settings → Wallpaper**. Horizon is one generated scene with eight lighting states. Switching runs entirely offline using Windows' native `IDesktopWallpaper` API, on a background STA worker. It does not add a desktop overlay, play a video, or continuously render frames.

**New installations enable Dynamic Horizon on first launch**, after backing up the previous Windows background. Updates preserve existing wallpaper preferences, including an explicitly disabled Horizon. If the artwork is unavailable or the original background cannot be backed up, the first change is not applied. Horizon is the only bundled wallpaper collection; the installer verifies all eight images against the asset manifest.

## Controls

- **Use Clean11 Horizon:** enables the wallpaper and privately backs up the previous Windows background before changing it. Turning it off restores the backup.
- **Dynamic:** follows the local Windows clock and time zone.
- **Always light / Always dark:** keep Midday or Night respectively.
- **Preview a time of day:** previews the corresponding state inside Settings without changing the actual wallpaper or system clock. The eight thumbnails also select preview times.
- **Restore previous wallpaper:** reapplies the saved background, including when Horizon has paused after an external wallpaper change.

Wallpaper controls apply immediately. The Settings Apply button is reserved for other preferences.

## Local schedule

| Local time | State |
| :-- | :-- |
| 05:00–06:30 | Pre-dawn |
| 06:30–08:00 | Sunrise |
| 08:00–11:00 | Morning |
| 11:00–15:00 | Midday |
| 15:00–18:00 | Afternoon |
| 18:00–20:00 | Sunset |
| 20:00–22:00 | Twilight |
| 22:00–05:00 | Night |

These are fixed clock periods, not astronomical sunrise/sunset calculations. No location access or account is needed. A 30-second timer catches state boundaries; clock changes and resume events request an immediate reevaluation. Time-zone caches are refreshed on clock/resume events. Display changes request reapplication for connected monitors.

Clean11 must be running to switch states. Exiting leaves the most recently applied picture in place. Relaunch catches up to the current local state. If another app or the user changes the Windows wallpaper, the next check pauses Horizon instead of repeatedly replacing the new choice. Third-party live wallpaper renderers may cover the native Windows background; their rendering is not controlled by Clean11.

## Backup and restoration

Settings and backups are private to the current Windows account in `%LOCALAPPDATA%\Clean11\Wallpaper`. The original pictures are copied before enabling Horizon, so a mutable Windows wallpaper cache cannot overwrite the backup. The backup also records monitor identities, placement, background color, and native slideshow paths/options when available. It is saved before the first wallpaper change. A failed first apply attempts to restore that backup.

Restoration uses the saved per-monitor pictures, with the first saved picture as a fallback for newly connected displays. Native slideshows retain their original source paths and timing/shuffle options; those source files/folders must still exist. Separate third-party wallpaper engines and the Windows Spotlight provider configuration are not backed up by this picture/slideshow API. Turn Horizon off before uninstalling if you want the previous background restored. Backups and preferences are retained across uninstall and are not bundled in source or installers.

Horizon fills each connected monitor with the selected image. The original placement is restored when it is disabled. Glass uses the existing shared wallpaper cache, invalidated after an apply; other Clean11 processes refresh as they next request the changed wallpaper material. Changes are discrete lighting states; seamless crossfading is not implemented.

## Artwork and packaging

Original assets and prompts are in [assets/wallpapers/horizon](../assets/wallpapers/horizon/README.md). All eight images are **1672 × 941 generated originals**, not native 4K or upscaled. The composition stays closely aligned across generative relightings, with small generative differences possible. The asset manifest records hashes and schedule metadata.

The standard build copies the eight PNGs and manifest next to the app under `Wallpapers/Horizon`; the MSI includes them. The modern dock build copies the same assets. Included in v0.3.0 Alpha.

References: [Windows wallpaper API](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-idesktopwallpaper), [setting a wallpaper](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-idesktopwallpaper-setwallpaper).
