using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Diagnostics;
using System.Threading;

sealed class OverviewPreferences {
    public int MotionDuration=200,PreviewColumns=4,GlassStrength=60,BlurRadius=8;
    public string GlassStyle="Midnight";
    public bool ReducedMotion=false,Glass=true,LiveGamePreviews=false,CompositionMotion=false;
    static string Root {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Clean11","Overview");}}
    static string FileName {get{return Path.Combine(Root,"preferences.json");}}
    static readonly object cacheLock=new object();static OverviewPreferences cached;
    static FileSystemWatcher watcher;static Timer reloadTimer;static AppearanceSnapshot appearance;
    internal sealed class AppearanceSnapshot {
        internal readonly bool Glass,Light,CompositionMotion;internal readonly int Strength,Radius,Duration;
        internal AppearanceSnapshot(OverviewPreferences p){Glass=p.Glass;Light=p.GlassStyle=="Pearl";Strength=p.GlassStrength;Radius=p.BlurRadius;Duration=p.MotionDuration;CompositionMotion=p.CompositionMotion;}
    }
    static void Publish(OverviewPreferences value){cached=value;Volatile.Write(ref appearance,new AppearanceSnapshot(value));}
    static void EnsureLoaded(){
        if(Volatile.Read(ref appearance)!=null)return;
        lock(cacheLock){if(cached!=null)return;
            Directory.CreateDirectory(Root);
            reloadTimer=new Timer(_=>Reload(),null,Timeout.Infinite,Timeout.Infinite);
            watcher=new FileSystemWatcher(Root,"preferences.json"){NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName|NotifyFilters.Size};
            watcher.Changed+=(s,e)=>QueueReload();watcher.Created+=(s,e)=>QueueReload();watcher.Deleted+=(s,e)=>QueueReload();watcher.Renamed+=(s,e)=>QueueReload();
            watcher.Error+=(s,e)=>QueueReload();watcher.EnableRaisingEvents=true;
            try{Publish(LoadCore());}catch{Publish(new OverviewPreferences());QueueReload();}
        }
    }
    static void QueueReload(){try{reloadTimer.Change(80,Timeout.Infinite);}catch(ObjectDisposedException){}}
    static void Reload(){lock(cacheLock){try{Publish(LoadCore());}catch(IOException){reloadTimer.Change(250,Timeout.Infinite);}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}}}
    internal static AppearanceSnapshot Appearance {get{EnsureLoaded();return Volatile.Read(ref appearance);}}
    internal static OverviewPreferences Load(){EnsureLoaded();lock(cacheLock)return (OverviewPreferences)cached.MemberwiseClone();}
    static OverviewPreferences LoadCore(){
        var json=new JavaScriptSerializer();
        OverviewPreferences p;
        if(File.Exists(FileName))p=json.Deserialize<OverviewPreferences>(File.ReadAllText(FileName))??new OverviewPreferences();
        else {
            p=new OverviewPreferences();
            string legacy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ActivitiesOverview","state.json");
            if(File.Exists(legacy)){
                var values=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(legacy));
                if(values.ContainsKey("MotionDuration"))p.MotionDuration=Convert.ToInt32(values["MotionDuration"]);
                if(values.ContainsKey("PreviewColumns"))p.PreviewColumns=Convert.ToInt32(values["PreviewColumns"]);
                if(values.ContainsKey("ReducedMotion"))p.ReducedMotion=Convert.ToBoolean(values["ReducedMotion"]);
                if(values.ContainsKey("Glass"))p.Glass=Convert.ToBoolean(values["Glass"]);
                if(values.ContainsKey("GameFriendly"))p.LiveGamePreviews=!Convert.ToBoolean(values["GameFriendly"]);
            }
        }
        if(p.GlassStyle!="Pearl"&&p.GlassStyle!="Midnight")p.GlassStyle="Midnight";p.BlurRadius=Math.Max(0,Math.Min(40,p.BlurRadius));p.GlassStrength=Math.Max(0,Math.Min(100,p.GlassStrength));p.MotionDuration=Math.Max(120,Math.Min(600,p.MotionDuration));p.PreviewColumns=Math.Max(3,Math.Min(5,p.PreviewColumns));return p;
    }
    internal void Save(){Directory.CreateDirectory(Root);string temp=FileName+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this));if(File.Exists(FileName))File.Replace(temp,FileName,FileName+".bak");else File.Move(temp,FileName);EnsureLoaded();lock(cacheLock){Publish((OverviewPreferences)MemberwiseClone());}}
    internal static void AddPortable(string path){
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ActivitiesOverview");Directory.CreateDirectory(root);
        string file=Path.Combine(root,"portable-apps.json");var json=new JavaScriptSerializer();var list=File.Exists(file)?json.Deserialize<List<string>>(File.ReadAllText(file)):new List<string>();list=list??new List<string>();list.Add(path);File.WriteAllText(file,json.Serialize(list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
    }
    internal static void RunOverview(string arguments){
        string exe=Clean11Paths.Overview;
        if(!File.Exists(exe))throw new FileNotFoundException("Overview application is not installed.");
        Process.Start(new ProcessStartInfo(exe,arguments){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)});
    }
}
