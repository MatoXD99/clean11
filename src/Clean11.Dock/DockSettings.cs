using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
using Ui=Wpf.Ui.Controls;

sealed class DockSettings : Window {
    readonly DockWindow dock;
    readonly OverviewPreferences prefs;
    readonly List<Action> changes=new List<Action>();
    readonly Dictionary<string,StackPanel> pages=new Dictionary<string,StackPanel>();
    readonly Dictionary<string,Ui.Button> navigation=new Dictionary<string,Ui.Button>();
    readonly Dictionary<string,Border> navigationFrames=new Dictionary<string,Border>();
    readonly Dictionary<string,string> descriptions=new Dictionary<string,string>();
    readonly ContentControl pageHost=new ContentControl();
    readonly Border shell;
    readonly DispatcherTimer glassTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(750)};
    ScrollViewer pageScroll;
    readonly TextBlock heading=new TextBlock(),subtitle=new TextBlock(),status=new TextBlock();
    readonly Ui.Button apply;
    bool ready,dirty,glassQueued,closed;string materialKey="",selectedPage="";
    readonly Brush ink=ActivityTheme.Brush("Text","#F1F2F5"),muted=ActivityTheme.Brush("Muted","#A6ABB5");
    Border sampleGlass;TextBlock sampleTitle,sampleDetail;Grid sampleScene;
    ComboBox material;Slider visibility,blur;Ui.ToggleSwitch glass;

    internal DockSettings(DockWindow owner){
        dock=owner;prefs=OverviewPreferences.Load();Clean11Fluent.Apply(this);
        Title="Clean11 Settings";Width=1080;Height=820;MinWidth=900;MinHeight=600;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;ResizeMode=ResizeMode.CanResizeWithGrip;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");FontSize=14;
        Background=Brushes.Transparent;SetResourceReference(ForegroundProperty,"Text");
        var root=new Grid{Margin=new Thickness(24)};
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        // Include the outer top/side padding in the caption's hit area. Child
        // margins keep the visible title and buttons in their original places.
        var caption=new Grid{Background=Brushes.Transparent,Margin=new Thickness(-24,-24,-24,24)};
        caption.ColumnDefinitions.Add(new ColumnDefinition());caption.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var captionText=Label("Clean11 Settings",24,false);captionText.Margin=new Thickness(24,24,0,0);captionText.FontWeight=FontWeights.SemiBold;captionText.VerticalAlignment=VerticalAlignment.Center;caption.Children.Add(captionText);
        caption.MouseLeftButtonDown+=(s,e)=>{if(e.OriginalSource!=caption&&e.OriginalSource!=captionText)return;if(e.ClickCount==2)WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else if(e.ButtonState==System.Windows.Input.MouseButtonState.Pressed)DragMove();};
        var captionButtons=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,24,24,0)};
        var minimize=Clean11Fluent.IconButton("Minimize","Subtract24",()=>WindowState=WindowState.Minimized);Clean11Fluent.Quiet(minimize);captionButtons.Children.Add(minimize);
        var close=Clean11Fluent.IconButton("Close settings","Dismiss24",Close);Clean11Fluent.Quiet(close);captionButtons.Children.Add(close);Grid.SetColumn(captionButtons,1);caption.Children.Add(captionButtons);root.Children.Add(caption);
        var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(190)});layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});layout.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetRow(layout,1);root.Children.Add(layout);
        var sidebar=new Grid();sidebar.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});sidebar.RowDefinitions.Add(new RowDefinition());sidebar.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var navTitle=Label("YOUR DESKTOP",10,true);navTitle.FontWeight=FontWeights.SemiBold;navTitle.Margin=new Thickness(14,16,12,14);sidebar.Children.Add(navTitle);
        var nav=new StackPanel{Margin=new Thickness(8,0,8,0)};var navScroll=new ScrollViewer{Content=nav,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(navScroll,1);sidebar.Children.Add(navScroll);
        var credit=Label("Made with WPF UI",11,true);credit.Margin=new Thickness(14,18,10,16);Grid.SetRow(credit,2);sidebar.Children.Add(credit);
        var navFrame=new Border{Child=sidebar,CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1)};navFrame.SetResourceReference(Border.BackgroundProperty,"Card");navFrame.SetResourceReference(Border.BorderBrushProperty,"Divider");layout.Children.Add(navFrame);
        var main=new Grid();main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition());Grid.SetColumn(main,2);layout.Children.Add(main);
        var titleArea=new StackPanel{Margin=new Thickness(0,4,8,22)};
        heading.FontSize=26;heading.FontWeight=FontWeights.SemiBold;heading.SetResourceReference(TextBlock.ForegroundProperty,"Text");
        subtitle.SetResourceReference(TextBlock.ForegroundProperty,"Muted");subtitle.FontSize=14;subtitle.TextWrapping=TextWrapping.Wrap;subtitle.Margin=new Thickness(0,7,0,0);titleArea.Children.Add(heading);titleArea.Children.Add(subtitle);main.Children.Add(titleArea);
        pageScroll=new ScrollViewer{Content=pageHost,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,4,8)};Grid.SetRow(pageScroll,1);main.Children.Add(pageScroll);
        var footer=new Grid{Margin=new Thickness(214,20,0,0)};footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        status.SetResourceReference(TextBlock.ForegroundProperty,"Muted");status.Text="All changes saved · Your desktop, your way.";status.TextWrapping=TextWrapping.Wrap;status.FontSize=12;status.VerticalAlignment=VerticalAlignment.Center;status.Margin=new Thickness(0,0,20,0);footer.Children.Add(status);
        apply=Clean11Fluent.Button("Apply changes",Save,true);apply.IsEnabled=false;Grid.SetColumn(apply,1);footer.Children.Add(apply);Grid.SetRow(footer,2);root.Children.Add(footer);
        shell=new Border{BorderThickness=new Thickness(1),BorderBrush=FrostedMaterial.Edge(),CornerRadius=new CornerRadius(ActivityTheme.GlassRadius),Child=root};Content=shell;
        shell.SizeChanged+=(s,e)=>shell.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,shell.ActualWidth),Math.Max(0,shell.ActualHeight)),ActivityTheme.GlassRadius,ActivityTheme.GlassRadius);
        AddPage(nav,"Appearance","Materials, clarity, and a little personality.",Appearance());
        AddPage(nav,"Wallpaper","One horizon. A different light for every part of your day.",new HorizonSettingsPage());
        AddPage(nav,"Dock","The apps you keep close. The space you keep clear.",DockPage());
        AddPage(nav,"Top Bar","Everyday controls, right where you need them.",TopBarPage());
        AddPage(nav,"Widgets","Useful little spaces, styled your way.",new WidgetGalleryPage());
        AddPage(nav,"Overview","Your windows, with room to breathe.",OverviewPage());
        AddPage(nav,"Apps & search","Find your next thought, file, or favorite app.",AppsPage());
        AddPage(nav,"System","A few essentials to make Clean11 feel at home.",SystemPage());
        ready=true;SelectPage("Appearance");UpdateSample();
        glassTimer.Tick+=(s,e)=>UpdateGlass();LocationChanged+=(s,e)=>QueueGlass();SizeChanged+=(s,e)=>QueueGlass();StateChanged+=(s,e)=>QueueGlass();
        IsVisibleChanged+=(s,e)=>{if(IsVisible){glassTimer.Start();QueueGlass();}else glassTimer.Stop();};
        Closed+=(s,e)=>{closed=true;glassTimer.Stop();};
        SourceInitialized+=(s,e)=>{var h=new WindowInteropHelper(this).Handle;HwndSource.FromHwnd(h).AddHook(SettingsMessages);};
        Loaded+=(s,e)=>{var area=SystemParameters.WorkArea;MinHeight=Math.Min(600,area.Height);MinWidth=Math.Min(900,area.Width);MaxHeight=area.Height;MaxWidth=area.Width;QueueGlass();};
        Closing+=(s,e)=>{if(!dirty)return;var answer=MessageBox.Show(this,"Apply your changes before closing?","Unsaved settings",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);if(answer==MessageBoxResult.Cancel)e.Cancel=true;else if(answer==MessageBoxResult.Yes){Save();e.Cancel=dirty;}};
    }
    IntPtr SettingsMessages(IntPtr window,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x0047||message==0x02E0||message==0x0232)QueueGlass();return IntPtr.Zero;}
    void QueueGlass(){if(closed||glassQueued)return;glassQueued=true;Dispatcher.BeginInvoke(DispatcherPriority.Render,(Action)(()=>{glassQueued=false;if(!closed)UpdateGlass();}));}
    void UpdateGlass(){
        if(closed||!IsVisible||WindowState==WindowState.Minimized)return;
        var bounds=FrostedMaterial.BoundsOnScreen(shell);if(bounds.IsEmpty)return;
        var screen=Forms.Screen.FromPoint(new System.Drawing.Point(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2));
        var p=OverviewPreferences.Appearance;string key=p.Glass+"|"+p.Strength+"|"+p.Radius+"|"+p.Light+"|"+bounds+"|"+screen.Bounds+"|"+(DateTime.UtcNow.Ticks/TimeSpan.FromSeconds(5).Ticks);
        if(key==materialKey)return;materialKey=key;Clean11Fluent.RefreshPalette(this);shell.BorderBrush=FrostedMaterial.Edge();
        shell.Background=FrostedMaterial.PanelBrush(bounds,screen.Bounds,p.Glass);
    }
    TextBlock Label(string text,double size,bool secondary){var textBlock=new TextBlock{Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap};textBlock.SetResourceReference(TextBlock.ForegroundProperty,secondary?"Muted":"Text");return textBlock;}
    void AddPage(Panel nav,string name,string description,StackPanel content){
        pages[name]=content;descriptions[name]=description;
        var button=Clean11Fluent.Button(name,()=>SelectPage(name),false);Clean11Fluent.Quiet(button);
        button.HorizontalContentAlignment=HorizontalAlignment.Left;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Height=44;button.Padding=new Thickness(12,0,10,0);
        string symbol=name=="Appearance"?"WeatherSunny24":name=="Wallpaper"?"Image24":name=="Dock"?"Apps24":name=="Top Bar"?"Desktop24":name=="Widgets"?"Grid24":name=="Overview"?"WindowApps24":name=="Apps & search"?"Search24":"Settings24";
        var label=new StackPanel{Orientation=Orientation.Horizontal};var icon=Clean11Fluent.Icon(symbol,18);icon.Margin=new Thickness(0,0,10,0);label.Children.Add(icon);var text=Label(name,13,false);text.VerticalAlignment=VerticalAlignment.Center;label.Children.Add(text);button.Content=label;
        System.Windows.Automation.AutomationProperties.SetName(button,name);
        var frame=new Border{CornerRadius=new CornerRadius(12),Margin=new Thickness(0,0,0,5),Child=button};navigation[name]=button;navigationFrames[name]=frame;nav.Children.Add(frame);
    }
    internal void SelectPage(string name){
        if(!pages.ContainsKey(name))return;
        selectedPage=name;apply.Visibility=(name=="Widgets"||name=="Wallpaper")&&!dirty?Visibility.Collapsed:Visibility.Visible;if(!dirty)status.Text=name=="Wallpaper"?"Wallpaper changes apply immediately - Previewing stays local to this page":name=="Widgets"?"Widgets save automatically - Stored on this PC":"All changes saved - Your desktop, your way.";
        heading.Text=name=="Widgets"?"Desktop widgets":name;subtitle.Text=descriptions[name];
        if(pageHost.Content!=pages[name]){pageHost.Content=pages[name];if(pageScroll!=null)pageScroll.ScrollToTop();}
        foreach(var pair in navigation){pair.Value.FontWeight=pair.Key==name?FontWeights.SemiBold:FontWeights.Normal;if(pair.Key==name)navigationFrames[pair.Key].SetResourceReference(Border.BackgroundProperty,"Selected");else navigationFrames[pair.Key].Background=Brushes.Transparent;}
    }
    void Changed(){if(!ready)return;dirty=true;apply.IsEnabled=true;apply.Visibility=Visibility.Visible;status.Text="Unsaved settings changes";}
    void Save(){
        try{bool previousStartup=dock.State.Startup;foreach(var change in changes)change();SetStartup(dock.State.Startup,previousStartup!=dock.State.Startup);prefs.Save();Clean11Fluent.RefreshPalette(this);UpdateGlass();dock.CloseFlyout();dock.ApplySettings();dock.RefreshGlass();TrayAppearance.Sync();dirty=false;apply.IsEnabled=false;apply.Visibility=selectedPage=="Widgets"||selectedPage=="Wallpaper"?Visibility.Collapsed:Visibility.Visible;status.Text="Saved. Shared appearance updated.";}
        catch(Exception error){status.Text="Some changes could not be saved. Please try again.";DockLog.Write("Settings save failed: "+error.GetType().Name);dirty=true;apply.IsEnabled=true;}
    }
    StackPanel Page(){return new StackPanel{HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,12,0)};}
    void Section(Panel panel,string title){var text=Label(title,12,true);text.FontWeight=FontWeights.SemiBold;text.Margin=new Thickness(2,panel.Children.Count==0?0:20,0,10);panel.Children.Add(text);}
    Border Card(UIElement content){var card=new Border{BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Padding=new Thickness(18),Margin=new Thickness(0,0,0,10),Child=content};card.SetResourceReference(Border.BackgroundProperty,"Card");card.SetResourceReference(Border.BorderBrushProperty,"Divider");return card;}
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
        sampleScene=new Grid{Height=138,ClipToBounds=true,Background=new LinearGradientBrush(Color.FromRgb(40,64,86),Color.FromRgb(76,83,108),25)};
        var wave=new System.Windows.Shapes.Ellipse{Width=300,Height=100,Fill=new SolidColorBrush(Color.FromRgb(151,173,183)),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(-40,15,0,0)};sampleScene.Children.Add(wave);
        var sampleText=new StackPanel{Margin=new Thickness(20)};sampleTitle=Label("A calmer desktop",18,false);sampleTitle.FontWeight=FontWeights.SemiBold;sampleDetail=Label("Clean11 glass · material preview",12,true);sampleDetail.Margin=new Thickness(0,5,0,0);sampleText.Children.Add(sampleTitle);sampleText.Children.Add(sampleDetail);
        sampleGlass=new Border{CornerRadius=new CornerRadius(ActivityTheme.GlassRadius),BorderThickness=new Thickness(1),Margin=new Thickness(22,20,22,20),Child=sampleText};
        var sample=new Grid();sample.Children.Add(sampleScene);sample.Children.Add(sampleGlass);var frame=Card(sample);frame.Padding=new Thickness(0);frame.SizeChanged+=(s,e)=>frame.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,frame.ActualWidth),Math.Max(0,frame.ActualHeight)),ActivityTheme.RowRadius,ActivityTheme.RowRadius);panel.Children.Add(frame);
        Section(panel,"MATERIAL");
        glass=Toggle(panel,"Frosted glass","One material for Settings, dock, top bar, search, and window previews.",prefs.Glass,v=>prefs.Glass=v);
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
    StackPanel TopBarPage(){var p=Page();var state=dock.State;Section(p,"BEHAVIOR");
        Toggle(p,"Show top bar","Keep Clean11 controls along the top of your display.",state.TopBarEnabled,v=>state.TopBarEnabled=v);
        Toggle(p,"Hide in fullscreen","Hide the bar in fullscreen apps and games.",state.TopBarHideInFullscreen,v=>state.TopBarHideInFullscreen=v);
        Toggle(p,"Reserve screen space","Keep maximized windows below the bar.",state.TopBarReserveSpace,v=>state.TopBarReserveSpace=v);
        var screens=Forms.Screen.AllScreens;var names=screens.Select((x,i)=>"Display "+(i+1)+(x.Primary?" · Primary":"")).ToArray();int selected=Array.FindIndex(screens,x=>x.DeviceName==state.TopBarMonitor);if(selected<0)selected=Array.FindIndex(screens,x=>x.Primary);Choice(p,"Display","Choose the monitor for the top bar.",names,names[Math.Max(0,selected)],v=>state.TopBarMonitor=screens[Array.IndexOf(names,v)].DeviceName);
        Section(p,"MODULES");Toggle(p,"Active app","Show the focused app and its window actions.",state.TopBarShowActiveApp,v=>state.TopBarShowActiveApp=v);Toggle(p,"Network status","Show connection status and a shortcut to network settings.",state.TopBarShowNetwork,v=>state.TopBarShowNetwork=v);Toggle(p,"Battery","Show battery status on devices with a battery.",state.TopBarShowBattery,v=>state.TopBarShowBattery=v);Toggle(p,"Clock seconds","Include seconds in the clock.",state.TopBarShowSeconds,v=>state.TopBarShowSeconds=v);
        Section(p,"APPEARANCE");var note=Label("The top bar and its menus use the shared material, glass visibility, and blur radius from Appearance.",13,true);note.TextWrapping=TextWrapping.Wrap;p.Children.Add(Card(note));return p;}
    StackPanel OverviewPage(){var p=Page();Section(p,"WINDOWS");Choice(p,"Windows per row","Maximum columns in the window overview.",new[]{3,4,5},prefs.PreviewColumns,v=>prefs.PreviewColumns=v);Toggle(p,"Live game previews","Show live game content. Turn off to reduce GPU work.",prefs.LiveGamePreviews,v=>prefs.LiveGamePreviews=v);Section(p,"MOVEMENT");Range(p,"Animation duration","Time used for overview transitions.",prefs.MotionDuration,120,600,20," ms",v=>prefs.MotionDuration=(int)v);Toggle(p,"Reduce overview motion","Keep transitions immediate and distraction-free.",prefs.ReducedMotion,v=>prefs.ReducedMotion=v);return p;}
    StackPanel AppsPage(){var p=Page();Section(p,"SEARCH");ActionRow(p,"Portable applications","Add an executable or shortcut to search. Applies immediately.","Add app",()=>{var picker=new OpenFileDialog{Filter="Applications and shortcuts|*.exe;*.lnk"};if(picker.ShowDialog(this)==true){OverviewPreferences.AddPortable(picker.FileName);status.Text="Application added to search.";}});ActionRow(p,"Windows Settings index","Refresh the list of settings available on this PC.","Refresh",()=>{OverviewPreferences.RunOverview("--refresh-settings");status.Text="Settings index refresh requested.";});Section(p,"PIN TO DOCK");ActionRow(p,"Application or file","Add a dock pin now. Drag pins in the dock to reorder.","Add pin",()=>{var picker=new OpenFileDialog{Filter="Apps and shortcuts|*.exe;*.lnk|All files|*.*"};if(picker.ShowDialog(this)==true){dock.State.Pins.Add(DockState.FromPath(picker.FileName));dock.ApplySettings();status.Text="Dock pin added.";}});ActionRow(p,"Folder","Keep a folder within reach. Applies immediately.","Add folder",()=>{using(var picker=new Forms.FolderBrowserDialog()){if(picker.ShowDialog()==Forms.DialogResult.OK){dock.State.Pins.Add(DockState.FromPath(picker.SelectedPath));dock.ApplySettings();status.Text="Folder pinned.";}}});return p;}
    StackPanel SystemPage(){var p=Page();Section(p,"INTEGRATION");Toggle(p,"Start with Windows","Launch Clean11 when you sign in.",dock.State.Startup,v=>dock.State.Startup=v);Toggle(p,"Replace the bottom bar","Hide the existing bottom bar while Clean11 runs. Exiting restores it.",dock.State.TakeOver,v=>dock.State.TakeOver=v);Section(p,"TOOLS & ABOUT");ActionRow(p,"Power controls","Open available power controls or Windows power settings.","Open",()=>OverviewPreferences.RunOverview("--power-menu"));ActionRow(p,"Third-party licenses","Clean11 uses WPF UI and its supporting libraries.","View",()=>System.Diagnostics.Process.Start(Clean11Paths.Licenses));return p;}
    internal static void SetStartup(bool enabled,bool explicitlyEnable=false){
        bool bundled=System.IO.File.Exists(Clean11Paths.Launcher);
        Clean11Startup.SetEnabled(enabled,bundled?Clean11Paths.Launcher:DockProgram.Executable,bundled?"--background --sign-in":"--startup",explicitlyEnable);
    }
}
