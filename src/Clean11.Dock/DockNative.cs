using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

sealed class DockAppWindow {
    public IntPtr Handle;public string Title,Path,AppId;public uint Pid;
}
static class DockNative {
    internal delegate bool EnumProc(IntPtr hwnd,IntPtr data);
    [StructLayout(LayoutKind.Sequential)]internal struct Rect{public int Left,Top,Right,Bottom;public int Width{get{return Right-Left;}}public int Height{get{return Bottom-Top;}}}
    [StructLayout(LayoutKind.Sequential)]internal struct Point{public int X,Y;}
    [DllImport("user32.dll")]internal static extern bool EnumWindows(EnumProc callback,IntPtr data);
    [DllImport("user32.dll")]internal static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")]internal static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")]internal static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")]internal static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")]internal static extern IntPtr GetWindow(IntPtr h,uint command);
    [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]internal static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern int GetClassName(IntPtr h,StringBuilder text,int count);
    [DllImport("user32.dll")]internal static extern bool GetWindowRect(IntPtr h,out Rect r);
    [DllImport("user32.dll")]internal static extern bool GetCursorPos(out Point p);
    [DllImport("user32.dll")]internal static extern bool ShowWindowAsync(IntPtr h,int command);
    [DllImport("user32.dll")]internal static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")]internal static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]internal static extern IntPtr GetLong(IntPtr h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]internal static extern IntPtr SetLong(IntPtr h,int index,IntPtr value);
    [DllImport("user32.dll")]internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")]internal static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("dwmapi.dll")]internal static extern int DwmGetWindowAttribute(IntPtr h,int attribute,out int value,int size);
    [DllImport("dwmapi.dll")]internal static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
    [DllImport("kernel32.dll")]static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool QueryFullProcessImageName(IntPtr h,uint flags,StringBuilder path,ref int size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern int GetApplicationUserModelId(IntPtr h,ref uint length,StringBuilder id);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
    [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr h);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SHGetFileInfo(string file,uint attributes,ref FileInfo info,uint size,uint flags);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHGetKnownFolderPath(ref Guid id,uint flags,IntPtr token,out IntPtr path);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct FileInfo{public IntPtr Icon;public int Index;public uint Attributes;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string Type;}
    internal static string Title(IntPtr h){var s=new StringBuilder(512);GetWindowText(h,s,s.Capacity);return s.ToString();}
    internal static string Class(IntPtr h){var s=new StringBuilder(128);GetClassName(h,s,s.Capacity);return s.ToString();}
    internal static string ProcessPath(uint pid,out string appId){
        appId="";var h=OpenProcess(0x1000,false,pid);if(h==IntPtr.Zero)return "";
        try{var path=new StringBuilder(2048);int length=path.Capacity;QueryFullProcessImageName(h,0,path,ref length);uint size=512;var id=new StringBuilder((int)size);if(GetApplicationUserModelId(h,ref size,id)==0)appId=id.ToString();return path.ToString();}finally{CloseHandle(h);}
    }
    internal static List<DockAppWindow> Windows(){
        var result=new List<DockAppWindow>();var processes=new Dictionary<uint,string[]>();uint own=(uint)Process.GetCurrentProcess().Id;
        EnumWindows((h,p)=>{
            if(!IsWindowVisible(h)||GetWindow(h,4)!=IntPtr.Zero||(GetLong(h,-20).ToInt64()&0x80)!=0)return true;
            int cloaked;if(DwmGetWindowAttribute(h,14,out cloaked,4)==0&&cloaked!=0)return true;
            string title=Title(h),kind=Class(h);if(title.Length==0||kind=="Progman"||kind=="WorkerW"||kind.StartsWith("Shell_"))return true;
            uint pid;GetWindowThreadProcessId(h,out pid);if(pid==own)return true;
            string[] identity;if(!processes.TryGetValue(pid,out identity)){string id;string path=ProcessPath(pid,out id);identity=new[]{path,id};processes[pid]=identity;}
            string executable=Path.GetFileNameWithoutExtension(identity[0]);if(executable.Equals("seelen-ui",StringComparison.OrdinalIgnoreCase)||executable.Equals("ActivitiesOverview",StringComparison.OrdinalIgnoreCase))return true;
            result.Add(new DockAppWindow{Handle=h,Pid=pid,Title=title,Path=identity[0],AppId=identity[1]});return true;
        },IntPtr.Zero);return result;
    }
    internal static void Activate(DockAppWindow window,bool toggle){if(!IsWindow(window.Handle))return;if(toggle&&GetForegroundWindow()==window.Handle&&!IsIconic(window.Handle)){ShowWindowAsync(window.Handle,6);return;}if(IsIconic(window.Handle))ShowWindowAsync(window.Handle,9);SetForegroundWindow(window.Handle);}
    internal static bool Fullscreen(Forms.Screen screen,IntPtr own){var h=GetForegroundWindow();if(h==IntPtr.Zero||h==own||IsIconic(h))return false;string c=Class(h);if(c=="Progman"||c=="WorkerW"||c.StartsWith("Shell_"))return false;Rect r;if(!GetWindowRect(h,out r))return false;var b=screen.Bounds;return r.Left<=b.Left+2&&r.Top<=b.Top+2&&r.Right>=b.Right-2&&r.Bottom>=b.Bottom-2;}
    internal static BitmapSource Icon(string path){
        var info=new FileInfo();IntPtr h=IntPtr.Zero;try{
            SHGetFileInfo(path,0,ref info,(uint)Marshal.SizeOf(typeof(FileInfo)),0x100);h=info.Icon;
            if(h==IntPtr.Zero)return null;var image=Imaging.CreateBitmapSourceFromHIcon(h,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(64,64));image.Freeze();return image;
        }catch{return null;}finally{if(h!=IntPtr.Zero)DestroyIcon(h);}
    }
    internal static string Downloads(){var id=new Guid("374DE290-123F-4565-9164-39C4925E467B");IntPtr p;if(SHGetKnownFolderPath(ref id,0,IntPtr.Zero,out p)!=0)return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);try{return Marshal.PtrToStringUni(p);}finally{Marshal.FreeCoTaskMem(p);}}
    internal static void NoActivate(Window window){var h=new WindowInteropHelper(window).Handle;SetLong(h,-20,new IntPtr(GetLong(h,-20).ToInt64()|0x08000080));HwndSource.FromHwnd(h).AddHook((IntPtr handle,int message,IntPtr w,IntPtr l,ref bool handled)=>{if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});}
    [StructLayout(LayoutKind.Sequential)]internal struct ThumbnailProperties{public uint Flags;public Rect Destination,Source;public byte Opacity;[MarshalAs(UnmanagedType.Bool)]public bool Visible;[MarshalAs(UnmanagedType.Bool)]public bool ClientOnly;}
    [DllImport("dwmapi.dll")]internal static extern int DwmRegisterThumbnail(IntPtr destination,IntPtr source,out IntPtr thumb);
    [DllImport("dwmapi.dll")]internal static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")]internal static extern int DwmUpdateThumbnailProperties(IntPtr thumb,ref ThumbnailProperties properties);
    [StructLayout(LayoutKind.Sequential)]internal struct NativeSize{public int Width,Height;}
    [DllImport("dwmapi.dll")]internal static extern int DwmQueryThumbnailSourceSize(IntPtr thumb,out NativeSize size);
}
