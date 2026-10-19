param([string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
foreach($project in @('Clean11.Overview.Modern.csproj','Clean11.Dock.Modern.csproj','Clean11.Settings.Modern.csproj')){
    & $Dotnet build (Join-Path $PSScriptRoot $project) -c Release --nologo
    if($LASTEXITCODE -ne 0){throw "Modern build failed: $project"}
}
