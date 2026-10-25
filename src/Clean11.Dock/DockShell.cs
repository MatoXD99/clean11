using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Media;
using Forms=System.Windows.Forms;

sealed class ShellEntry{public long Handle;public uint Pid;public string Kind,Title;public bool WasVisible=true;}
sealed class ShellLease {
    delegate void ShellEvent(IntPtr hook,uint kind,IntPtr window,int objectId,int childId,uint thread,uint time);
    [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,ShellEvent callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
    static readonly string Journal=Path.Combine(DockState.Root,"shell-restore.json");
    readonly List<ShellEntry> entries=new List<ShellEntry>();
    readonly List<IntPtr> hooks=new List<IntPtr>();readonly ShellEvent callback;
    bool enabled,handling,hookAttempted,updating,journalLoaded,reportedReady;DateTime lastRevealLog=DateTime.MinValue;
    ShellNotifications notifications;
    Forms.Screen currentScreen;
    internal bool ExistingDock,PendingShell;internal DateTime NativeTrayUntil=DateTime.MinValue;
    internal ShellLease(){callback=OnShellEvent;}
    void Watch(){
        if(hookAttempted)return;hookAttempted=true;
        notifications=new ShellNotifications(()=>{if(enabled&&currentScreen!=null){DockLog.Write("Explorer taskbar recreated; reconciling takeover.");Update(currentScreen,true);}});
        // Receive shell events on our UI message loop without injecting into
        // Explorer. Keep the delegate alive until all hooks have been removed.
        foreach(uint eventId in new uint[]{0x8002,0x800B,3}){
            var hook=SetWinEventHook(eventId,eventId,IntPtr.Zero,callback,0,0,2);
            if(hook!=IntPtr.Zero)hooks.Add(hook);
        }
        DockLog.Write("Taskbar visibility guard installed; hooks="+hooks.Count);
    }
    void OnShellEvent(IntPtr hook,uint kind,IntPtr window,int objectId,int childId,uint thread,uint time){
        if(!enabled||handling||updating||window==IntPtr.Zero||objectId!=0||childId!=0||DateTime.UtcNow<NativeTrayUntil)return;
        var entry=entries.FirstOrDefault(x=>x.Handle==window.ToInt64());
        if(entry!=null&&!Matches(entry))entry=null;
        if(entry==null&&kind!=0x8002)return;
        handling=true;
        try{
            if(entry!=null){if(Matches(entry)&&DockNative.IsWindowVisible(window)){DockNative.ShowWindowAsync(window,0);if((DateTime.UtcNow-lastRevealLog).TotalSeconds>5){lastRevealLog=DateTime.UtcNow;DockLog.Write("Suppressed Explorer taskbar show request; class="+entry.Kind);}}}
            else {
                string name=DockNative.Class(window);
                if(currentScreen!=null&&(name=="Shell_TrayWnd"||name=="Shell_SecondaryTrayWnd"))Update(currentScreen,true);
            }
        }catch(Exception error){DockLog.Write("Taskbar visibility guard: "+error.GetType().Name);}
        finally{handling=false;}
    }
    internal void Enforce(){
        if(!enabled)return;
        foreach(var entry in entries){
            if(DateTime.UtcNow<NativeTrayUntil&&entry.Kind.StartsWith("Shell_"))continue;
            var window=new IntPtr(entry.Handle);
            if(Matches(entry)&&DockNative.IsWindowVisible(window))DockNative.ShowWindowAsync(window,0);
        }
    }
    internal void Update(Forms.Screen screen,bool enabled){
        if(updating)return;
        if(!enabled){Release();return;}
        if(currentScreen!=null&&currentScreen.DeviceName!=screen.DeviceName)Release();
        currentScreen=screen;this.enabled=true;Watch();updating=true;
        try{
        var next=journalLoaded?entries.Where(Matches).ToList():ReadJournal();journalLoaded=true;
        // Don't discard a prior recovery record or stall UI while Explorer is
        // still starting. TaskbarCreated and the next pulse retry acquisition.
        if(!TaskbarPolicyLease.Ready){PendingShell=true;entries.Clear();entries.AddRange(next);return;}
        PendingShell=false;
        bool changed=next.Count!=entries.Count;ExistingDock=false;
        DockNative.EnumWindows((h,p)=>{
            string kind=DockNative.Class(h),title=DockNative.Title(h);bool taskbar=TaskbarPolicyLease.IsTaskbar(kind);
            if(!taskbar&&title!="Dock/Taskbar")return true;if(!OnScreen(h,kind,screen))return true;
            uint pid;DockNative.GetWindowThreadProcessId(h,out pid);
            string id;string process=Path.GetFileNameWithoutExtension(DockNative.ProcessPath(pid,out id));
            if(taskbar&&!process.Equals("explorer",StringComparison.OrdinalIgnoreCase))return true;
            if(!taskbar&&!process.Equals("seelen-ui",StringComparison.OrdinalIgnoreCase))return true;
            if(!taskbar)ExistingDock=true;
            // Auto-hidden/boot-time taskbars must be tracked before they reveal.
            if(taskbar||DockNative.IsWindowVisible(h)){
                if(!next.Any(x=>x.Handle==h.ToInt64()&&x.Pid==pid&&x.Kind==kind)){next.Add(new ShellEntry{Handle=h.ToInt64(),Pid=pid,Kind=kind,Title=title});changed=true;}
            }
            return true;
        },IntPtr.Zero);
        // Persist the recovery journal before changing any window visibility.
        if(changed)SaveJournal(next);
        // Turning off Explorer's edge-reveal controller prevents mouse-edge
        // activation, instead of repeatedly racing it with ShowWindow calls.
        bool policyReady=TaskbarPolicyLease.SuppressEdgeReveal();
        entries.Clear();entries.AddRange(next);
        Enforce();
        if(!policyReady)reportedReady=false;
        else if(!reportedReady||changed){reportedReady=true;DockLog.Write("Taskbar takeover ready; tracked taskbars="+entries.Count+"; monitor="+screen.DeviceName);}
        }catch(Exception error){DockLog.Write("Taskbar takeover reconciliation failed: "+error.GetType().Name);}
        finally{updating=false;}
    }
    static bool OnScreen(IntPtr window,string kind,Forms.Screen screen){
        // The primary auto-hide bar can sit outside its display's bounds; its
        // nearest monitor then isn't necessarily the primary display at boot.
        var target=kind=="Shell_TrayWnd"?Forms.Screen.PrimaryScreen:Forms.Screen.FromHandle(window);
        return target!=null&&target.DeviceName==screen.DeviceName;
    }
    static List<ShellEntry> ReadJournal(){
        if(!File.Exists(Journal))return new List<ShellEntry>();
        try{return new JavaScriptSerializer().Deserialize<List<ShellEntry>>(File.ReadAllText(Journal))??new List<ShellEntry>();}
        catch(Exception error){DockLog.Write("Taskbar visibility journal could not be adopted: "+error.GetType().Name);return new List<ShellEntry>();}
    }
    static void SaveJournal(List<ShellEntry> value){Directory.CreateDirectory(DockState.Root);string temp=Journal+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(value));if(File.Exists(Journal))File.Replace(temp,Journal,null);else File.Move(temp,Journal);}
    static bool Matches(ShellEntry e){var h=new IntPtr(e.Handle);uint pid;DockNative.GetWindowThreadProcessId(h,out pid);return DockNative.IsWindow(h)&&pid==e.Pid&&DockNative.Class(h)==e.Kind&&(TaskbarPolicyLease.IsTaskbar(e.Kind)||DockNative.Title(h)==e.Title);}
    internal void Release(){
        enabled=false;foreach(var hook in hooks)UnhookWinEvent(hook);hooks.Clear();hookAttempted=false;
        if(notifications!=null){notifications.Dispose();notifications=null;}NativeTrayUntil=DateTime.MinValue;
        bool shellReady=TaskbarPolicyLease.Ready;
        if(shellReady)foreach(var entry in entries)if(Matches(entry))DockNative.ShowWindowAsync(new IntPtr(entry.Handle),entry.WasVisible?4:0);
        entries.Clear();ExistingDock=false;PendingShell=false;journalLoaded=false;reportedReady=false;
        bool policyRestored=false;try{policyRestored=TaskbarPolicyLease.Restore();}catch(Exception error){DockLog.Write("Taskbar policy restoration deferred: "+error.GetType().Name);}
        if(shellReady&&policyRestored)try{if(File.Exists(Journal))File.Delete(Journal);}catch{}
    }
    internal static bool Recover(){
        if(!TaskbarPolicyLease.Ready&&(File.Exists(Journal)||File.Exists(Path.Combine(DockState.Root,"taskbar-policy-restore.json"))))return false;
        try{
            if(File.Exists(Journal)){var list=new JavaScriptSerializer().Deserialize<List<ShellEntry>>(File.ReadAllText(Journal));foreach(var entry in list??new List<ShellEntry>())if(Matches(entry))DockNative.ShowWindowAsync(new IntPtr(entry.Handle),entry.WasVisible?4:0);File.Delete(Journal);}
            return TaskbarPolicyLease.Restore();
        }catch(Exception error){DockLog.Write("Taskbar recovery deferred: "+error.GetType().Name);return false;}
    }
    internal static string Seelen(){try{foreach(var p in Process.GetProcessesByName("seelen-ui"))using(p){string candidate=Path.Combine(Path.GetDirectoryName(p.MainModule.FileName),"slu.exe");if(File.Exists(candidate))return candidate;}}catch{}return null;}
    internal bool ShowTray(){
        var cli=Seelen();if(cli!=null){try{string widgets=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"com.seelen.seelen-ui","widgets");bool local=Directory.Exists(widgets)&&Directory.EnumerateFiles(widgets,"metadata.yml",SearchOption.AllDirectories).Any(p=>File.ReadAllText(p).Contains("@local/system-tray"));if(local){Process.Start(new ProcessStartInfo(cli,"widget trigger @local/system-tray"){UseShellExecute=false,CreateNoWindow=true});return true;}}catch{}}
        NativeTrayUntil=DateTime.UtcNow.AddSeconds(15);
        DockNative.EnumWindows((h,p)=>{string kind=DockNative.Class(h);if(TaskbarPolicyLease.IsTaskbar(kind)&&TaskbarPolicyLease.IsExplorer(h)&&(currentScreen==null||OnScreen(h,kind,currentScreen))){uint pid;DockNative.GetWindowThreadProcessId(h,out pid);if(!entries.Any(x=>x.Handle==h.ToInt64()&&x.Pid==pid))entries.Add(new ShellEntry{Handle=h.ToInt64(),Pid=pid,Kind=kind,Title=DockNative.Title(h),WasVisible=true});SaveJournal(entries);DockNative.ShowWindowAsync(h,4);}return true;},IntPtr.Zero);
        return false;
    }
}

