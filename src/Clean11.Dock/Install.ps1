param([string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs/Clean11/Dock'))
$ErrorActionPreference = 'Stop'
$binary = Join-Path $PSScriptRoot 'bin/Clean11.Dock.exe'
if(!(Test-Path -LiteralPath $binary)){throw 'Build the dock before installing it.'}
$existing = Join-Path $Destination 'Clean11.Dock.exe'
if(Test-Path -LiteralPath $existing){
    Start-Process -FilePath $existing -ArgumentList '--shutdown' -WindowStyle Hidden -Wait
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $running = @(Get-Process -Name 'Clean11.Dock' -ErrorAction SilentlyContinue | Where-Object Path -eq $existing)
        if($running.Count -eq 0){break}
        Start-Sleep -Milliseconds 200
    } while([DateTime]::UtcNow -lt $deadline)
    if($running.Count -gt 0){throw 'The previous dock has not exited; installation stopped.'}
}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'bin/*') -Destination $Destination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../THIRD-PARTY-NOTICES.md') -Destination $Destination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../licenses') -Destination $Destination -Recurse -Force
$trayWidget = Join-Path $env:APPDATA 'com.seelen.seelen-ui/widgets/tray-toggle-fix'
if(Test-Path -LiteralPath (Join-Path $trayWidget 'metadata.yml')){
    & (Join-Path $PSScriptRoot 'Tray/Install.ps1') -WidgetDirectory $trayWidget
}
Start-Process -FilePath $existing -WorkingDirectory ([Environment]::GetFolderPath('UserProfile')) -WindowStyle Hidden
Write-Output 'Clean11 Dock installed and started. Settings are stored privately in LocalAppData/Clean11/Dock.'

$shortcutDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) 'Clean11'
New-Item -ItemType Directory -Path $shortcutDirectory -Force | Out-Null
$shortcutShell = New-Object -ComObject WScript.Shell
$shortcut = $shortcutShell.CreateShortcut((Join-Path $shortcutDirectory 'Clean11 Settings.lnk'))
$shortcut.TargetPath = Join-Path $Destination 'Clean11.Settings.exe'
$shortcut.WorkingDirectory = [Environment]::GetFolderPath('UserProfile')
$shortcut.Description = 'Overview, search, and dock preferences'
$shortcut.Save()
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcutShell) | Out-Null
