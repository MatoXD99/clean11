param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='0.1.0',[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$parsedVersion=[Version]$Version
if($parsedVersion.Major -gt 255 -or $parsedVersion.Minor -gt 255 -or $parsedVersion.Build -gt 65535){throw 'MSI version fields must fit 255.255.65535.'}
$repo=(Resolve-Path (Split-Path $PSScriptRoot)).Path
$out=Join-Path $repo 'artifacts/installer'
$stage=Join-Path $out ('staging/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $out | Out-Null
if(!$SkipBuild){
    if(!(Test-Path "$repo/src/Clean11.Overview/packages/WPF-UI.4.3.0")){& "$repo/src/Clean11.Overview/RestoreDependencies.ps1"}
    & "$repo/src/Clean11.Overview/Build.ps1" -Version $Version
    & "$repo/src/Clean11.Dock/Build.ps1" -Version $Version
}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
New-Item -ItemType Directory -Force "$stage/Overview","$stage/Dock","$stage/licenses" | Out-Null
$launcherSource=(Resolve-Path (Join-Path $repo 'src/Clean11.Launcher/Program.cs')).Path
$versionSource=Join-Path $out 'LauncherVersion.cs'
[IO.File]::WriteAllText($versionSource,('[assembly:System.Reflection.AssemblyVersion("{0}.0")][assembly:System.Reflection.AssemblyFileVersion("{0}.0")][assembly:System.Reflection.AssemblyCompany("Clean11 Contributors")][assembly:System.Reflection.AssemblyProduct("Clean11")]' -f $Version))
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$stage/Clean11.exe" /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll $launcherSource $versionSource
if($LASTEXITCODE -ne 0){throw 'Launcher build failed.'}
$libraries=@('Wpf.Ui.dll','Wpf.Ui.Abstractions.dll','System.Memory.dll','System.Buffers.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll')
foreach($module in @('Overview','Dock')){
    $source=if($module -eq 'Dock'){"$repo/src/Clean11.Dock/bin"}else{"$repo/src/Clean11.Overview"}
    $executables=if($module -eq 'Dock'){@('Clean11.Dock.exe','Clean11.Settings.exe','Clean11.Widgets.exe')}else{@('ActivitiesOverview.exe')}
    foreach($exe in $executables){if([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $source $exe)).FileVersion -ne ($Version+'.0')){throw "Component version does not match package $Version. Rebuild without -SkipBuild."}}
    foreach($file in ($libraries+$executables+@('ActivitiesTheme.json'))){Copy-Item -LiteralPath (Join-Path $source $file) -Destination "$stage/$module/$file"}
}
# Stage the shipped collection explicitly; stale local build assets never ship.
$horizonSource=Join-Path $repo 'src/Clean11.Dock/bin/Wallpapers/Horizon'
$horizonTarget=Join-Path $stage 'Dock/Wallpapers/Horizon'
New-Item -ItemType Directory -Force $horizonTarget | Out-Null
$horizonManifest=Get-Content (Join-Path $horizonSource 'manifest.json') -Raw | ConvertFrom-Json
if($horizonManifest.phases.Count -ne 8){throw 'The Horizon package must contain eight lighting states.'}
foreach($phase in $horizonManifest.phases){
    if($phase.file -cne ($phase.id+'.png') -or $phase.id -cnotmatch '^(predawn|sunrise|morning|midday|afternoon|sunset|twilight|night)$'){throw 'Unexpected wallpaper asset in Horizon manifest.'}
    $source=Join-Path $horizonSource $phase.file
    if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ine $phase.sha256){throw 'Horizon artwork does not match its manifest. Rebuild before packaging.'}
    Copy-Item -LiteralPath $source -Destination $horizonTarget
}
Copy-Item -LiteralPath (Join-Path $horizonSource 'manifest.json') -Destination $horizonTarget
Copy-Item "$repo/licenses/*.txt" "$stage/licenses/"
Copy-Item "$repo/THIRD-PARTY-NOTICES.md" "$stage/"
# Generate redirects from the actual assembly metadata, without loading app code.
$redirects=foreach($name in $libraries){
    $assembly=[Reflection.AssemblyName]::GetAssemblyName("$stage/Dock/$name")
    $token=([BitConverter]::ToString($assembly.GetPublicKeyToken())).Replace('-','').ToLowerInvariant()
    if($token){'<dependentAssembly><assemblyIdentity name="{0}" publicKeyToken="{1}" culture="neutral"/><bindingRedirect oldVersion="0.0.0.0-{2}" newVersion="{2}"/></dependentAssembly>' -f $assembly.Name,$token,$assembly.Version}
}
$config='<configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/></startup><runtime><assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">'+($redirects -join '')+'</assemblyBinding></runtime></configuration>'
foreach($exe in Get-ChildItem $stage -Filter '*.exe' -Recurse){[IO.File]::WriteAllText($exe.FullName+'.config',$config)}
$temporaryPackage=Join-Path $out ('building-'+[Guid]::NewGuid().ToString('N')+'.msi')
& "$PSScriptRoot/New-Msi.ps1" -Payload $stage -Output $temporaryPackage -Version $Version
if($LASTEXITCODE -ne 0){throw 'MSI creation failed.'}
Move-Item -LiteralPath $temporaryPackage -Destination "$out/Clean11-$Version-x64.msi" -Force
$manifest=foreach($file in Get-ChildItem $stage -File -Recurse){[pscustomobject]@{Path=$file.FullName.Substring($stage.Length).TrimStart([char[]]'\/').Replace('\','/');Length=$file.Length;SHA256=(Get-FileHash $file.FullName).Hash}}
$manifest | ConvertTo-Json | Set-Content "$out/payload-manifest.json" -Encoding utf8
$manifest | ConvertTo-Json | Set-Content "$out/payload-manifest-$Version.json" -Encoding utf8
Get-FileHash "$out/Clean11-$Version-x64.msi" | ForEach-Object { $_.Hash+'  '+[IO.Path]::GetFileName($_.Path) } | Set-Content "$out/SHA256SUMS.txt" -Encoding ascii
Write-Output "Built $out/Clean11-$Version-x64.msi"
