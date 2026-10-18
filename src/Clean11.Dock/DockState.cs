using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

sealed class DockPin {
    public string Id=Guid.NewGuid().ToString("N"),Name="",Path="",Arguments="",WorkingDirectory="",AppId="",MatchPath="";
}
sealed class DockState {
    public int Version=1,IconSize=44;public double Magnification=1.4;
    public string Visibility="Intelligent",Monitor="";
    public bool ReducedMotion=false,ShowDownloads=true,ShowRecycleBin=true,ShowBackgroundApps=true,TakeOver=true,Startup=true;
    public List<DockPin> Pins=new List<DockPin>();
    internal static readonly string Root=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Clean11","Dock");
    static readonly string FileName=System.IO.Path.Combine(Root,"settings.json");
    internal static DockState Load(){
        if(File.Exists(FileName)){try{var state=new JavaScriptSerializer().Deserialize<DockState>(File.ReadAllText(FileName));if(state!=null){state.Pins=state.Pins??new List<DockPin>();state.IconSize=Math.Max(28,Math.Min(64,state.IconSize));state.Magnification=Math.Max(1,Math.Min(1.7,state.Magnification));if(!new[]{"Always","Auto-hide","Intelligent"}.Contains(state.Visibility))state.Visibility="Intelligent";return state;}}catch{try{File.Copy(FileName,FileName+".invalid-"+DateTime.UtcNow.Ticks);}catch{}}}
        var fresh=new DockState();fresh.Pins=ImportSeelen();if(fresh.Pins.Count==0)fresh.Pins.Add(new DockPin{Name="File Explorer",Path="explorer.exe"});fresh.Save();return fresh;
    }
    internal void Save(){Directory.CreateDirectory(Root);string temp=FileName+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this));if(File.Exists(FileName))File.Replace(temp,FileName,FileName+".bak");else File.Move(temp,FileName);}
    static string Value(string text){text=text.Trim();if(text=="null")return "";if(text.StartsWith("'")&&text.EndsWith("'"))return text.Substring(1,text.Length-2).Replace("''","'");if(text.StartsWith("\"")){try{return new JavaScriptSerializer().Deserialize<string>(text);}catch{}}return text;}
    // Reads only the simple AppOrFile records, not arbitrary YAML tags or code.
    static List<DockPin> ImportSeelen(){
        var output=new List<DockPin>();string file=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"com.seelen.seelen-ui","data","seelen-weg","state.yml");if(!File.Exists(file))return output;
        DockPin pin=null;bool pinned=false,relaunch=false;Action finish=()=>{if(pin!=null&&pinned&&(!String.IsNullOrEmpty(pin.Path)||!String.IsNullOrEmpty(pin.AppId))){ResolveShortcut(pin);output.Add(pin);}};
        foreach(string line in File.ReadLines(file)){
            string trim=line.Trim();if(trim.StartsWith("- type:")){finish();pin=trim=="- type: AppOrFile"?new DockPin():null;pinned=false;relaunch=false;continue;}if(pin==null)continue;
            int colon=trim.IndexOf(':');if(colon<0)continue;string key=trim.Substring(0,colon),value=Value(trim.Substring(colon+1));
            if(key=="relaunch"){relaunch=trim=="relaunch:";continue;}
            if(relaunch&&line.StartsWith("    ")){if(key=="command"&&value.Length>0)pin.Path=value;if(key=="args")pin.Arguments=value;if(key=="workingDir")pin.WorkingDirectory=value;continue;}
            if(key=="displayName")pin.Name=value;if(key=="path"){pin.Path=value;pin.MatchPath=value;}if(key=="umid")pin.AppId=value;if(key=="pinned")pinned=value=="true";
        }finish();return output;
    }
    internal static void ResolveShortcut(DockPin pin){
        if(!pin.Path.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase))return;object shell=null,link=null;
        try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));dynamic automation=shell;link=automation.CreateShortcut(pin.Path);dynamic shortcut=link;string target=(string)shortcut.TargetPath;if(!String.IsNullOrWhiteSpace(target))pin.MatchPath=Environment.ExpandEnvironmentVariables(target);if(String.IsNullOrEmpty(pin.WorkingDirectory))pin.WorkingDirectory=(string)shortcut.WorkingDirectory;}catch{}finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
    }
    internal static DockPin FromPath(string path){var pin=new DockPin{Path=path,MatchPath=path,Name=System.IO.Path.GetFileNameWithoutExtension(path)};if(Directory.Exists(path))pin.Name=new DirectoryInfo(path).Name;ResolveShortcut(pin);return pin;}
    internal static void Launch(DockPin pin){
        try{
            if(!String.IsNullOrEmpty(pin.AppId)&&pin.AppId.Contains("!")){Process.Start(new ProcessStartInfo("explorer.exe","shell:AppsFolder\\"+pin.AppId){UseShellExecute=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)});return;}
            string working=pin.WorkingDirectory;if(String.IsNullOrWhiteSpace(working)||!Directory.Exists(working))working=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Process.Start(new ProcessStartInfo(pin.Path,pin.Arguments??""){UseShellExecute=true,WorkingDirectory=working});
        }catch(Exception e){DockLog.Write("Launch failed: "+e.GetType().Name);throw;}
    }
}
static class DockLog {
    internal static void Write(string message){try{Directory.CreateDirectory(DockState.Root);string path=Path.Combine(DockState.Root,"dock.log");if(File.Exists(path)&&new FileInfo(path).Length>512000)File.Move(path,path+"."+DateTime.UtcNow.Ticks);File.AppendAllText(path,DateTime.Now.ToString("s")+" "+message+Environment.NewLine);}catch{}}
}
