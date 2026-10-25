param([Parameter(Mandatory)][string]$Payload,[Parameter(Mandatory)][string]$Output,[Parameter(Mandatory)][string]$Version)
$ErrorActionPreference='Stop'
$Payload=(Resolve-Path $Payload).Path
$Output=[IO.Path]::GetFullPath($Output)
if(Test-Path $Output){throw 'Refusing to overwrite an existing MSI.'}
function Stable-Guid([string]$Name){
    $hash=[Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes('Clean11 MSI component v1/'+$Name))
    $bytes=New-Object byte[] 16;[Array]::Copy($hash,$bytes,16)
    '{'+([Guid]::new($bytes)).ToString().ToUpperInvariant()+'}'
}
function Id([string]$Name){'I'+(Stable-Guid $Name).Replace('-','').Trim('{','}')}
$installer=New-Object -ComObject WindowsInstaller.Installer
$db=$installer.OpenDatabase($Output,3)
$columns=@{}
function Table([string]$Name,[string]$Definition,[string]$Keys){
    $query='CREATE TABLE `{0}` ({1} PRIMARY KEY {2})' -f $Name,$Definition,$Keys
    $view=$db.OpenView($query);$view.Execute();$view.Close();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    $columns[$Name]=@([regex]::Matches($Definition,'`([^`]+)`\s+(CHAR|SHORT|LONG|OBJECT)') | ForEach-Object {$_.Groups[1].Value})
}
function Row([string]$Name,[object[]]$Values){
    $names=($columns[$Name] | ForEach-Object {'`'+$_+'`'}) -join ','
    $parameters=(@('?')*$Values.Count) -join ','
    $view=$db.OpenView(('INSERT INTO `{0}` ({1}) VALUES ({2})' -f $Name,$names,$parameters))
    $record=$installer.CreateRecord($Values.Count)
    for($i=0;$i -lt $Values.Count;$i++){
        if($null -eq $Values[$i]){continue}
        $property=if($Values[$i] -is [int]){'IntegerData'}else{'StringData'}
        [void]$record.GetType().InvokeMember($property,[Reflection.BindingFlags]::SetProperty,$null,$record,@(($i+1),$Values[$i]))
    }
    $view.Execute($record);$view.Close()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
}
Table Property '`Property` CHAR(72) NOT NULL, `Value` CHAR(0) NOT NULL' '`Property`'
Table Directory '`Directory` CHAR(72) NOT NULL, `Directory_Parent` CHAR(72), `DefaultDir` CHAR(255) NOT NULL' '`Directory`'
Table Component '`Component` CHAR(72) NOT NULL, `ComponentId` CHAR(38), `Directory_` CHAR(72) NOT NULL, `Attributes` SHORT NOT NULL, `Condition` CHAR(255), `KeyPath` CHAR(72)' '`Component`'
Table Feature '`Feature` CHAR(38) NOT NULL, `Feature_Parent` CHAR(38), `Title` CHAR(64), `Description` CHAR(255), `Display` SHORT, `Level` SHORT NOT NULL, `Directory_` CHAR(72), `Attributes` SHORT NOT NULL' '`Feature`'
Table FeatureComponents '`Feature_` CHAR(38) NOT NULL, `Component_` CHAR(72) NOT NULL' '`Feature_`,`Component_`'
Table File '`File` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) NOT NULL, `FileSize` LONG NOT NULL, `Version` CHAR(72), `Language` CHAR(20), `Attributes` SHORT, `Sequence` LONG NOT NULL' '`File`'
Table Media '`DiskId` SHORT NOT NULL, `LastSequence` LONG NOT NULL, `DiskPrompt` CHAR(64), `Cabinet` CHAR(255), `VolumeLabel` CHAR(32), `Source` CHAR(72)' '`DiskId`'
Table Registry '`Registry` CHAR(72) NOT NULL, `Root` SHORT NOT NULL, `Key` CHAR(255) NOT NULL, `Name` CHAR(255), `Value` CHAR(0), `Component_` CHAR(72) NOT NULL' '`Registry`'
Table Shortcut '`Shortcut` CHAR(72) NOT NULL, `Directory_` CHAR(72) NOT NULL, `Name` CHAR(128) NOT NULL, `Component_` CHAR(72) NOT NULL, `Target` CHAR(255) NOT NULL, `Arguments` CHAR(255), `Description` CHAR(255), `Hotkey` SHORT, `Icon_` CHAR(72), `IconIndex` SHORT, `ShowCmd` SHORT, `WkDir` CHAR(72)' '`Shortcut`'
Table RemoveFile '`FileKey` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255), `DirProperty` CHAR(72) NOT NULL, `InstallMode` SHORT NOT NULL' '`FileKey`'
Table Upgrade '`UpgradeCode` CHAR(38) NOT NULL, `VersionMin` CHAR(20), `VersionMax` CHAR(20), `Language` CHAR(255), `Attributes` LONG NOT NULL, `Remove` CHAR(255), `ActionProperty` CHAR(72) NOT NULL' '`UpgradeCode`,`VersionMin`,`VersionMax`,`Language`,`Attributes`'
Table LaunchCondition '`Condition` CHAR(255) NOT NULL, `Description` CHAR(255) NOT NULL' '`Condition`'
Table AppSearch '`Property` CHAR(72) NOT NULL, `Signature_` CHAR(72) NOT NULL' '`Property`,`Signature_`'
Table RegLocator '`Signature_` CHAR(72) NOT NULL, `Root` SHORT NOT NULL, `Key` CHAR(255) NOT NULL, `Name` CHAR(255), `Type` SHORT' '`Signature_`'
Table Signature '`Signature` CHAR(72) NOT NULL, `FileName` CHAR(255), `MinVersion` CHAR(20), `MaxVersion` CHAR(20), `MinSize` LONG, `MaxSize` LONG, `MinDate` LONG, `MaxDate` LONG, `Languages` CHAR(255)' '`Signature`'
foreach($name in @('InstallExecuteSequence','InstallUISequence','AdminExecuteSequence')){Table $name '`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT' '`Action`'}
Table CustomAction '`Action` CHAR(72) NOT NULL, `Type` SHORT NOT NULL, `Source` CHAR(72), `Target` CHAR(255)' '`Action`'
Table Dialog '`Dialog` CHAR(72) NOT NULL, `HCentering` SHORT NOT NULL, `VCentering` SHORT NOT NULL, `Width` SHORT NOT NULL, `Height` SHORT NOT NULL, `Attributes` LONG, `Title` CHAR(128), `Control_First` CHAR(50), `Control_Default` CHAR(50), `Control_Cancel` CHAR(50)' '`Dialog`'
Table Control '`Dialog_` CHAR(72) NOT NULL, `Control` CHAR(50) NOT NULL, `Type` CHAR(20) NOT NULL, `X` SHORT NOT NULL, `Y` SHORT NOT NULL, `Width` SHORT NOT NULL, `Height` SHORT NOT NULL, `Attributes` LONG, `Property` CHAR(72), `Text` CHAR(0), `Control_Next` CHAR(50), `Help` CHAR(50)' '`Dialog_`,`Control`'
Table ControlEvent '`Dialog_` CHAR(72) NOT NULL, `Control_` CHAR(50) NOT NULL, `Event` CHAR(50) NOT NULL, `Argument` CHAR(255) NOT NULL, `Condition` CHAR(255), `Ordering` SHORT' '`Dialog_`,`Control_`,`Event`,`Argument`,`Condition`'
Table CheckBox '`Property` CHAR(72) NOT NULL, `Value` CHAR(64)' '`Property`'
Table TextStyle '`TextStyle` CHAR(72) NOT NULL, `FaceName` CHAR(32) NOT NULL, `Size` SHORT NOT NULL, `Color` LONG, `StyleBits` SHORT' '`TextStyle`'
Table EventMapping '`Dialog_` CHAR(72) NOT NULL, `Control_` CHAR(50) NOT NULL, `Event` CHAR(50) NOT NULL, `Attribute` CHAR(50) NOT NULL' '`Dialog_`,`Control_`,`Event`'
Table ControlCondition '`Dialog_` CHAR(72) NOT NULL, `Control_` CHAR(50) NOT NULL, `Action` CHAR(50) NOT NULL, `Condition` CHAR(255) NOT NULL' '`Dialog_`,`Control_`,`Action`,`Condition`'
$upgrade='{8CD36734-0134-47DB-9EF3-0A2E6C047941}'
$properties=@{ProductCode=(Stable-Guid "product/$Version");UpgradeCode=$upgrade;ProductName='Clean11';ProductVersion=$Version;ProductLanguage='1033';Manufacturer='Clean11 Contributors';INSTALLLEVEL='1';DefaultUIFont='Body';STARTWITHWINDOWS='1';LAUNCHCLEAN11='1';ARPNOMODIFY='1';ARPURLINFOABOUT='https://github.com/MatoXD99/clean11';SecureCustomProperties='OLDPRODUCTS;NEWERPRODUCTS;STARTWITHWINDOWS;FORCESTARTUP';MSIRESTARTMANAGERCONTROL='DisableShutdown'}
foreach($key in $properties.Keys){Row Property @($key,[string]$properties[$key])}
Row Directory @('TARGETDIR',$null,'SourceDir')
Row Directory @('LocalAppDataFolder','TARGETDIR','.')
Row Directory @('ProgramsDir','LocalAppDataFolder','Programs')
Row Directory @('INSTALLDIR','ProgramsDir','Clean11')
Row Directory @('ProgramMenuFolder','TARGETDIR','.')
Row Directory @('AppPrograms','ProgramMenuFolder','Clean11')
$directories=@{''='INSTALLDIR'}
$files=@(Get-ChildItem $Payload -File -Recurse | Sort-Object FullName)
$ddf=@('.OPTION EXPLICIT','.Set Cabinet=on','.Set Compress=on','.Set CompressionType=MSZIP','.Set MaxDiskSize=0','.Set MaxCabinetSize=0','.Set CabinetNameTemplate=payload.cab',('.Set DiskDirectoryTemplate="'+(Split-Path $Output)+'"'),('.Set RptFileName="'+(Join-Path (Split-Path $Output) 'cabinet.rpt')+'"'),('.Set InfFileName="'+(Join-Path (Split-Path $Output) 'cabinet.inf')+'"'))
$sequence=0
foreach($file in $files){
    $relative=$file.FullName.Substring($Payload.Length).TrimStart([char[]]'\/').Replace('\','/')
    $parts=$relative.Split('/');$parent='';$directory='INSTALLDIR'
    for($i=0;$i -lt $parts.Count-1;$i++){
        $next=($parent+'/'+$parts[$i]).TrimStart('/')
        if(!$directories.ContainsKey($next)){$directories[$next]=Id ('dir/'+$next);Row Directory @($directories[$next],$directory,$parts[$i])}
        $parent=$next;$directory=$directories[$parent]
    }
    $component=Id ('component/'+$relative);$fileId=Id ('file/'+$relative);$sequence++
    Row Component @($component,(Stable-Guid $relative),$directory,256,$null,$fileId)
    Row FeatureComponents @('Core',$component)
    $versionInfo=[Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName).FileVersion
    if($versionInfo -notmatch '^\d+\.\d+\.\d+\.\d+$'){$versionInfo=$null}
    Row File @($fileId,$component,$file.Name,[int]$file.Length,$versionInfo,$null,512,$sequence)
    $ddf+=('"'+$file.FullName+'" '+$fileId)
}
Row Feature @('Core',$null,'Clean11','Dock, overview, search, and Settings',1,1,'INSTALLDIR',0)
Row Media @(1,$sequence,$null,'#payload.cab',$null,$null)
Row Component @('Integration',(Stable-Guid 'integration'),'INSTALLDIR',260,$null,'InstallStamp')
Row FeatureComponents @('Core','Integration')
Row Registry @('InstallStamp',1,'Software\Clean11\Installer','Version',$Version,'Integration')
Row Registry @('StartupChoice',1,'Software\Clean11\Installer','PendingStartup','[STARTWITHWINDOWS]','Integration')
Row Component @('Startup',(Stable-Guid 'startup'),'INSTALLDIR',260,'STARTWITHWINDOWS','StartupStamp')
Row FeatureComponents @('Core','Startup')
Row Registry @('StartupStamp',1,'Software\Clean11\Installer','StartupComponent','1','Startup')
Row Registry @('StartupEnabled',1,'Software\Clean11\Installer','StartupEnabled','1','Startup')
Row Shortcut @('Start','AppPrograms','Clean11','Integration','[INSTALLDIR]Clean11.exe',$null,'Start Clean11',$null,$null,$null,1,'INSTALLDIR')
Row Shortcut @('Settings','AppPrograms','Clean11 Settings','Integration','[INSTALLDIR]Clean11.exe','--settings','Customize Clean11',$null,$null,$null,1,'INSTALLDIR')
Row Shortcut @('Exit','AppPrograms','Exit Clean11','Integration','[INSTALLDIR]Clean11.exe','--exit','Exit Clean11 and restore hidden bars',$null,$null,$null,1,'INSTALLDIR')
Row RemoveFile @('ShortcutFolder','Integration',$null,'AppPrograms',2)
Row RemoveFile @('OverviewLog','Integration','native.log',$directories['Overview'],2)
foreach($directory in $directories.Values){Row RemoveFile @((Id ('remove/'+$directory)),'Integration',$null,$directory,2)}
Row Upgrade @($upgrade,$null,$Version,$null,512,$null,'OLDPRODUCTS')
Row Upgrade @($upgrade,$Version,$null,$null,2,$null,'NEWERPRODUCTS')
Row RegLocator @('BuildNumber',2,'SOFTWARE\Microsoft\Windows NT\CurrentVersion','CurrentBuildNumber',18)
Row AppSearch @('WINDOWSBUILD','BuildNumber')
Row RegLocator @('NetRelease',2,'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full','Release',18)
Row AppSearch @('NETRELEASE','NetRelease')
Row RegLocator @('PreviousInstall',1,'Software\Clean11\Installer','Version',18)
Row AppSearch @('EXISTINGINSTALL','PreviousInstall')
Row RegLocator @('PreviousStartup',1,'Software\Clean11\Installer','StartupEnabled',18)
Row AppSearch @('EXISTINGSTARTUP','PreviousStartup')
Row RegLocator @('LegacyStartup',1,'Software\Microsoft\Windows\CurrentVersion\Run','Clean11',18)
Row AppSearch @('LEGACYSTARTUP','LegacyStartup')
Row RegLocator @('LegacyDockStartup',1,'Software\Microsoft\Windows\CurrentVersion\Run','Clean11Dock',18)
Row AppSearch @('LEGACYDOCKSTARTUP','LegacyDockStartup')
Row LaunchCondition @('Installed OR (VersionNT64 AND WINDOWSBUILD >= "22000")','Clean11 requires Windows 11 x64.')
Row LaunchCondition @('Installed OR NETRELEASE >= "#528040"','Clean11 requires .NET Framework 4.8 or later (included with Windows 11).')
Row LaunchCondition @('NOT ALLUSERS','Clean11 installs for the current user. Do not set ALLUSERS.')
Row LaunchCondition @('NOT NEWERPRODUCTS','A newer Clean11 version is already installed.')
$launcher=Id 'file/Clean11.exe'
Row CustomAction @('StopClean11',18,$launcher,'--stop-for-maintenance')
Row CustomAction @('LaunchClean11',210,$launcher,'--settings')
Row CustomAction @('RegisterStartup',1042,$launcher,'--register-startup')
Row CustomAction @('UnregisterStartup',1042,$launcher,'--unregister-startup')
Row CustomAction @('DisableStartup',1042,$launcher,'--unregister-startup')
Row CustomAction @('UndoNewStartup',1298,$launcher,'--unregister-startup')
Row CustomAction @('StartupOff',51,'STARTWITHWINDOWS','')
Row CustomAction @('StartupOn',51,'STARTWITHWINDOWS','1')
$actions=@(@('FindRelatedProducts',25),@('AppSearch',50),@('LaunchConditions',100),@('CostInitialize',800),@('FileCost',900),@('CostFinalize',1000),@('InstallValidate',1400),@('InstallInitialize',1500),@('RemoveExistingProducts',1550),@('ProcessComponents',1600),@('UnpublishFeatures',1800),@('RemoveShortcuts',3200),@('RemoveRegistryValues',3300),@('RemoveFiles',3500),@('RemoveFolders',3600),@('InstallFiles',4000),@('CreateShortcuts',4500),@('WriteRegistryValues',5000),@('RegisterUser',6000),@('RegisterProduct',6100),@('PublishFeatures',6300),@('PublishProduct',6400),@('InstallFinalize',6600))
foreach($action in $actions){Row InstallExecuteSequence @($action[0],$null,[int]$action[1])}
Row InstallExecuteSequence @('StopClean11','Installed',1390)
Row InstallExecuteSequence @('UnregisterStartup','REMOVE="ALL"',3400)
Row InstallExecuteSequence @('UndoNewStartup','NOT Installed AND NOT REMOVE AND STARTWITHWINDOWS',5080)
Row InstallExecuteSequence @('RegisterStartup','NOT REMOVE AND STARTWITHWINDOWS',5100)
Row InstallExecuteSequence @('DisableStartup','NOT REMOVE AND NOT STARTWITHWINDOWS',5110)
Row InstallExecuteSequence @('StartupOff','UILevel < 5 AND EXISTINGINSTALL AND NOT (EXISTINGSTARTUP OR LEGACYSTARTUP OR LEGACYDOCKSTARTUP) AND NOT FORCESTARTUP',1100)
Row InstallExecuteSequence @('StartupOn','UILevel < 5 AND (EXISTINGSTARTUP OR LEGACYSTARTUP OR LEGACYDOCKSTARTUP) AND NOT FORCESTARTUP',1110)
foreach($action in @(@('FindRelatedProducts',25),@('AppSearch',50),@('LaunchConditions',100),@('CostInitialize',800),@('FileCost',900),@('CostFinalize',1000),@('ExecuteAction',1300))){Row InstallUISequence @($action[0],$null,[int]$action[1])}
Row InstallUISequence @('WelcomeDlg','NOT Installed',1200)
Row InstallUISequence @('StartupOff','EXISTINGINSTALL AND NOT (EXISTINGSTARTUP OR LEGACYSTARTUP OR LEGACYDOCKSTARTUP) AND NOT FORCESTARTUP',1100)
Row InstallUISequence @('StartupOn','(EXISTINGSTARTUP OR LEGACYSTARTUP OR LEGACYDOCKSTARTUP) AND NOT FORCESTARTUP',1110)
Row InstallUISequence @('ProgressDlg',$null,1290)
Row InstallUISequence @('ExitDlg',$null,-1)
foreach($action in @(@('CostInitialize',800),@('FileCost',900),@('CostFinalize',1000),@('InstallValidate',1400),@('InstallInitialize',1500),@('InstallAdminPackage',3900),@('InstallFiles',4000),@('InstallFinalize',6600))){Row AdminExecuteSequence @($action[0],$null,[int]$action[1])}
Row TextStyle @('Body','Segoe UI',9,0,0)
Row TextStyle @('Title','Segoe UI',19,0,1)
Row CheckBox @('STARTWITHWINDOWS','1')
Row CheckBox @('LAUNCHCLEAN11','1')
function Control([string]$Dialog,[string]$Name,[string]$Type,[int]$X,[int]$Y,[int]$Width,[int]$Height,[string]$Property,[string]$Text,[string]$Next){Row Control @($Dialog,$Name,$Type,$X,$Y,$Width,$Height,3,$Property,$Text,$Next,$null)}
Row Dialog @('WelcomeDlg',50,50,380,250,3,'Clean11 Setup','Startup','Install','Cancel')
Control WelcomeDlg Title Text 24 20 330 30 $null '{\Title}Meet Clean11.' $null
Control WelcomeDlg Description Text 24 65 330 74 $null 'Install the floating dock, window overview, search, and Settings for your Windows account. Other desktop bars stay enabled until you choose otherwise in Settings.' $null
Control WelcomeDlg Startup CheckBox 24 144 330 20 'STARTWITHWINDOWS' 'Start Clean11 when I sign in' 'Install'
Control WelcomeDlg Install PushButton 206 210 72 22 $null 'Install' 'Cancel'
Control WelcomeDlg Cancel PushButton 286 210 72 22 $null 'Cancel' 'Startup'
Row ControlEvent @('WelcomeDlg','Install','EndDialog','Return','1',1)
Row ControlEvent @('WelcomeDlg','Cancel','EndDialog','Exit','1',1)
Row Dialog @('ProgressDlg',50,50,380,210,1,'Clean11 Setup','Cancel','Cancel','Cancel')
Control ProgressDlg Title Text 24 20 330 30 $null '{\Title}Setting things up' $null
Control ProgressDlg Description Text 24 65 330 42 $null 'Windows Installer is updating Clean11. Your personal settings are kept when repairing or uninstalling.' $null
Control ProgressDlg Progress ProgressBar 24 118 330 14 $null $null $null
Control ProgressDlg Cancel PushButton 286 170 72 22 $null 'Cancel' $null
Row ControlEvent @('ProgressDlg','Cancel','EndDialog','Exit','1',1)
Row EventMapping @('ProgressDlg','Progress','SetProgress','Progress')
Row Dialog @('ExitDlg',50,50,380,230,3,'Clean11 Setup','Launch','Finish','Finish')
Control ExitDlg Title Text 24 20 330 30 $null '{\Title}Setup complete.' $null
Control ExitDlg Description Text 24 65 330 50 $null 'Clean11 setup has finished. You can open the app and Settings from the Start menu.' $null
Control ExitDlg Launch CheckBox 24 132 330 20 'LAUNCHCLEAN11' 'Open Clean11 Settings' 'Finish'
Control ExitDlg Finish PushButton 286 190 72 22 $null 'Finish' 'Launch'
Row ControlEvent @('ExitDlg','Finish','DoAction','LaunchClean11','NOT REMOVE AND LAUNCHCLEAN11',1)
Row ControlEvent @('ExitDlg','Finish','EndDialog','Return','1',2)
Row ControlCondition @('ExitDlg','Launch','Hide','REMOVE')
$ddfPath=Join-Path (Split-Path $Output) 'payload.ddf'
$ddf | Set-Content $ddfPath -Encoding ascii
& "$env:WINDIR/System32/makecab.exe" /F $ddfPath /V1 | Out-Null
if($LASTEXITCODE -ne 0){throw 'Cabinet generation failed.'}
$stream=$installer.CreateRecord(2)
[void]$stream.GetType().InvokeMember('StringData',[Reflection.BindingFlags]::SetProperty,$null,$stream,@(1,'payload.cab'))
$stream.SetStream(2,(Join-Path (Split-Path $Output) 'payload.cab'))
$view=$db.OpenView('INSERT INTO `_Streams` (`Name`,`Data`) VALUES (?,?)');$view.Execute($stream);$view.Close()
$summary=$db.SummaryInformation(20)
foreach($entry in @(@(1,1252),@(2,'Installation Database'),@(3,'Clean11'),@(4,'Clean11 Contributors'),@(5,'Installer'),@(7,'x64;1033'),@(9,('{'+[Guid]::NewGuid().ToString().ToUpperInvariant()+'}')),@(14,500),@(15,10))){[void]$summary.GetType().InvokeMember('Property',[Reflection.BindingFlags]::SetProperty,$null,$summary,@($entry[0],$entry[1]))}
$summary.Persist();$db.Commit()
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($summary)
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($stream)
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($db)
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)
[GC]::Collect();[GC]::WaitForPendingFinalizers()
