using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

sealed class WidgetTask {public string Id=Guid.NewGuid().ToString("N"),Text="";public bool Done;}
sealed class DesktopWidgetState {
    public string Id=Guid.NewGuid().ToString("N"),Kind="clock",Size="Small",Monitor="",Note="";
    public double X=24,Y=24;public bool Visible=true,TwentyFourHour=true;
    public List<WidgetTask> Tasks=new List<WidgetTask>();
}
sealed class DesktopWidgetStore {
    public int Version=1;public bool Enabled=true;public List<DesktopWidgetState> Widgets=new List<DesktopWidgetState>();
    internal static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Clean11","Widgets");
    internal static readonly string FileName=Path.Combine(Root,"widgets.json");
    internal static readonly string[] Kinds={"clock","calendar","todo","notes"};
    internal static DesktopWidgetStore Load(){
        var json=new JavaScriptSerializer{MaxJsonLength=4194304};DesktopWidgetStore store=null;
        if(File.Exists(FileName)){
            try{store=json.Deserialize<DesktopWidgetStore>(File.ReadAllText(FileName));if(store==null||store.Version!=1)throw new InvalidDataException("Unsupported widget data version.");}
            catch(Exception error){
                if(!(error is IOException)&&!(error is ArgumentException)&&!(error is InvalidOperationException)&&!(error is InvalidDataException))throw;
                // Preserve the unreadable file before trying the previous atomic save.
                File.Copy(FileName,FileName+".unreadable-"+DateTime.UtcNow.Ticks);
                if(!File.Exists(FileName+".bak"))throw new InvalidDataException("Widget data could not be read. The original has been preserved.",error);
                store=json.Deserialize<DesktopWidgetStore>(File.ReadAllText(FileName+".bak"));
                if(store==null||store.Version!=1)throw new InvalidDataException("Widget backup could not be read.");
                File.Move(FileName,FileName+".recovered-"+DateTime.UtcNow.Ticks);
            }
        }
        if(store==null){store=new DesktopWidgetStore();for(int i=0;i<Kinds.Length;i++)store.Widgets.Add(new DesktopWidgetState{Kind=Kinds[i],Size=i>1?"Medium":"Small",X=Math.Max(24,Forms.Screen.PrimaryScreen.WorkingArea.Width-680)+(i%2)*336,Y=24+(i/2)*350,Monitor=Forms.Screen.PrimaryScreen.DeviceName});}
        store.Widgets=(store.Widgets??new List<DesktopWidgetState>()).Where(x=>x!=null).ToList();
        foreach(var item in store.Widgets){if(!Kinds.Contains(item.Kind))item.Visible=false;if(!new[]{"Small","Medium","Large"}.Contains(item.Size))item.Size="Small";if(Double.IsNaN(item.X)||Double.IsInfinity(item.X))item.X=24;if(Double.IsNaN(item.Y)||Double.IsInfinity(item.Y))item.Y=24;item.Tasks=(item.Tasks??new List<WidgetTask>()).Where(x=>x!=null).ToList();item.Note=item.Note??"";}
        return store;
    }
    internal void Save(){Directory.CreateDirectory(Root);string temp=FileName+".tmp";File.WriteAllText(temp,new JavaScriptSerializer{MaxJsonLength=4194304}.Serialize(this));if(File.Exists(FileName))File.Replace(temp,FileName,FileName+".bak");else File.Move(temp,FileName);}
}

