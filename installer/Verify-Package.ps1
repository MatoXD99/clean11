param([string]$Package=(Join-Path $PSScriptRoot '../artifacts/installer/Clean11-0.1.0-x64.msi'))
$ErrorActionPreference='Stop'
$path=(Resolve-Path $Package).Path
$installer=New-Object -ComObject WindowsInstaller.Installer
$installer.UILevel=2
$db=$installer.OpenDatabase($path,0)
$properties=@{}
$view=$db.OpenView('SELECT `Property`,`Value` FROM `Property`');$view.Execute()
while($record=$view.Fetch()){$properties[$record.StringData(1)]=$record.StringData(2)};$view.Close()
if($properties['ALLUSERS']){throw 'Package is not restricted to per-user installation.'}
if($properties['Manufacturer'] -ne 'Clean11 Contributors'){throw 'Unexpected publisher metadata.'}
foreach($table in @('File','Component','FeatureComponents','Shortcut','Registry','Upgrade','LaunchCondition')){
    $view=$db.OpenView(('SELECT * FROM `{0}`' -f $table));$view.Execute();$count=0;while($view.Fetch()){$count++};$view.Close()
    if(!$count){throw "Missing package rows in $table"};Write-Output "$table : $count rows"
}
$view=$db.OpenView('SELECT `Name` FROM `_Streams`');$view.Execute();$cab=$false
while($record=$view.Fetch()){if($record.StringData(1) -eq 'payload.cab'){$cab=$true}};$view.Close()
if(!$cab){throw 'Embedded cabinet missing.'}
$installer.EnableLog('voicewarmup',(Join-Path (Split-Path $path) 'verification.log'))
$session=$installer.OpenPackage($path,0)
foreach($action in @('AppSearch','CostInitialize','FileCost','CostFinalize')){if($session.DoAction($action) -ne 1){throw "Package action failed: $action"}}
foreach($condition in @('VersionNT64','WINDOWSBUILD >= "22000"','NETRELEASE >= "#528040"')){
    if($session.EvaluateCondition($condition) -ne 1){throw "Host prerequisite check failed: $condition"}
}
Write-Output ('Prerequisites pass; install directory: '+$session.TargetPath('INSTALLDIR'))
Write-Output 'Database, cabinet, costing and prerequisite checks passed. No installation was performed.'
