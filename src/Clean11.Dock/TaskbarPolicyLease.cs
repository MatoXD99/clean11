using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Forms=System.Windows.Forms;

// Hiding the HWND alone leaves Explorer's auto-hide edge activation alive.
// Lease that policy through the supported appbar API, with recovery on disk.
static class TaskbarPolicyLease {
    [StructLayout(LayoutKind.Sequential)]struct Data {public uint Size;public IntPtr Window;public uint Callback,Edge;public DockNative.Rect Rect;public IntPtr Param;}
    [DllImport("shell32.dll")]static extern UIntPtr SHAppBarMessage(uint message,ref Data data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string kind,string title);
    sealed class SavedPolicy {public int Version=2;public uint OriginalState;public bool RestoreAutoHide=true;}
    static readonly string Journal=Path.Combine(DockState.Root,"taskbar-policy-restore.json");
    internal static bool IsTaskbar(string kind){return kind=="Shell_TrayWnd"||kind=="Shell_SecondaryTrayWnd";}
    internal static bool IsExplorer(IntPtr window){
        uint pid;DockNative.GetWindowThreadProcessId(window,out pid);
        string id;return String.Equals(Path.GetFileName(DockNative.ProcessPath(pid,out id)),"explorer.exe",StringComparison.OrdinalIgnoreCase);
    }
    static IntPtr Primary(){var window=FindWindow("Shell_TrayWnd",null);return window!=IntPtr.Zero&&IsExplorer(window)?window:IntPtr.Zero;}
    internal static bool Ready {get{return Primary()!=IntPtr.Zero;}}
    static uint State(IntPtr window){var data=new Data{Size=(uint)Marshal.SizeOf(typeof(Data)),Window=window};return (uint)SHAppBarMessage(4,ref data).ToUInt64();}
    static void Set(IntPtr window,uint state){var data=new Data{Size=(uint)Marshal.SizeOf(typeof(Data)),Window=window,Param=new IntPtr((long)state)};SHAppBarMessage(10,ref data);}
    internal static bool SuppressEdgeReveal(){
        var window=Primary();if(window==IntPtr.Zero)return false;
        uint state=State(window);if((state&1)==0)return true;
        if(!File.Exists(Journal)){
            Directory.CreateDirectory(DockState.Root);
            string temp=Journal+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(new SavedPolicy{OriginalState=state}));File.Move(temp,Journal);
        }else ReadJournal(); // Do not overwrite a restoration record we cannot read.
        Set(window,state&~1u);
        uint after=State(window);bool suppressed=(after&1)==0;
        DockLog.Write("Taskbar policy requested auto-hide off; confirmed="+suppressed+"; original state="+state+"; current state="+after);
        return suppressed;
    }
    static SavedPolicy ReadJournal(){var saved=new JavaScriptSerializer().Deserialize<SavedPolicy>(File.ReadAllText(Journal));if(saved==null||(saved.Version!=1&&saved.Version!=2))throw new InvalidDataException("Unknown taskbar policy journal.");return saved;}
    internal static bool Restore(){
        if(!File.Exists(Journal))return true;
        var saved=ReadJournal();var window=Primary();if(window==IntPtr.Zero)return false; // Keep the journal until Explorer is ready.
        uint expected=State(window);
        if(saved.Version==2){expected=saved.OriginalState;Set(window,expected);}
        else if(saved.RestoreAutoHide){expected|=1u;Set(window,expected);}
        uint after=State(window);if(after!=expected){DockLog.Write("Taskbar policy restore deferred; expected="+expected+"; current="+after);return false;}
        File.Delete(Journal);DockLog.Write("Original Windows taskbar policy restored; state="+after);return true;
    }
}

// A real hidden top-level HWND receives TaskbarCreated broadcasts even while
// every visible Clean11 surface is hidden. No injection into Explorer.
sealed class ShellNotifications : Forms.NativeWindow,IDisposable {
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
    readonly uint taskbarCreated=RegisterWindowMessage("TaskbarCreated");readonly Action reconcile;
    internal ShellNotifications(Action action){reconcile=action;CreateHandle(new Forms.CreateParams{Caption="Clean11 shell notifications",Style=unchecked((int)0x80000000),ExStyle=0x08000080});}
    protected override void WndProc(ref Forms.Message message){
        if(message.Msg==taskbarCreated)PostMessage(Handle,0x8031,IntPtr.Zero,IntPtr.Zero);
        if(message.Msg==0x8031)reconcile();
        base.WndProc(ref message);
    }
    public void Dispose(){DestroyHandle();}
}
