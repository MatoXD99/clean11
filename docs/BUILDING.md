# Building the overview prototype

The current application is a Windows-only C# / WPF / WinForms prototype. It uses WPF UI 4.3.0's .NET Framework 4.7.2-compatible assemblies and the existing Windows .NET Framework compiler. Windows 11 normally supplies a newer compatible .NET Framework runtime. This is a transitional build, not the proposed future modern-.NET solution.

From `src/Clean11.Overview` in PowerShell:

```powershell
.\RestoreDependencies.ps1
.\Build.ps1
```

Restore downloads the NuGet command-line client from its official distribution endpoint and resolves the pinned WPF-UI package from nuget.org. Build copies the required assemblies alongside `ActivitiesOverview.exe`. This compatibility executable name is retained for existing startup/hotkey integrations.

Build does not run the app, install it, or execute tests. No development SDK migration is required for this iteration. It expects the 64-bit .NET Framework compiler under the Windows directory.

The companion DLLs must travel with the executable. Also copy the repository's `licenses` directory next to it when packaging. The application reads `ActivitiesTheme.json` for shared visual tokens; personal preferences remain in the existing per-user data location and are not part of this repository.

The optional Everything SDK DLL is deliberately omitted. Basic app/window/settings search does not require it. Existing deployments retain their installed file-search component; public redistribution requires a separate review.

The dock and unified Settings have their own build under `src/Clean11.Dock`; see [Dock](DOCK.md). A unified launcher and preview MSI package the implemented modules. Optional power-menu integration falls back to Windows power settings when its compatibility host is absent. The integrated top bar and device controls run inside the dock host; see [Top Bar](TOP-BAR.md). Desktop, a standalone power menu, and release recovery validation remain roadmap work.

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

## Installer and Windows Sandbox

`installer/Build.ps1 -Version 0.2.0` builds the current .NET Framework application and a per-user x64 MSI using Windows Installer's native database API, `makecab.exe`, and the Windows C# compiler. No third-party installer authoring tool is required. Dependency restore needs internet access on the build machine. End users do not need a compiler, NuGet, or the .NET 10 SDK.

The MSI requires Windows 11 and .NET Framework 4.8 or later, installs beneath the current user's `LocalAppData/Programs/Clean11`, and includes only an explicit set of binaries, theme configuration, and license notices. Logs, personal settings, keys, and development files are excluded. `Clean11.exe` starts both modules; sign-in startup uses this same launcher. Modules resolve their siblings in the package before considering legacy development locations.

The welcome screen offers startup at sign-in. The package records the choice for first launch, including when existing preferences are present. On upgrades, the previous startup state supplies the default. Quiet deployments that intentionally override that default can pass `FORCESTARTUP=1 STARTWITHWINDOWS=1` (or `STARTWITHWINDOWS=""` for off). Other preferences remain intact. First-time dock settings leave taskbar replacement off until selected in Settings.

Setup provides Start-menu shortcuts, standard Installed apps registration, repair, major-upgrade authoring, a downgrade check, and uninstall. Maintenance asks the modules to stop gracefully and does not kill a Settings window with pending edits. If Settings prevents shutdown, resolve its save/discard prompt and retry. Personal app data is preserved on uninstall.

### Local validation

```powershell
.\installer\Build.ps1 -Version 0.2.0
.\installer\Verify-Package.ps1 -Package artifacts/installer/Clean11-0.2.0-x64.msi
```

The verifier checks database structure, embedded cabinet, directory costing, and prerequisite detection without installing. Administrative extraction with `msiexec /a` was also exercised, and all 28 extracted payload files matched their manifest hashes. The packaged overview logic/graphics checks and launcher path check passed. The welcome screen and cancellation were inspected on the host; installation was not performed on the development account.

Build output includes versioned payload manifests and a SHA-256 checksum. Builds use a fresh staging folder and replace the final MSI only after successful creation. Increase the three-part package version for each distributed update; do not replace a published version with different contents.

### Disposable clean-machine trial

```powershell
# Optional second package exercises major upgrade as well.
.\installer\Build.ps1 -Version 0.1.1
.\installer\Prepare-Sandbox.ps1 -UpgradePackage .\artifacts\installer\Clean11-0.1.1-x64.msi -Launch
```

This creates `artifacts/installer/Test-Clean11.wsb`, a read-only `SandboxPackage` input folder, and a separate writable `SandboxResults` folder for test logs. Only those two dedicated folders are mapped; the repository and personal folders are not shared. Networking and clipboard redirection are disabled. The test runs with the inbox Windows PowerShell. Its interactive pause allows visual checks before repair, optional major upgrade/downgrade rejection, and uninstall. Results remain in the host's `SandboxResults` folder after the disposable environment closes.

The script checks installation files, shortcuts, startup registration, module startup, graceful shutdown, repair, optional upgrade, uninstall, retained personal data, and an installation with startup disabled. Manually inspect dragging, search, the Windows-key overview, dock behavior, and taskbar recovery. Sandbox graphics are useful for compatibility testing but do not establish performance on physical hardware.

Windows Sandbox was unavailable on the development host: the executable was absent and Windows reported no active hypervisor. The clean lifecycle test has therefore **not run**. Enable Sandbox on a suitable Windows 11 Pro/Enterprise machine with firmware virtualization and any required restart before launching the `.wsb` file. See [Microsoft's Sandbox installation instructions](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install).

### Before publishing binaries

The MSI is an unsigned preview. Clean Sandbox lifecycle and recovery checks, the project's own license decision, signing/publisher identity, and a supported-build matrix remain release gates. Required dependency notices are bundled. The optional Everything SDK, third-party tray host, proprietary wallpapers, and personal app inventories are not bundled. The native Windows tray is available as a fallback, and the power action opens Windows power settings if the optional host is absent.
