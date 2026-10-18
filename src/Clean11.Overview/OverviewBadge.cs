using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

// Small labels keep the preview grid native; hovering never redraws a screen-
// sized WPF bitmap. These windows cannot activate or take search focus.
sealed class OverviewBadge : Window {
    readonly TextBlock name;readonly Image icon;readonly Border surface,tint;
    ImageBrush material;Drawing.Rectangle screen;IntPtr hwnd;double dpi=1;
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
    internal OverviewBadge(Overview owner,Action<Forms.MouseEventArgs> move,Action<Forms.MouseEventArgs> down){
        Clean11Fluent.Apply(this);WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;Title="Clean11 app label";FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");
        new WindowInteropHelper(this).Owner=owner.Handle;
        var content=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(8,3,10,3)};icon=new Image{Width=22,Height=22,Margin=new Thickness(0,0,7,0)};name=new TextBlock{FontSize=13,FontWeight=FontWeights.Normal,Foreground=ActivityTheme.Brush("Text","#F2F5FC"),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};content.Children.Add(icon);content.Children.Add(name);
        tint=new Border{CornerRadius=new CornerRadius(ActivityTheme.RowRadius-1),Child=content};surface=new Border{CornerRadius=new CornerRadius(ActivityTheme.RowRadius),BorderBrush=ActivityTheme.Brush("Border","#536176"),BorderThickness=new Thickness(1),Child=tint};Content=surface;
        SourceInitialized+=(s,e)=>{hwnd=new WindowInteropHelper(this).Handle;dpi=Native.GetDpiForWindow(hwnd)/96.0;SetWindowLongPtr(hwnd,-20,new IntPtr(Native.GetWindowLongPtr(hwnd,-20).ToInt64()|0x08000080));var source=HwndSource.FromHwnd(hwnd);source.AddHook((IntPtr window,int message,IntPtr w,IntPtr l,ref bool handled)=>{if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});};
        MouseMove+=(s,e)=>{var p=PointToScreen(e.GetPosition(this));move(new Forms.MouseEventArgs(Forms.MouseButtons.None,0,(int)p.X-owner.Left,(int)p.Y-owner.Top,0));};
        MouseDown+=(s,e)=>{var p=PointToScreen(e.GetPosition(this));down(new Forms.MouseEventArgs(e.ChangedButton==MouseButton.Right?Forms.MouseButtons.Right:Forms.MouseButtons.Left,1,(int)p.X-owner.Left,(int)p.Y-owner.Top,0));e.Handled=true;};
    }
    internal void Place(Drawing.Rectangle preview,string text,Drawing.Icon image,bool glass){
        Opacity=1;
        name.Text=text;if(image!=null&&icon.Source==null){var bitmap=Imaging.CreateBitmapSourceFromHIcon(image.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(32,32));bitmap.Freeze();icon.Source=bitmap;}
        new WindowInteropHelper(this).EnsureHandle();name.MaxWidth=Math.Max(20,preview.Width/dpi-50);name.Measure(new Size(Double.PositiveInfinity,28));double width=Math.Min(preview.Width/dpi,Math.Ceiling(name.DesiredSize.Width)+50);Width=width;Height=30;
        Left=(preview.Left+(preview.Width-width*dpi)/2)/dpi;Top=(preview.Top-36*dpi)/dpi;
        var currentScreen=Forms.Screen.FromHandle(hwnd).Bounds;if(screen!=currentScreen||material==null){screen=currentScreen;material=FrostedMaterial.Enabled(glass)?FrostedMaterial.Create(screen):null;}
        bool enabled=FrostedMaterial.Enabled(glass)&&material!=null;surface.Background=enabled?(Brush)material:ActivityTheme.Brush("Surface","#202632");tint.Background=enabled?ActivityTheme.Brush("GlassTint","#8E181E29"):Brushes.Transparent;
        FrostedMaterial.Position(material,new Drawing.Rectangle((int)Math.Round(Left*dpi),(int)Math.Round(Top*dpi),(int)Math.Round(Width*dpi),(int)Math.Round(Height*dpi)),screen);if(!IsVisible)Show();
    }
}

partial class Overview {
    readonly System.Collections.Generic.Dictionary<IntPtr,OverviewBadge> previewBadges=new System.Collections.Generic.Dictionary<IntPtr,OverviewBadge>();
    bool HasSpotlightBadge(WindowItem window){OverviewBadge badge;return previewBadges.TryGetValue(window.Hwnd,out badge)&&badge.IsVisible;}
    void PlaceSpotlightBadge(WindowItem window){OverviewBadge badge;if(!previewBadges.TryGetValue(window.Hwnd,out badge)){badge=new OverviewBadge(this,e=>OnMouseMove(e),e=>OnMouseDown(e));previewBadges[window.Hwnd]=badge;}var bounds=window.Preview;bounds.Offset(Left,Top);badge.Place(bounds,WindowAppName(window),AppIcon(window.ProcessPath),state.Glass);}
    void ReleaseSpotlightBadge(IntPtr window){OverviewBadge badge;if(previewBadges.TryGetValue(window,out badge)){badge.Close();previewBadges.Remove(window);}}
}
