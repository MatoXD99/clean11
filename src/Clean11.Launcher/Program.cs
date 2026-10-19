using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Microsoft.Win32;

static class Clean11Launcher {
    static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
    static readonly string Overview=Path.Combine(Root,"Overview","ActivitiesOverview.exe");
    static readonly string Dock=Path.Combine(Root,"Dock","Clean11.Dock.exe");
    static void Start(string executable,string arguments){
        if(!File.Exists(executable))throw new FileNotFoundException("A Clean11 component is missing. Repair the installation.");
        Process.Start(new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)});
    }
    static bool IsRunning(string executable){
        foreach(var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))using(process)try{if(String.Equals(process.MainModule.FileName,executable,StringComparison.OrdinalIgnoreCase))return true;}catch{}
        return false;
    }
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
        var deadline=DateTime.UtcNow.AddSeconds(20);
        while(DateTime.UtcNow<deadline){if(!IsRunning(Dock)&&!IsRunning(Overview))return 0;Thread.Sleep(100);}
        MessageBox.Show("Please save or discard pending Clean11 Settings changes and close Settings, then retry setup.","Clean11 setup",MessageBoxButtons.OK,MessageBoxIcon.Information);
        return 1;
    }
    [STAThread]static int Main(string[] args){
        try{
            if(args.Contains("--stop-for-maintenance")||args.Contains("--exit"))return Stop();
            if(args.Contains("--check-install"))return File.Exists(Dock)&&File.Exists(Overview)?0:2;
            bool created;using(var gate=new Mutex(true,"Local\\Clean11.Launcher",out created)){
                if(!created)return 0;
                ApplyInstallChoice();
                bool first=!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Clean11","Dock","settings.json"));
                if(args.Contains("--overview")){Start(Dock,"");Start(Overview,"");}
                else {Start(Overview,"--background");Start(Dock,args.Contains("--settings")||(!args.Contains("--background")&&first)?"--settings":"");}
            }
            return 0;
        }catch(Exception error){MessageBox.Show(error.Message,"Clean11",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
}
