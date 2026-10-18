param([string]$WidgetDirectory = (Join-Path $env:APPDATA 'com.seelen.seelen-ui/widgets/tray-toggle-fix'))
$ErrorActionPreference = 'Stop'
$metadata = Join-Path $WidgetDirectory 'metadata.yml'
if (!(Test-Path -LiteralPath $metadata) -or
    !(Select-String -LiteralPath $metadata -SimpleMatch '@local/system-tray' -Quiet)) {
    throw 'The existing @local/system-tray compatibility widget is required.'
}
$theme = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../Clean11.Overview/ActivitiesTheme.json') -Raw | ConvertFrom-Json
$variables = foreach ($property in $theme.PSObject.Properties) {
    $value = [string]$property.Value
    if ($value -match '^#[0-9a-fA-F]{8}$') {
        # WPF uses AARRGGBB; CSS uses RRGGBBAA.
        $value = '#' + $value.Substring(3,6) + $value.Substring(1,2)
    } elseif ($property.Name -match 'Radius$') {
        $value += 'px'
    }
    "  --clean11-$($property.Name): $value;"
}
$css = ":root {`n" + ($variables -join "`n") + "`n}`n" + (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'style.css') -Raw)
$target = Join-Path $WidgetDirectory 'style.css'
$backupDirectory = Join-Path $env:LOCALAPPDATA 'Clean11/Dock'
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
$backup = Join-Path $backupDirectory 'tray-style.before-clean11.css'
if ((Test-Path -LiteralPath $target) -and !(Test-Path -LiteralPath $backup)) {
    Copy-Item -LiteralPath $target -Destination $backup
}
[IO.File]::WriteAllText($target, $css, [Text.UTF8Encoding]::new($false))
Write-Output 'Clean11 tray appearance installed. Reload the compatibility host to apply it.'
