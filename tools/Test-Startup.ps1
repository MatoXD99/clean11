param([string]$ResultsPath=(Join-Path $env:LOCALAPPDATA 'Clean11/Diagnostics/startup-validation.json'))
$ErrorActionPreference='Stop'
# Run assertions in a real interactive user process. A tool host can expose a
# different registry/file view, even for the same user and identical paths.
$name='Clean11 startup validation '+[Guid]::NewGuid().ToString('N')
$script=@'
$ErrorActionPreference='Stop'
$ValidationTask='__TASK_NAME__'
$mainName='Clean11 Startup '+[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$exe=Join-Path $env:LOCALAPPDATA 'Programs/Clean11/Clean11.exe'
$svc=New-Object -ComObject Schedule.Service;$svc.Connect();$folder=$svc.GetFolder('\')
$checks=New-Object 'Collections.Generic.List[string]'
$failureName=$null;$fixture=$null
function Check([bool]$value,[string]$message){if(!$value){throw $message};$checks.Add($message)}
function Command([string]$arg){$p=Start-Process $exe -ArgumentList $arg -PassThru -Wait -WindowStyle Hidden;Check ($p.ExitCode -eq 0) $arg}
function ApprovalSnapshot {
 $key=Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
 @($key.PSObject.Properties | Where-Object {$_.Name -notmatch '^PS'} | Sort-Object Name | ForEach-Object {"$($_.Name)=$([BitConverter]::ToString([byte[]]$_.Value))"}) -join '|'
}
function RunMainTask {
 $task=$folder.GetTask($mainName);$null=$task.Run($null)
 $deadline=[DateTime]::UtcNow.AddSeconds(40)
 do{Start-Sleep -Milliseconds 100;$task=$folder.GetTask($mainName)}while($task.State -eq 4 -and [DateTime]::UtcNow -lt $deadline)
 Check ($task.LastTaskResult -eq 0 -and $task.State -ne 4) 'Registered sign-in task reports successful UI readiness'
}
function ModuleIds {
 @(Get-CimInstance Win32_Process | Where-Object {$_.Name -in @('Clean11.Dock.exe','Clean11.Widgets.exe','ActivitiesOverview.exe')} | Sort-Object ProcessId | ForEach-Object {$_.ProcessId}) -join ','
}
$result=[ordered]@{Passed=$false;Checks=@();Error=''}
$before=ApprovalSnapshot
try {
 Check ($folder.GetTask($mainName).Enabled) 'Start with Windows is initially enabled'
 RunMainTask
 foreach($eventName in @('Local\Clean11DockReady','Local\Clean11OverviewReady')){
  $event=[Threading.EventWaitHandle]::OpenExisting($eventName)
  try{Check ($event.WaitOne(0)) ($eventName+' signaled')}finally{$event.Dispose()}
 }
 $ids=ModuleIds;RunMainTask
 Check ((ModuleIds) -eq $ids) 'Repeated sign-in execution preserves module PIDs without duplicates'
 Command '--unregister-startup'
 $exists=$true;try{$null=$folder.GetTask($mainName)}catch{$exists=$false}
 Check (!$exists) 'Disabling startup removes its task'
 Command '--register-startup'
 Check ($folder.GetTask($mainName).Enabled) 'Enabling startup recreates the task'
 $task=$folder.GetTask($mainName);$task.Enabled=$false
 $assembly=[Reflection.Assembly]::LoadFrom($exe)
 $method=$assembly.GetType('Clean11Startup').GetMethod('SetEnabled',[Reflection.BindingFlags]'NonPublic,Static')
 $ensure=[Delegate]::CreateDelegate([Action[bool,string,string,bool]],$method)
 $ensure.Invoke($true,$exe,'--background --sign-in',$false)
 Check (!$folder.GetTask($mainName).Enabled) 'Ordinary registration preserves a task disabled by the user'
 Command '--register-startup'
 Check ($folder.GetTask($mainName).Enabled) 'Explicit enable restores the task'
 $run=Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
 Check (!$run.PSObject.Properties['Clean11'] -and !$run.PSObject.Properties['Clean11Dock']) 'Legacy duplicate startup entries removed'
 Check ((ApprovalSnapshot) -eq $before) 'Other apps startup approval records unchanged'
 $fixtureRoot=Join-Path $env:LOCALAPPDATA ('Clean11/Diagnostics/MissingComponents-'+[Guid]::NewGuid().ToString('N'))
 [IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
 $fixture=Join-Path $fixtureRoot 'Clean11.exe';[IO.File]::Copy($exe,$fixture)
 $failureName='Clean11 failure diagnostic '+[Guid]::NewGuid().ToString('N')
 $d=$svc.NewTask(0);$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
 $d.Principal.UserId=$sid;$d.Principal.LogonType=3;$d.Principal.RunLevel=0
 $d.Settings.DisallowStartIfOnBatteries=$false;$d.Settings.StopIfGoingOnBatteries=$false
 $a=$d.Actions.Create(0);$a.Path=$fixture;$a.Arguments='--background --sign-in'
 $task=$folder.RegisterTaskDefinition($failureName,$d,6,$sid,$null,3,$null);$null=$task.Run($null)
 $deadline=[DateTime]::UtcNow.AddSeconds(10)
 do{Start-Sleep -Milliseconds 100;$task=$folder.GetTask($failureName)}while($task.State -eq 4 -and [DateTime]::UtcNow -lt $deadline)
 Check ($task.LastTaskResult -eq 1 -and $task.State -ne 4) 'Missing component produces task result 1 without a blocking dialog'
 $result.Passed=$true
} catch {$result.Error=$_.Exception.ToString()}
finally {
 try{Command '--register-startup';RunMainTask}catch{$result.Error+='; recovery: '+$_.Exception.Message;$result.Passed=$false}
 if($failureName){try{$folder.DeleteTask($failureName,0)}catch{}}
 if($fixture){try{[IO.File]::Delete($fixture);[IO.Directory]::Delete([IO.Path]::GetDirectoryName($fixture))}catch{}}
 $result.Checks=@($checks.ToArray())
 $task=$folder.GetTask($ValidationTask);$definition=$task.Definition;$definition.RegistrationInfo.Description=($result | ConvertTo-Json -Compress)
 $null=$folder.RegisterTaskDefinition($ValidationTask,$definition,6,[Security.Principal.WindowsIdentity]::GetCurrent().User.Value,$null,3,$null)
}
if(!$result.Passed){exit 1}
'@
$mainName='Clean11 Startup '+[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
if((Get-ScheduledTask -TaskName $mainName).State -eq 'Disabled'){throw 'This test requires Start with Windows to be enabled.'}
$encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script.Replace('__TASK_NAME__',$name)))
$svc=New-Object -ComObject Schedule.Service;$svc.Connect();$folder=$svc.GetFolder('\')
try {
 $d=$svc.NewTask(0);$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
 $d.Principal.UserId=$sid;$d.Principal.LogonType=3;$d.Principal.RunLevel=0
 $d.Settings.DisallowStartIfOnBatteries=$false;$d.Settings.StopIfGoingOnBatteries=$false
 $a=$d.Actions.Create(0);$a.Path=Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe';$a.Arguments='-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand '+$encoded
 $task=$folder.RegisterTaskDefinition($name,$d,6,$sid,$null,3,$null);$null=$task.Run($null)
 $deadline=[DateTime]::UtcNow.AddSeconds(90)
 do{Start-Sleep -Milliseconds 200;$task=$folder.GetTask($name)}while($task.State -eq 4 -and [DateTime]::UtcNow -lt $deadline)
 $report=$task.Definition.RegistrationInfo.Description
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ResultsPath))) | Out-Null
 [IO.File]::WriteAllText($ResultsPath,$report)
 $data=$report | ConvertFrom-Json
 $data.Checks | ForEach-Object {Write-Output ('PASS: '+$_)}
 if(!$data.Passed -or $task.LastTaskResult -ne 0){throw ('Startup validation failed: '+$data.Error)}
 Write-Output ('Startup validation passed; report: '+$ResultsPath)
} finally {try{$folder.DeleteTask($name,0)}catch{}}