// An independent invisible appbar owns reserved work area; the floating dock
// remains a small, nonactivating window with transparent margins.
sealed class DockAppBar : Forms.NativeWindow,IDisposable {
    [StructLayout(LayoutKind.Sequential)]struct Data{public uint Size;public IntPtr Window;public uint Callback,Edge;public DockNative.Rect Rect;public IntPtr Param;}
    [DllImport("shell32.dll")]static extern UIntPtr SHAppBarMessage(uint message,ref Data data);
    [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr h,uint key,byte alpha,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string name);
    readonly uint taskbarCreated=RegisterWindowMessage("TaskbarCreated");
    bool registered,busy,wanted;Forms.Screen screen;int height;
    internal DockAppBar(){CreateHandle(new Forms.CreateParams{Caption="Clean11 dock work area",Style=unchecked((int)0x80000000),ExStyle=0x080800A0});SetLayeredWindowAttributes(Handle,0,0,2);}
    internal void Configure(Forms.Screen monitor,int pixels,bool reserve){bool changed=screen!=null&&screen.DeviceName!=monitor.DeviceName;screen=monitor;height=pixels;wanted=reserve;if(!reserve||changed)Remove();if(!reserve)return;if(!registered){var d=New();d.Callback=0x8001;registered=SHAppBarMessage(0,ref d)!=UIntPtr.Zero;}Place();}
    Data New(){return new Data{Size=(uint)Marshal.SizeOf(typeof(Data)),Window=Handle};}
    void Place(){if(!registered||busy||screen==null)return;busy=true;try{var b=screen.Bounds;var d=New();d.Edge=3;d.Rect=new DockNative.Rect{Left=b.Left,Top=b.Top,Right=b.Right,Bottom=b.Bottom};SHAppBarMessage(2,ref d);d.Rect.Top=d.Rect.Bottom-height;SHAppBarMessage(3,ref d);DockNative.SetWindowPos(Handle,IntPtr.Zero,d.Rect.Left,d.Rect.Top,d.Rect.Width,d.Rect.Height,0x14);DockNative.ShowWindowAsync(Handle,4);}finally{busy=false;}}
    protected override void WndProc(ref Forms.Message m){if(m.Msg==0x8001&&m.WParam.ToInt32()==1)Place();if(m.Msg==taskbarCreated){registered=false;if(wanted&&screen!=null)Configure(screen,height,true);}if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
    void Remove(){if(!registered)return;var d=New();SHAppBarMessage(1,ref d);registered=false;DockNative.ShowWindowAsync(Handle,0);}
    public void Dispose(){wanted=false;Remove();DestroyHandle();}
}

static class DockProgram {
    [DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(uint processId);
    static void GrantSettingsForeground(){
        foreach(var process in Process.GetProcessesByName("Clean11.Dock"))using(process){
            try{if(process.Id!=Process.GetCurrentProcess().Id&&String.Equals(process.MainModule.FileName,Executable,StringComparison.OrdinalIgnoreCase))AllowSetForegroundWindow((uint)process.Id);}catch{}
        }
    }
    internal static string Executable{get{return System.Reflection.Assembly.GetExecutingAssembly().Location;}}
    internal static string StopName{get{return "Local\\Clean11DockStop";}}
    static void StartupMark(bool startup,Stopwatch clock,string phase){if(startup)DockLog.Write("startup +"+clock.ElapsedMilliseconds+"ms: "+phase);}
    [STAThread]static void Main(string[] args){
        if(args.Length>1&&args[0]=="--watch"){int pid;if(Int32.TryParse(args[1],out pid)){try{using(var parent=Process.GetProcessById(pid))parent.WaitForExit();}catch{}using(var guard=new Mutex(false,"Local\\Clean11.Dock")){bool acquired=false;try{acquired=guard.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}if(acquired){ShellLease.Recover();guard.ReleaseMutex();}}}return;}
        if(args.Contains("--shutdown")){try{using(var signal=EventWaitHandle.OpenExisting(StopName))signal.Set();}catch{}return;}
        bool startup=args.Contains("--startup");var startupClock=Stopwatch.StartNew();DockLog.Write("Dock launch requested; startup="+startup+"; version="+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);StartupMark(startup,startupClock,"process initialized");
        AppDomain.CurrentDomain.UnhandledException+=(s,e)=>DockLog.Write("Unhandled dock exception: "+e.ExceptionObject);
        bool created;using(var mutex=new Mutex(true,"Local\\Clean11.Dock",out created)){
            if(!created){if(args.Contains("--widgets")){GrantSettingsForeground();for(int attempt=0;attempt<30;attempt++){try{using(var signal=EventWaitHandle.OpenExisting("Local\\Clean11WidgetsOpen"))signal.Set();break;}catch(WaitHandleCannotBeOpenedException){Thread.Sleep(100);}}}if(args.Contains("--settings")){GrantSettingsForeground();for(int attempt=0;attempt<30;attempt++){try{using(var signal=EventWaitHandle.OpenExisting("Local\\Clean11SettingsOpen"))signal.Set();break;}catch(WaitHandleCannotBeOpenedException){Thread.Sleep(100);}catch(Exception error){DockLog.Write("Settings signal failed: "+error.GetType().Name);break;}}}return;}
            using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\Clean11DockReady"))
            using(var stop=new EventWaitHandle(false,EventResetMode.AutoReset,StopName)){
                ready.Reset();
                Clean11Fluent.Ensure();var application=System.Windows.Application.Current;application.ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown;
                var state=DockState.Load();StartupMark(startup,startupClock,"settings loaded");try{DockSettings.SetStartup(state.Startup);}catch(Exception error){DockLog.Write("Startup registration failed: "+error.GetType().Name);}
                // Preserve the takeover journal across sign-in. When takeover is
                // disabled, restore it once Explorer is ready without blocking UI.
                if(!state.TakeOver)ThreadPool.QueueUserWorkItem(_=>RecoverWhenShellReady());
                if(args.Contains("--startup")&&!args.Contains("--overview-started"))try{if(System.IO.File.Exists(Clean11Paths.Overview))Process.Start(new ProcessStartInfo(Clean11Paths.Overview,"--background"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)});}catch(Exception error){DockLog.Write("Overview background launch failed: "+error.GetType().Name);}
                Process.Start(new ProcessStartInfo(Executable,"--watch "+Process.GetCurrentProcess().Id){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)});
                TrayAppearance.Sync();var dock=new DockWindow(state,startup);application.MainWindow=dock;StartupMark(startup,startupClock,"dock and top bar created");
                var horizon=HorizonService.Instance;
                application.Exit+=(s,e)=>horizon.Dispose();
                try{DesktopWidgetBridge.Start(false,startup);StartupMark(startup,startupClock,"widget host requested");}catch(Exception error){DockLog.Write("Widget host launch failed: "+error.GetType().Name);}
                var widgetsSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Clean11WidgetsOpen");var widgetsWait=ThreadPool.RegisterWaitForSingleObject(widgetsSignal,(s,t)=>{try{application.Dispatcher.BeginInvoke((Action)(()=>dock.ShowSettingsPage("Widgets")));}catch{}},null,-1,false);application.Exit+=(s,e)=>{widgetsWait.Unregister(null);widgetsSignal.Dispose();};
                var settingsSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Clean11SettingsOpen");
                var settingsWait=ThreadPool.RegisterWaitForSingleObject(settingsSignal,(s,t)=>{try{application.Dispatcher.BeginInvoke((Action)dock.ShowSettings);}catch{}},null,-1,false);
                application.Exit+=(s,e)=>{settingsWait.Unregister(null);settingsSignal.Dispose();};
                if(args.Contains("--widgets"))application.Dispatcher.BeginInvoke((Action)(()=>dock.ShowSettingsPage("Widgets")));else if(args.Contains("--settings"))application.Dispatcher.BeginInvoke((Action)dock.ShowSettings);
                var wait=ThreadPool.RegisterWaitForSingleObject(stop,(s,t)=>{try{application.Dispatcher.BeginInvoke((Action)(()=>dock.RequestShutdown()));}catch{}},null,-1,false);
                int firstFrameLogged=0;EventHandler firstFrame=null;firstFrame=(s,e)=>{if(Interlocked.Exchange(ref firstFrameLogged,1)!=0)return;CompositionTarget.Rendering-=firstFrame;ready.Set();StartupMark(startup,startupClock,"first WPF composition frame; startup ready");};CompositionTarget.Rendering+=firstFrame;
                application.Exit+=(s,e)=>{wait.Unregister(null);if(firstFrame!=null)CompositionTarget.Rendering-=firstFrame;DesktopWidgetBridge.Stop();dock.DisposeDock();};
                try{
                    DockLog.Write("Dock started; WPF UI 4.3.0; private settings loaded.");StartupMark(startup,startupClock,"entering WPF dispatcher");application.Run();
                }catch(Exception error){DockLog.Write("Dock stopped: "+error.GetType().Name);dock.DisposeDock();throw;}
            }
        }
    }
    static void RecoverWhenShellReady(){
        var deadline=DateTime.UtcNow.AddMinutes(2);
        while(DateTime.UtcNow<deadline){
            if(TaskbarPolicyLease.Ready){if(ShellLease.Recover())return;}
            Thread.Sleep(500);
        }
        DockLog.Write("Deferred taskbar recovery remains pending; Explorer was not ready within two minutes.");
    }
}
