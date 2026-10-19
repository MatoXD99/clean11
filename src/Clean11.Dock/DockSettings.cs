using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Interop;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
using Ui=Wpf.Ui.Controls;

sealed class DockSettings : Window {
    readonly DockWindow dock;
    readonly OverviewPreferences prefs;
    readonly List<Action> changes=new List<Action>();
    readonly Dictionary<string,StackPanel> pages=new Dictionary<string,StackPanel>();
    readonly Dictionary<string,Ui.Button> navigation=new Dictionary<string,Ui.Button>();
    readonly ContentControl pageHost=new ContentControl();
    ScrollViewer pageScroll;DockBackdrop settingsGlass;readonly System.Windows.Threading.DispatcherTimer glassTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};
    readonly TextBlock heading=new TextBlock(),subtitle=new TextBlock(),status=new TextBlock();
    readonly Ui.Button apply;
    bool ready,dirty;
    readonly Brush ink=ActivityTheme.Brush("Text","#F1F2F5"),muted=ActivityTheme.Brush("Muted","#A6ABB5");
    Border sampleGlass;TextBlock sampleTitle,sampleDetail;Grid sampleScene;
    ComboBox material;Slider visibility,blur;Ui.ToggleSwitch glass;

    internal DockSettings(DockWindow owner){
        dock=owner;prefs=OverviewPreferences.Load();Clean11Fluent.Apply(this);
        Title="Clean11 Settings";Width=1020;Height=780;MinWidth=800;MinHeight=560;WindowStyle=WindowStyle.None;AllowsTransparency=true;ResizeMode=ResizeMode.CanResizeWithGrip;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");FontSize=14;
        Background=Brushes.Transparent;SetResourceReference(ForegroundProperty,"Text");
        var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(210)});layout.ColumnDefinitions.Add(new ColumnDefinition());var windowRoot=new Grid();windowRoot.RowDefinitions.Add(new RowDefinition{Height=new GridLength(40)});windowRoot.RowDefinitions.Add(new RowDefinition());Grid.SetRow(layout,1);windowRoot.Children.Add(layout);
        var caption=new Grid{Background=new SolidColorBrush(Color.FromArgb(1,0,0,0)),Margin=new Thickness(16,0,8,0)};
        caption.MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2)WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else if(e.ButtonState==System.Windows.Input.MouseButtonState.Pressed)DragMove();};
        var captionText=Label("Clean11 Settings",12,true);captionText.VerticalAlignment=VerticalAlignment.Center;caption.Children.Add(captionText);
        var captionButtons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        var minimize=Clean11Fluent.IconButton("Minimize","Subtract24",()=>WindowState=WindowState.Minimized);Clean11Fluent.Quiet(minimize);captionButtons.Children.Add(minimize);
        var closeCaption=Clean11Fluent.IconButton("Close settings","Dismiss24",Close);Clean11Fluent.Quiet(closeCaption);captionButtons.Children.Add(closeCaption);caption.Children.Add(captionButtons);windowRoot.Children.Add(caption);
        var shell=new Border{BorderThickness=new Thickness(1),BorderBrush=FrostedMaterial.Edge(),CornerRadius=new CornerRadius(ActivityTheme.GlassRadius),Background=new SolidColorBrush(Color.FromArgb(1,0,0,0)),Child=windowRoot};
        windowRoot.SizeChanged+=(s,e)=>windowRoot.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,windowRoot.ActualWidth),Math.Max(0,windowRoot.ActualHeight)),ActivityTheme.GlassRadius-1,ActivityTheme.GlassRadius-1);Content=shell;
        var sidebar=new Grid{Background=ActivityTheme.Brush("Card","#2B2E34")};layout.Children.Add(sidebar);sidebar.SetResourceReference(Panel.BackgroundProperty,"Card");
        sidebar.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});sidebar.RowDefinitions.Add(new RowDefinition());sidebar.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var brand=new StackPanel{Margin=new Thickness(24,28,20,30)};brand.Children.Add(Label("Clean11",25,false));brand.Children.Add(Label("Make your desktop yours",12,true));sidebar.Children.Add(brand);
        var nav=new StackPanel{Margin=new Thickness(12,0,12,0)};Grid.SetRow(nav,1);sidebar.Children.Add(nav);
        var credit=Label("Built with WPF UI",11,true);credit.Margin=new Thickness(24,20,16,24);Grid.SetRow(credit,2);sidebar.Children.Add(credit);
        var main=new Grid();Grid.SetColumn(main,1);layout.Children.Add(main);
        main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition());main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var titleArea=new StackPanel{Margin=new Thickness(30,26,30,20)};
        heading.FontSize=28;heading.FontWeight=FontWeights.SemiBold;heading.SetResourceReference(TextBlock.ForegroundProperty,"Text");
        subtitle.SetResourceReference(TextBlock.ForegroundProperty,"Muted");subtitle.FontSize=13;subtitle.TextWrapping=TextWrapping.Wrap;subtitle.Margin=new Thickness(0,6,0,0);titleArea.Children.Add(heading);titleArea.Children.Add(subtitle);main.Children.Add(titleArea);
        var scroll=new ScrollViewer{Content=pageHost,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(30,0,24,24)};pageScroll=scroll;Grid.SetRow(scroll,1);main.Children.Add(scroll);
        var footer=new Grid{Margin=new Thickness(30,16,30,18)};footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        status.SetResourceReference(TextBlock.ForegroundProperty,"Muted");status.Text="All changes saved";status.TextWrapping=TextWrapping.Wrap;status.VerticalAlignment=VerticalAlignment.Center;status.Margin=new Thickness(0,0,16,0);footer.Children.Add(status);
        var close=Clean11Fluent.Button("Close",Close,false);close.Margin=new Thickness(0,0,10,0);Grid.SetColumn(close,1);footer.Children.Add(close);
        apply=Clean11Fluent.Button("Apply changes",Save,true);apply.IsEnabled=false;Grid.SetColumn(apply,2);footer.Children.Add(apply);
        var footerFrame=new Border{BorderThickness=new Thickness(0,1,0,0),BorderBrush=ActivityTheme.Brush("Divider","#18FFFFFF"),Child=footer};Grid.SetRow(footerFrame,2);main.Children.Add(footerFrame);
        AddPage(nav,"Appearance","Materials, clarity, and a little personality.",Appearance());
        AddPage(nav,"Dock","Layout, visibility, and the apps you keep close.",DockPage());
        AddPage(nav,"Overview","Window previews and movement, tuned to your workflow.",OverviewPage());
        AddPage(nav,"Apps & search","Manage shortcuts and discover Windows settings.",AppsPage());
        AddPage(nav,"System","Startup, desktop integration, and useful controls.",SystemPage());
        glassTimer.Tick+=(s,e)=>UpdateGlass();
        IsVisibleChanged+=(s,e)=>{if(IsVisible){glassTimer.Start();UpdateGlass();}else{glassTimer.Stop();if(settingsGlass!=null)settingsGlass.Hide();}};
        Closed+=(s,e)=>{glassTimer.Stop();if(settingsGlass!=null){settingsGlass.Close();settingsGlass=null;}};
        ready=true;Select("Appearance","Materials, clarity, and a little personality.");UpdateSample();
        SourceInitialized+=(s,e)=>{int dark=FrostedMaterial.Light?0:1,round=2;var h=new WindowInteropHelper(this).Handle;DockNative.DwmSetWindowAttribute(h,20,ref dark,4);DockNative.DwmSetWindowAttribute(h,33,ref round,4);};
        Loaded+=(s,e)=>{var area=SystemParameters.WorkArea;MaxHeight=area.Height;MaxWidth=area.Width;};
        Closing+=(s,e)=>{if(!dirty)return;var answer=MessageBox.Show(this,"Apply your changes before closing?","Unsaved settings",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);if(answer==MessageBoxResult.Cancel)e.Cancel=true;else if(answer==MessageBoxResult.Yes){Save();e.Cancel=dirty;}};
    }
    void UpdateGlass(){
        if(!IsVisible||WindowState==WindowState.Minimized){if(settingsGlass!=null)settingsGlass.Hide();return;}
        var h=new WindowInteropHelper(this).Handle;DockNative.Rect bounds;if(h==IntPtr.Zero||!DockNative.GetWindowRect(h,out bounds))return;
        if(settingsGlass==null)settingsGlass=new DockBackdrop();double scale=Math.Max(96,DockNative.GetDpiForWindow(h))/96.0;
        var current=OverviewPreferences.Load();settingsGlass.Update(h,bounds.Left/scale,bounds.Top/scale,bounds.Width/scale,bounds.Height/scale,scale,true,FrostedMaterial.Enabled(current.Glass),current.GlassStrength);
    }
    TextBlock Label(string text,double size,bool secondary){var textBlock=new TextBlock{Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap};textBlock.SetResourceReference(TextBlock.ForegroundProperty,secondary?"Muted":"Text");return textBlock;}
    void AddPage(Panel nav,string name,string description,StackPanel content){
        pages[name]=content;var button=Clean11Fluent.Button(name,()=>Select(name,description),false);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Height=44;button.Margin=new Thickness(0,0,0,6);button.Padding=new Thickness(14,0,14,0);navigation[name]=button;nav.Children.Add(button);
    }
    void Select(string name,string description){heading.Text=name;subtitle.Text=description;pageHost.Content=pages[name];if(pageScroll!=null)pageScroll.ScrollToTop();foreach(var pair in navigation){pair.Value.Appearance=pair.Key==name?Ui.ControlAppearance.Secondary:Ui.ControlAppearance.Transparent;pair.Value.FontWeight=pair.Key==name?FontWeights.SemiBold:FontWeights.Normal;}}
    void Changed(){if(!ready)return;dirty=true;apply.IsEnabled=true;status.Text="Unsaved changes";}
    void Save(){
        try{foreach(var change in changes)change();prefs.Save();Clean11Fluent.RefreshPalette(this);UpdateGlass();SetStartup(dock.State.Startup);dock.CloseFlyout();dock.ApplySettings();dock.RefreshGlass();TrayAppearance.Sync();dirty=false;apply.IsEnabled=false;status.Text="Saved. Shared appearance updated.";}
        catch(Exception error){status.Text="Some changes could not be saved. Please try again.";DockLog.Write("Settings save failed: "+error.GetType().Name);dirty=true;apply.IsEnabled=true;}
    }
    StackPanel Page(){return new StackPanel{MaxWidth=780,HorizontalAlignment=HorizontalAlignment.Stretch};}
    void Section(Panel panel,string title){var text=Label(title,12,true);text.FontWeight=FontWeights.SemiBold;text.Margin=new Thickness(2,panel.Children.Count==0?0:20,0,10);panel.Children.Add(text);}
    Border Card(UIElement content){var card=new Border{Background=ActivityTheme.Brush("Card","#2B2E34"),BorderBrush=ActivityTheme.Brush("Divider","#18FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(ActivityTheme.RowRadius),Padding=new Thickness(16),Margin=new Thickness(0,0,0,8),Child=content};card.SetResourceReference(Border.BackgroundProperty,"Card");card.SetResourceReference(Border.BorderBrushProperty,"Divider");return card;}
    void Row(Panel panel,string title,string description,FrameworkElement control){
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,22,0)};var name=Label(title,14,false);name.FontWeight=FontWeights.SemiBold;labels.Children.Add(name);
        if(!String.IsNullOrEmpty(description)){var detail=Label(description,12,true);detail.Margin=new Thickness(0,4,0,0);labels.Children.Add(detail);}grid.Children.Add(labels);
        control.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(control,1);grid.Children.Add(control);System.Windows.Automation.AutomationProperties.SetName(control,title);panel.Children.Add(Card(grid));
    }
    Ui.ToggleSwitch Toggle(Panel panel,string title,string description,bool initial,Action<bool> save){var control=new Ui.ToggleSwitch{IsChecked=initial};Row(panel,title,description,control);control.Checked+=(s,e)=>Changed();control.Unchecked+=(s,e)=>Changed();changes.Add(()=>save(control.IsChecked==true));return control;}
    ComboBox Choice<T>(Panel panel,string title,string description,IEnumerable<T> items,T selected,Action<T> save){var control=new ComboBox{ItemsSource=items,SelectedItem=selected,Width=180,MinHeight=34};Row(panel,title,description,control);control.SelectionChanged+=(s,e)=>Changed();changes.Add(()=>save((T)control.SelectedItem));return control;}
    Slider Range(Panel panel,string title,string description,double initial,double min,double max,double step,string suffix,Action<double> save){
        var box=new StackPanel();var top=new DockPanel();var value=Label(initial.ToString("0.##")+suffix,13,true);DockPanel.SetDock(value,Dock.Right);top.Children.Add(value);var name=Label(title,14,false);name.FontWeight=FontWeights.SemiBold;top.Children.Add(name);box.Children.Add(top);
        var info=Label(description,12,true);info.Margin=new Thickness(0,5,0,14);box.Children.Add(info);
        var slider=new Slider{Minimum=min,Maximum=max,Value=initial,TickFrequency=step,IsSnapToTickEnabled=true,MinHeight=24};System.Windows.Automation.AutomationProperties.SetName(slider,title);box.Children.Add(slider);panel.Children.Add(Card(box));
        slider.ValueChanged+=(s,e)=>{value.Text=slider.Value.ToString("0.##")+suffix;Changed();};changes.Add(()=>save(slider.Value));return slider;
    }
    void ActionRow(Panel panel,string title,string description,string caption,Action action){var button=Clean11Fluent.Button(caption,()=>{try{action();}catch(Exception error){status.Text="Could not complete this action.";DockLog.Write("Settings action failed: "+error.GetType().Name);}},false);button.MinWidth=100;Row(panel,title,description,button);}
    StackPanel Appearance(){
        var panel=Page();
        sampleScene=new Grid{Height=138,ClipToBounds=true,Background=new LinearGradientBrush(Color.FromRgb(36,109,153),Color.FromRgb(114,67,141),25)};
        var wave=new System.Windows.Shapes.Ellipse{Width=300,Height=100,Fill=new SolidColorBrush(Color.FromRgb(100,203,201)),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(-40,15,0,0)};sampleScene.Children.Add(wave);
        var sampleText=new StackPanel{Margin=new Thickness(20)};sampleTitle=Label("A calmer desktop",18,false);sampleTitle.FontWeight=FontWeights.SemiBold;sampleDetail=Label("Clean11 glass · material preview",12,true);sampleDetail.Margin=new Thickness(0,5,0,0);sampleText.Children.Add(sampleTitle);sampleText.Children.Add(sampleDetail);
        sampleGlass=new Border{CornerRadius=new CornerRadius(ActivityTheme.GlassRadius),BorderThickness=new Thickness(1),Margin=new Thickness(22,20,22,20),Child=sampleText};
        var sample=new Grid();sample.Children.Add(sampleScene);sample.Children.Add(sampleGlass);var frame=Card(sample);frame.Padding=new Thickness(0);frame.SizeChanged+=(s,e)=>frame.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,frame.ActualWidth),Math.Max(0,frame.ActualHeight)),ActivityTheme.RowRadius,ActivityTheme.RowRadius);panel.Children.Add(frame);
        Section(panel,"MATERIAL");
        glass=Toggle(panel,"Frosted glass","One material for Settings, dock, search, and window previews.",prefs.Glass,v=>prefs.Glass=v);
        material=Choice(panel,"Material","Midnight is smoked blue. Pearl is a pale frost.",new[]{"Midnight","Pearl"},prefs.GlassStyle,v=>prefs.GlassStyle=v);
        visibility=Range(panel,"Glass visibility","Reveal more of the background while keeping a gentle tint.",prefs.GlassStrength,0,100,5,"%",v=>prefs.GlassStrength=(int)v);
        blur=Range(panel,"Blur radius","Lower values keep background shapes sharper.",prefs.BlurRadius,0,40,1," px",v=>prefs.BlurRadius=(int)v);
        Toggle(panel,"Compositor glass animation (experimental)","Animate glass appearance independently of the UI thread. Turn off to use the established renderer.",prefs.CompositionMotion,v=>prefs.CompositionMotion=v);
        material.SelectionChanged+=(s,e)=>UpdateSample();visibility.ValueChanged+=(s,e)=>UpdateSample();blur.ValueChanged+=(s,e)=>UpdateSample();glass.Checked+=(s,e)=>UpdateSample();glass.Unchecked+=(s,e)=>UpdateSample();
        if(!DockBackdrop.AdjustableBlurAvailable){var note=Label("System blur fallback is active. Adjustable radius requires composition support.",12,true);note.Margin=new Thickness(2,8,2,0);panel.Children.Add(note);}return panel;
    }
    void UpdateSample(){if(material==null||visibility==null||blur==null||glass==null)return;bool light=(string)material.SelectedItem=="Pearl",enabled=glass.IsChecked==true;
        material.IsEnabled=visibility.IsEnabled=blur.IsEnabled=enabled;
        var color=light?Color.FromRgb(222,235,244):Color.FromRgb(23,36,51);color.A=enabled?FrostedMaterial.TintAlpha((int)visibility.Value):(byte)255;sampleGlass.Background=FrostedMaterial.Tint(color,(int)visibility.Value,enabled);
        sampleGlass.BorderBrush=new SolidColorBrush(Color.FromArgb(95,225,245,255));sampleTitle.Foreground=new SolidColorBrush(light?Color.FromRgb(19,35,51):Color.FromRgb(244,249,255));sampleDetail.Foreground=sampleTitle.Foreground;
        sampleScene.Effect=new BlurEffect{Radius=enabled?blur.Value:0,RenderingBias=RenderingBias.Performance};
    }
    StackPanel DockPage(){var p=Page();var state=dock.State;Section(p,"BEHAVIOR");
        Choice(p,"Visibility","Choose when the dock stays on screen.",new[]{"Never hide","Intelligent","Auto-hide"},state.Visibility=="Always"?"Never hide":state.Visibility,v=>state.Visibility=v=="Never hide"?"Always":v);
        Toggle(p,"Hide in fullscreen","Overrides visibility in fullscreen apps and games.",state.HideInFullscreen,v=>state.HideInFullscreen=v);
        var screens=Forms.Screen.AllScreens;var displayNames=screens.Select((x,i)=>"Display "+(i+1)+(x.Primary?" · Primary":"")).ToArray();Choice(p,"Display","Choose the monitor that hosts the dock.",displayNames,displayNames[Array.FindIndex(screens,x=>x.DeviceName==dock.Screen.DeviceName)],v=>state.Monitor=screens[Array.IndexOf(displayNames,v)].DeviceName);
        Section(p,"SIZE & MOVEMENT");Range(p,"Icon size","Base size before magnification.",state.IconSize,28,64,2," px",v=>state.IconSize=(int)v);
        Range(p,"Magnification","How much app icons grow under the pointer.",state.Magnification,1,1.7,.05,"×",v=>state.Magnification=v);
        Choice(p,"Preview columns","Maximum windows in each preview row.",new[]{1,2,3,4,5},state.PreviewColumns,v=>state.PreviewColumns=v);
        Toggle(p,"Reduce dock motion","Use immediate changes instead of animations.",state.ReducedMotion,v=>state.ReducedMotion=v);
        Section(p,"DOCK ITEMS");Toggle(p,"Downloads","Show the Downloads folder stack.",state.ShowDownloads,v=>state.ShowDownloads=v);Toggle(p,"Background apps","Show the system tray shortcut.",state.ShowBackgroundApps,v=>state.ShowBackgroundApps=v);Toggle(p,"Recycle Bin","Keep the Recycle Bin at the end of the dock.",state.ShowRecycleBin,v=>state.ShowRecycleBin=v);return p;}
    StackPanel OverviewPage(){var p=Page();Section(p,"WINDOWS");Choice(p,"Windows per row","Maximum columns in the window overview.",new[]{3,4,5},prefs.PreviewColumns,v=>prefs.PreviewColumns=v);Toggle(p,"Live game previews","Show live game content. Turn off to reduce GPU work.",prefs.LiveGamePreviews,v=>prefs.LiveGamePreviews=v);Section(p,"MOVEMENT");Range(p,"Animation duration","Time used for overview transitions.",prefs.MotionDuration,120,600,20," ms",v=>prefs.MotionDuration=(int)v);Toggle(p,"Reduce overview motion","Keep transitions immediate and distraction-free.",prefs.ReducedMotion,v=>prefs.ReducedMotion=v);return p;}
    StackPanel AppsPage(){var p=Page();Section(p,"SEARCH");ActionRow(p,"Portable applications","Add an executable or shortcut to search. Applies immediately.","Add app",()=>{var picker=new OpenFileDialog{Filter="Applications and shortcuts|*.exe;*.lnk"};if(picker.ShowDialog(this)==true){OverviewPreferences.AddPortable(picker.FileName);status.Text="Application added to search.";}});ActionRow(p,"Windows Settings index","Refresh the list of settings available on this PC.","Refresh",()=>{OverviewPreferences.RunOverview("--refresh-settings");status.Text="Settings index refresh requested.";});Section(p,"PIN TO DOCK");ActionRow(p,"Application or file","Add a dock pin now. Drag pins in the dock to reorder.","Add pin",()=>{var picker=new OpenFileDialog{Filter="Apps and shortcuts|*.exe;*.lnk|All files|*.*"};if(picker.ShowDialog(this)==true){dock.State.Pins.Add(DockState.FromPath(picker.FileName));dock.ApplySettings();status.Text="Dock pin added.";}});ActionRow(p,"Folder","Keep a folder within reach. Applies immediately.","Add folder",()=>{using(var picker=new Forms.FolderBrowserDialog()){if(picker.ShowDialog()==Forms.DialogResult.OK){dock.State.Pins.Add(DockState.FromPath(picker.SelectedPath));dock.ApplySettings();status.Text="Folder pinned.";}}});return p;}
    StackPanel SystemPage(){var p=Page();Section(p,"INTEGRATION");Toggle(p,"Start with Windows","Launch the dock when you sign in.",dock.State.Startup,v=>dock.State.Startup=v);Toggle(p,"Replace the bottom bar","Hide the existing bottom bar while Clean11 runs. Exiting restores it.",dock.State.TakeOver,v=>dock.State.TakeOver=v);Section(p,"TOOLS & ABOUT");ActionRow(p,"Power controls","Open the existing system power menu.","Open",()=>OverviewPreferences.RunOverview("--power-menu"));ActionRow(p,"Third-party licenses","Clean11 uses WPF UI and its supporting libraries.","View",()=>System.Diagnostics.Process.Start(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"licenses")));return p;}
    internal static void SetStartup(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(enabled)key.SetValue("Clean11Dock","\""+DockProgram.Executable+"\"");else key.DeleteValue("Clean11Dock",false);}}
}
