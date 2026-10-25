param([string]$Package=(Join-Path $PSScriptRoot 'Clean11-0.3.2-x64.msi'),[string]$ResultsDirectory=(Join-Path $env:USERPROFILE 'Desktop/Clean11-test-results'),[switch]$Interactive)
$ErrorActionPreference='Stop'
$logRoot=$ResultsDirectory
New-Item -ItemType Directory -Force $logRoot | Out-Null
Start-Transcript -Path (Join-Path $logRoot 'results.txt') -Force | Out-Null
function Check([bool]$Condition,[string]$Message){if(!$Condition){throw $Message};Write-Output "PASS: $Message"}
function Msi([string[]]$Options){$process=Start-Process msiexec.exe -ArgumentList $Options -Wait -PassThru;if($process.ExitCode -notin @(0,3010)){throw "MSI returned $($process.ExitCode). See the verbose log."}}
$taskName='Clean11 Startup '+[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
function StartupTask {Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue}
function LaunchFromTask {
    $started=Get-Date
    Start-ScheduledTask -TaskName $taskName
    $deadline=[DateTime]::UtcNow.AddSeconds(40)
    do {Start-Sleep -Milliseconds 200;$info=Get-ScheduledTaskInfo -TaskName $taskName;$task=StartupTask} while(($task.State -eq 'Running' -or $info.LastRunTime -lt $started.AddSeconds(-1)) -and [DateTime]::UtcNow -lt $deadline)
    Check ($task.State -ne 'Running' -and $info.LastRunTime -ge $started.AddSeconds(-1) -and $info.LastTaskResult -eq 0) 'Registered sign-in action confirms rendered dock, ready overview and widgets.'
}
try {
    $packagePath=(Resolve-Path $Package).Path
    $root=Join-Path $env:LOCALAPPDATA 'Programs/Clean11'
    Check (!(Test-Path $root)) 'The disposable account has no prior Clean11 installation.'
    Msi @('/i',('"'+$packagePath+'"'),'/qn','STARTWITHWINDOWS=1','LAUNCHCLEAN11=0','/l*v',('"'+(Join-Path $logRoot 'install.log')+'"'))
    $launcher=Join-Path $root 'Clean11.exe'
    Check (Test-Path $launcher) 'Launcher installed.'
    Check (Test-Path (Join-Path $root 'Overview/ActivitiesOverview.exe')) 'Overview installed.'
    Check (Test-Path (Join-Path $root 'Dock/Clean11.Dock.exe')) 'Dock installed.'
    Check (Test-Path (Join-Path $root 'licenses/WPF-UI-LICENSE.txt')) 'Dependency license included.'
    $shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'Clean11/Clean11 Settings.lnk'
    Check (Test-Path $shortcut) 'Settings Start-menu shortcut installed.'
    $runKey='HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
    $task=StartupTask
    Check ($null -ne $task -and $task.Actions.Execute -eq $launcher -and $task.Actions.Arguments -eq '--background --sign-in') 'Unified per-user logon task installed.'
    Check ($task.Principal.LogonType -eq 'Interactive' -and $task.Principal.RunLevel -eq 'Limited') 'Startup uses the current user session without elevation.'
    $check=Start-Process $launcher -ArgumentList '--check-install' -Wait -PassThru
    Check ($check.ExitCode -eq 0) 'Launcher resolves both installed modules.'
    LaunchFromTask
    foreach($name in @('Clean11.Dock','ActivitiesOverview')){
        $running=@(Get-Process $name -ErrorAction SilentlyContinue | Where-Object {$_.Path -like "$root*"})
        Check ($running.Count -gt 0) "$name starts from the package."
    }
    if($Interactive){
        Start-Process $launcher -ArgumentList '--settings'
        Write-Output 'Try Settings, dock, the Windows-key overview, typing, and window switching. Close/save Settings before continuing.'
        Read-Host 'Press Enter to test shutdown, repair, upgrade (if supplied), and uninstall' | Out-Null
    }
    $stop=Start-Process $launcher -ArgumentList '--stop-for-maintenance' -Wait -PassThru
    Check ($stop.ExitCode -eq 0) 'Graceful module shutdown succeeds.'
    $data=Join-Path $env:LOCALAPPDATA 'Clean11'
    $marker=Join-Path $data 'installer-test-marker.txt';[IO.File]::WriteAllText($marker,'Preserve user data across maintenance.')
    Msi @('/fa',('"'+$packagePath+'"'),'/qn','LAUNCHCLEAN11=0','/l*v',('"'+(Join-Path $logRoot 'repair.log')+'"'))
    Check (Test-Path $launcher) 'Repair retains the app.'
    $next=Get-ChildItem $PSScriptRoot -Filter 'Clean11-upgrade.msi' | Select-Object -First 1
    $uninstall=$packagePath
    if($next){
        Msi @('/i',('"'+$next.FullName+'"'),'/qn','LAUNCHCLEAN11=0','/l*v',('"'+(Join-Path $logRoot 'upgrade.log')+'"'))
        Check (Test-Path $launcher) 'Major upgrade retains the app.'
        Check (Test-Path $marker) 'Major upgrade preserves user data.'
        $downgrade=Start-Process msiexec.exe -ArgumentList @('/i',('"'+$packagePath+'"'),'/qn','LAUNCHCLEAN11=0','/l*v',('"'+(Join-Path $logRoot 'downgrade-rejected.log')+'"')) -PassThru -Wait
        Check ($downgrade.ExitCode -ne 0) 'An older package is rejected after upgrade.'
        $uninstall=$next.FullName
    }
    Msi @('/x',('"'+$uninstall+'"'),'/qn','/l*v',('"'+(Join-Path $logRoot 'uninstall.log')+'"'))
    Check (!(Test-Path $launcher)) 'Uninstall removes the app executable.'
    Check (!(Test-Path $shortcut)) 'Uninstall removes its shortcut.'
    Check ($null -eq (StartupTask)) 'Uninstall removes the logon task.'
    $startup=Get-ItemProperty -Path $runKey -ErrorAction SilentlyContinue
    Check (!$startup -or !$startup.PSObject.Properties['Clean11']) 'Uninstall removes its startup entry.'
    Check (Test-Path $marker) 'Uninstall preserves user data.'
    # Repeat from an unregistered state with startup explicitly disabled.
    Msi @('/i',('"'+$packagePath+'"'),'/qn','STARTWITHWINDOWS=""','LAUNCHCLEAN11=0','/l*v',('"'+(Join-Path $logRoot 'no-startup.log')+'"'))
    $startup=Get-ItemProperty -Path $runKey -ErrorAction SilentlyContinue
    Check (!$startup -or !$startup.PSObject.Properties['Clean11']) 'The unchecked startup option is honored.'
    Check ($null -eq (StartupTask)) 'Unchecked startup does not install a logon task.'
    Start-Process $launcher -ArgumentList '--background'
    Start-Sleep -Seconds 4
    $startup=Get-ItemProperty -Path $runKey -ErrorAction SilentlyContinue
    Check (!$startup -or !$startup.PSObject.Properties['Clean11']) 'First launch retains the unchecked startup choice even with old preferences.'
    Check ($null -eq (StartupTask)) 'First launch leaves sign-in startup disabled.'
    $stop=Start-Process $launcher -ArgumentList '--stop-for-maintenance' -Wait -PassThru
    Check ($stop.ExitCode -eq 0) 'Second-install shutdown succeeds.'
    Msi @('/x',('"'+$packagePath+'"'),'/qn','/l*v',('"'+(Join-Path $logRoot 'final-uninstall.log')+'"'))
    Write-Output 'Sandbox lifecycle checks completed. Visual smoothness and taskbar recovery still need human inspection.'
} catch {Write-Error $_;throw} finally {Stop-Transcript | Out-Null}
