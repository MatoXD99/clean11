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
    internal bool IsMinimizedCard {get{return minimizedCard;}}
    bool minimizedCard;Action restoreCard,closeCard;TextBlock subtitle,minimizedLabel;Wpf.Ui.Controls.Button closeButton;
    OverviewPreferences cachedGlassPreferences;
    DockBackdrop glassBackdrop;readonly System.Windows.Threading.DispatcherTimer materialTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};IntPtr hwnd;double dpi=1;
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
    internal OverviewBadge(Overview owner,Action<Forms.MouseEventArgs> move,Action<Forms.MouseEventArgs> down){
        Clean11Fluent.Apply(this);WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;Title="Clean11 app label";FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");
        new WindowInteropHelper(this).Owner=owner.Handle;
        var content=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10,5,10,5)};icon=new Image{Width=20,Height=20,Margin=new Thickness(0,0,7,0)};name=new TextBlock{FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=ActivityTheme.Brush("Text","#F2F5FC"),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};content.Children.Add(icon);content.Children.Add(name);
        var header=new DockPanel();
        closeButton=Clean11Fluent.IconButton("Close window","Dismiss16",()=>{if(closeCard!=null)closeCard();});
        closeButton.Width=28;closeButton.Height=28;closeButton.Margin=new Thickness(0,2,4,2);closeButton.Focusable=false;
        Clean11Fluent.Quiet(closeButton);DockPanel.SetDock(closeButton,Dock.Right);header.Children.Add(closeButton);header.Children.Add(content);
        tint=new Border{CornerRadius=new CornerRadius(ActivityTheme.GlassRadius-1),Child=header};surface=new Border{CornerRadius=new CornerRadius(ActivityTheme.GlassRadius),BorderBrush=ActivityTheme.Brush("Border","#536176"),BorderThickness=new Thickness(1),Child=tint};Content=surface;
        SourceInitialized+=(s,e)=>{hwnd=new WindowInteropHelper(this).Handle;dpi=Native.GetDpiForWindow(hwnd)/96.0;SetWindowLongPtr(hwnd,-20,new IntPtr(Native.GetWindowLongPtr(hwnd,-20).ToInt64()|0x08000080));var source=HwndSource.FromHwnd(hwnd);source.AddHook((IntPtr window,int message,IntPtr w,IntPtr l,ref bool handled)=>{if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});};
        materialTimer.Tick+=(s,e)=>RefreshGlass();IsVisibleChanged+=(s,e)=>{if(IsVisible)materialTimer.Start();else{materialTimer.Stop();if(glassBackdrop!=null)glassBackdrop.Hide();}};Closed+=(s,e)=>{materialTimer.Stop();if(glassBackdrop!=null)glassBackdrop.Close();};
        MouseMove+=(s,e)=>{var p=PointToScreen(e.GetPosition(this));move(new Forms.MouseEventArgs(Forms.MouseButtons.None,0,(int)p.X-owner.Left,(int)p.Y-owner.Top,0));};
        MouseDown+=(s,e)=>{if(closeButton!=null&&closeButton.IsMouseOver)return;if(minimizedCard){if(e.ChangedButton==MouseButton.Left&&restoreCard!=null)restoreCard();e.Handled=true;return;}var p=PointToScreen(e.GetPosition(this));down(new Forms.MouseEventArgs(e.ChangedButton==MouseButton.Right?Forms.MouseButtons.Right:Forms.MouseButtons.Left,1,(int)p.X-owner.Left,(int)p.Y-owner.Top,0));e.Handled=true;};
    }
    internal void Place(Drawing.Rectangle preview,string text,Drawing.Icon image,bool glass,Action close){
        closeCard=close;
        name.Text=text;if(image!=null&&icon.Source==null){var bitmap=Imaging.CreateBitmapSourceFromHIcon(image.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(32,32));bitmap.Freeze();icon.Source=bitmap;}
        new WindowInteropHelper(this).EnsureHandle();Width=Math.Max(80,preview.Width/dpi);Height=34;
        name.MaxWidth=Math.Max(20,Width-78);MoveWithPreview(preview);
        name.Foreground=ActivityTheme.Brush("Text","#F2F5FC");surface.BorderBrush=new SolidColorBrush(Color.FromArgb(35,255,255,255));
        // A lightweight translucent caption can fade without a second native glass HWND.
        var tone=FrostedMaterial.Tone;tone.A=205;surface.Background=new SolidColorBrush(tone);tint.Background=Brushes.Transparent;
        if(!IsVisible&&Opacity>0)Show();
    }
    internal void MoveWithPreview(Drawing.Rectangle bounds){
        if(minimizedCard){Left=bounds.Left/dpi;Top=bounds.Top/dpi;return;}
        Left=bounds.Left/dpi;Top=bounds.Top/dpi-40;
    }
    internal void SetVisibilityProgress(double progress){
        Opacity=Math.Max(0,Math.Min(1,progress));IsHitTestVisible=progress>=.99;
        if(progress<=0){Hide();return;}if(!IsVisible)Show();
        if(glassBackdrop!=null){glassBackdrop.Opacity=Opacity;if(cachedGlassPreferences!=null)glassBackdrop.Update(hwnd,Left,Top,Width,Height,dpi,true,FrostedMaterial.Enabled(cachedGlassPreferences.Glass),cachedGlassPreferences.GlassStrength);}
    }

    internal void PlaceMinimized(Drawing.Rectangle bounds,string app,string title,Drawing.Icon image,Action restore,Action close){
        restoreCard=restore;closeCard=close;
        if(!minimizedCard){
            minimizedCard=true;Title="Clean11 minimized window";Cursor=Cursors.Hand;
            var grid=new Grid{Margin=new Thickness(16,12,12,12)};
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(52)});
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});
            // Detach the existing icon and name before placing them in the card.
            ((Panel)icon.Parent).Children.Remove(icon);((Panel)name.Parent).Children.Remove(name);
            icon.Width=40;icon.Height=40;icon.Margin=new Thickness(0,0,12,0);grid.Children.Add(icon);
            var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(labels,1);
            name.FontSize=14;name.FontWeight=FontWeights.SemiBold;name.MaxWidth=Double.PositiveInfinity;labels.Children.Add(name);
            subtitle=new TextBlock{FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,4,0,0)};labels.Children.Add(subtitle);
            minimizedLabel=new TextBlock{Text="Minimized",FontSize=11,Margin=new Thickness(0,7,0,0)};labels.Children.Add(minimizedLabel);grid.Children.Add(labels);
            closeButton=new Wpf.Ui.Controls.Button{Content="\u00D7",Width=24,Height=24,Padding=new Thickness(0),FontSize=18,VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Hidden,ToolTip="Close window",Focusable=false};
            Clean11Fluent.Quiet(closeButton);Grid.SetColumn(closeButton,2);grid.Children.Add(closeButton);
            closeButton.PreviewMouseDown+=(s,e)=>{e.Handled=true;};
            closeButton.PreviewMouseUp+=(s,e)=>{e.Handled=true;if(e.ChangedButton==MouseButton.Left&&closeCard!=null)closeCard();};
            closeButton.Click+=(s,e)=>{e.Handled=true;if(closeCard!=null)closeCard();};
            MouseEnter+=(s,e)=>{closeButton.Visibility=Visibility.Visible;tint.Background=ActivityTheme.Brush("Hover","#244A6070");};
            MouseLeave+=(s,e)=>{closeButton.Visibility=Visibility.Hidden;tint.Background=Brushes.Transparent;};
            tint.Child=grid;
        }
        new WindowInteropHelper(this).EnsureHandle();
        name.Text=app;subtitle.Text=title;ToolTip=title+" \u00B7 Click to restore";
        if(image!=null&&icon.Source==null){var bitmap=Imaging.CreateBitmapSourceFromHIcon(image.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(48,48));bitmap.Freeze();icon.Source=bitmap;}
        Width=bounds.Width/dpi;Height=bounds.Height/dpi;Left=bounds.Left/dpi;Top=bounds.Top/dpi;
        surface.Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));surface.BorderBrush=FrostedMaterial.Edge();
        subtitle.Visibility=Height<90?Visibility.Collapsed:Visibility.Visible;
        name.Foreground=ActivityTheme.Brush("Text","#F2F5FC");subtitle.Foreground=minimizedLabel.Foreground=ActivityTheme.Brush("Muted","#AABBCC");
        if(!IsVisible&&Opacity>0)Show();UpdateLayout();if(glassBackdrop==null){glassBackdrop=new DockBackdrop();glassBackdrop.SetOwner(new WindowInteropHelper(this).Owner);}RefreshGlass();
    }
    internal void SetHighlighted(bool highlighted){surface.BorderBrush=highlighted?new SolidColorBrush(Color.FromArgb(95,140,190,220)):new SolidColorBrush(Color.FromArgb(35,255,255,255));}
    void RefreshGlass(){if(!IsVisible||glassBackdrop==null)return;var prefs=OverviewPreferences.Load();cachedGlassPreferences=prefs;name.Foreground=ActivityTheme.Brush("Text","#F2F5FC");if(minimizedCard)subtitle.Foreground=minimizedLabel.Foreground=ActivityTheme.Brush("Muted","#AABBCC");glassBackdrop.Update(hwnd,Left,Top,Width,Height,dpi,true,FrostedMaterial.Enabled(prefs.Glass),prefs.GlassStrength);}
}

