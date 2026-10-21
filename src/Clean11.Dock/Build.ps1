param([string]$DependenciesRoot = (Join-Path $PSScriptRoot '../Clean11.Overview'),[ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='0.1.0')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$framework = Split-Path $compiler
$out = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$horizon=Join-Path $PSScriptRoot '../../assets/wallpapers/horizon'
$horizonOut=Join-Path $out 'Wallpapers/Horizon'
New-Item -ItemType Directory -Force -Path $horizonOut | Out-Null
foreach($phase in @('predawn','sunrise','morning','midday','afternoon','sunset','twilight','night')){Copy-Item -LiteralPath (Join-Path $horizon "$phase.png") -Destination $horizonOut -Force}
if(Test-Path (Join-Path $horizon 'manifest.json')){Copy-Item -LiteralPath (Join-Path $horizon 'manifest.json') -Destination $horizonOut -Force}
foreach($name in @('Wpf.Ui','Wpf.Ui.Abstractions','System.Memory','System.Buffers','System.Numerics.Vectors','System.Runtime.CompilerServices.Unsafe')){Copy-Item -LiteralPath (Join-Path $DependenciesRoot "$name.dll") -Destination $out -Force}
$shared = Join-Path $PSScriptRoot '../Clean11.Overview'
Copy-Item -LiteralPath (Join-Path $shared 'ActivitiesTheme.json') -Destination $out -Force
$references = @('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll','/r:Microsoft.CSharp.dll','/r:System.Core.dll','/r:System.Xml.dll','/r:System.Xaml.dll')
foreach($name in @('WindowsBase','PresentationCore','PresentationFramework','WindowsFormsIntegration')){$references += '/r:'+(Join-Path $framework "WPF/$name.dll")}
foreach($name in @('Wpf.Ui','Wpf.Ui.Abstractions')){$references += '/r:'+(Join-Path $out "$name.dll")}
$references += '/r:'+(Join-Path $framework 'System.Runtime.WindowsRuntime.dll')
$references += '/r:System.Numerics.dll','/r:System.Runtime.dll','/r:System.Runtime.InteropServices.WindowsRuntime.dll'
$references += '/r:'+(Join-Path $out 'System.Numerics.Vectors.dll')
foreach($metadata in @('Windows.Foundation','Windows.UI','Windows.Graphics','Windows.Devices')){$references += '/r:'+(Join-Path ([Environment]::GetFolderPath('Windows')) "System32/WinMetadata/$metadata.winmd")}
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
$sources += Join-Path $shared 'Clean11Paths.cs'
$sources += Join-Path $shared 'Clean11Fluent.cs'
$sources += Join-Path $shared 'ActivityTheme.cs'
$sources += Join-Path $shared 'OverviewPreferences.cs'
$sources += Join-Path $shared 'FrostedMaterial.cs'
$sources += Join-Path $shared 'DockBackdrop.cs'
$sources += Join-Path $shared 'DockComposition.cs'
$versionSource=Join-Path $out 'AssemblyVersion.cs'
[IO.File]::WriteAllText($versionSource,('[assembly:System.Reflection.AssemblyVersion("{0}.0")][assembly:System.Reflection.AssemblyFileVersion("{0}.0")][assembly:System.Reflection.AssemblyCompany("Clean11 Contributors")][assembly:System.Reflection.AssemblyProduct("Clean11")]' -f $Version))
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /main:DockProgram "/win32manifest:$PSScriptRoot/app.manifest" "/out:$out/Clean11.Dock.exe" @references @sources $versionSource
if($LASTEXITCODE -ne 0){throw 'Clean11 Dock build failed'}
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /main:WidgetsProgram "/win32manifest:$PSScriptRoot/app.manifest" "/out:$out/Clean11.Widgets.exe" @references @sources $versionSource
if($LASTEXITCODE -ne 0){throw 'Clean11 Widgets build failed'}

& $compiler /nologo /target:winexe /platform:x64 /optimize+ /main:SettingsProgram "/out:$out/Clean11.Settings.exe" (Join-Path $PSScriptRoot "SettingsProgram.cs") $versionSource
if($LASTEXITCODE -ne 0){throw "Clean11 Settings launcher build failed"}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Tray/style.css") -Destination (Join-Path $out "tray.css") -Force
