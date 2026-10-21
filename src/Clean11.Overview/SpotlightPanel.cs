using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
using Ui=Wpf.Ui.Controls;
using Rect=System.Drawing.Rectangle;

sealed class SpotlightRow {
    public ResultItem Item {get;set;}
    public string Title {get;set;}
    public string Subtitle {get;set;}
    public ImageSource Icon {get;set;}
}

// The whole frosted surface uses per-pixel alpha. DWM's rectangular backdrop is
// deliberately disabled: a rounded WPF Border cannot mask that separate layer.
sealed class SpotlightPanel : Window {
    readonly Overview overview;
    readonly Border surface;
    readonly Grid root,searchPage,header;
    readonly RectangleGeometry contentClip=new RectangleGeometry();
    readonly TextBox input;
    readonly TextBlock placeholder;
    readonly StackPanel settingsHeading;
    readonly ListBox list;
    readonly StackPanel detail;
    readonly TextBlock status;
    readonly ScrollViewer settingsPage;
    readonly Border settingsFooter;
    readonly Border overviewPage;
    readonly StackPanel favorites;
    string favoriteSignature="";
    ImageBrush material;
    Rect materialScreen;
    readonly ObservableCollection<SpotlightRow> rows=new ObservableCollection<SpotlightRow>();
    readonly Dictionary<string,ImageSource> icons=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
    bool animating,enteringOverview;
    readonly System.Windows.Threading.DispatcherTimer dismissTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};
    readonly Stopwatch clock=new Stopwatch();
    Rect from,to;Action animationDone;int animationDuration;
    int fadeFrom,fadeTo;bool fadingPreviews;
    double bodyFrom,bodyTo;
    TimeSpan lastRender=TimeSpan.MinValue;
    internal bool IsAnimating {get{return animating;}}
    internal bool Expanded {get;private set;}
    bool synchronizing,settings,dead;
    int previewGeneration;
    TextBlock catalogLabel;
    ContextMenu activeMenu;
    string pressedRowKey;
    string savedQuery;
    int savedSelectionStart,savedSelectionLength;
    IntPtr hwnd;
    double dpi=1;
    SolidColorBrush TextBrush=new SolidColorBrush(FrostedMaterial.Ink(false)),Muted=new SolidColorBrush(FrostedMaterial.Ink(true)),Accent=ActivityTheme.Brush("Accent","#6EB3FF");
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int length);
    [StructLayout(LayoutKind.Sequential)]struct Margins {public int Left,Right,Top,Bottom;}
    [DllImport("dwmapi.dll")]static extern int DwmExtendFrameIntoClientArea(IntPtr handle,ref Margins margins);
    [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    static SolidColorBrush Brush(byte r,byte g,byte b){var brush=new SolidColorBrush(Color.FromRgb(r,g,b));brush.Freeze();return brush;}
    static SolidColorBrush Alpha(byte a,byte r,byte g,byte b){var brush=new SolidColorBrush(Color.FromArgb(a,r,g,b));brush.Freeze();return brush;}
    internal SpotlightPanel(Overview owner) {
        Clean11Fluent.Ensure();overview=owner;Title="Clean11 Search";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        ShowInTaskbar=Environment.CommandLine.Contains("--inspect-ui");Topmost=true;AllowsTransparency=true;Background=Brushes.Transparent;FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");Foreground=TextBrush;
        if(!Environment.CommandLine.Contains("--inspect-ui"))new WindowInteropHelper(this).Owner=owner.Handle;
        System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(this);
        Clean11Fluent.Apply(this);
        surface=new Border{CornerRadius=new CornerRadius(ActivityTheme.PanelRadius),BorderThickness=new Thickness(1),BorderBrush=(Brush)FindResource("Border"),Padding=new Thickness(0),SnapsToDevicePixels=true};Content=surface;
        root=new Grid{ClipToBounds=true};surface.Child=root;root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(64)});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        contentClip.RadiusX=contentClip.RadiusY=7;root.Clip=contentClip;
        root.SizeChanged+=(s,e)=>{if(!animating)contentClip.Rect=new System.Windows.Rect(0,0,Math.Max(0,root.ActualWidth),Math.Max(0,root.ActualHeight));};
        header=new Grid{Margin=new Thickness(20,12,14,12)};header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(36)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(36)});
        
        var searchGlyph=Clean11Fluent.Icon("Search24",20);searchGlyph.Foreground=Muted;header.Children.Add(searchGlyph);
        input=new TextBox{FocusVisualStyle=null,FontSize=20,Foreground=TextBrush,CaretBrush=Accent,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),VerticalContentAlignment=VerticalAlignment.Center,SelectionBrush=(Brush)FindResource("Selected"),Margin=new Thickness(8,0,10,0)};
        input.Template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Border Background='Transparent'><ScrollViewer x:Name='PART_ContentHost' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' VerticalAlignment='Center'/></Border></ControlTemplate>");
        Grid.SetColumn(input,1);header.Children.Add(input);
        placeholder=new TextBlock{Text="Search anything",FontSize=20,Foreground=Muted,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,10,0),IsHitTestVisible=false};Grid.SetColumn(placeholder,1);header.Children.Add(placeholder);
        settingsHeading=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed,Margin=new Thickness(10,0,0,0)};
        settingsHeading.Children.Add(Clean11Fluent.Icon("Settings24",24));settingsHeading.Children.Add(new TextBlock{Text="Settings",FontSize=24,FontWeight=FontWeights.SemiBold,Foreground=TextBrush,Margin=new Thickness(14,0,0,0)});Grid.SetColumn(settingsHeading,1);header.Children.Add(settingsHeading);
        var gear=Clean11Fluent.IconButton("Settings","Settings24",()=>{if(settings)overview.ExitSpotlightSettings();else overview.OpenSpotlightSettings();});Clean11Fluent.Quiet(gear);gear.Opacity=.65;Grid.SetColumn(gear,2);header.Children.Add(gear);var close=Clean11Fluent.IconButton("Close","Dismiss24",()=>overview.CloseSpotlight());Clean11Fluent.Quiet(close);close.Opacity=.65;Grid.SetColumn(close,3);header.Children.Add(close);root.Children.Add(header);
        favorites=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(14,6,14,12)};
        overviewPage=new Border{BorderBrush=(Brush)FindResource("Divider"),BorderThickness=new Thickness(0,1,0,0),Margin=new Thickness(20,0,20,0),Child=favorites,Visibility=Visibility.Collapsed};Grid.SetRow(overviewPage,1);root.Children.Add(overviewPage);
        searchPage=new Grid();searchPage.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1.55,GridUnitType.Star)});searchPage.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});Grid.SetRow(searchPage,1);root.Children.Add(searchPage);
        var left=new Grid{Margin=new Thickness(10,0,0,12)};left.RowDefinitions.Add(new RowDefinition{Height=new GridLength(22)});left.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});searchPage.Children.Add(left);
        status=new TextBlock{Margin=new Thickness(14,3,0,3),Foreground=Muted,FontSize=11};left.Children.Add(status);
        list=new ListBox{ItemsSource=rows,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=TextBrush,Padding=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch};Grid.SetRow(list,1);left.Children.Add(list);
        ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);ScrollViewer.SetVerticalScrollBarVisibility(list,ScrollBarVisibility.Auto);VirtualizingStackPanel.SetIsVirtualizing(list,true);VirtualizingStackPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);
        list.ItemTemplate=(DataTemplate)XamlReader.Parse(@"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Grid Margin='9,4'><Grid.ColumnDefinitions><ColumnDefinition Width='34'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><Image Source='{Binding Icon}' Width='28' Height='28' VerticalAlignment='Center'/><StackPanel Grid.Column='1' VerticalAlignment='Center' Margin='8,0,4,0'><TextBlock Text='{Binding Title}' FontSize='14' Foreground='{DynamicResource Text}' TextTrimming='CharacterEllipsis'/><TextBlock Text='{Binding Subtitle}' FontSize='12' Foreground='{DynamicResource Muted}' TextTrimming='CharacterEllipsis' Margin='0,2,0,0'/></StackPanel></Grid></DataTemplate>");
        var itemStyle=new Style(typeof(ListBoxItem),Application.Current.TryFindResource(typeof(ListBoxItem)) as Style);
        itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));
        itemStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,50.0));
        itemStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(2,2,2,2)));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(4,1,4,1)));
        itemStyle.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ListBoxItem'><Border x:Name='Row' Background='Transparent' CornerRadius='{DynamicResource RowRadius}' BorderThickness='1' BorderBrush='Transparent'><Grid><Border x:Name='AccentMark' Width='3' Height='22' CornerRadius='2' Background='{DynamicResource Accent}' HorizontalAlignment='Left' Visibility='Collapsed'/><ContentPresenter Margin='6,2,2,2'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Row' Property='Background' Value='{DynamicResource Hover}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='Row' Property='Background' Value='{DynamicResource Selected}'/><Setter TargetName='AccentMark' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='Row' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));
        list.ItemContainerStyle=itemStyle;
        var separator=new Border{Background=Brushes.Transparent,BorderBrush=(Brush)FindResource("Divider"),BorderThickness=new Thickness(1,0,0,0),Margin=new Thickness(16,22,12,18),Padding=new Thickness(24,16,16,16)};detail=new StackPanel{VerticalAlignment=VerticalAlignment.Center};separator.Child=detail;Grid.SetColumn(separator,1);searchPage.Children.Add(separator);
        settingsPage=new ScrollViewer{Visibility=Visibility.Collapsed,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(28,0,28,22)};Grid.SetRow(settingsPage,1);root.Children.Add(settingsPage);
        settingsFooter=new Border{Visibility=Visibility.Collapsed,BorderThickness=new Thickness(0,1,0,0),BorderBrush=(Brush)FindResource("Divider"),Padding=new Thickness(28,7,28,12)};var footer=new DockPanel();var done=Button("Done",()=>overview.ExitSpotlightSettings());done.FontSize=14;DockPanel.SetDock(done,Dock.Right);footer.Children.Add(done);footer.Children.Add(new TextBlock{Text="Clean11  ·  Esc to return",FontSize=11,Foreground=Muted,VerticalAlignment=VerticalAlignment.Center});settingsFooter.Child=footer;Grid.SetRow(settingsFooter,2);root.Children.Add(settingsFooter);
        input.TextChanged+=(s,e)=>{placeholder.Visibility=input.Text.Length==0&&!settings?Visibility.Visible:Visibility.Collapsed;if(!synchronizing&&!settings)overview.SetSpotlightQuery(input.Text);};
        PreviewKeyDown+=HandleKeys;
        list.SelectionChanged+=(s,e)=>{if(synchronizing)return;overview.SetSpotlightSelection(list.SelectedIndex);UpdateDetail();};
        list.PreviewMouseLeftButtonDown+=(s,e)=>{var container=ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) as ListBoxItem;var row=container==null?null:container.DataContext as SpotlightRow;pressedRowKey=row==null?null:overview.SpotlightKey(row.Item);};
        list.PreviewMouseLeftButtonUp+=(s,e)=>{var container=ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) as ListBoxItem;var row=container==null?null:container.DataContext as SpotlightRow;if(row!=null&&pressedRowKey==overview.SpotlightKey(row.Item)){int index=list.ItemContainerGenerator.IndexFromContainer(container);overview.LaunchSpotlight(index);e.Handled=true;}pressedRowKey=null;};
        list.ContextMenu=new ContextMenu();
        list.ContextMenuOpening+=(s,e)=>{e.Handled=true;var element=e.OriginalSource as DependencyObject;var container=ItemsControl.ContainerFromElement(list,element) as ListBoxItem;if(container==null)return;int index=list.ItemContainerGenerator.IndexFromContainer(container);list.SelectedIndex=index;var menu=new ContextMenu{PlacementTarget=container};FrostedMaterial.StyleMenu(menu,overview.SpotlightGlass);foreach(string command in overview.SpotlightCommands(index)){string action=command;var item=new MenuItem{Header=action};item.Click+=(ss,ee)=>overview.InvokeSpotlightCommand(index,action);menu.Items.Add(item);}activeMenu=menu;menu.IsOpen=true;};
        materialTimer.Tick+=(s,e)=>{if(!IsVisible)return;if(MaterialKey(OverviewPreferences.Load())!=materialKey)ApplyMaterial();else UpdateGlass();if(Environment.CommandLine.Contains("--inspect-ui")){string frame=Expanded+"|"+animating+"|"+searchPage.Opacity+"|"+ActualWidth+"x"+ActualHeight;if(frame!=lastFrame){lastFrame=frame;Native.Log("Search visual state: "+frame);}}};
        IsVisibleChanged+=(s,e)=>{if(IsVisible){ApplyMaterial();materialTimer.Start();}else{materialTimer.Stop();if(glassBackdrop!=null)glassBackdrop.Hide();}};
        SourceInitialized+=(s,e)=>{hwnd=new WindowInteropHelper(this).Handle;dpi=Native.GetDpiForWindow(hwnd)/96.0;var source=HwndSource.FromHwnd(hwnd);if(source!=null)source.CompositionTarget.BackgroundColor=Colors.Transparent;ApplyMaterial();};
        Closing+=(s,e)=>{if(!dead){e.Cancel=true;overview.CloseSpotlight();}};
        Activated+=(s,e)=>dismissTimer.Stop();Deactivated+=(s,e)=>{if(IsVisible){dismissTimer.Stop();dismissTimer.Start();}};
        dismissTimer.Tick+=(s,e)=>{dismissTimer.Stop();if(!IsVisible||dead||Environment.CommandLine.Contains("--inspect-ui"))return;IntPtr focused=Native.GetForegroundWindow();if(focused==IntPtr.Zero)return;uint pid;Native.GetWindowThreadProcessId(focused,out pid);if(pid==(uint)Process.GetCurrentProcess().Id)return;IntPtr focusOwner=focused;for(int depth=0;depth<12&&focusOwner!=IntPtr.Zero;depth++){if(focusOwner==hwnd)return;focusOwner=Native.GetWindow(focusOwner,4);}overview.DismissSpotlightFocusLoss();};
    }
    Ui.Button Button(string text,Action action){return Clean11Fluent.Button(text,action,false);}
    void RefreshInk(DependencyObject node,Brush oldText,Brush oldMuted){
        var control=node as Control;var text=node as TextBlock;
        if(control!=null){if(Object.ReferenceEquals(control.Foreground,oldText))control.Foreground=TextBrush;else if(Object.ReferenceEquals(control.Foreground,oldMuted))control.Foreground=Muted;}
        if(text!=null){if(Object.ReferenceEquals(text.Foreground,oldText))text.Foreground=TextBrush;else if(Object.ReferenceEquals(text.Foreground,oldMuted))text.Foreground=Muted;}
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)RefreshInk(VisualTreeHelper.GetChild(node,i),oldText,oldMuted);
    }
    DockBackdrop glassBackdrop;
    OverviewPreferences glassPreferences;
    readonly System.Windows.Threading.DispatcherTimer materialTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};
    string materialKey="";
    string lastFrame="";
    string MaterialKey(OverviewPreferences p){return p.Glass+"|"+p.GlassStyle+"|"+p.GlassStrength+"|"+p.BlurRadius+"|"+FrostedMaterial.Enabled(p.Glass);}
    void UpdateGlass(){
        if(hwnd==IntPtr.Zero||!IsVisible){if(glassBackdrop!=null)glassBackdrop.Hide();return;}
        if(glassPreferences==null)glassPreferences=OverviewPreferences.Load();
        if(glassBackdrop==null){glassBackdrop=new DockBackdrop();glassBackdrop.SetOwner(overview.Handle);}
        var bounds=Current();
        glassBackdrop.Update(hwnd,bounds.Left/dpi,bounds.Top/dpi,bounds.Width/dpi,bounds.Height/dpi,dpi,true,FrostedMaterial.Enabled(glassPreferences.Glass),glassPreferences.GlassStrength,animating);
    }
    internal void ApplyMaterial(){
        var nextPreferences=OverviewPreferences.Load();var nextKey=MaterialKey(nextPreferences);
        if(materialKey==nextKey){UpdateGlass();return;}
        Clean11Fluent.RefreshPalette(this);
        // Styles may freeze their brushes. Replace resources rather than mutate them.
        var oldText=TextBrush;var oldMuted=Muted;
        TextBrush=new SolidColorBrush(FrostedMaterial.Ink(false));Muted=new SolidColorBrush(FrostedMaterial.Ink(true));
        RefreshInk(this,oldText,oldMuted);
        Resources["Text"]=TextBrush;Resources["Muted"]=Muted;
        Resources["Hover"]=new SolidColorBrush(Color.FromArgb(38,115,171,207));
        Resources["Selected"]=new SolidColorBrush(Color.FromArgb(85,84,157,204));
        surface.BorderBrush=FrostedMaterial.Edge();
        glassPreferences=OverviewPreferences.Load();materialKey=MaterialKey(glassPreferences);
        materialScreen=Forms.Screen.FromHandle(overview.Handle).Bounds;material=null;
        surface.Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));root.Background=Brushes.Transparent;
        surface.CornerRadius=new CornerRadius(ActivityTheme.GlassRadius);
        UpdateGlass();
    }
    internal void WarmHidden(){
        if(IsVisible)return;Width=940;Height=548;new WindowInteropHelper(this).EnsureHandle();
        surface.Measure(new Size(940,548));surface.Arrange(new System.Windows.Rect(0,0,940,548));surface.UpdateLayout();
        if(glassBackdrop==null){glassBackdrop=new DockBackdrop();glassBackdrop.SetOwner(overview.Handle);}
        var appearance=OverviewPreferences.Appearance;
        glassBackdrop.Prepare(940,548,dpi,FrostedMaterial.Enabled(appearance.Glass),appearance.Strength);
        Native.Log("Spotlight alpha material and keyboard surface warmed while hidden");
    }
    Rect Target(bool settingsMode){var work=Forms.Screen.FromHandle(overview.Handle).WorkingArea;double factor=Native.GetDpiForWindow(overview.Handle)/96.0;int width=Math.Min(work.Width-40,(int)((settingsMode?760:800)*factor));int height=Math.Min(work.Height-70,(int)((settingsMode?600:474)*factor));return new Rect(work.Left+(work.Width-width)/2,work.Top+(work.Height-height)/2,Math.Max(320,width),Math.Max(240,height));}
    internal bool OwnsForeground(IntPtr foreground){return IsVisible&&foreground==hwnd;}
    internal void FocusInput(){if(IsVisible&&!settings&&!input.IsKeyboardFocusWithin){Activate();input.Focus();Keyboard.Focus(input);}}
    internal void ShowOverview(Rect origin,bool focus=true){
        input.Visibility=Visibility.Visible;settingsHeading.Visibility=Visibility.Collapsed;
        if(Expanded)return;
        if(enteringOverview){if(focus)FocusInput();return;}
        StopAnimation();settings=false;input.IsReadOnly=false;searchPage.Visibility=Visibility.Collapsed;settingsPage.Visibility=Visibility.Collapsed;settingsFooter.Visibility=Visibility.Collapsed;RefreshOverview();
        synchronizing=true;input.Text="";synchronizing=false;placeholder.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
        bool first=!IsVisible;
        bool entrance=first&&!overview.SpotlightReduced;
        var start=origin;if(entrance)start.Offset(0,-(int)Math.Round(12*dpi));
        Position(start);
        if(first){ApplyMaterial();root.Opacity=entrance?0:1;ShowActivated=focus;Show();ShowActivated=true;}
        if(entrance){
            if(overview.SpotlightEntering){
                // Native previews, captions and the header share the same
                // submitted progress. A second WPF entrance clock could start
                // a frame late and continue after the preview motion settled.
                from=start;to=origin;presentedBounds=start;animating=true;enteringOverview=true;root.Opacity=0;if(glassBackdrop!=null)glassBackdrop.SetMotionOpacity(0);
            }else{MoveTo(origin,start,false,null);enteringOverview=true;root.Opacity=0;}
        }
        if(focus)FocusInput();
    }
    internal void ShowSearch(string query,Rect origin,bool reduced){
        input.Visibility=Visibility.Visible;settingsHeading.Visibility=Visibility.Collapsed;
        bool first=!IsVisible,wasCompact=!Expanded,wasSettings=settings;Expanded=true;settings=false;searchPage.Visibility=Visibility.Visible;settingsPage.Visibility=Visibility.Collapsed;settingsFooter.Visibility=Visibility.Collapsed;input.IsReadOnly=false;
        overviewPage.Visibility=Visibility.Collapsed;
        synchronizing=true;if(input.Text!=query){input.Text=query;input.CaretIndex=query.Length;}if(wasSettings&&savedQuery==query)input.Select(Math.Min(savedSelectionStart,query.Length),Math.Min(savedSelectionLength,query.Length-Math.Min(savedSelectionStart,query.Length)));synchronizing=false;
        if(first){Left=origin.Left/dpi;Top=origin.Top/dpi;Width=origin.Width/dpi;Height=origin.Height/dpi;Show();ApplyMaterial();}
        bool interrupting=animationDone!=null;
        if(first||wasCompact||wasSettings||interrupting){fadingPreviews=first||wasCompact||interrupting;fadeFrom=overview.SpotlightPreviewOpacity;fadeTo=0;MoveTo(Target(false),first?origin:Current(),reduced,null);}
        if(!input.IsKeyboardFocusWithin){input.Focus();Keyboard.Focus(input);}
    }
    internal void ShowSettings(Rect origin,bool reduced){
        input.Visibility=Visibility.Collapsed;settingsHeading.Visibility=Visibility.Visible;
        bool first=!IsVisible,wasCompact=!Expanded;Expanded=true;if(!settings){savedQuery=input.Text;savedSelectionStart=input.SelectionStart;savedSelectionLength=input.SelectionLength;}settings=true;searchPage.Visibility=Visibility.Collapsed;settingsPage.Visibility=Visibility.Visible;settingsFooter.Visibility=Visibility.Visible;input.IsReadOnly=true;synchronizing=true;input.Text="Clean11 settings";synchronizing=false;placeholder.Visibility=Visibility.Collapsed;
        overviewPage.Visibility=Visibility.Collapsed;
        PopulateSettings();if(first){Left=origin.Left/dpi;Top=origin.Top/dpi;Width=origin.Width/dpi;Height=origin.Height/dpi;Show();ApplyMaterial();}
        fadingPreviews=first||wasCompact;fadeFrom=overview.SpotlightPreviewOpacity;fadeTo=0;MoveTo(Target(true),first?origin:Current(),reduced,null);
    }
    internal void ReturnToOverview(Rect origin,bool reduced,Action complete){if(!IsVisible){complete();return;}if(settings){settings=false;input.IsReadOnly=false;synchronizing=true;input.Text="";synchronizing=false;}fadingPreviews=true;fadeFrom=overview.SpotlightPreviewOpacity;fadeTo=255;overview.FadeSpotlightPreviews((byte)fadeFrom);MoveTo(origin,Current(),reduced,()=>{Expanded=false;ShowOverview(origin);complete();});}
    Rect presentedBounds;
    Rect Current(){if(animating)return presentedBounds;Native.Rect bounds;if(hwnd!=IntPtr.Zero&&Native.GetWindowRect(hwnd,out bounds))return bounds.Box;return new Rect((int)Math.Round(Left*dpi),(int)Math.Round(Top*dpi),Math.Max(1,(int)Math.Round(ActualWidth*dpi)),Math.Max(1,(int)Math.Round(ActualHeight*dpi)));}
    readonly System.Windows.Threading.DispatcherTimer animationCompletion=new System.Windows.Threading.DispatcherTimer();
    void FinishAnimation(object sender,EventArgs e){if(animating)Animate();}
    void StopAnimation(){animationCompletion.Stop();animationCompletion.Tick-=FinishAnimation;if(animating)CompositionTarget.Rendering-=RenderAnimation;animating=false;enteringOverview=false;root.Opacity=1;root.Width=root.Height=Double.NaN;root.HorizontalAlignment=HorizontalAlignment.Stretch;root.VerticalAlignment=VerticalAlignment.Stretch;header.Width=Double.NaN;}
    void RenderAnimation(object sender,EventArgs e){var frame=e as RenderingEventArgs;if(frame!=null){if(frame.RenderingTime==lastRender)return;lastRender=frame.RenderingTime;}Animate();}
    void BodyOpacity(double value){searchPage.Opacity=value;settingsPage.Opacity=value;settingsFooter.Opacity=value;}
    void MoveTo(Rect target,Rect start,bool reduced,Action done){StopAnimation();from=start;to=target;animationDone=done;animationDuration=Math.Max(120,Math.Min(600,overview.SpotlightMotion));clock.Restart();PerformanceEvents.Log.TransitionBegin("search");frameGaps.Clear();previousFrame=0;positionMilliseconds=0;transitionFrames=0;firstFrameMilliseconds=-1;
        // Keep results at their final layout size while the outer shell moves.
        // Clipping and opacity reveal them without repeatedly squeezing columns.
        bodyFrom=!settings&&searchPage.Visibility!=Visibility.Visible?0:Math.Min(start.Height<=180*dpi?0:1,settings?settingsPage.Opacity:searchPage.Opacity);bodyTo=done!=null?0:1;
        if(bodyTo>0){searchPage.Width=Math.Max(1,target.Width/dpi-2);searchPage.Height=Math.Max(1,target.Height/dpi-66);searchPage.HorizontalAlignment=HorizontalAlignment.Left;searchPage.VerticalAlignment=VerticalAlignment.Top;}
        if(reduced){Position(to);BodyOpacity(1);if(fadingPreviews)overview.FadeSpotlightPreviews((byte)fadeTo);fadingPreviews=false;var action=animationDone;animationDone=null;if(action!=null)action();}
        else{if(fadingPreviews){overview.PrepareSpotlightPreviewFade();overview.FadeSpotlightPreviews((byte)fadeFrom);}BodyOpacity(bodyFrom);
            // Allocate one layered buffer for the whole morph. Only its visible
            // surface changes size; resizing the HWND each tick reallocates it.
            Width=Math.Max(from.Width,to.Width)/dpi;Height=Math.Max(from.Height,to.Height)/dpi;
            surface.HorizontalAlignment=HorizontalAlignment.Left;surface.VerticalAlignment=VerticalAlignment.Top;
            surface.Width=from.Width/dpi;surface.Height=from.Height/dpi;
            // Keep the results/Settings tree laid out once at the largest shell
            // size. Only the small header and outer border need new geometry;
            // the reusable clip reveals the body without reflowing every row.
            root.HorizontalAlignment=HorizontalAlignment.Left;root.VerticalAlignment=VerticalAlignment.Top;
            root.Width=Math.Max(from.Width,to.Width)/dpi-2;root.Height=Math.Max(from.Height,to.Height)/dpi-2;
            UpdateLayout();presentedBounds=from;animating=true;Position(from);CompositionTarget.Rendering+=RenderAnimation;
            // Rendering callbacks can pause when the mixed WinForms/WPF scene
            // stops producing frames. Always settle geometry and opacity.
            animationCompletion.Interval=TimeSpan.FromMilliseconds(animationDuration+16);animationCompletion.Tick+=FinishAnimation;animationCompletion.Start();}}
    readonly List<double> frameGaps=new List<double>();
    double previousFrame,positionMilliseconds,firstFrameMilliseconds;int transitionFrames;
    void ReportTransition(){
        PerformanceEvents.Log.TransitionEnd("search",clock.Elapsed.TotalMilliseconds);
        var sorted=frameGaps.OrderBy(x=>x).ToArray();
        Native.Log(String.Format(System.Globalization.CultureInfo.InvariantCulture,"Search transition: direction={0}, frames={1}, elapsed={2:F1}ms, median={3:F1}ms, p95={4:F1}ms, max={5:F1}ms, positionTotal={6:F1}ms, firstFrame={7:F1}ms",to.Height>from.Height?"expand":"collapse",transitionFrames,clock.Elapsed.TotalMilliseconds,sorted.Length==0?0:sorted[sorted.Length/2],sorted.Length==0?0:sorted[Math.Min(sorted.Length-1,(int)(sorted.Length*.95))],sorted.Length==0?0:sorted[sorted.Length-1],positionMilliseconds,firstFrameMilliseconds));
    }
    bool advancingFrame;
    void Animate(){
        if(advancingFrame||!animating)return;
        advancingFrame=true;
        try{
            double now=clock.Elapsed.TotalMilliseconds;
            if(firstFrameMilliseconds<0){firstFrameMilliseconds=now;animationCompletion.Stop();animationCompletion.Start();}
            else frameGaps.Add(now-previousFrame);
            previousFrame=now;transitionFrames++;
            // Preparation must not consume the start of the visible motion.
            double t=Math.Min(1,(now-firstFrameMilliseconds)/animationDuration);
            double eased=Overview.MotionCurve(t);
            if(enteringOverview)root.Opacity=eased;
            long positionStarted=Stopwatch.GetTimestamp();
            Position(new Rect((int)Math.Round(from.X+(to.X-from.X)*eased),(int)Math.Round(from.Y+(to.Y-from.Y)*eased),
                (int)Math.Round(from.Width+(to.Width-from.Width)*eased),(int)Math.Round(from.Height+(to.Height-from.Height)*eased)));
            double workMilliseconds=(Stopwatch.GetTimestamp()-positionStarted)*1000.0/Stopwatch.Frequency;
            positionMilliseconds+=workMilliseconds;PerformanceEvents.Log.Frame("search",t,workMilliseconds);
            double reveal=bodyTo>bodyFrom?Math.Max(0,Math.Min(1,(t-.18)/.66)):Math.Min(1,t/.55);
            reveal=reveal*reveal*(3-2*reveal);BodyOpacity(bodyFrom+(bodyTo-bodyFrom)*reveal);
            if(fadingPreviews)overview.FadeSpotlightPreviews((byte)Math.Round(fadeFrom+(fadeTo-fadeFrom)*eased));
            if(t>=1){
                StopAnimation();surface.Width=Double.NaN;surface.Height=Double.NaN;
                Position(to);BodyOpacity(1);fadingPreviews=false;ReportTransition();
                var done=animationDone;animationDone=null;if(done!=null)done();
            }
        }finally{advancingFrame=false;}
    }
    void Position(Rect bounds){
        if(!animating&&hwnd!=IntPtr.Zero)dpi=Math.Max(96,Native.GetDpiForWindow(hwnd))/96.0;
        if(animating&&hwnd!=IntPtr.Zero){
            presentedBounds=bounds;
            surface.Width=bounds.Width/dpi;surface.Height=bounds.Height/dpi;
            if(!exitingOverview){
                header.Width=Math.Max(1,bounds.Width/dpi-36);
                contentClip.Rect=new System.Windows.Rect(0,0,Math.Max(0,bounds.Width/dpi-2),Math.Max(0,bounds.Height/dpi-2));
            }
            // UpdateGlass batches both HWND changes into one desktop commit.
            UpdateGlass();return;
        }
        surface.Width=Double.NaN;surface.Height=Double.NaN;
        Left=bounds.X/dpi;Top=bounds.Y/dpi;Width=bounds.Width/dpi;Height=bounds.Height/dpi;
        UpdateLayout();UpdateGlass();
    }
    Rect exitOrigin;double exitProgress,exitOpacity;bool exitingOverview;
    internal void UpdateOverviewEntrance(double progress){
        if(!enteringOverview||!IsVisible)return;
        progress=Math.Max(0,Math.Min(1,progress));root.Opacity=progress;
        Position(new Rect(to.X,(int)Math.Round(from.Y+(to.Y-from.Y)*progress),to.Width,to.Height));
        if(glassBackdrop!=null)glassBackdrop.SetMotionOpacity(progress);
        if(progress>=1){StopAnimation();Position(to);}
    }
    internal void BeginOverviewExit(double progress){
        if(!IsVisible)return;
        exitOrigin=Current();exitOpacity=root.Opacity;exitProgress=Math.Max(.001,progress);
        StopAnimation();dismissTimer.Stop();animationDone=null;fadingPreviews=false;
        exitingOverview=true;animating=true;presentedBounds=exitOrigin;IsHitTestVisible=false;
        UpdateOverviewExit(progress);
    }
    internal void UpdateOverviewExit(double progress){
        if(!exitingOverview||!IsVisible)return;
        double fraction=Math.Max(0,Math.Min(1,progress/exitProgress));
        root.Opacity=exitOpacity*fraction;
        var bounds=exitOrigin;bounds.Offset(0,-(int)Math.Round(12*dpi*(1-fraction)));Position(bounds);
        if(glassBackdrop!=null)glassBackdrop.SetMotionOpacity(fraction);
    }
    internal void HidePanel(){StopAnimation();exitingOverview=false;IsHitTestVisible=true;dismissTimer.Stop();animationDone=null;fadingPreviews=false;Expanded=false;previewGeneration++;if(activeMenu!=null)activeMenu.IsOpen=false;if(IsVisible)Hide();}
    internal void Shutdown(){HidePanel();materialTimer.Stop();if(glassBackdrop!=null){glassBackdrop.Close();glassBackdrop=null;}dead=true;dismissTimer.Stop();Close();}
    internal void SaveOffscreenPreview(string file,bool settingsMode,IList<ResultItem> items,string query){
        input.Visibility=settingsMode?Visibility.Collapsed:Visibility.Visible;settingsHeading.Visibility=settingsMode?Visibility.Visible:Visibility.Collapsed;
        // The complete material is now part of the WPF visual, so this hidden
        // render includes its real rounded alpha rather than a DWM substitute.
        settings=settingsMode;overviewPage.Visibility=Visibility.Collapsed;ApplyMaterial();Position(Target(settingsMode));searchPage.Width=Double.NaN;searchPage.Height=Double.NaN;
        synchronizing=true;input.Text=settingsMode?"Clean11 settings":query;synchronizing=false;
        settingsPage.Visibility=settingsMode?Visibility.Visible:Visibility.Collapsed;settingsFooter.Visibility=settingsMode?Visibility.Visible:Visibility.Collapsed;searchPage.Visibility=settingsMode?Visibility.Collapsed:Visibility.Visible;
        if(settingsMode)PopulateSettings();else UpdateResults(items,0,items.Count+" results · Best match first");
        int width=settingsMode?820:940,height=settingsMode?600:548;var size=new Size(width,height);surface.Measure(size);surface.Arrange(new System.Windows.Rect(size));surface.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(file))encoder.Save(output);
    }
    internal void SaveOffscreenHeader(string file){
        input.Visibility=Visibility.Visible;settingsHeading.Visibility=Visibility.Collapsed;
        settings=false;synchronizing=true;input.Text="";synchronizing=false;placeholder.Visibility=Visibility.Visible;
        searchPage.Visibility=Visibility.Collapsed;settingsPage.Visibility=Visibility.Collapsed;settingsFooter.Visibility=Visibility.Collapsed;RefreshOverview();ApplyMaterial();int height=overview.SpotlightFavorites().Count>0?124:64;Position(new Rect(materialScreen.Left+(materialScreen.Width-784)/2,materialScreen.Top+44,784,height));
        var size=new Size(784,height);surface.Measure(size);surface.Arrange(new System.Windows.Rect(size));surface.UpdateLayout();var bitmap=new RenderTargetBitmap(784,height,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(file))encoder.Save(output);
    }
    internal object HiddenMaterialInfo(){int backdrop;int result=Native.DwmGetInt(hwnd,38,out backdrop,4);return new{perPixelAlpha=AllowsTransparency,layeredWindow=(Native.GetWindowLongPtr(hwnd,-20).ToInt64()&0x80000)!=0,systemBackdropNone=result==0&&backdrop==1,wallpaperMaterial=material!=null};}
    void HandleKeys(object sender,KeyEventArgs e){
        if(e.Key==Key.Escape){overview.SpotlightEscape();e.Handled=true;return;}
        if(settings)return;
        if(!Expanded){var key=(Forms.Keys)KeyInterop.VirtualKeyFromKey(e.Key);if((Keyboard.Modifiers&ModifierKeys.Control)!=0)key|=Forms.Keys.Control;if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)key|=Forms.Keys.Shift;if((Keyboard.Modifiers&ModifierKeys.Alt)!=0)key|=Forms.Keys.Alt;if(overview.HandleSpotlightShortcut(key))e.Handled=true;return;}
        if(e.Key==Key.Left||e.Key==Key.Right){if(overview.HandleSpotlightShortcut(e.Key==Key.Left?Forms.Keys.Left:Forms.Keys.Right)){e.Handled=true;return;}}
        if((Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Alt))!=0){var key=(Forms.Keys)KeyInterop.VirtualKeyFromKey(e.Key);if((Keyboard.Modifiers&ModifierKeys.Control)!=0)key|=Forms.Keys.Control;if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)key|=Forms.Keys.Shift;if((Keyboard.Modifiers&ModifierKeys.Alt)!=0)key|=Forms.Keys.Alt;if(overview.HandleSpotlightShortcut(key)){e.Handled=true;return;}}
        if(e.Key==Key.Down||e.Key==Key.Up){int next=Math.Max(0,Math.Min(rows.Count-1,list.SelectedIndex+(e.Key==Key.Down?1:-1)));if(rows.Count>0){list.SelectedIndex=next;list.ScrollIntoView(list.SelectedItem);}e.Handled=true;}
        else if(e.Key==Key.Enter){overview.LaunchSpotlight(list.SelectedIndex);e.Handled=true;}
    }
    internal void UpdateResults(IList<ResultItem> items,int selected,string message){
        if(settings)return;
        int previousSelection=list.SelectedIndex;
        synchronizing=true;
        bool same=items.Count==rows.Count&&items.Select((item,index)=>overview.SpotlightKey(item)==overview.SpotlightKey(rows[index].Item)).All(value=>value);
        if(!same){rows.Clear();foreach(var item in items)rows.Add(new SpotlightRow{Item=item,Title=item.Label,Subtitle=Subtitle(item),Icon=Image(item)});}
        else for(int i=0;i<items.Count;i++){rows[i].Item=items[i];if(rows[i].Icon==null){var icon=Image(items[i]);if(icon!=null)rows[i]=new SpotlightRow{Item=items[i],Title=items[i].Label,Subtitle=Subtitle(items[i]),Icon=icon};}}
        list.SelectedIndex=items.Count==0?-1:Math.Max(0,Math.Min(items.Count-1,selected));status.Text=message;synchronizing=false;
        if(list.SelectedItem!=null&&(!same||previousSelection!=list.SelectedIndex))list.ScrollIntoView(list.SelectedItem);UpdateDetail();
    }
    string Subtitle(ResultItem item){return item.Setting!=null?(item.Setting.Direct?(String.IsNullOrEmpty(item.Setting.Group)?"Windows Settings":"Windows Settings · opens containing page"):"Windows Settings · open Settings to find this"):item.App!=null?"Application":item.FolderPath!=null?"Folder":item.FilePath!=null?"File":item.Window!=null?"Open window":item.Action=="calculate"?"Calculator · Enter to copy":"Action";}
    ImageSource Image(ResultItem item){string key=item.Setting!=null?"settings":item.App!=null?item.App.IconPath:item.Window!=null?item.Window.ProcessPath:item.FolderPath!=null?"folder":item.FilePath??item.Action??"";ImageSource source;if(icons.TryGetValue(key,out source))return source;var icon=overview.SpotlightIcon(item);if(icon==null)return null;try{source=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(64,64));source.Freeze();if(icons.Count<256)icons[key]=source;return source;}catch{return null;}}
    TextBlock Text(string value,double size,Brush color){return new TextBlock{Text=value??"",FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)};}
    string detailKey="";
    void UpdateDetail(){
        var row=list.SelectedItem as SpotlightRow;string key=row==null?"empty":overview.SpotlightKey(row.Item)+":"+(row.Icon!=null);if(key==detailKey)return;detailKey=key;int generation=++previewGeneration;detail.Children.Clear();
        if(row==null){detail.Children.Add(Text("No matching results",20,TextBrush));detail.Children.Add(Text("Try another name, or narrow your search with app:, file:, folder:, window: or settings:.",13,Muted));return;}
        if(row.Icon!=null)detail.Children.Add(new Image{Source=row.Icon,Width=52,Height=52,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,14)});
        else {var glyph=Clean11Fluent.Icon(row.Item.Action=="calculate"?"Calculator24":row.Item.Setting!=null?"Settings24":row.Item.FolderPath!=null?"Folder24":"Search24",48);glyph.Foreground=Accent;glyph.HorizontalAlignment=HorizontalAlignment.Left;glyph.Margin=new Thickness(0,0,0,14);detail.Children.Add(glyph);}
        var title=Text(row.Title,21,TextBrush);title.MaxHeight=68;title.TextTrimming=TextTrimming.CharacterEllipsis;title.ToolTip=row.Title;detail.Children.Add(title);detail.Children.Add(Text(row.Subtitle,12,Muted));
        string location=row.Item.Setting!=null?row.Item.Setting.Direct?row.Item.Setting.Target:"This entry is discovered from Windows. Open Settings and search for this name.":row.Item.App!=null?row.Item.App.Target:row.Item.FolderPath??row.Item.FilePath??(row.Item.Window!=null?row.Item.Window.Title:row.Item.Action=="calculate"?"Press Enter to copy the answer.":"");var context=Text(location,12,Muted);context.MaxHeight=88;context.TextTrimming=TextTrimming.CharacterEllipsis;detail.Children.Add(context);
        if(row.Item.Setting!=null&&!String.IsNullOrEmpty(row.Item.Setting.Keywords)){var keywords=Text(String.Join(" · ",row.Item.Setting.Keywords.Split(';').Distinct().Take(5)),11,Muted);keywords.MaxHeight=72;keywords.TextTrimming=TextTrimming.CharacterEllipsis;detail.Children.Add(keywords);}
        var action=Button(row.Item.Action=="calculate"?"Copy answer  ↵":"Open  ↵",()=>overview.LaunchSpotlight(list.SelectedIndex));action.Appearance=Ui.ControlAppearance.Secondary;action.Icon=Clean11Fluent.Icon(row.Item.Action=="calculate"?"Copy24":"Open24",16);action.HorizontalAlignment=HorizontalAlignment.Left;action.FontSize=13;action.Margin=new Thickness(0,18,0,0);detail.Children.Add(action);
        string path=row.Item.FilePath??row.Item.FolderPath;
        if(path!=null)ThreadPool.QueueUserWorkItem(_=>{string metadata="";try{if(File.Exists(path)){var file=new FileInfo(path);metadata=file.Length.ToString("N0")+" bytes\nModified "+file.LastWriteTime.ToString("g");}else if(Directory.Exists(path))metadata="Folder";}catch{}try{Dispatcher.BeginInvoke((Action)(()=>{if(generation==previewGeneration&&!dead)detail.Children.Add(Text(metadata,11,Muted));}));}catch{}});
    }
    void PopulateSettings(){
        var panel=new StackPanel();settingsPage.Content=panel;
        panel.Children.Add(Text("Appearance & behavior",16,TextBrush));
        panel.Children.Add(Text("Personalize your overview without changing how your apps work.",12,Muted));
        SettingSlider(panel,"Animation duration",overview.SpotlightMotion,120,600,20,value=>overview.SetSpotlightPreference("motion",value)," ms");
        SettingSlider(panel,"Windows per row",overview.SpotlightColumns,3,5,1,value=>overview.SetSpotlightPreference("columns",value),"");
        SettingToggle(panel,"Reduced motion",overview.SpotlightReduced,value=>overview.SetSpotlightPreference("reduced",value?1:0));
        SettingToggle(panel,"Frosted glass",overview.SpotlightGlass,value=>{overview.SetSpotlightPreference("glass",value?1:0);ApplyMaterial();});
        SettingToggle(panel,"Live game previews",!overview.SpotlightGameFriendly,value=>overview.SetSpotlightPreference("game",value?0:1));
        panel.Children.Add(Text("Search & applications",16,TextBrush));
        catalogLabel=Text(SettingsCatalog.Status,12,Muted);panel.Children.Add(catalogLabel);
        panel.Children.Add(SettingsButton("Refresh Windows Settings index",()=>SettingsCatalog.Refresh(true)));
        panel.Children.Add(SettingsButton("Add a portable application…",()=>{var dialog=new OpenFileDialog{Title="Add a portable application",Filter="Applications and shortcuts|*.exe;*.lnk",CheckFileExists=true};if(dialog.ShowDialog(this)==true)overview.SpotlightAddPortable(dialog.FileName);}));
        panel.Children.Add(Text("System",16,TextBrush));
        panel.Children.Add(SettingsButton("Open power controls",()=>overview.SpotlightPower()));
        panel.Children.Add(Text("Clean11 · Interface powered by WPF UI",12,Muted));
        panel.Children.Add(SettingsButton("Third-party licenses",()=>ShowLicenses()));
    }
    void ShowLicenses(){
        string folder=Clean11Paths.Licenses;
        string text="WPF UI 4.3.0 — Leszek Pomianowski and WPF UI Contributors\nMIT License\nhttps://github.com/lepoco/wpfui\n\n";
        try{foreach(var file in Directory.GetFiles(folder,"*.txt"))text+=Path.GetFileName(file)+"\n"+File.ReadAllText(file)+"\n\n";}catch{}
        var window=new Window{Title="Clean11 · Third-party licenses",Width=680,Height=540,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=this,ShowInTaskbar=false,Background=(Brush)FindResource("Surface"),Foreground=TextBrush,FontFamily=FontFamily};
        Clean11Fluent.Apply(window);
        window.SourceInitialized+=(s,e)=>{int enabled=1;DwmSetWindowAttribute(new WindowInteropHelper(window).Handle,20,ref enabled,4);};
        window.Content=new ScrollViewer{Padding=new Thickness(24),Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=13,Foreground=TextBrush}};
        window.ShowDialog();
    }
    internal void RefreshCatalogStatus(){if(catalogLabel!=null)catalogLabel.Text=SettingsCatalog.Status;}
    internal void RefreshOverview(){
        if(Expanded)return;var items=overview.SpotlightFavorites();overviewPage.Visibility=items.Count>0?Visibility.Visible:Visibility.Collapsed;
        string key=String.Join("|",items.Select(item=>overview.SpotlightKey(item)+":"+item.Label+":"+(Image(item)!=null)));
        if(favoriteSignature==key)return;favoriteSignature=key;favorites.Children.Clear();
        for(int i=0;i<items.Count;i++){
            int index=i;var item=items[i];var content=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};var icon=Image(item);
            if(icon!=null)content.Children.Add(new Image{Source=icon,Width=24,Height=24,Margin=new Thickness(0,0,8,0)});
            else content.Children.Add(Clean11Fluent.Icon("Apps24",32));
            content.Children.Add(new TextBlock{Text=item.Label,Foreground=TextBrush,FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center,MaxWidth=100});
            var button=Button("",()=>overview.OpenSpotlightFavorite(index));button.Content=content;Clean11Fluent.Quiet(button);button.Width=Math.Min(160,Math.Max(60,650.0/Math.Max(1,items.Count)));button.Padding=new Thickness(7,6,7,6);button.Margin=new Thickness(3,0,3,0);button.ToolTip=item.Label;
            var menu=new ContextMenu();FrostedMaterial.StyleMenu(menu,overview.SpotlightGlass);
            var open=new MenuItem{Header="Open"};open.Click+=(s,e)=>overview.OpenSpotlightFavorite(index);menu.Items.Add(open);
            var rename=new MenuItem{Header="Rename…"};rename.Click+=(s,e)=>RenameFavorite(index,item.Label);menu.Items.Add(rename);
            var unpin=new MenuItem{Header="Unpin"};unpin.Click+=(s,e)=>{overview.UnpinSpotlightFavorite(index);RefreshOverview();};menu.Items.Add(unpin);
            button.ContextMenu=menu;favorites.Children.Add(button);
        }
        if(IsVisible&&!Expanded)Position(overview.SpotlightOverviewBounds);
    }
    void RenameFavorite(int index,string name){
        var dialog=new Window{Title="Rename favorite",Width=420,Height=218,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=FontFamily,Foreground=TextBrush};
        Clean11Fluent.Apply(dialog);var body=new Grid{Margin=new Thickness(24)};body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        body.Children.Add(new TextBlock{Text="Rename favorite",FontSize=20,Margin=new Thickness(0,0,0,18)});
        var field=new Ui.TextBox{Text=name,MaxLength=80,FontSize=14,ClearButtonEnabled=false,PlaceholderText="Favorite name",CaretBrush=Accent,SelectionBrush=(Brush)FindResource("Selected")};
        var fieldBorder=new Border{CornerRadius=new CornerRadius(ActivityTheme.ControlRadius),Background=(Brush)FindResource("Hover"),Child=field};Grid.SetRow(fieldBorder,1);body.Children.Add(fieldBorder);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};
        var cancel=Button("Cancel",()=>dialog.DialogResult=false);cancel.FontSize=14;cancel.IsCancel=true;actions.Children.Add(cancel);
        var save=Button("Save",()=>dialog.DialogResult=true);save.FontSize=14;save.Appearance=Ui.ControlAppearance.Primary;save.IsDefault=true;save.Margin=new Thickness(10,0,0,0);actions.Children.Add(save);Grid.SetRow(actions,2);body.Children.Add(actions);
        var dialogSurface=new Border{CornerRadius=new CornerRadius(ActivityTheme.PanelRadius),BorderThickness=new Thickness(1),BorderBrush=(Brush)FindResource("Border"),Background=(Brush)FindResource("Surface"),Child=body};dialog.Content=dialogSurface;
        dialog.Loaded+=(s,e)=>{IntPtr handle=new WindowInteropHelper(dialog).Handle;double scale=Native.GetDpiForWindow(handle)/96.0;Native.Rect bounds;if(Native.GetWindowRect(handle,out bounds))dialogSurface.Background=FrostedMaterial.PanelBrush(bounds.Box,Forms.Screen.FromHandle(handle).Bounds,overview.SpotlightGlass);field.Focus();field.SelectAll();};
        if(dialog.ShowDialog()==true){overview.RenameSpotlightFavorite(index,field.Text);favoriteSignature="";RefreshOverview();}FocusInput();
    }
    internal Rect SelectedRowBounds(){
        var container=list.ItemContainerGenerator.ContainerFromIndex(list.SelectedIndex) as FrameworkElement;
        if(container==null)return Rect.Empty;var point=container.PointToScreen(new Point(0,0));
        double factor=hwnd==IntPtr.Zero?dpi:Native.GetDpiForWindow(hwnd)/96.0;
        return new Rect((int)Math.Round(point.X)-overview.Left,(int)Math.Round(point.Y)-overview.Top,(int)Math.Round(container.ActualWidth*factor),(int)Math.Round(container.ActualHeight*factor));
    }
    Ui.Button SettingsButton(string title,Action action){
        var button=Button(title,action);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,0,0,6);button.Padding=new Thickness(16,12,16,12);
        button.Icon=Clean11Fluent.Icon(title.StartsWith("Refresh")?"ArrowSync24":title.StartsWith("Add")?"Add24":title.StartsWith("Open power")?"Power24":"Info24",20);return button;
    }
    void SettingSlider(Panel panel,string title,int value,int min,int max,int tick,Action<int> change,string suffix){
        var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(230)});
        var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};labels.Children.Add(Text(title,14,TextBrush));var label=Text(value+suffix,12,Muted);labels.Children.Add(label);row.Children.Add(labels);
        var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=tick,IsSnapToTickEnabled=true,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(20,0,0,0)};
        System.Windows.Automation.AutomationProperties.SetName(slider,title);
        slider.ValueChanged+=(s,e)=>{int next=(int)Math.Round(slider.Value);label.Text=next+suffix;change(next);};Grid.SetColumn(slider,1);row.Children.Add(slider);panel.Children.Add(Clean11Fluent.Card(row));
    }
    void SettingToggle(Panel panel,string title,bool value,Action<bool> change){
        var row=new DockPanel();var toggle=new Ui.ToggleSwitch{IsChecked=value,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(20,0,0,0)};
        System.Windows.Automation.AutomationProperties.SetName(toggle,title);
        toggle.Checked+=(s,e)=>change(true);toggle.Unchecked+=(s,e)=>change(false);DockPanel.SetDock(toggle,Dock.Right);row.Children.Add(toggle);
        var labels=new StackPanel();labels.Children.Add(Text(title,14,TextBrush));labels.Children.Add(Text(title=="Reduced motion"?"Keep transitions immediate and distraction-free.":title=="Frosted glass"?"Use a translucent, wallpaper-tinted surface.":"Use compatible previews while games are running.",12,Muted));row.Children.Add(labels);panel.Children.Add(Clean11Fluent.Card(row));
    }
}