partial class Overview {
    readonly System.Collections.Generic.Dictionary<IntPtr,OverviewBadge> previewBadges=new System.Collections.Generic.Dictionary<IntPtr,OverviewBadge>();
    bool HasSpotlightBadge(WindowItem window){OverviewBadge badge;return previewBadges.TryGetValue(window.Hwnd,out badge)&&badge.IsVisible;}
    void PlaceSpotlightBadge(WindowItem window,double opacity=1){OverviewBadge badge;if(previewBadges.TryGetValue(window.Hwnd,out badge)&&badge.IsMinimizedCard!=window.Minimized){ReleaseSpotlightBadge(window.Hwnd);}
        if(!previewBadges.TryGetValue(window.Hwnd,out badge)){badge=new OverviewBadge(this,e=>OnMouseMove(e),e=>OnMouseDown(e));previewBadges[window.Hwnd]=badge;}badge.Opacity=opacity;badge.IsHitTestVisible=opacity>=.99;var bounds=window.Preview;bounds.Offset(Left,Top);if(window.Minimized)badge.PlaceMinimized(bounds,WindowAppName(window),window.Title,AppIcon(window.ProcessPath),()=>ChooseWindow(window),()=>RequestWindowClose(window));else badge.Place(bounds,WindowAppName(window),AppIcon(window.ProcessPath),state.Glass,()=>RequestWindowClose(window));}
    void UpdateMotionBadges(double progress){
        foreach(var window in windows){
            OverviewBadge badge;if(!previewBadges.TryGetValue(window.Hwnd,out badge))continue;
            var original=window.Original;original.Offset(-desktop.Left,-desktop.Top);
            var bounds=!window.Minimized&&original.Width>0&&original.Height>0&&original.IntersectsWith(ClientRectangle)?Mix(original,window.Preview,progress):window.Preview;
            bounds.Offset(Left,Top);badge.MoveWithPreview(bounds);
            badge.SetVisibilityProgress(Math.Max(0,Math.Min(1,(progress-.25)/.75)));
        }
    }
    void ReleaseSpotlightBadge(IntPtr window){OverviewBadge badge;if(previewBadges.TryGetValue(window,out badge)){badge.Close();previewBadges.Remove(window);}}
}