static class DesktopWidgetBridge {
    internal static string Executable {get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Clean11.Widgets.exe");}}
    internal static void Start(bool gallery,bool startup=false){
        if(gallery){OpenSettings();return;}
        if(!File.Exists(Executable)){DockLog.Write("Desktop widgets executable is not installed.");return;}
        Process.Start(new ProcessStartInfo(Executable,"--background "+(startup?"--startup ":"")+"--parent "+Process.GetCurrentProcess().Id){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});
    }
    internal static void OpenSettings(){var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Clean11.Dock.exe");if(File.Exists(path))Process.Start(new ProcessStartInfo(path,"--widgets"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});}
    internal static void Stop(){try{using(var stop=EventWaitHandle.OpenExisting("Local\\Clean11WidgetsStop"))stop.Set();}catch(WaitHandleCannotBeOpenedException){}}
}

// Bottom-order behavior adapted from DeskWidgets/core.py.
// Copyright (c) 2026 VaticUI, MIT. See licenses/DESKWIDGETS-LICENSE.txt.
// Do not adopt DeskWidgets' cross-process Progman ownership. It implicitly
// attaches this UI thread (including any settings UI) to Explorer's input queue.
// Only our own HWNDs are positioned; application and shell owners are unchanged.
static class WidgetDesktop {
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string name,string title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string name,string title);
    [StructLayout(LayoutKind.Sequential)]struct Position {public IntPtr Window,After;public int X,Y,Width,Height;public uint Flags;}
    static IntPtr DesktopSurface(){
        IntPtr surface=IntPtr.Zero;
        DockNative.EnumWindows((window,p)=>{if(FindWindowEx(window,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero){surface=window;return false;}return true;},IntPtr.Zero);
        return surface!=IntPtr.Zero?surface:FindWindow("Progman",null);
    }
    static IntPtr BehindApplications(IntPtr window){
        IntPtr desktop=DesktopSurface();if(desktop==IntPtr.Zero)return new IntPtr(1);
        // Insert immediately above the desktop, not HWND_BOTTOM (which can put
        // an unowned card underneath the wallpaper). Never move the desktop.
        IntPtr previous=DockNative.GetWindow(desktop,3); // GW_HWNDPREV
        if(previous==window)previous=DockNative.GetWindow(previous,3);
        return previous==IntPtr.Zero?new IntPtr(1):previous;
    }
    internal static void Pin(IntPtr window){
        if(window==IntPtr.Zero||!DockNative.IsWindow(window))return;
        IntPtr owner=DockNative.GetWindow(window,4);uint pid;
        if(owner!=IntPtr.Zero){DockNative.GetWindowThreadProcessId(owner,out pid);if(pid!=(uint)Process.GetCurrentProcess().Id)DockNative.SetLong(window,-8,IntPtr.Zero);}
        DockNative.SetWindowPos(window,BehindApplications(window),0,0,0,0,0x213);
    }
    internal static IntPtr Messages(IntPtr window,int message,IntPtr w,IntPtr l,ref bool handled){
        if(message==0x46&&l!=IntPtr.Zero){var position=(Position)Marshal.PtrToStructure(l,typeof(Position));if((position.Flags&4)==0){position.After=BehindApplications(window);position.Flags|=0x200;Marshal.StructureToPtr(position,l,false);}}
        return IntPtr.Zero;
    }
}

sealed class DesktopWidgetHost : IDisposable {
    internal readonly DesktopWidgetStore Store;
    internal readonly List<DesktopWidgetWindow> Windows=new List<DesktopWidgetWindow>();
    internal bool Editing;internal string SaveError="";
    readonly DispatcherTimer saveTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)},tick=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
    readonly WidgetControlServer controls;readonly int parent;int seconds;bool dirty,disposed;StartupSceneMotion startupMotion;double startupProgress=1;
    internal DesktopWidgetHost(int owner,bool animateStartup){
        parent=owner;Store=DesktopWidgetStore.Load();startupProgress=animateStartup&&!OverviewPreferences.Load().ReducedMotion?0:1;saveTimer.Tick+=(s,e)=>Flush();tick.Tick+=(s,e)=>Tick();
        foreach(var state in Store.Widgets.Where(x=>DesktopWidgetStore.Kinds.Contains(x.Kind)))Create(state);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplaysChanged;controls=new WidgetControlServer(this);tick.Start();Tick();Changed();
        overviewScene=new OverviewSceneReceiver(SetOverviewProgress,()=>OverviewPreferences.Load().ReducedMotion);
        if(startupProgress<1){startupMotion=new StartupSceneMotion(SetStartupProgress,()=>startupMotion=null,520);startupMotion.Start();}
    }
    void DisplaysChanged(object sender,EventArgs args){if(disposed)return;try{if(!Application.Current.Dispatcher.HasShutdownStarted)Application.Current.Dispatcher.BeginInvoke((Action)(()=>{if(!disposed)foreach(var window in Windows)window.Place();}));}catch(InvalidOperationException){}}
    void Create(DesktopWidgetState state){var window=new DesktopWidgetWindow(this,state);Windows.Add(window);new WindowInteropHelper(window).EnsureHandle();window.Place();window.SetStartupProgress(startupProgress);window.SetOverviewProgress(overviewProgress);}
    internal void Add(string kind){if(!DesktopWidgetStore.Kinds.Contains(kind))return;var state=new DesktopWidgetState{Kind=kind,X=32+(Store.Widgets.Count%5)*32,Y=40+(Store.Widgets.Count%5)*32};Store.Widgets.Add(state);Create(state);Tick();Changed();}
    internal void Changed(){dirty=true;saveTimer.Stop();saveTimer.Start();}
    internal void Flush(){saveTimer.Stop();if(!dirty)return;try{Store.Save();dirty=false;SaveError="";}catch(Exception error){SaveError="Could not save widgets. Check available disk space and folder access.";DockLog.Write("Widget save failed: "+error.GetType().Name);}}
    internal void SetEditing(bool value){Editing=value;foreach(var window in Windows)window.EditChanged();}
    internal void SetEnabled(bool value){Store.Enabled=value;Changed();Tick();}
    internal void Toggle(DesktopWidgetState state){state.Visible=!state.Visible;Changed();Tick();}
    internal void Gallery(){DesktopWidgetBridge.OpenSettings();}
    OverviewSceneReceiver overviewScene;double overviewProgress;
    void SetOverviewProgress(double progress){overviewProgress=progress;foreach(var window in Windows)window.SetOverviewProgress(progress);}
    void SetStartupProgress(double progress){startupProgress=Math.Max(0,Math.Min(1,progress));foreach(var window in Windows)window.SetStartupProgress(startupProgress);}
    void Tick(){
        if(disposed)return;
        if(parent>0){try{using(var process=Process.GetProcessById(parent)){if(process.HasExited){Application.Current.Shutdown();return;}}}catch(ArgumentException){Application.Current.Shutdown();return;}}
        seconds++;foreach(var window in Windows){bool show=Store.Enabled&&window.State.Visible&&!DockNative.Fullscreen(window.Screen,IntPtr.Zero);if(show&&!window.IsVisible){window.Show();window.Place();}else if(!show&&window.IsVisible)window.Hide();if(show)window.Tick(seconds%5==0);}
    }
    public void Dispose(){if(disposed)return;disposed=true;if(startupMotion!=null){startupMotion.Dispose();startupMotion=null;}if(overviewScene!=null)overviewScene.Dispose();tick.Stop();saveTimer.Stop();Flush();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplaysChanged;controls.Dispose();foreach(var window in Windows)window.Close();}
}

