using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Forms=System.Windows.Forms;

// Separate transparent reservation window keeps maximized apps below the bar.
// Explorer releases the reservation automatically if the owning process dies.
sealed class TopBarReservation : Forms.NativeWindow,IDisposable {
    [StructLayout(LayoutKind.Sequential)]struct Data {public uint Size;public IntPtr Window;public uint Callback,Edge;public DockNative.Rect Rect;public IntPtr Param;}
    [DllImport("shell32.dll")]static extern UIntPtr SHAppBarMessage(uint message,ref Data data);
    [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr handle,uint key,byte alpha,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string name);
    readonly uint taskbarCreated=RegisterWindowMessage("TaskbarCreated");
    internal bool Registered;internal DockNative.Rect Bounds;internal event Action Changed;
    bool busy,wanted;Forms.Screen screen;int height;
    internal TopBarReservation(){CreateHandle(new Forms.CreateParams{Caption="Clean11 top bar work area",Style=unchecked((int)0x80000000),ExStyle=0x080800A0});SetLayeredWindowAttributes(Handle,0,0,2);}
    Data New(){return new Data{Size=(uint)Marshal.SizeOf(typeof(Data)),Window=Handle,Callback=0x8002,Edge=1};}
    internal void Configure(Forms.Screen monitor,int pixels,bool reserve){bool changed=screen==null||screen.DeviceName!=monitor.DeviceName;screen=monitor;height=pixels;wanted=reserve;if(!reserve||changed)Remove();if(!reserve)return;if(!Registered){var d=New();Registered=SHAppBarMessage(0,ref d)!=UIntPtr.Zero;}Place();}
    void Place(){if(!Registered||busy||screen==null)return;busy=true;try{var b=screen.Bounds;var d=New();d.Rect=new DockNative.Rect{Left=b.Left,Top=b.Top,Right=b.Right,Bottom=b.Bottom};SHAppBarMessage(2,ref d);d.Rect.Bottom=d.Rect.Top+height;SHAppBarMessage(3,ref d);Bounds=d.Rect;DockNative.SetWindowPos(Handle,IntPtr.Zero,d.Rect.Left,d.Rect.Top,d.Rect.Width,d.Rect.Height,0x14);DockNative.ShowWindowAsync(Handle,4);if(Changed!=null)Changed();}finally{busy=false;}}
    protected override void WndProc(ref Forms.Message m){if(m.Msg==0x8002&&m.WParam.ToInt32()==1)Place();if(m.Msg==taskbarCreated){Registered=false;if(wanted&&screen!=null)Configure(screen,height,true);}if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
    void Remove(){if(!Registered)return;var d=New();SHAppBarMessage(1,ref d);Registered=false;DockNative.ShowWindowAsync(Handle,0);}
    public void Dispose(){wanted=false;Remove();DestroyHandle();}
}

static class TopBarSystem {
    [DllImport("user32.dll")]internal static extern bool LockWorkStation();
    internal static void Power(string arguments){Process.Start(new ProcessStartInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"shutdown.exe"),arguments){UseShellExecute=false,CreateNoWindow=true});}
}

// Supported Core Audio endpoint APIs. Resolve the current endpoint per operation
// so device changes do not leave the slider bound to an unplugged device.
static partial class TopBarAudio {
    [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]class Enumerator {}
    [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IEnumerator {
        [PreserveSig]int EnumAudioEndpoints(int flow,uint mask,out IntPtr devices);
        [PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out IDevice device);
    }
    [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IDevice {
        [PreserveSig]int Activate(ref Guid iid,uint context,IntPtr activation,[MarshalAs(UnmanagedType.IUnknown)]out object value);
        [PreserveSig]int OpenPropertyStore(uint access,out IProperties properties);
        [PreserveSig]int GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);
        [PreserveSig]int GetState(out uint state);
    }
    [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IVolume {
        [PreserveSig]int RegisterControlChangeNotify(IntPtr callback);
        [PreserveSig]int UnregisterControlChangeNotify(IntPtr callback);
        [PreserveSig]int GetChannelCount(out uint channels);
        [PreserveSig]int SetMasterVolumeLevel(float level,ref Guid context);
        [PreserveSig]int SetMasterVolumeLevelScalar(float level,ref Guid context);
        [PreserveSig]int GetMasterVolumeLevel(out float level);
        [PreserveSig]int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig]int SetChannelVolumeLevel(uint channel,float level,ref Guid context);
        [PreserveSig]int SetChannelVolumeLevelScalar(uint channel,float level,ref Guid context);
        [PreserveSig]int GetChannelVolumeLevel(uint channel,out float level);
        [PreserveSig]int GetChannelVolumeLevelScalar(uint channel,out float level);
        [PreserveSig]int SetMute([MarshalAs(UnmanagedType.Bool)]bool mute,ref Guid context);
        [PreserveSig]int GetMute([MarshalAs(UnmanagedType.Bool)]out bool mute);
    }
    static bool Use(Func<IVolume,bool> action){object enumerator=null,value=null;IDevice device=null;try{enumerator=new Enumerator();if(((IEnumerator)enumerator).GetDefaultAudioEndpoint(0,1,out device)<0)return false;Guid id=typeof(IVolume).GUID;if(device.Activate(ref id,23,IntPtr.Zero,out value)<0)return false;return action((IVolume)value);}catch(COMException){return false;}finally{if(value!=null)Marshal.ReleaseComObject(value);if(device!=null)Marshal.ReleaseComObject(device);if(enumerator!=null)Marshal.ReleaseComObject(enumerator);}}
    internal static bool Read(out float level,out bool muted){float v=0;bool m=false;bool ok=Use(x=>x.GetMasterVolumeLevelScalar(out v)>=0&&x.GetMute(out m)>=0);level=v;muted=m;return ok;}
    internal static bool Set(float level){return Use(x=>{Guid context=Guid.Empty;return x.SetMasterVolumeLevelScalar(Math.Max(0,Math.Min(1,level)),ref context)>=0;});}
    internal static bool Mute(bool muted){return Use(x=>{Guid context=Guid.Empty;return x.SetMute(muted,ref context)>=0;});}
}
