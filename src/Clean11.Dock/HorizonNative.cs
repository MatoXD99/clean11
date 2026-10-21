using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

sealed class HorizonMonitorBackup { public string Monitor="",Original="",Copy=""; }
sealed class HorizonBackgroundBackup {
    public int Position;public uint Color,State,SlideshowOptions,SlideshowInterval;
    public List<HorizonMonitorBackup> Monitors=new List<HorizonMonitorBackup>();
    public List<string> Slideshow=new List<string>();
}

// Native Windows wallpaper APIs only. All COM calls stay on the service's STA.
sealed class HorizonNative : IDisposable {
    [StructLayout(LayoutKind.Sequential)]struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]struct PropertyKey {public Guid Format;public uint Id;}
    [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ShellItem {
        void BindToHandler(IntPtr context,ref Guid handler,ref Guid iid,out IntPtr result);
        void GetParent(out ShellItem parent);
        void GetDisplayName(uint kind,[MarshalAs(UnmanagedType.LPWStr)]out string name);
        void GetAttributes(uint mask,out uint attributes);
        void Compare(ShellItem other,uint hint,out int order);
    }
    [ComImport,Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ShellItemArray {
        void BindToHandler(IntPtr context,ref Guid handler,ref Guid iid,out IntPtr result);
        void GetPropertyStore(int flags,ref Guid iid,out IntPtr store);
        void GetPropertyDescriptionList(ref PropertyKey key,ref Guid iid,out IntPtr list);
        void GetAttributes(uint flags,uint mask,out uint attributes);
        void GetCount(out uint count);
        void GetItemAt(uint index,out ShellItem item);
        void EnumItems(out IntPtr items);
    }
    [ComImport,Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface DesktopWallpaper {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string monitor,[MarshalAs(UnmanagedType.LPWStr)]string wallpaper);
        void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string monitor,[MarshalAs(UnmanagedType.LPWStr)]out string wallpaper);
        void GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)]out string monitor);
        void GetMonitorDevicePathCount(out uint count);
        [PreserveSig]int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)]string monitor,out Rect rectangle);
        void SetBackgroundColor(uint color);
        void GetBackgroundColor(out uint color);
        void SetPosition(int position);
        void GetPosition(out int position);
        void SetSlideshow(ShellItemArray items);
        void GetSlideshow(out ShellItemArray items);
        void SetSlideshowOptions(uint options,uint interval);
        void GetSlideshowOptions(out uint options,out uint interval);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)]string monitor,int direction);
        void GetStatus(out uint state);
        void Enable([MarshalAs(UnmanagedType.Bool)]bool enabled);
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHParseDisplayName(string name,IntPtr context,out IntPtr id,uint attributes,out uint found);
    [DllImport("shell32.dll")]static extern int SHCreateShellItemArrayFromIDLists(uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)]IntPtr[] ids,out ShellItemArray items);
    readonly DesktopWallpaper wallpaper=(DesktopWallpaper)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")));
    List<string> Monitors(){
        uint count;wallpaper.GetMonitorDevicePathCount(out count);var result=new List<string>();
        for(uint i=0;i<count;i++){
            string monitor;wallpaper.GetMonitorDevicePathAt(i,out monitor);Rect bounds;
            int status=wallpaper.GetMonitorRECT(monitor,out bounds);
            // Windows retains disconnected display identities. Some shell
            // versions return E_FAIL rather than S_FALSE for their rectangles.
            if(status==1||status==unchecked((int)0x80004005)||status==unchecked((int)0x80070057))continue;
            Marshal.ThrowExceptionForHR(status);
            if(bounds.Right>bounds.Left&&bounds.Bottom>bounds.Top)result.Add(monitor);
        }
        if(result.Count==0)throw new InvalidOperationException("No connected wallpaper displays are available.");
        return result;
    }
    internal HorizonBackgroundBackup Capture(string folder){
        var result=new HorizonBackgroundBackup();wallpaper.GetPosition(out result.Position);wallpaper.GetBackgroundColor(out result.Color);wallpaper.GetStatus(out result.State);
        Directory.CreateDirectory(folder);
        foreach(string monitor in Monitors()){
            string path;wallpaper.GetWallpaper(monitor,out path);var item=new HorizonMonitorBackup{Monitor=monitor,Original=path??""};
            if(!String.IsNullOrEmpty(path)){if(!File.Exists(path))throw new IOException("The previous wallpaper cannot be backed up.");string extension=Path.GetExtension(path);item.Copy=Path.Combine(folder,Guid.NewGuid().ToString("N")+(String.IsNullOrEmpty(extension)?".jpg":extension));File.Copy(path,item.Copy);}
            result.Monitors.Add(item);
        }
        if((result.State&2)!=0){
            ShellItemArray items=null;try{wallpaper.GetSlideshow(out items);uint count;items.GetCount(out count);for(uint i=0;i<count;i++){ShellItem item=null;try{items.GetItemAt(i,out item);string name;item.GetDisplayName(0x80058000,out name);result.Slideshow.Add(name);}finally{Release(item);}}wallpaper.GetSlideshowOptions(out result.SlideshowOptions,out result.SlideshowInterval);}finally{Release(items);}
            if(result.Slideshow.Count==0)throw new IOException("The slideshow cannot be backed up.");
        }
        return result;
    }
    internal bool Matches(string path){if(String.IsNullOrEmpty(path))return false;foreach(string monitor in Monitors()){string current;wallpaper.GetWallpaper(monitor,out current);if(!String.Equals(current,path,StringComparison.OrdinalIgnoreCase))return false;}return true;}
    internal void Apply(string path){if(!File.Exists(path))throw new FileNotFoundException("Horizon artwork is missing.");wallpaper.Enable(true);wallpaper.SetPosition(4);wallpaper.SetWallpaper(null,path);}
    internal void Restore(HorizonBackgroundBackup previous){
        if(previous==null)throw new InvalidOperationException("There is no previous wallpaper backup.");
        var connected=Monitors();wallpaper.SetPosition(previous.Position);wallpaper.SetBackgroundColor(previous.Color);
        if(previous.Slideshow!=null&&previous.Slideshow.Count>0){
            var ids=new List<IntPtr>();ShellItemArray items=null;
            try{foreach(string path in previous.Slideshow){IntPtr id;uint found;Marshal.ThrowExceptionForHR(SHParseDisplayName(path,IntPtr.Zero,out id,0,out found));ids.Add(id);}Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromIDLists((uint)ids.Count,ids.ToArray(),out items));wallpaper.SetSlideshow(items);wallpaper.SetSlideshowOptions(previous.SlideshowOptions,previous.SlideshowInterval);}finally{Release(items);foreach(var id in ids)Marshal.FreeCoTaskMem(id);}
        }else{
            foreach(string monitor in connected){var item=previous.Monitors.Find(x=>x.Monitor==monitor)??(previous.Monitors.Count>0?previous.Monitors[0]:null);if(item==null)continue;string path=File.Exists(item.Copy)?item.Copy:item.Original;if(!String.IsNullOrEmpty(path)&&!File.Exists(path))throw new FileNotFoundException("A wallpaper backup is missing.");wallpaper.SetWallpaper(monitor,path??"");}
        }
        wallpaper.Enable((previous.State&1)!=0);
    }
    static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
    public void Dispose(){Release(wallpaper);}
}
