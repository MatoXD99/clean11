param([string]$DependenciesRoot = (Join-Path $PSScriptRoot '../Clean11.Overview'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$framework = Split-Path $compiler
$out = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $out | Out-Null
foreach($name in @('Wpf.Ui','Wpf.Ui.Abstractions','System.Memory','System.Buffers','System.Numerics.Vectors','System.Runtime.CompilerServices.Unsafe')){Copy-Item -LiteralPath (Join-Path $DependenciesRoot "$name.dll") -Destination $out -Force}
$shared = Join-Path $PSScriptRoot '../Clean11.Overview'
Copy-Item -LiteralPath (Join-Path $shared 'ActivitiesTheme.json') -Destination $out -Force
$references = @('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll','/r:Microsoft.CSharp.dll','/r:System.Core.dll','/r:System.Xml.dll','/r:System.Xaml.dll')
foreach($name in @('WindowsBase','PresentationCore','PresentationFramework','WindowsFormsIntegration')){$references += '/r:'+(Join-Path $framework "WPF/$name.dll")}
foreach($name in @('Wpf.Ui','Wpf.Ui.Abstractions')){$references += '/r:'+(Join-Path $out "$name.dll")}
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
$sources += Join-Path $shared 'Clean11Fluent.cs'
$sources += Join-Path $shared 'ActivityTheme.cs'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /main:DockProgram "/win32manifest:$PSScriptRoot/app.manifest" "/out:$out/Clean11.Dock.exe" @references @sources
if($LASTEXITCODE -ne 0){throw 'Clean11 Dock build failed'}
