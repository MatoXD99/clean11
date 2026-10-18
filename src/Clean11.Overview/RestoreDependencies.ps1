$ErrorActionPreference = 'Stop'
$nuget = Join-Path $PSScriptRoot 'nuget.exe'
if (-not (Test-Path -LiteralPath $nuget)) {
    Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $nuget
}
& $nuget install WPF-UI -Version 4.3.0 -Framework net472 -OutputDirectory (Join-Path $PSScriptRoot 'packages') -NonInteractive -Source https://api.nuget.org/v3/index.json -DirectDownload
if ($LASTEXITCODE -ne 0) { throw 'WPF UI dependency restore failed' }
