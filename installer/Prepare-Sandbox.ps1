param([string]$Package=(Join-Path $PSScriptRoot '../artifacts/installer/Clean11-0.1.0-x64.msi'),[string]$UpgradePackage,[switch]$Launch)
$ErrorActionPreference='Stop'
$packagePath=(Resolve-Path $Package).Path
$bundle=Join-Path (Split-Path $packagePath) 'SandboxPackage'
New-Item -ItemType Directory -Force $bundle | Out-Null
Copy-Item -LiteralPath $packagePath -Destination (Join-Path $bundle 'Clean11-0.1.0-x64.msi') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Test-Sandbox.ps1') -Destination $bundle -Force
if($UpgradePackage){Copy-Item -LiteralPath (Resolve-Path $UpgradePackage).Path -Destination (Join-Path $bundle 'Clean11-upgrade.msi') -Force}
$hostPath=[Security.SecurityElement]::Escape($bundle)
$results=Join-Path (Split-Path $packagePath) 'SandboxResults'
New-Item -ItemType Directory -Force $results | Out-Null
$resultsPath=[Security.SecurityElement]::Escape($results)
$configuration=@"
<Configuration>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <MappedFolders>
    <MappedFolder><HostFolder>$hostPath</HostFolder><SandboxFolder>C:\Clean11Package</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$resultsPath</HostFolder><SandboxFolder>C:\Clean11Results</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -NoExit -Command "&amp; 'C:\Clean11Package\Test-Sandbox.ps1' -ResultsDirectory 'C:\Clean11Results' -Interactive"</Command></LogonCommand>
</Configuration>
"@
$file=Join-Path (Split-Path $packagePath) 'Test-Clean11.wsb'
[IO.File]::WriteAllText($file,$configuration)
Write-Output "Prepared $file. Package input is read-only; the separate SandboxResults folder receives logs. Networking and clipboard are disabled."
if($Launch){
    $sandbox=Join-Path $env:WINDIR 'System32/WindowsSandbox.exe'
    if(!(Test-Path $sandbox)){throw 'Windows Sandbox is not enabled on this host. The test bundle is ready for a Windows 11 machine with Sandbox and virtualization enabled.'}
    Start-Process -FilePath $sandbox -ArgumentList ('"'+$file+'"')
}
