param([string]$Package=(Join-Path $PSScriptRoot '../artifacts/installer/Clean11-0.3.2-x64.msi'))
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
if([version]$properties['ProductVersion'] -ge [version]'0.3.2'){
    $actions=@{};$sequence=@{}
    $view=$db.OpenView('SELECT `Action`,`Type`,`Target` FROM `CustomAction`');$view.Execute()
    while($record=$view.Fetch()){$actions[$record.StringData(1)]=@($record.IntegerData(2),$record.StringData(3))};$view.Close()
    $view=$db.OpenView('SELECT `Action`,`Sequence` FROM `InstallExecuteSequence`');$view.Execute()
    while($record=$view.Fetch()){$sequence[$record.StringData(1)]=$record.IntegerData(2)};$view.Close()
    foreach($name in @('RegisterStartup','UnregisterStartup','DisableStartup')){
        $expected=if($name -eq 'RegisterStartup'){'--register-startup'}else{'--unregister-startup'}
        if(!$actions.ContainsKey($name) -or $actions[$name][0] -ne 1042 -or $actions[$name][1] -ne $expected){throw "Invalid per-user startup custom action: $name"}
    }
    if($sequence['RegisterStartup'] -le $sequence['WriteRegistryValues'] -or $sequence['RegisterStartup'] -ge $sequence['InstallFinalize']){throw 'Startup registration must run after payload/registry installation and before finalization.'}
    if($sequence['DisableStartup'] -le $sequence['InstallFiles']){throw 'Disabling startup on a fresh installation requires the installed launcher.'}
    if($sequence['UnregisterStartup'] -ge $sequence['RemoveFiles']){throw 'Uninstall must unregister startup before deleting the launcher.'}
    if(!$actions.ContainsKey('UndoNewStartup') -or $actions['UndoNewStartup'][0] -ne 1298 -or $sequence['UndoNewStartup'] -ge $sequence['RegisterStartup']){throw 'Fresh-install startup rollback is missing or incorrectly sequenced.'}
    Write-Output 'Per-user startup registration, removal, disable and rollback actions pass.'
}
$installer.EnableLog('voicewarmup',(Join-Path (Split-Path $path) 'verification.log'))
$session=$installer.OpenPackage($path,0)
foreach($action in @('AppSearch','CostInitialize','FileCost','CostFinalize')){if($session.DoAction($action) -ne 1){throw "Package action failed: $action"}}
foreach($condition in @('VersionNT64','WINDOWSBUILD >= "22000"','NETRELEASE >= "#528040"')){
    if($session.EvaluateCondition($condition) -ne 1){throw "Host prerequisite check failed: $condition"}
}
Write-Output ('Prerequisites pass; install directory: '+$session.TargetPath('INSTALLDIR'))
Write-Output 'Database, cabinet, costing and prerequisite checks passed. No installation was performed.'