sealed class DesktopWidgetWindow : Window {
    double startupProgress=1,overviewProgress;
    internal void SetOverviewProgress(double progress){overviewProgress=progress;ApplyMotionOpacity();if(progress>0&&ContextMenu!=null)ContextMenu.IsOpen=false;}
    internal void SetStartupProgress(double progress){startupProgress=Math.Max(0,Math.Min(1,progress));ApplyMotionOpacity();}
    void ApplyMotionOpacity(){Opacity=startupProgress*(1-overviewProgress);IsHitTestVisible=startupProgress>=1&&overviewProgress==0;}
    internal readonly DesktopWidgetState State;readonly DesktopWidgetHost host;
    readonly Border frame;readonly Grid body=new Grid();readonly StackPanel heading=new StackPanel();
    readonly TextBlock name;TextBlock time,date;TextBox note;IntPtr handle;double dpi=1;string materialKey="",clockKey="";DateTime month=DateTime.Today;DateTime renderedDate;
    internal Forms.Screen Screen {get{return Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName==State.Monitor)??Forms.Screen.PrimaryScreen;}}
    internal DesktopWidgetWindow(DesktopWidgetHost owner,DesktopWidgetState state){
        host=owner;State=state;Clean11Fluent.Apply(this);Title="Clean11 "+DisplayName(state.Kind)+" widget";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=Environment.CommandLine.Contains("--inspect-ui");ShowActivated=false;Topmost=false;FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");
        var layout=new Grid{Margin=new Thickness(18)};layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition());
        var header=new Grid{Margin=new Thickness(0,0,0,14)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        name=Text(DisplayName(State.Kind).ToUpperInvariant(),11,true);name.FontWeight=FontWeights.SemiBold;header.Children.Add(name);
        var menu=SmallButton("⋯",()=>{BuildMenu();ContextMenu.PlacementTarget=this;ContextMenu.IsOpen=true;});menu.ToolTip="Widget options";Grid.SetColumn(menu,1);header.Children.Add(menu);
        header.MouseLeftButtonDown+=(s,e)=>{if(!host.Editing||e.OriginalSource is Button)return;try{DragMove();Snap();SavePosition();}catch(InvalidOperationException){}e.Handled=true;};
        layout.Children.Add(header);Grid.SetRow(body,1);layout.Children.Add(body);
        frame=new Border{CornerRadius=new CornerRadius(ActivityTheme.GlassRadius),BorderThickness=new Thickness(1),BorderBrush=FrostedMaterial.Edge(),Child=layout};Content=frame;
        ContextMenu=new ContextMenu();ContextMenuOpening+=(s,e)=>BuildMenu();
        SourceInitialized+=(s,e)=>{handle=new WindowInteropHelper(this).Handle;dpi=Math.Max(96,DockNative.GetDpiForWindow(handle))/96.0;if(!Environment.CommandLine.Contains("--inspect-ui"))DockNative.SetLong(handle,-20,new IntPtr(DockNative.GetLong(handle,-20).ToInt64()|0x80));if(!Environment.CommandLine.Contains("--inspect-ui")){HwndSource.FromHwnd(handle).AddHook(WidgetDesktop.Messages);WidgetDesktop.Pin(handle);}};
        LocationChanged+=(s,e)=>Material();SizeChanged+=(s,e)=>Material();Loaded+=(s,e)=>{Place();Material();};
        Build();
    }
    internal static string DisplayName(string kind){return String.IsNullOrWhiteSpace(kind)?"Unknown":kind=="todo"?"To-do":CultureInfo.CurrentCulture.TextInfo.ToTitleCase(kind);}
    internal static TextBlock Text(string value,double size,bool secondary=false){var label=new TextBlock{Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};label.SetResourceReference(TextBlock.ForegroundProperty,secondary?"Muted":"Text");return label;}
    internal static Wpf.Ui.Controls.Button SmallButton(string text,Action action){var b=Clean11Fluent.Button(text,action,false);Clean11Fluent.Quiet(b);b.MinHeight=28;b.Padding=new Thickness(7,3,7,3);return b;}
    internal void EditChanged(){name.Text=(host.Editing?"↔  ":"")+DisplayName(State.Kind).ToUpperInvariant();name.ToolTip=host.Editing?"Drag this header to move the widget":"Open the widget gallery to arrange widgets";}
    void BuildMenu(){var menu=ContextMenu;menu.Items.Clear();FrostedMaterial.StyleMenu(menu,OverviewPreferences.Appearance.Glass);foreach(var size in new[]{"Small","Medium","Large"}){string selected=size;var item=new MenuItem{Header=size,IsCheckable=true,IsChecked=State.Size==size};item.Click+=(s,e)=>{State.Size=selected;Build();Place();host.Changed();};menu.Items.Add(item);}if(State.Kind=="clock"){var format=new MenuItem{Header="24-hour clock",IsCheckable=true,IsChecked=State.TwentyFourHour};format.Click+=(s,e)=>{State.TwentyFourHour=!State.TwentyFourHour;clockKey="";Tick(false);host.Changed();};menu.Items.Add(format);}menu.Items.Add(new Separator());var edit=new MenuItem{Header="Edit widgets…"};edit.Click+=(s,e)=>{host.SetEditing(true);host.Gallery();};menu.Items.Add(edit);var hide=new MenuItem{Header="Remove from desktop (keep content)"};hide.Click+=(s,e)=>host.Toggle(State);menu.Items.Add(hide);}
    internal void ResizeCard(){Build();Place();}
    void Build(){
        double w=State.Size=="Small"?260:State.Size=="Medium"?320:380;Width=w;Height=State.Kind=="clock"?w*.73:State.Kind=="calendar"?w+30:State.Kind=="todo"?w+20:w;
        body.Children.Clear();time=null;date=null;
        if(State.Kind=="clock"){var stack=new StackPanel{VerticalAlignment=VerticalAlignment.Center};time=Text("",State.Size=="Small"?44:58);time.FontWeight=FontWeights.SemiBold;date=Text("",13,true);date.Margin=new Thickness(0,10,0,0);stack.Children.Add(time);stack.Children.Add(date);body.Children.Add(stack);clockKey="";}
        else if(State.Kind=="calendar")Calendar();else if(State.Kind=="todo")Tasks();else Notes();
        EditChanged();Tick(false);
    }
    void Notes(){note=new Wpf.Ui.Controls.TextBox{PlaceholderText="Write a note…",ClearButtonEnabled=false,Text=State.Note,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=Brushes.Transparent,BorderThickness=new Thickness(0),FontSize=15,Padding=new Thickness(0),MaxLength=20000};note.SetResourceReference(TextBox.ForegroundProperty,"Text");System.Windows.Automation.AutomationProperties.SetName(note,"Note text");note.TextChanged+=(s,e)=>{State.Note=note.Text;host.Changed();};body.Children.Add(note);}
    void Tasks(){
        var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var list=new StackPanel();grid.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        if(State.Tasks.Count==0)list.Children.Add(Text("A little space for what matters.\nAdd your first task below.",14,true));
        foreach(var task in State.Tasks){var item=task;var row=new Grid{Margin=new Thickness(0,0,0,10)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var check=new CheckBox{IsChecked=item.Done,Content=Text(item.Text,14),VerticalContentAlignment=VerticalAlignment.Center};check.Checked+=(s,e)=>{item.Done=true;host.Changed();};check.Unchecked+=(s,e)=>{item.Done=false;host.Changed();};row.Children.Add(check);var remove=SmallButton("×",()=>{State.Tasks.Remove(item);host.Changed();Build();});remove.ToolTip="Delete task";Grid.SetColumn(remove,1);row.Children.Add(remove);list.Children.Add(row);}
        var addRow=new Grid{Margin=new Thickness(0,10,0,0)};addRow.ColumnDefinitions.Add(new ColumnDefinition());addRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var input=new Wpf.Ui.Controls.TextBox{PlaceholderText="Add a task…",MaxLength=300,MinWidth=0};System.Windows.Automation.AutomationProperties.SetName(input,"New task");Action add=()=>{string text=input.Text.Trim();if(text.Length==0||State.Tasks.Count>=200)return;State.Tasks.Add(new WidgetTask{Text=text});host.Changed();Build();};input.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){add();e.Handled=true;}};addRow.Children.Add(input);var button=SmallButton("+",add);button.ToolTip="Add task";Grid.SetColumn(button,1);addRow.Children.Add(button);Grid.SetRow(addRow,1);grid.Children.Add(addRow);body.Children.Add(grid);
    }
    void Calendar(){
        body.Children.Clear();var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition());var header=new Grid{Margin=new Thickness(0,0,0,10)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var title=Text(month.ToString("MMMM yyyy",CultureInfo.CurrentCulture),16);title.FontWeight=FontWeights.SemiBold;header.Children.Add(title);var prev=SmallButton("‹",()=>{month=month.AddMonths(-1);Calendar();});prev.ToolTip="Previous month";Grid.SetColumn(prev,1);header.Children.Add(prev);var next=SmallButton("›",()=>{month=month.AddMonths(1);Calendar();});next.ToolTip="Next month";Grid.SetColumn(next,2);header.Children.Add(next);layout.Children.Add(header);
        var dates=new Grid();for(int col=0;col<7;col++)dates.ColumnDefinitions.Add(new ColumnDefinition());for(int row=0;row<7;row++)dates.RowDefinitions.Add(new RowDefinition());int first=(int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        for(int col=0;col<7;col++){var label=Text(CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(col+first)%7],10,true);label.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(label,col);dates.Children.Add(label);}
        var start=new DateTime(month.Year,month.Month,1);start=start.AddDays(-(((int)start.DayOfWeek-first+7)%7));for(int index=0;index<42;index++){var day=start.AddDays(index);var label=Text(day.Day.ToString(),12,day.Month!=month.Month);label.HorizontalAlignment=HorizontalAlignment.Center;var cell=new Border{CornerRadius=new CornerRadius(8),Margin=new Thickness(1),Child=label};if(day.Date==DateTime.Today)cell.SetResourceReference(Border.BackgroundProperty,"Selected");Grid.SetColumn(cell,index%7);Grid.SetRow(cell,1+index/7);dates.Children.Add(cell);}Grid.SetRow(dates,1);layout.Children.Add(dates);body.Children.Add(layout);renderedDate=DateTime.Today;
    }
    internal void Tick(bool pin){if(time!=null){string key=DateTime.Now.ToString(State.TwentyFourHour?"HH:mm":"h:mm tt",CultureInfo.CurrentCulture);if(key!=clockKey){clockKey=key;time.Text=key;date.Text=DateTime.Now.ToString("dddd, d MMMM",CultureInfo.CurrentCulture);}}if(State.Kind=="calendar"&&renderedDate!=DateTime.Today)Calendar();if(pin){if(!Environment.CommandLine.Contains("--inspect-ui"))WidgetDesktop.Pin(handle);materialKey="";}Material();}
    internal void Place(){var work=Screen.WorkingArea;Left=work.Left/dpi+Math.Max(0,Math.Min(State.X,work.Width/dpi-Width));Top=work.Top/dpi+Math.Max(0,Math.Min(State.Y,work.Height/dpi-Height));Material();}
    void SavePosition(){var screen=Forms.Screen.FromHandle(handle);State.Monitor=screen.DeviceName;State.X=Left-screen.WorkingArea.Left/dpi;State.Y=Top-screen.WorkingArea.Top/dpi;host.Changed();}
    void Snap(){Left=Math.Round(Left/8)*8;Top=Math.Round(Top/8)*8;foreach(var other in host.Windows.Where(x=>x!=this&&x.IsVisible)){if(Math.Abs(Left-(other.Left+other.Width+16))<12)Left=other.Left+other.Width+16;if(Math.Abs(Top-other.Top)<12)Top=other.Top;}var work=Forms.Screen.FromHandle(handle).WorkingArea;Left=Math.Max(work.Left/dpi,Math.Min(Left,work.Right/dpi-Width));Top=Math.Max(work.Top/dpi,Math.Min(Top,work.Bottom/dpi-Height));Material();}
    void Material(){if(handle==IntPtr.Zero)return;var p=OverviewPreferences.Appearance;var bounds=FrostedMaterial.BoundsOnScreen(frame);if(bounds.IsEmpty)return;var screen=Forms.Screen.FromPoint(new System.Drawing.Point(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2));string key=p.Glass+"|"+p.Strength+"|"+p.Radius+"|"+p.Light+"|"+bounds+"|"+screen.Bounds;if(key==materialKey)return;materialKey=key;Clean11Fluent.RefreshPalette(this);frame.BorderBrush=FrostedMaterial.Edge();frame.Background=FrostedMaterial.PanelBrush(bounds,screen.Bounds,p.Glass);frame.Clip=new RectangleGeometry(new Rect(0,0,Width,Height),ActivityTheme.GlassRadius,ActivityTheme.GlassRadius);}
}

