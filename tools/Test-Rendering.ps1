param()
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$framework=Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET/Framework64/v4.0.30319'
$out=Join-Path $repo 'artifacts/performance'
New-Item -ItemType Directory -Force $out | Out-Null
$references=@('/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Xaml.dll')
foreach($name in @('WindowsBase','PresentationCore','PresentationFramework')){$references+='/r:'+(Join-Path $framework "WPF/$name.dll")}
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /optimize+ "/out:$out/RenderingRegression.exe" @references (Join-Path $PSScriptRoot 'RenderingRegression.cs')
if($LASTEXITCODE -ne 0){throw 'Rendering regression harness did not compile.'}
& "$out/RenderingRegression.exe" "$repo/src/Clean11.Overview/ActivitiesOverview.exe" "$repo/src/Clean11.Dock/bin/Clean11.Dock.exe"
if($LASTEXITCODE -ne 0){throw 'Rendering regression checks failed.'}
