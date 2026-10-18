using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Wallpaper discovery/DWM presentation adapted from miguelo96/WindowScatter.
// Copyright (c) 2025 miguelo96, MIT. See WINDOWSCATTER-LICENSE.txt.
// This window MUST stay non-layered: native thumbnails do not compose correctly
// on the color-keyed interface window. The interface is owned by this host so
// it stays above both the wallpaper and the moving application previews.
sealed class PreviewHost : Form {
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string name,string title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string name,string title);
    IntPtr wallpaper,wallpaperSource;
    Rectangle wallpaperDesktop,wallpaperSourceRect;
    int wallpaperRegistrations;
    Image fallback;
    internal bool LiveWallpaper {get{return wallpaper!=IntPtr.Zero;}}
    internal IntPtr WallpaperThumbnail {get{return wallpaper;}}
    internal int WallpaperRegistrations {get{return wallpaperRegistrations;}}
    internal static bool CanReuseWallpaper(IntPtr thumbnail,IntPtr cachedSource,IntPtr currentSource,Rectangle cachedDesktop,Rectangle currentDesktop,bool sourceAlive) {
        return thumbnail!=IntPtr.Zero&&sourceAlive&&currentSource!=IntPtr.Zero&&cachedSource==currentSource&&cachedDesktop==currentDesktop;
    }
    internal PreviewHost(Action<MouseEventArgs> move,Action<MouseEventArgs> down,Action<MouseEventArgs> wheel) {
        Text="Activities wallpaper and previews";FormBorderStyle=FormBorderStyle.None;
        StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;
        ShowInTaskbar=false;TopMost=true;BackColor=Color.Black;
        MouseMove+=(s,e)=>move(e);MouseDown+=(s,e)=>down(e);MouseWheel+=(s,e)=>wheel(e);
        SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
    }
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80;return p;}}
    protected override void WndProc(ref Message message) {
        if(message.Msg==0x0021){message.Result=new IntPtr(3);return;} // MA_NOACTIVATE
        base.WndProc(ref message);
    }
    internal void Prepare(Rectangle desktop) {
        Bounds=desktop;
        IntPtr source=FindWallpaperWindow();
        if(CanReuseWallpaper(wallpaper,wallpaperSource,source,wallpaperDesktop,desktop,Native.IsWindow(source))) {
            var cached=new Native.Props{Flags=1|2|4|8|16,Destination=new Native.Rect(ClientRectangle),Source=new Native.Rect(wallpaperSourceRect),Opacity=255,Visible=true,ClientOnly=false};
            if(Native.DwmUpdateThumbnailProperties(wallpaper,ref cached)==0) {
                Native.Log("Reused live desktop wallpaper thumbnail; source="+source+", size="+ClientSize.Width+"x"+ClientSize.Height);
                return;
            }
            Native.Log("Cached wallpaper thumbnail became invalid; registering again");
        }
        ReleaseWallpaper();
        if(source!=IntPtr.Zero) {
            int result=Native.DwmRegisterThumbnail(Handle,source,out wallpaper);
            Native.Size size;
            if(result==0&&wallpaper!=IntPtr.Zero&&Native.DwmQueryThumbnailSourceSize(wallpaper,out size)==0&&size.Width>0&&size.Height>0) {
                Native.Rect sourceWindow;Native.GetWindowRect(source,out sourceWindow);
                int x=desktop.Left-sourceWindow.Left,y=desktop.Top-sourceWindow.Top;
                if(x<0||y<0||x+desktop.Width>size.Width||y+desktop.Height>size.Height){x=0;y=0;}
                var sourceRect=new Rectangle(x,y,Math.Min(desktop.Width,size.Width-x),Math.Min(desktop.Height,size.Height-y));
                var properties=new Native.Props{Flags=1|2|4|8|16,Destination=new Native.Rect(ClientRectangle),Source=new Native.Rect(sourceRect),Opacity=255,Visible=true,ClientOnly=false};
                if(Native.DwmUpdateThumbnailProperties(wallpaper,ref properties)==0) {
                    wallpaperSource=source;wallpaperDesktop=desktop;wallpaperSourceRect=sourceRect;wallpaperRegistrations++;
                    Native.Log("Live desktop wallpaper registered on separate non-layered preview host; source="+source+", size="+size.Width+"x"+size.Height);
                    return;
                }
            }
            ReleaseWallpaper();Native.Log("Live wallpaper registration unavailable; loading original wallpaper image");
        }
        LoadFallback();Invalidate();
    }
    static IntPtr FindWallpaperWindow() {
        IntPtr worker=IntPtr.Zero;
        Native.EnumWindows((window,data)=> {
            if(FindWindowEx(window,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero)
                worker=FindWindowEx(IntPtr.Zero,window,"WorkerW",null);
            return true;
        },IntPtr.Zero);
        return worker!=IntPtr.Zero?worker:FindWindow("Progman",null);
    }
    void LoadFallback() {
        if(fallback!=null){fallback.Dispose();fallback=null;}
        foreach(string path in new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","Windows","Themes","TranscodedWallpaper"),GetWallpaperPath()}) {
            if(String.IsNullOrWhiteSpace(path)||!File.Exists(path))continue;
            try{using(var image=Image.FromFile(path))fallback=new Bitmap(image);return;}catch{}
        }
    }
    static string GetWallpaperPath() {
        try{using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))return key.GetValue("WallPaper") as string;}catch{return null;}
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);if(fallback==null||wallpaper!=IntPtr.Zero)return;
        double factor=Math.Max(ClientSize.Width/(double)fallback.Width,ClientSize.Height/(double)fallback.Height);
        int width=(int)Math.Ceiling(fallback.Width*factor),height=(int)Math.Ceiling(fallback.Height*factor);
        e.Graphics.DrawImage(fallback,new Rectangle((ClientSize.Width-width)/2,(ClientSize.Height-height)/2,width,height));
    }
    internal void ReleaseWallpaper(){if(wallpaper!=IntPtr.Zero){Native.DwmUnregisterThumbnail(wallpaper);wallpaper=IntPtr.Zero;}wallpaperSource=IntPtr.Zero;wallpaperDesktop=Rectangle.Empty;wallpaperSourceRect=Rectangle.Empty;}
    internal void HideRendering(){Hide();ReleaseWallpaper();}
    protected override void Dispose(bool disposing){if(disposing){ReleaseWallpaper();if(fallback!=null){fallback.Dispose();fallback=null;}}base.Dispose(disposing);}
}