static class WidgetsProgram {
    static void Dispatch(Application app,Action action){try{if(!app.Dispatcher.HasShutdownStarted)app.Dispatcher.BeginInvoke(action);}catch(InvalidOperationException){}}
    [STAThread]static int Main(string[] args){
        try{if(args.Contains("--shutdown")){DesktopWidgetBridge.Stop();return 0;}bool created;using(var mutex=new Mutex(true,"Local\\Clean11.Widgets",out created)){
            if(!created){if(args.Contains("--gallery"))try{using(var signal=EventWaitHandle.OpenExisting("Local\\Clean11WidgetsGallery"))signal.Set();}catch(WaitHandleCannotBeOpenedException){}return 0;}
            Clean11Fluent.Ensure();var app=Application.Current;app.ShutdownMode=ShutdownMode.OnExplicitShutdown;int parent=0;int pos=Array.IndexOf(args,"--parent");if(pos>=0&&pos+1<args.Length)Int32.TryParse(args[pos+1],out parent);
             using(var host=new DesktopWidgetHost(parent,args.Contains("--startup")))using(var stop=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Clean11WidgetsStop"))using(var gallery=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Clean11WidgetsGallery")){
                var stopWait=ThreadPool.RegisterWaitForSingleObject(stop,(s,t)=>Dispatch(app,app.Shutdown),null,-1,false);var showWait=ThreadPool.RegisterWaitForSingleObject(gallery,(s,t)=>Dispatch(app,host.Gallery),null,-1,false);
                if(args.Contains("--gallery"))host.Gallery();DockLog.Write("Desktop widget host ready; "+host.Store.Widgets.Count+" saved cards.");try{app.Run();}finally{stopWait.Unregister(null);showWait.Unregister(null);}return 0;
            }
        }}catch(Exception error){DockLog.Write("Desktop widgets failed: "+error);MessageBox.Show("Clean11 Widgets could not start. Your saved widget files have been kept. See the private Clean11 log for details.","Clean11 Widgets");return 1;}
    }
}
