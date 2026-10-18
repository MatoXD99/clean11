param([string]$Installation = (Join-Path $env:LOCALAPPDATA 'Programs/Clean11/Dock'))
$ErrorActionPreference = 'Stop'
$executable = Join-Path $Installation 'Clean11.Dock.exe'
if(Test-Path -LiteralPath $executable){Start-Process -FilePath $executable -ArgumentList '--shutdown' -WindowStyle Hidden -Wait}
$run = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
Remove-ItemProperty -LiteralPath $run -Name Clean11Dock -ErrorAction SilentlyContinue
$settingsFile = Join-Path $env:LOCALAPPDATA 'Clean11/Dock/settings.json'
if(Test-Path -LiteralPath $settingsFile){
    $settings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
    $settings.Startup = $false
    $settings | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $settingsFile -Encoding UTF8
}
Write-Output 'Clean11 Dock shutdown requested and automatic startup disabled. Pins and settings have been preserved.'
