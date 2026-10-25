param([string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs/Clean11/Dock'),[switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$binary = Join-Path $PSScriptRoot 'bin/Clean11.Dock.exe'
$launcherBinary = Join-Path $PSScriptRoot 'bin/Clean11.exe'
$overviewSource = Join-Path $PSScriptRoot '../Clean11.Overview'
if(!(Test-Path -LiteralPath $binary) -or !(Test-Path -LiteralPath $launcherBinary)){throw 'Build the overview and dock before installing them.'}
if(!(Test-Path -LiteralPath (Join-Path $overviewSource 'ActivitiesOverview.exe'))){throw 'Build the overview before installing the unified startup components.'}
$installRoot = Split-Path -Parent $Destination
$existing = Join-Path $Destination 'Clean11.Dock.exe'
$widgetHost = Join-Path $Destination 'Clean11.Widgets.exe'
$overviewInstalled = Join-Path $installRoot 'Overview/ActivitiesOverview.exe'

# Request orderly shutdown before replacing files. The app itself restores the
# Explorer policy lease and preserves any pending Settings edits.
if(Test-Path -LiteralPath $existing){Start-Process -FilePath $existing -ArgumentList '--shutdown' -WindowStyle Hidden -Wait}
if(Test-Path -LiteralPath $widgetHost){Start-Process -FilePath $widgetHost -ArgumentList '--shutdown' -WindowStyle Hidden -Wait}
if(Test-Path -LiteralPath $overviewInstalled){Start-Process -FilePath $overviewInstalled -ArgumentList '--shutdown' -WindowStyle Hidden -Wait}
$legacyOverview = Join-Path $env:USERPROFILE 'Tools/ActivitiesOverview/ActivitiesOverview.exe'
if(!(Test-Path -LiteralPath $overviewInstalled) -and (Test-Path -LiteralPath $legacyOverview)){
    Start-Process -FilePath $legacyOverview -ArgumentList '--shutdown' -WindowStyle Hidden -Wait
}
$deadline = [DateTime]::UtcNow.AddSeconds(15)
do {
    $running = @(Get-Process -Name 'Clean11.Dock','Clean11.Widgets','ActivitiesOverview' -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -eq $existing -or $_.Path -eq $widgetHost -or $_.Path -eq $overviewInstalled -or $_.Path -eq $legacyOverview } catch { $false }
    })
    if($running.Count -eq 0){break}
    Start-Sleep -Milliseconds 200
} while([DateTime]::UtcNow -lt $deadline)
if($running.Count -gt 0){throw 'A Clean11 window is still open; installation stopped without replacing running files.'}

New-Item -ItemType Directory -Path $installRoot,$Destination -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'bin/*') -Destination $Destination -Recurse -Force
Copy-Item -LiteralPath $launcherBinary -Destination (Join-Path $installRoot 'Clean11.exe') -Force
# The developer build folder also contains a copy of the launcher. Keep the
# installed launcher only at the product root so its module paths stay correct.
$nestedLauncher=Join-Path $Destination 'Clean11.exe'
if(Test-Path -LiteralPath $nestedLauncher){Remove-Item -LiteralPath $nestedLauncher -Force}
$overviewDestination = Join-Path $installRoot 'Overview'
New-Item -ItemType Directory -Path $overviewDestination -Force | Out-Null
foreach($file in @('ActivitiesOverview.exe','ActivitiesTheme.json','Wpf.Ui.dll','Wpf.Ui.Abstractions.dll','System.Memory.dll','System.Buffers.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll')){
    Copy-Item -LiteralPath (Join-Path $overviewSource $file) -Destination $overviewDestination -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../THIRD-PARTY-NOTICES.md') -Destination $installRoot -Force
$licenses = Join-Path $installRoot 'licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot '../../licenses/*') -Destination $licenses -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../THIRD-PARTY-NOTICES.md') -Destination $Destination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../licenses') -Destination $Destination -Recurse -Force

$trayWidget = Join-Path $env:APPDATA 'com.seelen.seelen-ui/widgets/tray-toggle-fix'
if(Test-Path -LiteralPath (Join-Path $trayWidget 'metadata.yml')){
    & (Join-Path $PSScriptRoot 'Tray/Install.ps1') -WidgetDirectory $trayWidget
}

$shortcutDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) 'Clean11'
New-Item -ItemType Directory -Path $shortcutDirectory -Force | Out-Null
$shortcutShell = New-Object -ComObject WScript.Shell
$shortcut = $shortcutShell.CreateShortcut((Join-Path $shortcutDirectory 'Clean11 Settings.lnk'))
$shortcut.TargetPath = Join-Path $installRoot 'Clean11.exe'
$shortcut.Arguments = '--settings'
$shortcut.WorkingDirectory = [Environment]::GetFolderPath('UserProfile')
$shortcut.Description = 'Overview, search, widgets, and dock preferences'
$shortcut.Save()
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcutShell) | Out-Null

$registration=Start-Process -FilePath (Join-Path $installRoot 'Clean11.exe') -ArgumentList '--sync-startup' -Wait -PassThru -WindowStyle Hidden
if($registration.ExitCode -ne 0){throw 'Clean11 startup registration failed. See LocalAppData/Clean11/Dock/dock.log.'}
if(!$NoLaunch){Start-Process -FilePath (Join-Path $installRoot 'Clean11.exe') -ArgumentList '--background' -WorkingDirectory ([Environment]::GetFolderPath('UserProfile')) -WindowStyle Hidden}
Write-Output 'Clean11 installed; per-user sign-in startup synchronized. Settings are stored privately in LocalAppData/Clean11.'
