using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using Forms=System.Windows.Forms;

sealed class ShellEntry{public long Handle;public uint Pid;public string Kind,Title;public bool WasVisible=true;}
sealed class ShellLease {
    static readonly string Journal=Path.Combine(DockState.Root,"shell-restore.json");
    readonly List<ShellEntry> entries=new List<ShellEntry>();
    Forms.Screen currentScreen;
    internal bool ExistingDock;internal DateTime NativeTrayUntil=DateTime.MinValue;
    internal void Update(Forms.Screen screen,bool enabled){
        currentScreen=screen;if(!enabled){Release();return;}bool changed=false;ExistingDock=false;
        DockNative.EnumWindows((h,p)=>{
            string kind=DockNative.Class(h),title=DockNative.Title(h);bool taskbar=kind=="Shell_TrayWnd"||kind=="Shell_SecondaryTrayWnd";
            if(!taskbar&&title!="Dock/Taskbar")return true;if(Forms.Screen.FromHandle(h).DeviceName!=screen.DeviceName)return true;
            uint pid;DockNative.GetWindowThreadProcessId(h,out pid);
            string id;string process=Path.GetFileNameWithoutExtension(DockNative.ProcessPath(pid,out id));
            if(taskbar&&!process.Equals("explorer",StringComparison.OrdinalIgnoreCase))return true;
            if(!taskbar&&!process.Equals("seelen-ui",StringComparison.OrdinalIgnoreCase))return true;
            if(!taskbar)ExistingDock=true;
            if(taskbar&&DateTime.UtcNow<NativeTrayUntil)return true;
            if(DockNative.IsWindowVisible(h)){
                if(!entries.Any(x=>x.Handle==h.ToInt64())){entries.Add(new ShellEntry{Handle=h.ToInt64(),Pid=pid,Kind=kind,Title=title});changed=true;}
            }
            return true;
        },IntPtr.Zero);
        // Persist the recovery journal before changing any window visibility.
        if(changed){Directory.CreateDirectory(DockState.Root);File.WriteAllText(Journal,new JavaScriptSerializer().Serialize(entries));}
        foreach(var entry in entries){if(DateTime.UtcNow<NativeTrayUntil&&entry.Kind.StartsWith("Shell_"))continue;var h=new IntPtr(entry.Handle);if(Matches(entry))DockNative.ShowWindowAsync(h,0);}
    }
    static bool Matches(ShellEntry e){var h=new IntPtr(e.Handle);uint pid;DockNative.GetWindowThreadProcessId(h,out pid);return DockNative.IsWindow(h)&&pid==e.Pid&&DockNative.Class(h)==e.Kind&&DockNative.Title(h)==e.Title;}
    internal void Release(){foreach(var entry in entries)if(Matches(entry))DockNative.ShowWindowAsync(new IntPtr(entry.Handle),entry.WasVisible?4:0);entries.Clear();ExistingDock=false;try{if(File.Exists(Journal))File.Delete(Journal);}catch{}}
    internal static void Recover(){try{if(!File.Exists(Journal))return;var list=new JavaScriptSerializer().Deserialize<List<ShellEntry>>(File.ReadAllText(Journal));foreach(var entry in list)if(Matches(entry))DockNative.ShowWindowAsync(new IntPtr(entry.Handle),entry.WasVisible?4:0);File.Delete(Journal);}catch{}}
    internal static string Seelen(){try{foreach(var p in Process.GetProcessesByName("seelen-ui"))using(p){string candidate=Path.Combine(Path.GetDirectoryName(p.MainModule.FileName),"slu.exe");if(File.Exists(candidate))return candidate;}}catch{}return null;}
    internal bool ShowTray(){
        var cli=Seelen();if(cli!=null){try{string widgets=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"com.seelen.seelen-ui","widgets");bool local=Directory.Exists(widgets)&&Directory.EnumerateFiles(widgets,"metadata.yml",SearchOption.AllDirectories).Any(p=>File.ReadAllText(p).Contains("@local/system-tray"));if(local){Process.Start(new ProcessStartInfo(cli,"widget trigger @local/system-tray"){UseShellExecute=false,CreateNoWindow=true});return true;}}catch{}}
        NativeTrayUntil=DateTime.UtcNow.AddSeconds(15);
        DockNative.EnumWindows((h,p)=>{string kind=DockNative.Class(h);if((kind=="Shell_TrayWnd"||kind=="Shell_SecondaryTrayWnd")&&(currentScreen==null||Forms.Screen.FromHandle(h).DeviceName==currentScreen.DeviceName)){uint pid;DockNative.GetWindowThreadProcessId(h,out pid);if(!entries.Any(x=>x.Handle==h.ToInt64()))entries.Add(new ShellEntry{Handle=h.ToInt64(),Pid=pid,Kind=kind,Title=DockNative.Title(h),WasVisible=DockNative.IsWindowVisible(h)});Directory.CreateDirectory(DockState.Root);File.WriteAllText(Journal,new JavaScriptSerializer().Serialize(entries));DockNative.ShowWindowAsync(h,4);}return true;},IntPtr.Zero);
        return false;
    }
}

