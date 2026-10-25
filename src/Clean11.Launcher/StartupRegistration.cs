using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

// One per-user logon task owns startup for every installed Clean11 module.
// Do not edit StartupApproved: those records belong to Windows and the user.
static class Clean11Startup {
    internal static string TaskName {get{return "Clean11 Startup "+WindowsIdentity.GetCurrent().User.Value;}}
    static dynamic Keep(List<object> handles,object value){if(value!=null)handles.Add(value);return value;}
    static bool Missing(COMException error){return error.ErrorCode==unchecked((int)0x80070002);}
    static bool IsCurrentUser(string value){
        string sid=WindowsIdentity.GetCurrent().User.Value;if(value==sid)return true;
        try{return ((SecurityIdentifier)new NTAccount(value).Translate(typeof(SecurityIdentifier))).Value==sid;}catch{return false;}
    }
    internal static void SetEnabled(bool enabled,string executable,string arguments,bool explicitlyEnable){
        if(enabled&&!File.Exists(executable))throw new FileNotFoundException("The Clean11 startup launcher is missing.",executable);
        var handles=new List<object>();bool registeredEnabled=enabled;
        try {
            dynamic service=Keep(handles,Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")));
            service.Connect();dynamic folder=Keep(handles,service.GetFolder("\\"));dynamic existing=null;
            try{existing=Keep(handles,folder.GetTask(TaskName));}catch(FileNotFoundException){}catch(COMException error){if(!Missing(error))throw;}
            if(enabled){
                bool taskEnabled=existing==null||explicitlyEnable||(bool)existing.Enabled;
                registeredEnabled=taskEnabled;
                bool matches=false;
                if(existing!=null){
                    dynamic old=Keep(handles,existing.Definition);dynamic actions=Keep(handles,old.Actions);dynamic triggers=Keep(handles,old.Triggers);
                    dynamic principal=Keep(handles,old.Principal);dynamic settings=Keep(handles,old.Settings);
                    if(actions.Count==1&&triggers.Count==1){
                        dynamic action=Keep(handles,actions.Item(1));dynamic trigger=Keep(handles,triggers.Item(1));
                        matches=action.Type==0&&trigger.Type==9&&String.Equals((string)action.Path,executable,StringComparison.OrdinalIgnoreCase)
                            &&(string)action.Arguments==arguments&&principal.LogonType==3&&principal.RunLevel==0
                            &&IsCurrentUser((string)principal.UserId)&&IsCurrentUser((string)trigger.UserId)
                            &&!settings.DisallowStartIfOnBatteries&&!settings.StopIfGoingOnBatteries&&!settings.RunOnlyIfIdle
                            &&!settings.RunOnlyIfNetworkAvailable&&settings.MultipleInstances==2&&settings.ExecutionTimeLimit=="PT1M"
                            &&settings.RestartCount==3&&(string)settings.RestartInterval=="PT1M"&&(bool)existing.Enabled==taskEnabled;
                    }
                }
                if(!matches){
                    dynamic definition=Keep(handles,service.NewTask(0));
                    dynamic registration=Keep(handles,definition.RegistrationInfo);
                    registration.Author="Clean11";registration.Source="Clean11";
                    registration.Description="Start the Clean11 dock, top bar, overview, search and widgets when this user signs in.";
                    string sid=WindowsIdentity.GetCurrent().User.Value;
                    dynamic principal=Keep(handles,definition.Principal);principal.UserId=sid;principal.LogonType=3;principal.RunLevel=0;
                    dynamic settings=Keep(handles,definition.Settings);
                    settings.Enabled=taskEnabled;settings.AllowDemandStart=true;settings.StartWhenAvailable=true;
                    settings.DisallowStartIfOnBatteries=false;settings.StopIfGoingOnBatteries=false;settings.RunOnlyIfIdle=false;
                    settings.RunOnlyIfNetworkAvailable=false;settings.MultipleInstances=2;settings.ExecutionTimeLimit="PT1M";
                    settings.RestartCount=3;settings.RestartInterval="PT1M";settings.Priority=6;
                    dynamic triggers=Keep(handles,definition.Triggers);dynamic trigger=Keep(handles,triggers.Create(9));trigger.UserId=sid;trigger.Enabled=true;
                    dynamic actions=Keep(handles,definition.Actions);dynamic action=Keep(handles,actions.Create(0));
                    action.Path=executable;action.Arguments=arguments;action.WorkingDirectory=Path.GetDirectoryName(executable);
                    Keep(handles,folder.RegisterTaskDefinition(TaskName,definition,6,sid,null,3,null));
                }
            }else if(existing!=null)folder.DeleteTask(TaskName,0);
            // Retire earlier launch mechanisms only after the task operation succeeds.
            using(var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true))if(run!=null){run.DeleteValue("Clean11",false);run.DeleteValue("Clean11Dock",false);}
            string startup=Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            foreach(string name in new[]{"Clean11 Dock.lnk","Activities Overview (native).lnk"}){string path=Path.Combine(startup,name);if(File.Exists(path))File.Delete(path);}
            using(var install=Registry.CurrentUser.CreateSubKey(@"Software\Clean11\Installer")){
                if(registeredEnabled)install.SetValue("StartupEnabled","1");else install.DeleteValue("StartupEnabled",false);
            }
        }finally{for(int i=handles.Count-1;i>=0;i--)try{if(Marshal.IsComObject(handles[i]))Marshal.FinalReleaseComObject(handles[i]);}catch{}}
    }
}
