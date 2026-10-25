param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='0.1.0')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$sources = @('ActivitiesOverview.cs','Clean11Paths.cs','OverviewScene.cs','OverviewPreferences.cs','SearchServices.cs','FlowScoring.cs','SuperKey.cs','FramePacer.cs','PerformanceEvents.cs','PreviewHost.cs','ProcessSnapshot.cs','WindowLayout.cs','SettingsCatalog.cs','SpotlightPanel.cs','SpotlightIntegration.cs','RoundedPreviews.cs','ActivityTheme.cs','FrostedMaterial.cs','OverviewBadge.cs','Clean11Fluent.cs','DockBackdrop.cs','DockComposition.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$libraries = @(
 'WPF-UI.4.3.0/lib/net472/Wpf.Ui.dll',
 'WPF-UI.Abstractions.4.3.0/lib/net472/Wpf.Ui.Abstractions.dll',
 'System.Memory.4.6.3/lib/net462/System.Memory.dll',
 'System.Buffers.4.6.1/lib/net462/System.Buffers.dll',
 'System.Numerics.Vectors.4.6.1/lib/net462/System.Numerics.Vectors.dll',
 'System.Runtime.CompilerServices.Unsafe.6.1.2/lib/net462/System.Runtime.CompilerServices.Unsafe.dll'
)
foreach($library in $libraries){Copy-Item -LiteralPath (Join-Path $PSScriptRoot "packages/$library") -Destination $PSScriptRoot -Force}
$references = @('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll','/r:Microsoft.CSharp.dll','/r:System.Core.dll','/r:System.Xml.dll','/r:System.Xaml.dll')
foreach($assembly in @('WindowsBase','PresentationCore','PresentationFramework','WindowsFormsIntegration')){$references += '/r:'+(Join-Path $framework "WPF/$assembly.dll")}
$references += '/r:'+(Join-Path $PSScriptRoot 'Wpf.Ui.dll')
$references += '/r:'+(Join-Path $PSScriptRoot 'Wpf.Ui.Abstractions.dll')
$references += '/r:'+(Join-Path $framework 'System.Runtime.WindowsRuntime.dll')
$references += '/r:System.Numerics.dll','/r:System.Runtime.dll','/r:System.Runtime.InteropServices.WindowsRuntime.dll'
$references += '/r:'+(Join-Path $PSScriptRoot 'System.Numerics.Vectors.dll')
foreach($metadata in @('Windows.Foundation','Windows.UI','Windows.Graphics')){$references += '/r:'+(Join-Path ([Environment]::GetFolderPath('Windows')) "System32/WinMetadata/$metadata.winmd")}
$versionDirectory=Join-Path $PSScriptRoot 'bin';New-Item -ItemType Directory -Force $versionDirectory | Out-Null
$versionSource=Join-Path $versionDirectory 'AssemblyVersion.cs'
[IO.File]::WriteAllText($versionSource,('[assembly:System.Reflection.AssemblyVersion("{0}.0")][assembly:System.Reflection.AssemblyFileVersion("{0}.0")][assembly:System.Reflection.AssemblyCompany("Clean11 Contributors")][assembly:System.Reflection.AssemblyProduct("Clean11")]' -f $Version))
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /main:Program "/out:$PSScriptRoot/ActivitiesOverview.exe" @references @sources $versionSource
if($LASTEXITCODE -ne 0){throw 'Clean11 WPF UI build failed'}
