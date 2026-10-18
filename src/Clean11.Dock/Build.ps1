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
$references += '/r:'+(Join-Path $framework 'System.Runtime.WindowsRuntime.dll')
$references += '/r:System.Numerics.dll','/r:System.Runtime.dll','/r:System.Runtime.InteropServices.WindowsRuntime.dll'
$references += '/r:'+(Join-Path $out 'System.Numerics.Vectors.dll')
foreach($metadata in @('Windows.Foundation','Windows.UI','Windows.Graphics')){$references += '/r:'+(Join-Path ([Environment]::GetFolderPath('Windows')) "System32/WinMetadata/$metadata.winmd")}
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
$sources += Join-Path $shared 'Clean11Fluent.cs'
$sources += Join-Path $shared 'ActivityTheme.cs'
$sources += Join-Path $shared 'OverviewPreferences.cs'
$sources += Join-Path $shared 'FrostedMaterial.cs'
$sources += Join-Path $shared 'DockBackdrop.cs'
$sources += Join-Path $shared 'DockComposition.cs'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /main:DockProgram "/win32manifest:$PSScriptRoot/app.manifest" "/out:$out/Clean11.Dock.exe" @references @sources
if($LASTEXITCODE -ne 0){throw 'Clean11 Dock build failed'}

& $compiler /nologo /target:winexe /platform:x64 /optimize+ /main:SettingsProgram "/out:$out/Clean11.Settings.exe" (Join-Path $PSScriptRoot "SettingsProgram.cs")
if($LASTEXITCODE -ne 0){throw "Clean11 Settings launcher build failed"}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Tray/style.css") -Destination (Join-Path $out "tray.css") -Force
