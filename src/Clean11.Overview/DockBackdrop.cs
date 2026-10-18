using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

// Click-through companion for the dock's layered icon window.
sealed class DockBackdrop : Window {
    IntPtr handle;DockPopupMaterial material;
    internal static bool AdjustableBlurAvailable;
    internal DockBackdrop(){
        Title="Clean11 dock material";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        ShowInTaskbar=false;ShowActivated=false;Topmost=true;Background=Brushes.Transparent;IsHitTestVisible=false;
        SourceInitialized+=(s,e)=>{
            handle=new WindowInteropHelper(this).Handle;GlassNative.NoActivate(this);
            GlassNative.SetLong(handle,-20,new IntPtr(GlassNative.GetLong(handle,-20).ToInt64()|0x20));
            int dark=1,round=2,border=-2;GlassNative.DwmSetWindowAttribute(handle,20,ref dark,4);GlassNative.DwmSetWindowAttribute(handle,33,ref round,4);GlassNative.DwmSetWindowAttribute(handle,34,ref border,4);
            material=new DockPopupMaterial(handle,true);
        };
        Closed+=(s,e)=>{if(material!=null)material.Dispose();};
        new WindowInteropHelper(this).EnsureHandle();
    }
    internal void Update(IntPtr icons,double left,double top,double width,double height,double dpi,bool visible,bool glass,int strength){
        if(!visible){Hide();return;}
        if(!IsVisible){Width=Math.Max(1,width);Height=Math.Max(1,height);Left=left;Top=top;Show();}
        GlassNative.SetWindowPos(handle,icons,(int)Math.Round(left*dpi),(int)Math.Round(top*dpi),(int)Math.Round(width*dpi),(int)Math.Round(height*dpi),0x10);
        Background=material.Update(width,height,dpi,glass,strength);
    }
}

// Native glass below WPF content keeps live DWM thumbnails on a non-layered host.
sealed class DockPopupMaterial : IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct Margins {public int Left,Right,Top,Bottom;}
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr window,ref Margins margins);
    [StructLayout(LayoutKind.Sequential)] struct AccentPolicy {public int State,Flags,GradientColor,AnimationId;}
    [StructLayout(LayoutKind.Sequential)] struct CompositionData {public int Attribute;public IntPtr Data;public int Size;}
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool SetWindowCompositionAttribute(IntPtr window,ref CompositionData data);
    readonly IntPtr handle; readonly bool aboveContent;
    string lastMaterialStatus="";
    bool SetBlur(bool enabled,int mode=3){
        // ACCENT_ENABLE_BLURBEHIND avoids desktop Acrylic's focus-dependent
        // solid fallback. This compatibility API may be unavailable on a build.
        var policy=new AccentPolicy{State=enabled?mode:0};
        IntPtr memory=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(AccentPolicy)));
        try{
            Marshal.StructureToPtr(policy,memory,false);
            var data=new CompositionData{Attribute=19,Data=memory,Size=Marshal.SizeOf(typeof(AccentPolicy))};
            return SetWindowCompositionAttribute(handle,ref data);
        }catch(EntryPointNotFoundException){return false;}
        finally{Marshal.FreeHGlobal(memory);}
    }
    
    bool Acrylic;DockComposition composition;bool compositionFailed;

    internal static void PrepareContentHost(IntPtr window){
        HwndSource.FromHwnd(window).CompositionTarget.BackgroundColor=Colors.Transparent;
        var margins=new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1};
        DwmExtendFrameIntoClientArea(window,ref margins);
    }
    internal DockPopupMaterial(IntPtr window,bool above=false){handle=window;aboveContent=above;HwndSource.FromHwnd(handle).CompositionTarget.BackgroundColor=Colors.Transparent;}
    internal Brush Update(double width,double height,double dpi,bool glass,int strength){
        int type=1;GlassNative.DwmSetWindowAttribute(handle,38,ref type,4);
        int radius=8;try{radius=OverviewPreferences.Load().BlurRadius;}catch{}
        if(glass&&composition==null&&!compositionFailed){
            try{composition=new DockComposition(handle,aboveContent);DockBackdrop.AdjustableBlurAvailable=true;System.Diagnostics.Trace.WriteLine("Adjustable dock blur initialized.");}
            catch(Exception error){compositionFailed=true;System.Diagnostics.Trace.WriteLine("Adjustable blur unavailable: "+error.ToString());}
        }
        bool custom=glass&&composition!=null;
        bool accepted=SetBlur(glass,custom?5:3);Acrylic=glass&&accepted;
        var margins=Acrylic?new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1}:new Margins();
        int frameResult=DwmExtendFrameIntoClientArea(handle,ref margins);
        string status="Dock blur: requested="+glass+", accepted="+accepted+", frame="+frameResult;
        if(status!=lastMaterialStatus){System.Diagnostics.Trace.WriteLine(status);lastMaterialStatus=status;}
        var tint=FrostedMaterial.Tone;tint.A=FrostedMaterial.TintAlpha(strength);
        if(composition!=null){
            composition.SetVisible(custom);
            if(custom)composition.Update((float)(width*dpi),(float)(height*dpi),(float)(radius*dpi),tint.A);
        }
        return custom?Brushes.Transparent:Acrylic?new SolidColorBrush(tint):ActivityTheme.Brush("Surface","#23252A");
    }
    public void Dispose(){if(composition!=null)composition.Dispose();}
}

static class GlassNative {
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]internal static extern IntPtr GetLong(IntPtr h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]internal static extern IntPtr SetLong(IntPtr h,int index,IntPtr value);
    [DllImport("user32.dll")]internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("dwmapi.dll")]internal static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
    internal static void NoActivate(Window window){var h=new WindowInteropHelper(window).Handle;SetLong(h,-20,new IntPtr(GetLong(h,-20).ToInt64()|0x08000080));}
}
