using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

// A non-layered backing window lets DWM sample the real desktop. The icon
// window stays layered so magnification can extend beyond the dock surface.
sealed class DockBackdrop : Window {
    [StructLayout(LayoutKind.Sequential)] struct Margins {public int Left,Right,Top,Bottom;}
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr window,ref Margins margins);
    IntPtr handle;
    internal bool Acrylic;
    internal DockBackdrop(){
        Title="Clean11 dock material";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        ShowInTaskbar=false;ShowActivated=false;Topmost=true;Background=Brushes.Transparent;
        IsHitTestVisible=false;
        SourceInitialized+=(s,e)=>{
            handle=new WindowInteropHelper(this).Handle;DockNative.NoActivate(this);
            DockNative.SetLong(handle,-20,new IntPtr(DockNative.GetLong(handle,-20).ToInt64()|0x20));
            HwndSource.FromHwnd(handle).CompositionTarget.BackgroundColor=Colors.Transparent;
            int dark=1,round=2,border=-2;DockNative.DwmSetWindowAttribute(handle,20,ref dark,4);DockNative.DwmSetWindowAttribute(handle,33,ref round,4);DockNative.DwmSetWindowAttribute(handle,34,ref border,4);
        };
        new WindowInteropHelper(this).EnsureHandle();
    }
    internal void Update(IntPtr icons,double left,double top,double width,double height,double dpi,bool visible,bool glass){
        Left=left;Top=top;Width=Math.Max(1,width);Height=Math.Max(1,height);
        int type=glass?3:1;
        Acrylic=glass&&DockNative.DwmSetWindowAttribute(handle,38,ref type,4)==0;
        if(!glass)DockNative.DwmSetWindowAttribute(handle,38,ref type,4);
        var margins=new Margins{Left=Acrylic?-1:0};DwmExtendFrameIntoClientArea(handle,ref margins);
        int strength=60;try{strength=OverviewPreferences.Load().GlassStrength;}catch{}
        var tint=ActivityTheme.Brush("Surface","#23252A").Color;tint.A=(byte)Math.Round(204*(1-Math.Max(0,Math.Min(100,strength))/100.0));
        Background=Acrylic?new SolidColorBrush(tint):ActivityTheme.Brush("Surface","#23252A");
        if(visible){if(!IsVisible)Show();DockNative.SetWindowPos(handle,icons,(int)Math.Round(left*dpi),(int)Math.Round(top*dpi),(int)Math.Round(width*dpi),(int)Math.Round(height*dpi),0x10);}
        else Hide();
    }
}
