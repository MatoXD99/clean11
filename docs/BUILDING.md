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

This prototype still calls the existing external power-menu integration. A standalone Clean11 power menu, Dock, Top Bar, Desktop, installer, and recovery system are later roadmap work.
