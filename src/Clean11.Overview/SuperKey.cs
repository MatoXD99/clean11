using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Only a bare, physical Super tap belongs to Activities. Modifier chords pass
// through unchanged. The reducer is tested without synthesizing user input.
sealed class SuperTapState {
    readonly HashSet<int> held=new HashSet<int>();bool chord;
    internal bool IsDown {get{return held.Count!=0;}}
    internal bool Accept(int key,bool down,bool injected,bool otherModifier) {
        if(injected)return false;
        bool win=key==0x5B||key==0x5C;
        if(!win){if(down&&IsDown)chord=true;return false;}
        if(down){if(held.Add(key)){if(held.Count==1)chord=otherModifier;else chord=true;}return false;}
        bool known=held.Remove(key);
        return known&&held.Count==0&&!chord&&!otherModifier;
    }
    internal void Reset(){held.Clear();chord=false;}
}

sealed class SuperKeyDispatcher : IDisposable {
    internal const int ToggleMessage=0x8000+72;
    delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    [StructLayout(LayoutKind.Sequential)] struct KeyEvent {public uint Key,Scan,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)] struct KeyboardInput {public ushort Key,Scan;public uint Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Explicit,Size=32)] struct InputData {[FieldOffset(0)]public KeyboardInput Keyboard;}
    [StructLayout(LayoutKind.Sequential)] struct Input {public uint Type;public InputData Data;}
    [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int kind,HookProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint count,Input[] input,int size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]static extern bool PostThreadMessage(uint thread,uint message,IntPtr wparam,IntPtr lparam);
    readonly SuperTapState tap=new SuperTapState();readonly IntPtr target;readonly Thread worker;
    HookProc callback;IntPtr hook;uint threadId;volatile bool disposed;
    internal static int InputSize {get{return Marshal.SizeOf(typeof(Input));}}
    internal SuperKeyDispatcher(IntPtr targetWindow) {
        target=targetWindow;worker=new Thread(Run);worker.Name="Activities Super dispatcher";worker.IsBackground=true;worker.SetApartmentState(ApartmentState.STA);worker.Start();
    }
    static bool ModifierHeld(){return Native.GetAsyncKeyState(0x10)<0||Native.GetAsyncKeyState(0x11)<0||Native.GetAsyncKeyState(0x12)<0;}
    void Install(){
        if(disposed||Native.GetAsyncKeyState(0x5B)<0||Native.GetAsyncKeyState(0x5C)<0)return;
        // A missed release (or a hook Windows silently removed) must not leave
        // the reducer permanently preventing its own recovery.
        if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);
        tap.Reset();hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
        if(hook==IntPtr.Zero)Native.Log("Super dispatcher install failed: "+Marshal.GetLastWin32Error());
    }
    IntPtr HandleKey(int code,IntPtr message,IntPtr data) {
        if(code>=0&&!disposed) {
            var key=(KeyEvent)Marshal.PtrToStructure(data,typeof(KeyEvent));int msg=message.ToInt32();
            bool down=msg==0x100||msg==0x104,up=msg==0x101||msg==0x105;
            if((down||up)&&tap.Accept((int)key.Key,down,(key.Flags&0x10)!=0,ModifierHeld())) {
                // Mark the still-held Win modifier as used so Windows won't
                // open Start on release. VK_E8 is an unassigned menu-mask key;
                // no application keystroke, focus request or enumeration here.
                var mask=new[]{new Input{Type=1,Data=new InputData{Keyboard=new KeyboardInput{Key=0xE8,Extra=new UIntPtr(0x41435456)}}},new Input{Type=1,Data=new InputData{Keyboard=new KeyboardInput{Key=0xE8,Flags=2,Extra=new UIntPtr(0x41435456)}}}};
                bool masked=SendInput(2,mask,InputSize)==2;
                // UIPI can reject the menu mask over an elevated application.
                // Dispatching our own overview does not depend on injection.
                Native.PostMessage(target,ToggleMessage,IntPtr.Zero,IntPtr.Zero);
                if(!masked){Native.Log("Super menu mask rejected; bare release consumed, overview toggle dispatched");return new IntPtr(1);}
            }
        }
        return CallNextHookEx(hook,code,message,data);
    }
    void Run(){
        threadId=GetCurrentThreadId();callback=HandleKey;
        using(var dispatcher=new Control())using(var watchdog=new System.Windows.Forms.Timer()) {
            IntPtr handle=dispatcher.Handle;Install();Native.Log("Native Super dispatcher ready="+(hook!=IntPtr.Zero));
            // Windows can silently discard a timed-out low-level hook. Renew
            // on this dedicated message pump, never during a Win-key chord.
            watchdog.Interval=10000;watchdog.Tick+=(s,e)=>Install();watchdog.Start();
            if(!disposed)Application.Run();watchdog.Stop();
            if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}
        }
    }
    public void Dispose(){disposed=true;if(threadId!=0)PostThreadMessage(threadId,0x12,IntPtr.Zero,IntPtr.Zero);if(Thread.CurrentThread!=worker)worker.Join(1200);}
}

static class DesktopActions {
    internal static string SeelenCliPath(){
        try{foreach(var process in Process.GetProcessesByName("seelen-ui"))using(process){string candidate=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(process.MainModule.FileName),"slu.exe");if(System.IO.File.Exists(candidate))return candidate;}}catch{}
        return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\WindowsApps\slu.exe");
    }
    internal static void PowerMenu(){
        try{string cli=SeelenCliPath();if(System.IO.File.Exists(cli)){Process.Start(new ProcessStartInfo(cli,"widget trigger @seelen/power-menu"){UseShellExecute=false,CreateNoWindow=true});return;}}catch(Exception error){Native.Log("Power menu: "+error.GetType().Name);}
        Process.Start(new ProcessStartInfo("explorer.exe","ms-settings:powersleep"){UseShellExecute=true});
    }
}
