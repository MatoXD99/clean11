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
    internal static bool Fullscreen(Forms.Screen screen,IntPtr own){var h=GetForegroundWindow();if(h==IntPtr.Zero||h==own||IsIconic(h)||OverviewSceneNative.OwnsWindow(h))return false;string c=Class(h);if(c=="Progman"||c=="WorkerW"||c.StartsWith("Shell_"))return false;Rect r;if(!GetWindowRect(h,out r))return false;var b=screen.Bounds;return r.Left<=b.Left+2&&r.Top<=b.Top+2&&r.Right>=b.Right-2&&r.Bottom>=b.Bottom-2;}
    [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ImageFactory{[PreserveSig]int GetImage(NativeSize size,uint flags,out IntPtr bitmap);}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,out ImageFactory factory);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr bitmap);
    internal static BitmapSource FilePreview(string path){return ShellImage(path,0)??Icon(path);}
    static BitmapSource ShellImage(string path,uint flags=5){ImageFactory factory=null;IntPtr bitmap=IntPtr.Zero;try{var iid=typeof(ImageFactory).GUID;if(SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out factory)!=0)return null;if(factory.GetImage(new NativeSize{Width=96,Height=96},flags,out bitmap)!=0)return null;var image=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image;}catch{return null;}finally{if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(factory!=null)Marshal.ReleaseComObject(factory);}}
    internal static BitmapSource Icon(string path){
        if(path.StartsWith("app:",StringComparison.Ordinal)){int split=path.IndexOf('|');string id=path.Substring(4,split-4),fallback=path.Substring(split+1);var packaged=ShellImage("::{4234d49b-0245-4df3-b780-3893943456e1}\\"+id);if(packaged!=null)return packaged;try{string directory=Path.GetDirectoryName(fallback);for(int level=0;level<3&&!String.IsNullOrEmpty(directory);level++,directory=Path.GetDirectoryName(directory)){string manifest=Path.Combine(directory,"AppxManifest.xml");if(!System.IO.File.Exists(manifest))continue;var xml=new System.Xml.XmlDocument();xml.XmlResolver=null;xml.Load(manifest);foreach(System.Xml.XmlElement app in xml.SelectNodes("//*[local-name()='Application']")){if(app.GetAttribute("Id")!=id.Substring(id.LastIndexOf('!')+1))continue;string executable=Path.Combine(directory,app.GetAttribute("Executable"));if(System.IO.File.Exists(executable)){fallback=executable;break;}}break;}}catch{}path=fallback;}
        var highResolution=ShellImage(path);if(highResolution!=null)return highResolution;var info=new FileInfo();IntPtr h=IntPtr.Zero;try{
            SHGetFileInfo(path,0,ref info,(uint)Marshal.SizeOf(typeof(FileInfo)),0x100);h=info.Icon;
            if(h==IntPtr.Zero)return null;var image=Imaging.CreateBitmapSourceFromHIcon(h,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(64,64));image.Freeze();return image;
        }catch{return null;}finally{if(h!=IntPtr.Zero)DestroyIcon(h);}
    }
    // Shell artwork contains inconsistent transparent padding. Normalize its visible
    // bounds once on the icon worker; every icon then fits the same optical box.
    internal static BitmapSource NormalizeIcon(BitmapSource source){
        if(source==null)return null;
        try{var converted=new FormatConvertedBitmap(source,System.Windows.Media.PixelFormats.Bgra32,null,0);int width=converted.PixelWidth,height=converted.PixelHeight,stride=width*4;var pixels=new byte[stride*height];converted.CopyPixels(pixels,stride,0);int left=width,top=height,right=-1,bottom=-1;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[y*stride+x*4+3]>24){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
            if(right<left||bottom<top)return source;
            var cropped=new CroppedBitmap(converted,new Int32Rect(left,top,right-left+1,bottom-top+1));cropped.Freeze();return cropped;
        }catch{return source;}
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
