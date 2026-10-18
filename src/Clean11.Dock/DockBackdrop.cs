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
    [StructLayout(LayoutKind.Sequential)] struct AccentPolicy {public int State,Flags,GradientColor,AnimationId;}
    [StructLayout(LayoutKind.Sequential)] struct CompositionData {public int Attribute;public IntPtr Data;public int Size;}
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool SetWindowCompositionAttribute(IntPtr window,ref CompositionData data);
    IntPtr handle;
    string lastMaterialStatus="";
    bool SetBlur(bool enabled){
        // ACCENT_ENABLE_BLURBEHIND avoids desktop Acrylic's focus-dependent
        // solid fallback. This compatibility API may be unavailable on a build.
        var policy=new AccentPolicy{State=enabled?3:0};
        IntPtr memory=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(AccentPolicy)));
        try{
            Marshal.StructureToPtr(policy,memory,false);
            var data=new CompositionData{Attribute=19,Data=memory,Size=Marshal.SizeOf(typeof(AccentPolicy))};
            return SetWindowCompositionAttribute(handle,ref data);
        }catch(EntryPointNotFoundException){return false;}
        finally{Marshal.FreeHGlobal(memory);}
    }
    bool Acrylic;
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
    internal void Update(IntPtr icons,double left,double top,double width,double height,double dpi,bool visible,bool glass,int strength){
        Left=left;Top=top;Width=Math.Max(1,width);Height=Math.Max(1,height);
        int type=1;DockNative.DwmSetWindowAttribute(handle,38,ref type,4);
        bool accepted=SetBlur(glass);Acrylic=glass&&accepted;
        var margins=Acrylic?new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1}:new Margins();
        int frameResult=DwmExtendFrameIntoClientArea(handle,ref margins);
        string status="Dock blur: requested="+glass+", accepted="+accepted+", frame="+frameResult;
        if(status!=lastMaterialStatus){DockLog.Write(status);lastMaterialStatus=status;}
        var tint=ActivityTheme.Brush("Surface","#23252A").Color;tint.A=(byte)Math.Round(204*(1-Math.Max(0,Math.Min(100,strength))/100.0));
        Background=Acrylic?new SolidColorBrush(tint):ActivityTheme.Brush("Surface","#23252A");
        if(visible){if(!IsVisible)Show();DockNative.SetWindowPos(handle,icons,(int)Math.Round(left*dpi),(int)Math.Round(top*dpi),(int)Math.Round(width*dpi),(int)Math.Round(height*dpi),0x10);}
        else Hide();
    }
}