// An independent invisible appbar owns reserved work area; the floating dock
// remains a small, nonactivating window with transparent margins.
sealed class DockAppBar : Forms.NativeWindow,IDisposable {
    [StructLayout(LayoutKind.Sequential)]struct Data{public uint Size;public IntPtr Window;public uint Callback,Edge;public DockNative.Rect Rect;public IntPtr Param;}
    [DllImport("shell32.dll")]static extern UIntPtr SHAppBarMessage(uint message,ref Data data);
    [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr h,uint key,byte alpha,uint flags);
    bool registered,busy;Forms.Screen screen;int height;
    internal DockAppBar(){CreateHandle(new Forms.CreateParams{Caption="Clean11 dock work area",Style=unchecked((int)0x80000000),ExStyle=0x080800A0});SetLayeredWindowAttributes(Handle,0,0,2);}
    internal void Configure(Forms.Screen monitor,int pixels,bool reserve){screen=monitor;height=pixels;if(!reserve){Remove();return;}if(!registered){var d=New();d.Callback=0x8001;registered=SHAppBarMessage(0,ref d)!=UIntPtr.Zero;}Place();}
    Data New(){return new Data{Size=(uint)Marshal.SizeOf(typeof(Data)),Window=Handle};}
    void Place(){if(!registered||busy||screen==null)return;busy=true;try{var b=screen.Bounds;var d=New();d.Edge=3;d.Rect=new DockNative.Rect{Left=b.Left,Top=b.Top,Right=b.Right,Bottom=b.Bottom};SHAppBarMessage(2,ref d);d.Rect.Top=d.Rect.Bottom-height;SHAppBarMessage(3,ref d);DockNative.SetWindowPos(Handle,IntPtr.Zero,d.Rect.Left,d.Rect.Top,d.Rect.Width,d.Rect.Height,0x14);DockNative.ShowWindowAsync(Handle,4);}finally{busy=false;}}
    protected override void WndProc(ref Forms.Message m){if(m.Msg==0x8001&&m.WParam.ToInt32()==1)Place();if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
    void Remove(){if(!registered)return;var d=New();SHAppBarMessage(1,ref d);registered=false;DockNative.ShowWindowAsync(Handle,0);}
    public void Dispose(){Remove();DestroyHandle();}
}

static class DockProgram {
    internal static string Executable{get{return System.Reflection.Assembly.GetExecutingAssembly().Location;}}
    internal static string StopName{get{return "Local\\Clean11DockStop";}}
    [STAThread]static void Main(string[] args){
        if(args.Length>1&&args[0]=="--watch"){int pid;if(Int32.TryParse(args[1],out pid)){try{using(var parent=Process.GetProcessById(pid))parent.WaitForExit();}catch{}using(var guard=new Mutex(false,"Local\\Clean11.Dock")){bool acquired=false;try{acquired=guard.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}if(acquired){ShellLease.Recover();guard.ReleaseMutex();}}}return;}
        if(args.Contains("--shutdown")){try{using(var signal=EventWaitHandle.OpenExisting(StopName))signal.Set();}catch{}return;}
        bool created;using(var mutex=new Mutex(true,"Local\\Clean11.Dock",out created)){
            if(!created){if(args.Contains("--settings")){for(int attempt=0;attempt<30;attempt++){try{using(var signal=EventWaitHandle.OpenExisting("Local\\Clean11SettingsOpen"))signal.Set();break;}catch(WaitHandleCannotBeOpenedException){Thread.Sleep(100);}catch(Exception error){DockLog.Write("Settings signal failed: "+error.GetType().Name);break;}}}return;}ShellLease.Recover();
            using(var stop=new EventWaitHandle(false,EventResetMode.ManualReset,StopName)){
                Clean11Fluent.Ensure();var application=System.Windows.Application.Current;application.ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown;
                var state=DockState.Load();DockSettings.SetStartup(state.Startup);
                Process.Start(new ProcessStartInfo(Executable,"--watch "+Process.GetCurrentProcess().Id){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)});
                var dock=new DockWindow(state);application.MainWindow=dock;
                var settingsSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Clean11SettingsOpen");
                var settingsWait=ThreadPool.RegisterWaitForSingleObject(settingsSignal,(s,t)=>{try{application.Dispatcher.BeginInvoke((Action)dock.ShowSettings);}catch{}},null,-1,false);
                application.Exit+=(s,e)=>{settingsWait.Unregister(null);settingsSignal.Dispose();};
                if(args.Contains("--settings"))application.Dispatcher.BeginInvoke((Action)dock.ShowSettings);
                var wait=ThreadPool.RegisterWaitForSingleObject(stop,(s,t)=>{try{application.Dispatcher.BeginInvoke((Action)(()=>application.Shutdown()));}catch{}},null,-1,true);
                application.Exit+=(s,e)=>{wait.Unregister(null);dock.DisposeDock();};
                try{
                    DockLog.Write("Dock started; WPF UI 4.3.0; private settings loaded.");application.Run();
                }catch(Exception error){DockLog.Write("Dock stopped: "+error.GetType().Name);dock.DisposeDock();throw;}
            }
        }
    }
}
