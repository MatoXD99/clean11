using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Runtime.InteropServices;

static class Clean11Launcher {
    static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
    static readonly string Overview=Path.Combine(Root,"Overview","ActivitiesOverview.exe");
    static readonly string Widgets=Path.Combine(Root,"Dock","Clean11.Widgets.exe");
    static readonly string Dock=Path.Combine(Root,"Dock","Clean11.Dock.exe");
    static readonly Stopwatch StartupClock=Stopwatch.StartNew();
    static bool StartupRun;
    [DllImport("user32.dll")]static extern IntPtr GetShellWindow();
    static void LogStartup(string message){
        if(!StartupRun)return;
        try{string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Clean11","Dock");Directory.CreateDirectory(folder);File.AppendAllText(Path.Combine(folder,"dock.log"),DateTime.Now.ToString("s")+" launcher +"+StartupClock.ElapsedMilliseconds+"ms: "+message+Environment.NewLine);}catch{}
    }
    static void Start(string executable,string arguments){
        if(!File.Exists(executable))throw new FileNotFoundException("A Clean11 component is missing. Repair the installation.");
        Process.Start(new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)});
        LogStartup("launched "+Path.GetFileName(executable)+" with "+arguments);
    }
    static int RunningProcessId(string executable){
        foreach(var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))using(process)try{if(String.Equals(process.MainModule.FileName,executable,StringComparison.OrdinalIgnoreCase))return process.Id;}catch{}
        return 0;
    }
    static bool IsRunning(string executable){return RunningProcessId(executable)!=0;}
    static bool StartupRequested(){
        using(var install=Registry.CurrentUser.OpenSubKey(@"Software\Clean11\Installer")){
            if(install!=null){var pending=install.GetValue("PendingStartup");if(pending!=null)return Convert.ToString(pending)=="1";}
        }
        string file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Clean11","Dock","settings.json");
        if(File.Exists(file)){
            var settings=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(file));
            object enabled;if(settings!=null&&settings.TryGetValue("Startup",out enabled))return Convert.ToBoolean(enabled);
        }
        using(var install=Registry.CurrentUser.OpenSubKey(@"Software\Clean11\Installer")){
            if(install==null)return true;
            if(Convert.ToString(install.GetValue("StartupEnabled"))=="1")return true;
        }
        using(var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))return run!=null&&(run.GetValue("Clean11")!=null||run.GetValue("Clean11Dock")!=null);
    }
    static void RegisterStartup(bool enabled){
        Clean11Startup.SetEnabled(enabled,Path.Combine(Root,"Clean11.exe"),"--background --sign-in",true);
        LogStartup("logon task "+(enabled?"registered: ":"removed: ")+Clean11Startup.TaskName);
    }
    static void WaitForDesktop(){
        var deadline=DateTime.UtcNow.AddSeconds(20);
        while(GetShellWindow()==IntPtr.Zero&&DateTime.UtcNow<deadline)Thread.Sleep(100);
        if(GetShellWindow()==IntPtr.Zero)throw new InvalidOperationException("Windows desktop was not ready; Task Scheduler will retry startup.");
        LogStartup("Windows desktop ready; session="+Process.GetCurrentProcess().SessionId);
    }
    static void EnsureStarted(string executable,string arguments){
        if(IsRunning(executable)){LogStartup(Path.GetFileName(executable)+" already running");return;}
        Start(executable,arguments);
    }
    static void CheckStartupHealth(){
        var clock=Stopwatch.StartNew();bool repairedWidgets=false;
        while(clock.ElapsedMilliseconds<15000){
            bool dockReady=IsReady("Local\\Clean11DockReady")&&IsRunning(Dock);
            bool overviewReady=IsReady("Local\\Clean11OverviewReady")&&IsRunning(Overview);
            bool widgets=IsRunning(Widgets);
            if(dockReady&&overviewReady&&widgets&&clock.ElapsedMilliseconds>=2000){LogStartup("startup health confirmed; dock rendered; overview hotkey ready; widgets running");return;}
            if(dockReady&&!widgets&&!repairedWidgets&&clock.ElapsedMilliseconds>5000){
                int parent=RunningProcessId(Dock);if(parent!=0){Start(Widgets,"--background --startup --parent "+parent);repairedWidgets=true;}
            }
            Thread.Sleep(100);
        }
        throw new InvalidOperationException("Clean11 startup did not become ready within 15 seconds. See dock.log and Overview/native.log; Task Scheduler will retry.");
    }
    static bool IsReady(string name){try{using(var ready=EventWaitHandle.OpenExisting(name))return ready.WaitOne(0);}catch(WaitHandleCannotBeOpenedException){return false;}}
    static void ApplyInstallChoice(){
        using(var install=Registry.CurrentUser.OpenSubKey(@"Software\Clean11\Installer",true)){
            if(install==null)return;var requested=install.GetValue("PendingStartup");if(requested==null)return;
            string file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Clean11","Dock","settings.json");
            if(File.Exists(file)){
                var json=new JavaScriptSerializer();var settings=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(file));
                settings["Startup"]=Convert.ToString(requested)=="1";
                string temp=file+".install.tmp";File.WriteAllText(temp,json.Serialize(settings));File.Replace(temp,file,file+".bak");
            }
            install.DeleteValue("PendingStartup",false);
        }
    }
    static int Stop(){
        // Ask the modules to save/close normally. Never kill an editor or discard settings.
        if(File.Exists(Dock)&&IsRunning(Dock))Start(Dock,"--shutdown");
        if(File.Exists(Overview)&&IsRunning(Overview))Start(Overview,"--shutdown");
        if(File.Exists(Widgets)&&IsRunning(Widgets))Start(Widgets,"--shutdown");
        var deadline=DateTime.UtcNow.AddSeconds(20);
        while(DateTime.UtcNow<deadline){if(!IsRunning(Dock)&&!IsRunning(Overview)&&!IsRunning(Widgets))return 0;Thread.Sleep(100);}
        MessageBox.Show("Please save or discard pending Clean11 Settings changes and close Settings, then retry setup.","Clean11 setup",MessageBoxButtons.OK,MessageBoxIcon.Information);
        return 1;
    }
    [STAThread]static int Main(string[] args){
        StartupRun=args.Contains("--background")||args.Contains("--sign-in")||args.Contains("--register-startup")||args.Contains("--unregister-startup")||args.Contains("--sync-startup");
        try{
            if(args.Contains("--stop-for-maintenance")||args.Contains("--exit"))return Stop();
            if(args.Contains("--check-install"))return File.Exists(Dock)&&File.Exists(Overview)&&File.Exists(Widgets)?0:2;
            LogStartup("launcher entered; sign-in="+args.Contains("--sign-in"));
            if(args.Contains("--register-startup")){RegisterStartup(true);return 0;}
            if(args.Contains("--unregister-startup")){RegisterStartup(false);return 0;}
            if(args.Contains("--sync-startup")){RegisterStartup(StartupRequested());return 0;}
            bool created;using(var gate=new Mutex(true,"Local\\Clean11.Launcher",out created)){
                if(!created){LogStartup("another launcher is already handling startup");return 0;}
                ApplyInstallChoice();
                if(args.Contains("--sign-in")){
                    if(!StartupRequested()){RegisterStartup(false);LogStartup("sign-in launch skipped: Start with Windows is off");return 0;}
                    WaitForDesktop();
                }
                bool first=!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Clean11","Dock","settings.json"));
                if(args.Contains("--overview")){Start(Dock,"");Start(Overview,"");}
                else {
                    string dockArgs=args.Contains("--settings")||(!args.Contains("--background")&&first)?"--settings":args.Contains("--background")?"--startup --overview-started":"";
                    if(dockArgs=="--settings")Start(Dock,dockArgs);else EnsureStarted(Dock,dockArgs);
                    EnsureStarted(Overview,"--background");
                }
                LogStartup("launch requests queued");
                if(args.Contains("--sign-in"))CheckStartupHealth();
            }
            return 0;
        }catch(Exception error){LogStartup("FAILED: "+error);if(!StartupRun)MessageBox.Show(error.Message,"Clean11",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
}
