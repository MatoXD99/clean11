using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

sealed class HorizonPhase {
    internal readonly string Id,Name;internal readonly double Starts;
    internal HorizonPhase(string id,string name,double starts){Id=id;Name=name;Starts=starts;}
    internal string FileName {get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Wallpapers","Horizon",Id+".png");}}
    internal string TimeLabel {get{return DateTime.Today.AddHours(Starts).ToString("HH:mm");}}
}
static class HorizonSchedule {
    internal static readonly HorizonPhase[] Phases={
        new HorizonPhase("predawn","Pre-dawn",5),new HorizonPhase("sunrise","Sunrise",6.5),
        new HorizonPhase("morning","Morning",8),new HorizonPhase("midday","Midday",11),
        new HorizonPhase("afternoon","Afternoon",15),new HorizonPhase("sunset","Sunset",18),
        new HorizonPhase("twilight","Twilight",20),new HorizonPhase("night","Night",22)};
    internal static HorizonPhase At(double hour){var phase=Phases[7];foreach(var item in Phases)if(hour>=item.Starts)phase=item;return phase;}
    internal static HorizonPhase ForMode(string mode){return mode=="Always light"?Phases[3]:mode=="Always dark"?Phases[7]:At(DateTime.Now.TimeOfDay.TotalHours);}
    internal static HorizonPhase Next(double hour){return Phases.FirstOrDefault(x=>x.Starts>hour)??Phases[0];}
    internal static bool AssetsAvailable {get{return Phases.All(x=>File.Exists(x.FileName));}}
    internal static bool UseDefault(bool hasPreferences,bool hasAssets){return !hasPreferences&&hasAssets;}
}
sealed class HorizonPreferences {
    public int Version=1;public bool Enabled;public string Mode="Dynamic",LastApplied="";
    public HorizonBackgroundBackup Previous;
    internal static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Clean11","Wallpaper");
    internal static readonly string FileName=Path.Combine(Root,"settings.json");
    internal static HorizonPreferences Load(){
        if(!File.Exists(FileName))return new HorizonPreferences();
        var result=new JavaScriptSerializer{MaxJsonLength=4194304}.Deserialize<HorizonPreferences>(File.ReadAllText(FileName));
        if(result==null||result.Version!=1)throw new InvalidDataException("Unsupported wallpaper preferences.");
        if(!new[]{"Dynamic","Always light","Always dark"}.Contains(result.Mode))result.Mode="Dynamic";
        return result;
    }
    internal void Save(){Directory.CreateDirectory(Root);string temp=FileName+".tmp";File.WriteAllText(temp,new JavaScriptSerializer{MaxJsonLength=4194304}.Serialize(this));if(File.Exists(FileName))File.Replace(temp,FileName,FileName+".bak");else File.Move(temp,FileName);}
}
sealed class HorizonSnapshot {
    internal bool Ready,Enabled,Available,CanRestore;internal string Mode="Dynamic",Phase="",Message="Loading Horizon…";
}

sealed class HorizonService : IDisposable {
    static HorizonService instance;
    internal static HorizonService Instance {get{return instance??(instance=new HorizonService());}}
    readonly BlockingCollection<Action> work=new BlockingCollection<Action>();readonly object statusGate=new object();
    readonly Timer timer;HorizonPreferences settings;HorizonSnapshot snapshot=new HorizonSnapshot();volatile bool disposed;int tickQueued,forceNext;
    internal HorizonSnapshot Snapshot {get{lock(statusGate)return snapshot;}}
    HorizonService(){
        work.Add(()=>{
            bool firstRun=!File.Exists(HorizonPreferences.FileName);
            settings=HorizonPreferences.Load();
            // New installations start with Horizon. Existing choices, including
            // explicitly disabled wallpaper, always survive updates/restarts.
            if(HorizonSchedule.UseDefault(!firstRun,HorizonSchedule.AssetsAvailable))EnableCore();else Tick(false);
        });
        var thread=new Thread(Run){IsBackground=true,Name="Clean11 Horizon wallpaper"};thread.SetApartmentState(ApartmentState.STA);thread.Start();
        timer=new Timer(_=>QueueTick(false),null,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(30));
        SystemEvents.TimeChanged+=TimeChanged;SystemEvents.PowerModeChanged+=PowerChanged;SystemEvents.DisplaySettingsChanged+=DisplaysChanged;
    }
    void Run(){foreach(var action in work.GetConsumingEnumerable()){try{action();}catch(Exception error){DockLog.Write("Horizon operation failed: "+error.GetType().Name+" ("+error.HResult.ToString("X")+")");Publish("Horizon could not update. Your previous wallpaper backup has been kept.");}}}
    void Publish(string message){
        var next=new HorizonSnapshot{Ready=settings!=null,Available=HorizonSchedule.AssetsAvailable,Enabled=settings!=null&&settings.Enabled,CanRestore=settings!=null&&settings.Previous!=null,Mode=settings==null?"Dynamic":settings.Mode,Phase=settings==null?"":HorizonSchedule.ForMode(settings.Mode).Name,Message=message};
        lock(statusGate)snapshot=next;
    }
    void Enqueue(Action action){if(disposed)return;try{work.Add(action);}catch(InvalidOperationException){}}
    void QueueTick(bool force){if(force)Interlocked.Exchange(ref forceNext,1);if(disposed||Interlocked.Exchange(ref tickQueued,1)!=0)return;Enqueue(()=>{try{if(settings!=null)Tick(Interlocked.Exchange(ref forceNext,0)!=0);}finally{Interlocked.Exchange(ref tickQueued,0);if(Volatile.Read(ref forceNext)!=0)QueueTick(false);}});}
    void TimeChanged(object sender,EventArgs args){TimeZoneInfo.ClearCachedData();QueueTick(false);}
    void PowerChanged(object sender,PowerModeChangedEventArgs args){if(args.Mode==PowerModes.Resume){TimeZoneInfo.ClearCachedData();QueueTick(false);}}
    void DisplaysChanged(object sender,EventArgs args){QueueTick(true);}
    void Tick(bool force){
        if(!settings.Enabled){Publish("Horizon is off. Preview the day below, then enable it when you are ready.");return;}
        if(!HorizonSchedule.AssetsAvailable){Publish("Horizon artwork is missing. Repair the Clean11 installation.");return;}
        using(var native=new HorizonNative()){
            if(!force&&!String.IsNullOrEmpty(settings.LastApplied)&&!native.Matches(settings.LastApplied)){
                settings.Enabled=false;settings.Save();Publish("Horizon paused because the wallpaper was changed outside Clean11.");return;
            }
            var phase=HorizonSchedule.ForMode(settings.Mode);
            if(force||!String.Equals(settings.LastApplied,phase.FileName,StringComparison.OrdinalIgnoreCase)){native.Apply(phase.FileName);settings.LastApplied=phase.FileName;settings.Save();RefreshGlass();}
        }
        Publish(settings.Mode=="Dynamic"?"Following this PC’s local time. The next lighting state changes automatically.":"Keeping "+HorizonSchedule.ForMode(settings.Mode).Name.ToLowerInvariant()+" lighting all day.");
    }
    static void RefreshGlass(){var app=System.Windows.Application.Current;try{if(app!=null&&!app.Dispatcher.HasShutdownStarted)app.Dispatcher.BeginInvoke((Action)FrostedMaterial.Invalidate);}catch(InvalidOperationException){}}
    Task<HorizonSnapshot> Command(Action action){
        var result=new TaskCompletionSource<HorizonSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(disposed){result.SetException(new ObjectDisposedException("Horizon"));return result.Task;}
        Enqueue(()=>{try{if(settings==null)throw new InvalidDataException("Wallpaper settings could not be loaded.");action();result.SetResult(Snapshot);}catch(Exception error){DockLog.Write("Horizon command failed: "+error.GetType().Name+" ("+error.HResult.ToString("X")+")");Publish("That wallpaper change could not be completed. Your previous backup has been kept.");result.SetResult(Snapshot);}});return result.Task;
    }
    void EnableCore(){
        if(!HorizonSchedule.AssetsAvailable)throw new FileNotFoundException("Horizon artwork is missing.");
        bool firstEnable=!settings.Enabled;
        if(firstEnable){
            var previous=settings.Previous;string lastApplied=settings.LastApplied;
            using(var native=new HorizonNative())settings.Previous=native.Capture(Path.Combine(HorizonPreferences.Root,"backups",Guid.NewGuid().ToString("N")));
            settings.Enabled=true;settings.LastApplied="";
            try{settings.Save();}catch{
                // A failed journal write must not let the periodic timer apply
                // wallpaper later without a persisted restoration record.
                settings.Enabled=false;settings.Previous=previous;settings.LastApplied=lastApplied;throw;
            }
        }
        try{Tick(true);}catch{if(firstEnable){try{using(var native=new HorizonNative())native.Restore(settings.Previous);}catch(Exception error){DockLog.Write("Horizon rollback: "+error.GetType().Name);}settings.Enabled=false;settings.LastApplied="";settings.Save();}throw;}
    }
    internal Task<HorizonSnapshot> SetEnabled(bool enabled){return Command(()=>{if(enabled)EnableCore();else RestoreCore();});}
    internal Task<HorizonSnapshot> SetMode(string mode){return Command(()=>{if(!new[]{"Dynamic","Always light","Always dark"}.Contains(mode))return;settings.Mode=mode;settings.Save();Tick(true);});}
    internal Task<HorizonSnapshot> Restore(){return Command(RestoreCore);}
    void RestoreCore(){
        if(settings.Previous==null){settings.Enabled=false;settings.Save();Publish("Horizon is off.");return;}
        using(var native=new HorizonNative())native.Restore(settings.Previous);
        settings.Enabled=false;settings.LastApplied="";settings.Save();RefreshGlass();Publish("Your previous Windows wallpaper has been restored.");
    }
    public void Dispose(){if(disposed)return;disposed=true;timer.Dispose();SystemEvents.TimeChanged-=TimeChanged;SystemEvents.PowerModeChanged-=PowerChanged;SystemEvents.DisplaySettingsChanged-=DisplaysChanged;work.CompleteAdding();}
}
