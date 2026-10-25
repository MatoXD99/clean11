# Rendering and performance

Clean11 uses native DWM live thumbnails for the overview, Windows Composition for supported live glass, and WPF for controls, labels and search. Hardware rendering capability does not guarantee that every operation uses the GPU. Wallpaper-derived frosted bitmaps are generated once and reused.

## Animation rules

- Overview previews use a dedicated DWM-paced worker. The header entrance/exit, captions and minimized cards follow its submitted progress. Coalesced UI callbacks read the latest sample rather than replaying an old queued sample.
- The top bar slides upward, the dock slides downward, and desktop widgets fade when overview opens; leaving restores them. Transition boundaries and remaining duration are sent to each process through targeted local window messages. Each process uses its own WPF animation clock, rather than receiving per-frame IPC. Interruptions replace the current animation from its displayed value. Reduced-motion preferences apply. Hidden notification state lets late-starting modules catch up and restores the desktop if overview exits unexpectedly. Work-area reservations remain unchanged throughout.
- At sign-in, the dock and top bar use one compositor-paced ease-out entrance; widgets fade in on their own UI thread. Reduced motion skips this entrance. The launcher records sign-in launch milestones in the existing local dock log so cold-start delay can be separated from app initialization; search indexing remains unchanged.
- Widget thumbnail surfaces fade in the overview background beneath app previews. Real widget windows retain their desktop stacking, and the overview itself is excluded from game/fullscreen detection so the normal fullscreen timers do not interrupt scene motion.
- Search expansion/collapse uses WPF rendering callbacks, fixed body layout, a reusable reveal clip, and one preallocated outer window buffer. The header and border retain their changing geometry. Input, interruption, reverse motion, and reduced motion keep their existing behavior.
- Dock magnification uses elapsed time with fractional milliseconds, ignores duplicate rendering timestamps, snaps to its final values, and unsubscribes when settled.
- Minimized-card glass fades through the composition visual, without changing native window layering. Plain captions do not poll for a glass companion they do not have.
- App-catalog refresh starts after warmup, defers during transitions, and uses a background thread below normal priority. Search providers and file-index selection are unchanged.
- The wallpaper-material cache retains up to four display/configuration entries and at most 32 MiB of image pixels. This limit excludes temporary build buffers and compositor copies. Wallpaper changes invalidate all entries.

Normal process priority is retained. The existing overview animation worker uses `AboveNormal`; other applications retain their normal scheduling and window hierarchy.

## Checks and measurements

Build the applications first, then run:

```powershell
./tools/Test-Rendering.ps1
```

The harness checks animation endpoints, interruption, shared header timing, reduced motion, minimized-card close callbacks and glass visibility, cache reuse/limits, Horizon schedule boundaries, first-install policy, and native wallpaper enumeration. It creates short-lived sample surfaces, does not inject input, and does not change wallpaper or user documents.

The existing overview `--self-test` and `--graphics-self-test` switches cover additional geometry/classification and composition API checks. They do not measure displayed FPS.

```powershell
./tools/Measure-Performance.ps1 -Executable <overview-exe> -LogPath <native-log> -Runs 8
```

This reports preparation time and application submission intervals. Compare repeated warm runs with the same windows, monitor, settings and workload. Changes in other applications, capture tools and cold startup affect the results. Submission counts and global DWM counters are not presented-frame counts.

For CPU, GPU and compositor correlation, use `tools/Record-Performance.ps1` from a session with Windows performance-profiling privileges. If Windows denies the trace, no GPU/FPS result is confirmed. Diagnostic recordings and reports stay in ignored local artifacts; do not publish recordings containing other applications' activity.
