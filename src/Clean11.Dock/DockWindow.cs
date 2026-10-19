using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
using Ui=Wpf.Ui.Controls;

sealed class DockGroup {
    internal string Key,Name,IconPath;internal DockPin Pin;internal List<DockAppWindow> Windows=new List<DockAppWindow>();
}
sealed class DockTile {
    internal Ui.Button Button;internal FrameworkElement Icon;internal ScaleTransform Scale;internal TranslateTransform Shift;
    internal bool? DotActive;internal Border Dot;internal DockGroup Group;internal double Center,Target=1,TargetShift;
}
sealed class DockWindow : Window {
    internal readonly DockState State;
    internal readonly ShellLease Lease=new ShellLease();
    readonly DockAppBar appbar=new DockAppBar();readonly DispatcherTimer pulse=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
    readonly DispatcherTimer refresh=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1.5)},hoverDelay=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};
    DockBackdrop glassBackdrop;
    TopBarWindow topBar;
    readonly Canvas strip=new Canvas();readonly Border backdrop;readonly List<DockTile> tiles=new List<DockTile>();
    readonly BlockingCollection<string> iconRequests=new BlockingCollection<string>();readonly Dictionary<string,BitmapSource> icons=new Dictionary<string,BitmapSource>(StringComparer.OrdinalIgnoreCase);readonly HashSet<string> pending=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly Forms.NotifyIcon notification;List<DockAppWindow> windows=new List<DockAppWindow>();List<DockGroup> groups=new List<DockGroup>();
    string signature="";bool shutdownRequested,enumerating,disposed,showing=true,moving,dragging,menuOpen;DateTime outsideSince=DateTime.MinValue,lastLease=DateTime.MinValue;IntPtr hwnd;double dpi=1,slot,baseWidth,dividerX;Border divider;int hoverIndex=-1;DockFlyout flyout;Window settings;
    Point dragStart;DockTile dragTile;Stopwatch motion=new Stopwatch();long lastFrame;
    internal Forms.Screen Screen{get{return Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==State.Monitor)??Forms.Screen.PrimaryScreen;}}
    internal DockWindow(DockState state){
        State=state;Clean11Fluent.Apply(this);Title="Clean11 Dock";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=Environment.CommandLine.Contains("--inspect-ui");ShowActivated=false;Topmost=true;FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");Foreground=ActivityTheme.Brush("Text","#F5F5F5");
        var root=new Grid();backdrop=new Border{Background=ActivityTheme.Brush("DockSurface","#ED202329"),BorderBrush=ActivityTheme.Brush("Border","#505050"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(ActivityTheme.PanelRadius),VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(36,0,36,0)};root.Children.Add(backdrop);root.Children.Add(strip);Content=root;AllowDrop=true;
        SourceInitialized+=(s,e)=>{hwnd=new WindowInteropHelper(this).Handle;if(!Environment.CommandLine.Contains("--inspect-ui"))DockNative.NoActivate(this);dpi=DockNative.GetDpiForWindow(hwnd)/96.0;HwndSource.FromHwnd(hwnd).AddHook(Messages);Rebuild();};
        MouseMove+=(s,e)=>Hover(e.GetPosition(strip));MouseLeave+=(s,e)=>{
            // Layered windows can lose mouse ownership to a tooltip or native
            // backdrop. Only collapse when the pointer actually leaves the dock.
            DockNative.Point cursor;
            if(DockNative.GetCursorPos(out cursor)){
                var point=PointFromScreen(new Point(cursor.X,cursor.Y));
                if(point.X>=36&&point.X<=Width-36&&point.Y>=Height-backdrop.Height&&point.Y<=Height){Hover(point);return;}
            }
            hoverIndex=-1;hoverDelay.Stop();SetMagnification(-10000);
        };
        PreviewMouseLeftButtonDown+=(s,e)=>{dragStart=e.GetPosition(this);dragTile=tiles.FirstOrDefault(t=>t.Button.IsMouseOver&&t.Group!=null&&t.Group.Pin!=null);};
        PreviewMouseMove+=(s,e)=>{if(e.LeftButton==MouseButtonState.Pressed&&dragTile!=null&&!dragging&&(e.GetPosition(this)-dragStart).Length>8){dragging=true;CloseFlyout();try{DragDrop.DoDragDrop(dragTile.Button,new DataObject("Clean11.Pin",dragTile.Group.Pin.Id),DragDropEffects.Move);}finally{dragging=false;dragTile=null;}}};
        PreviewMouseLeftButtonUp+=(s,e)=>dragTile=null;
        DragOver+=(s,e)=>{e.Effects=e.Data.GetDataPresent("Clean11.Pin")?DragDropEffects.Move:e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
        Drop+=Dropped;
        ContextMenu=new ContextMenu();ContextMenuOpening+=(s,e)=>{if(tiles.Any(t=>t.Button.IsMouseOver))return;e.Handled=true;OpenMenu(SettingsMenu(),this);};
        pulse.Tick+=(s,e)=>UpdateVisibility();refresh.Tick+=(s,e)=>RefreshWindows();hoverDelay.Tick+=(s,e)=>{hoverDelay.Stop();if(hoverIndex>=0&&hoverIndex<tiles.Count&&!dragging&&!menuOpen){var tile=tiles[hoverIndex];if(tile.Group!=null&&tile.Group.Windows.Count>0)ShowWindows(tile);}};
        var worker=new Thread(()=>{foreach(string path in iconRequests.GetConsumingEnumerable()){var image=DockNative.NormalizeIcon(DockNative.Icon(path));try{Dispatcher.BeginInvoke((Action)(()=>{pending.Remove(path);icons[path]=image;foreach(var tile in tiles)if(tile.Group!=null&&String.Equals(tile.Group.IconPath,path,StringComparison.OrdinalIgnoreCase))SetIcon(tile,image);}));}catch{}}});worker.IsBackground=true;worker.SetApartmentState(ApartmentState.STA);worker.Start();
        notification=new Forms.NotifyIcon{Text="Clean11 Dock",Icon=System.Drawing.SystemIcons.Application,Visible=true};notification.MouseClick+=(s,e)=>{if(e.Button==Forms.MouseButtons.Left)ShowSettings();else OpenMenu(SettingsMenu(),this);};
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        Closing+=(s,e)=>{if(!disposed&&!shutdownRequested){e.Cancel=true;RequestShutdown();}};
        Show();topBar=new TopBarWindow(this);topBar.ApplySettings();pulse.Start();refresh.Start();RefreshWindows();DockLog.Write("Dock window ready without activation; "+State.Pins.Count+" pinned items.");
    }
    IntPtr Messages(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x02E0){dpi=(w.ToInt64()&0xffff)/96.0;Dispatcher.BeginInvoke((Action)Rebuild);}return IntPtr.Zero;}
    void DisplayChanged(object s,EventArgs e){try{Dispatcher.BeginInvoke((Action)(()=>{Lease.Release();Rebuild();}));}catch{}}
    void RefreshWindows(){if(enumerating||disposed)return;enumerating=true;ThreadPool.QueueUserWorkItem(_=>{List<DockAppWindow> snapshot;try{snapshot=DockNative.Windows();}catch{snapshot=new List<DockAppWindow>();}try{Dispatcher.BeginInvoke((Action)(()=>{enumerating=false;if(disposed)return;windows=snapshot;var next=Group(snapshot);string key=String.Join("|",next.Select(g=>g.Key+":"+String.Join(",",g.Windows.Select(x=>x.Handle.ToInt64()+x.Title))));groups=next;if(key!=signature&&!dragging&&!menuOpen){signature=key;Rebuild();}else foreach(var tile in tiles){if(tile.Group!=null)tile.Group=groups.FirstOrDefault(g=>g.Key==tile.Group.Key)??tile.Group;}}));}catch{}});}
    List<DockGroup> Group(List<DockAppWindow> snapshot){
        var result=State.Pins.Select(p=>new DockGroup{Key=p.Id,Name=p.Name,IconPath=!String.IsNullOrEmpty(p.IconPath)&&File.Exists(p.IconPath)?p.IconPath:!String.IsNullOrEmpty(p.AppId)&&p.AppId.Contains("!")?"app:"+p.AppId+"|"+p.Path:p.Path,Pin=p}).ToList();
        foreach(var window in snapshot){var match=result.FirstOrDefault(g=>g.Pin!=null&&Matches(g.Pin,window));if(match==null){string key=String.IsNullOrEmpty(window.AppId)?window.Path:window.AppId;if(String.IsNullOrEmpty(key))key=window.Handle.ToString();match=result.FirstOrDefault(g=>g.Key.Equals(key,StringComparison.OrdinalIgnoreCase));if(match==null){match=new DockGroup{Key=key,Name=System.IO.Path.GetFileNameWithoutExtension(window.Path),IconPath=window.Path};if(String.IsNullOrEmpty(match.Name))match.Name=window.Title;result.Add(match);}}if(match.Pin!=null&&!String.IsNullOrEmpty(match.Pin.AppId)&&match.Pin.AppId.Contains("!")&&!String.IsNullOrEmpty(window.Path)){match.IconPath=window.Path;if(match.Pin.IconPath!=window.Path){match.Pin.IconPath=window.Path;State.Save();}}match.Windows.Add(window);}
        return result;
    }
    static bool Matches(DockPin pin,DockAppWindow window){if(!String.IsNullOrEmpty(pin.AppId)&&pin.AppId.Equals(window.AppId,StringComparison.OrdinalIgnoreCase))return true;return new[]{pin.Path,pin.MatchPath}.Any(p=>!String.IsNullOrEmpty(p)&&(p.Equals(window.Path,StringComparison.OrdinalIgnoreCase)||(!String.IsNullOrEmpty(window.Path)&&System.IO.Path.GetFileName(p).Equals(System.IO.Path.GetFileName(window.Path),StringComparison.OrdinalIgnoreCase))));}
    internal void Rebuild(){
        if(disposed)return;CloseFlyout();StopMotion();strip.Children.Clear();tiles.Clear();divider=null;hoverIndex=-1;groups=Group(windows);
        var screen=Screen;double available=screen.Bounds.Width/dpi-100;int count=groups.Count+1+(State.ShowDownloads?1:0)+(State.ShowRecycleBin?1:0)+(State.ShowBackgroundApps?1:0);
        int capacity=Math.Max(1,(int)(available/36)- (count-groups.Count)-1);var visibleGroups=groups.Take(capacity).ToList();var overflow=groups.Skip(capacity).ToList();count=visibleGroups.Count+(count-groups.Count)+(overflow.Count>0?1:0);
        slot=Math.Max(28,Math.Min(State.IconSize+12,available/Math.Max(1,count)));double size=Math.Min(State.IconSize,slot-10);baseWidth=count*slot+46;Width=baseWidth+72;Height=Math.Max(94,size*State.Magnification+36);backdrop.Height=size+24;
        AddUtility("Overview","Apps24",()=>Overview(),size);
        foreach(var group in visibleGroups)AddGroup(group,size);
        if(overflow.Count>0)AddUtility("More apps","MoreHorizontal24",()=>{var menu=new ContextMenu();foreach(var item in overflow){var group=item;menu.Items.Add(Item(group.Name,()=>{if(group.Windows.Count>0){CloseFlyout();flyout=new DockFlyout(this,group,FolderAnchor());flyout.Show();}else Launch(group);}));}OpenMenu(menu,this);},size);
        dividerX=52+tiles.Count*slot+7;divider=new Border{Width=1,Height=26,Background=ActivityTheme.Brush("Divider","#18FFFFFF"),IsHitTestVisible=false};Canvas.SetLeft(divider,dividerX);Canvas.SetTop(divider,Height-backdrop.Height/2-13);strip.Children.Add(divider);
        if(State.ShowDownloads)AddUtility("Downloads","Folder24",()=>ShowFolder(DockNative.Downloads()),size);
        if(State.ShowBackgroundApps)AddUtility("Background apps","ChevronUp24",()=>{CloseFlyout();bool bridged=Lease.ShowTray();if(!bridged){HideDock(true);DockLog.Write("Native notification area revealed for 15 seconds.");}},size);
        if(State.ShowRecycleBin)AddUtility("Recycle Bin","Delete24",()=>Open("shell:RecycleBinFolder"),size);

        backdrop.Width=baseWidth;Position();RefreshGlass();UpdateVisibility();
    }
    DockTile Tile(string label,double size){var button=Clean11Fluent.Button("",()=>{},false);Clean11Fluent.Quiet(button);button.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='Transparent'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");button.Appearance=Ui.ControlAppearance.Transparent;button.BorderThickness=new Thickness(0);button.Padding=new Thickness(0);button.Width=slot;button.Height=size;button.Focusable=false;button.ToolTip=label;ToolTipService.SetInitialShowDelay(button,600);button.RenderTransform=new TranslateTransform();
        var tile=new DockTile{Button=button,Shift=(TranslateTransform)button.RenderTransform,Scale=new ScaleTransform(1,1),Center=52+tiles.Count*slot+slot/2+(divider!=null?14:0)};
        Canvas.SetLeft(button,tile.Center-slot/2);Canvas.SetTop(button,Height-backdrop.Height/2-size/2);strip.Children.Add(button);tiles.Add(tile);return tile;}
    void AddUtility(string label,string symbol,Action action,double size){
        var tile=Tile(label,size);var content=new Grid{Width=size,Height=size,ClipToBounds=false};
        
        var icon=Clean11Fluent.Icon(symbol,22);icon.Foreground=new SolidColorBrush(FrostedMaterial.Ink(true));
        var holder=IconFrame(size);holder.Child=icon;holder.RenderTransform=tile.Scale;holder.RenderTransformOrigin=new Point(.5,1);tile.Icon=holder;content.Children.Add(holder);
        tile.Button.Content=content;tile.Button.Click+=(s,e)=>{CloseFlyout();action();};
    }
    Border IconFrame(double size){return new Border{Width=size,Height=size,Padding=new Thickness(3),Background=Brushes.Transparent,BorderThickness=new Thickness(0)};}
    void AddGroup(DockGroup group,double size){
        var tile=Tile(group.Name,size);tile.Group=group;var content=new Grid{Width=size,Height=size,ClipToBounds=false};
        var holder=IconFrame(size);holder.RenderTransform=tile.Scale;holder.RenderTransformOrigin=new Point(.5,1);tile.Icon=holder;content.Children.Add(holder);tile.Dot=new Border{Height=3,Width=3,IsHitTestVisible=false,RenderTransform=new TranslateTransform(0,size/2+5),CornerRadius=new CornerRadius(2),Background=ActivityTheme.Brush("Muted","#B8B8B8"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Visibility=group.Windows.Count>0?Visibility.Visible:Visibility.Hidden};content.Children.Add(tile.Dot);tile.Button.Content=content;
        BitmapSource image;icons.TryGetValue(group.IconPath??"",out image);SetIcon(tile,image);if(image==null&&!String.IsNullOrWhiteSpace(group.IconPath)&&!icons.ContainsKey(group.IconPath)&&pending.Add(group.IconPath))iconRequests.Add(group.IconPath);
        tile.Button.Click+=(s,e)=>{if(dragging)return;if(group.Pin!=null&&Directory.Exists(group.Pin.Path)){ShowFolder(group.Pin.Path);return;}if(group.Windows.Count==1){CloseFlyout();DockNative.Activate(group.Windows[0],true);}else if(group.Windows.Count>1){if(flyout!=null&&flyout.GroupKey==group.Key)CloseFlyout();else ShowWindows(tile);}else Launch(group);};
        tile.Button.PreviewMouseDown+=(s,e)=>{if(e.ChangedButton==MouseButton.Middle){Launch(tile.Group);e.Handled=true;}};
        tile.Button.ContextMenu=new ContextMenu();tile.Button.ContextMenuOpening+=(s,e)=>{e.Handled=true;CloseFlyout();OpenMenu(GroupMenu(tile.Group),tile.Button);};
    }
    void SetIcon(DockTile tile,BitmapSource image){RenderOptions.SetBitmapScalingMode(tile.Icon,BitmapScalingMode.HighQuality);var holder=tile.Icon as Border;if(holder!=null)holder.Child=image!=null?(UIElement)new Image{Source=image,Stretch=Stretch.Uniform,SnapsToDevicePixels=true}:Clean11Fluent.Icon("AppGeneric24",State.IconSize*.7);}
    void Hover(Point point){if(dragging||menuOpen)return;int index=tiles.FindIndex(t=>Math.Abs(point.X-t.Center-t.Shift.X)<slot/2);SetMagnification(point.X);if(index!=hoverIndex){hoverIndex=index;hoverDelay.Stop();if(index>=0&&tiles[index].Group!=null&&tiles[index].Group.Windows.Count>0)hoverDelay.Start();}}
    void SetMagnification(double x){
        double influence=slot*2.2;
        foreach(var tile in tiles){double distance=Math.Abs(x-tile.Center),weight=Math.Max(0,1-distance/influence);tile.Target=1+(tile.Group==null?0:State.Magnification-1)*weight*weight*(3-2*weight);}
        // Allocate space for enlarged icons instead of overlapping fixed neighbors.
        double totalExtra=tiles.Sum(t=>(t.Target-1)*State.IconSize),offset=-totalExtra/2;
        foreach(var tile in tiles){double extra=(tile.Target-1)*State.IconSize;tile.TargetShift=offset+extra/2;offset+=extra;if(State.ReducedMotion){tile.Scale.ScaleX=tile.Scale.ScaleY=tile.Target;tile.Shift.X=tile.TargetShift;}}
        if(!State.ReducedMotion&&!moving){moving=true;motion.Restart();lastFrame=0;CompositionTarget.Rendering+=Animate;}
    }
    void Animate(object sender,EventArgs args){long now=motion.ElapsedMilliseconds;double step=1-Math.Exp(-Math.Max(1,now-lastFrame)/45.0);lastFrame=now;bool pendingFrame=false;foreach(var tile in tiles){double scale=tile.Scale.ScaleX+(tile.Target-tile.Scale.ScaleX)*step;double shift=tile.Shift.X+(tile.TargetShift-tile.Shift.X)*step;pendingFrame|=Math.Abs(scale-tile.Target)>.002||Math.Abs(shift-tile.TargetShift)>.05;tile.Scale.ScaleX=tile.Scale.ScaleY=scale;tile.Shift.X=shift;}if(!pendingFrame)StopMotion();}
    void StopMotion(){if(moving)CompositionTarget.Rendering-=Animate;moving=false;}
    void Position(){var b=Screen.Bounds;Left=(b.Left+(b.Width-Width*dpi)/2)/dpi;Top=(b.Bottom-Height*dpi-10*dpi)/dpi;}
    readonly TrayAppearance trayAppearance=new TrayAppearance();
    void UpdateVisibility(){
        trayAppearance.Update();
        if(disposed||hwnd==IntPtr.Zero)return;var now=DateTime.UtcNow;var screen=Screen;
        if((now-lastLease).TotalSeconds>1){Lease.Update(screen,State.TakeOver);appbar.Configure(screen,(int)((State.IconSize+42)*dpi),State.Visibility=="Always"&&!Lease.ExistingDock);lastLease=now;}
        bool fullscreen=DockNative.Fullscreen(screen,hwnd);DockNative.Point pointer;DockNative.GetCursorPos(out pointer);DockNative.Rect bounds;DockNative.GetWindowRect(hwnd,out bounds);bool inside=pointer.X>=bounds.Left&&pointer.X<=bounds.Right&&pointer.Y>=bounds.Top&&pointer.Y<=screen.Bounds.Bottom;
        bool inFlyout=flyout!=null&&flyout.Contains(pointer.X,pointer.Y);bool edge=pointer.Y>=screen.Bounds.Bottom-3&&pointer.X>=bounds.Left&&pointer.X<=bounds.Right;
        if(flyout!=null&&!inside&&!inFlyout&&!menuOpen){if(outsideSince==DateTime.MinValue)outsideSince=now;if((now-outsideSince).TotalMilliseconds>400)CloseFlyout();}else outsideSince=DateTime.MinValue;
        bool overlap=false;DockNative.Rect fg;IntPtr foreground=DockNative.GetForegroundWindow();if(foreground!=hwnd&&DockNative.Class(foreground)!="Progman"&&DockNative.Class(foreground)!="WorkerW"&&DockNative.GetWindowRect(foreground,out fg))overlap=fg.Bottom>screen.Bounds.Bottom-(State.IconSize+48)*dpi&&fg.Right>bounds.Left&&fg.Left<bounds.Right;
        bool fullscreenOverride=State.HideInFullscreen&&fullscreen;
        bool shouldShow=!fullscreenOverride&&DateTime.UtcNow>=Lease.NativeTrayUntil&&(menuOpen||inFlyout||(inside&&showing)||edge||State.Visibility=="Always"||(State.Visibility=="Intelligent"&&!overlap));
        HideDock(!shouldShow);if(fullscreenOverride)CloseFlyout();
        foreach(var tile in tiles)if(tile.Group!=null&&tile.Dot!=null){bool active=tile.Group.Windows.Any(w=>w.Handle==foreground);if(tile.DotActive==active)continue;tile.DotActive=active;tile.Dot.Background=ActivityTheme.Brush(active?"Accent":"Muted",active?"#60CDFF":"#B8B8B8");}
    }
    void HideDock(bool hidden){if(showing==!hidden)return;showing=!hidden;RefreshGlass();if(hidden){hoverDelay.Stop();StopMotion();BeginAnimation(OpacityProperty,null);Opacity=0;IsHitTestVisible=false;DockNative.SetLong(hwnd,-20,new IntPtr(DockNative.GetLong(hwnd,-20).ToInt64()|0x20));}else{DockNative.SetLong(hwnd,-20,new IntPtr(DockNative.GetLong(hwnd,-20).ToInt64()&~0x20));IsHitTestVisible=true;if(State.ReducedMotion)Opacity=1;else BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(140)){FillBehavior=FillBehavior.Stop},HandoffBehavior.SnapshotAndReplace);Opacity=1;}}
    void Dropped(object sender,DragEventArgs e){if(e.Data.GetDataPresent("Clean11.Pin")){string id=(string)e.Data.GetData("Clean11.Pin");var pin=State.Pins.FirstOrDefault(p=>p.Id==id);if(pin!=null){int target=Math.Max(0,Math.Min(State.Pins.Count-1,(int)((e.GetPosition(strip).X-52)/slot)-1));State.Pins.Remove(pin);State.Pins.Insert(Math.Min(target,State.Pins.Count),pin);}}else if(e.Data.GetDataPresent(DataFormats.FileDrop)){foreach(string path in ((string[])e.Data.GetData(DataFormats.FileDrop)).Take(32))if(!State.Pins.Any(p=>p.Path.Equals(path,StringComparison.OrdinalIgnoreCase)))State.Pins.Add(DockState.FromPath(path));}State.Save();signature="";Rebuild();e.Handled=true;}
    internal void Launch(DockGroup group){CloseFlyout();try{DockState.Launch(group.Pin??new DockPin{Name=group.Name,Path=group.IconPath,AppId=group.Windows.Select(w=>w.AppId).FirstOrDefault()});}catch{Notify("Could not open this app. Remove the pin and add its app shortcut again in Dock settings.");}}
    internal void Open(string target){try{DockState.Launch(new DockPin{Path=target});}catch{Notify("This location could not be opened.");}}
    void Overview(){string app=Clean11Paths.Overview;if(File.Exists(app))Open(app);else Notify("Clean11 Overview is missing. Please repair the installation.");}
    internal void Notify(string message){notification.BalloonTipTitle="Clean11 Dock";notification.BalloonTipText=message;notification.ShowBalloonTip(4000);}
    internal void CloseFlyout(){if(flyout!=null){flyout.Close();flyout=null;}}
    void ShowWindows(DockTile tile){if(flyout!=null&&flyout.GroupKey==tile.Group.Key)return;CloseFlyout();if(tile.Group.Windows.Count==0)return;flyout=new DockFlyout(this,tile.Group,TileAnchor(tile));flyout.Show();}
    Point TileAnchor(DockTile tile){DockNative.Rect r;DockNative.GetWindowRect(hwnd,out r);return new Point(r.Left+(tile.Center+tile.Shift.X)*dpi,r.Bottom-backdrop.Height*dpi-8);}
    Point FolderAnchor(){DockNative.Rect r;DockNative.GetWindowRect(hwnd,out r);return new Point(r.Left+r.Width/2,r.Bottom-backdrop.Height*dpi-8);}
    void ShowFolder(string path){CloseFlyout();flyout=new DockFlyout(this,path,FolderAnchor());flyout.Show();}
    internal void OpenMenu(ContextMenu menu,FrameworkElement target){Clean11Fluent.Menu(menu);menu.PlacementTarget=target;menu.Placement=PlacementMode.MousePoint;menu.Opened+=(s,e)=>{menuOpen=true;hoverDelay.Stop();};menu.Closed+=(s,e)=>menuOpen=false;menu.IsOpen=true;}
    MenuItem Item(string label,Action action){var item=new MenuItem{Header=label};item.Click+=(s,e)=>action();return item;}
    ContextMenu GroupMenu(DockGroup group){var menu=new ContextMenu();menu.Items.Add(Item("Open new window",()=>Launch(group)));if(group.Windows.Count>0){menu.Items.Add(Item("Show windows",()=>{var tile=tiles.FirstOrDefault(t=>t.Group!=null&&t.Group.Key==group.Key);if(tile!=null)ShowWindows(tile);}));menu.Items.Add(Item("Minimize all windows",()=>{foreach(var w in group.Windows)DockNative.ShowWindowAsync(w.Handle,6);}));menu.Items.Add(Item("Close windows",()=>{foreach(var w in group.Windows)DockNative.PostMessage(w.Handle,0x10,IntPtr.Zero,IntPtr.Zero);}));}menu.Items.Add(new Separator());menu.Items.Add(Item(group.Pin==null?"Keep in dock":"Unpin from dock",()=>{if(group.Pin==null)State.Pins.Add(new DockPin{Name=group.Name,Path=group.IconPath,MatchPath=group.IconPath,AppId=group.Windows.Select(w=>w.AppId).FirstOrDefault()});else State.Pins.Remove(group.Pin);State.Save();Rebuild();}));if(group.Pin!=null){menu.Items.Add(Item("Move left",()=>MovePin(group.Pin,-1)));menu.Items.Add(Item("Move right",()=>MovePin(group.Pin,1)));}if(File.Exists(group.IconPath))menu.Items.Add(Item("Open file location",()=>Open(System.IO.Path.GetDirectoryName(group.IconPath))));menu.Items.Add(new Separator());menu.Items.Add(Item("Clean11 Settings",ShowSettings));return menu;}
    void MovePin(DockPin pin,int delta){int index=State.Pins.IndexOf(pin),next=Math.Max(0,Math.Min(State.Pins.Count-1,index+delta));State.Pins.RemoveAt(index);State.Pins.Insert(next,pin);State.Save();Rebuild();}
    ContextMenu SettingsMenu(){var menu=new ContextMenu();menu.Items.Add(Item("Clean11 Settings",ShowSettings));menu.Items.Add(Item("Open Task Manager",()=>Open("taskmgr.exe")));menu.Items.Add(Item("Background apps",()=>Lease.ShowTray()));menu.Items.Add(new Separator());menu.Items.Add(Item("Exit dock and restore previous bars",()=>RequestShutdown()));return menu;}
    internal void ShowSettings(){
        try{
            CloseFlyout();
            if(settings==null){var created=new DockSettings(this);created.Closed+=(s,e)=>{if(settings==created)settings=null;};settings=created;}
            if(settings.WindowState==WindowState.Minimized)settings.WindowState=WindowState.Normal;
            settings.Show();
            var settingsHandle=new WindowInteropHelper(settings).Handle;
            DockNative.Rect bounds;
            if(DockNative.GetWindowRect(settingsHandle,out bounds)){
                bool onScreen=Forms.Screen.AllScreens.Any(screen=>System.Drawing.Rectangle.Intersect(screen.WorkingArea,new System.Drawing.Rectangle(bounds.Left,bounds.Top,bounds.Width,bounds.Height)).Width>=160&&System.Drawing.Rectangle.Intersect(screen.WorkingArea,new System.Drawing.Rectangle(bounds.Left,bounds.Top,bounds.Width,bounds.Height)).Height>=100);
                if(!onScreen){var work=Screen.WorkingArea;double scale=Math.Max(96,DockNative.GetDpiForWindow(settingsHandle))/96.0;settings.Left=(work.Left+Math.Max(0,(work.Width-settings.ActualWidth*scale)/2))/scale;settings.Top=(work.Top+Math.Max(0,(work.Height-settings.ActualHeight*scale)/2))/scale;}
            }
            // Promote once for a requested launch; Settings must not stay above other apps.
            settings.Topmost=true;settings.Topmost=false;
            bool activated=settings.Activate();DockNative.SetForegroundWindow(settingsHandle);settings.Focus();
            DockLog.Write("Settings launch: visible="+settings.IsVisible+", state="+settings.WindowState+", activated="+activated+", foreground="+(DockNative.GetForegroundWindow()==settingsHandle));
        }catch(Exception error){var failed=settings;settings=null;if(failed!=null)try{failed.Close();}catch{}DockLog.Write("Settings launch failed: "+error);}
    }
    internal void RefreshGlass(){
        if(hwnd==IntPtr.Zero||disposed)return;
        foreach(var tile in tiles)tile.DotActive=null;
        bool glass=true;int strength=60;try{var preferences=OverviewPreferences.Load();glass=preferences.Glass;strength=preferences.GlassStrength;}catch{}
        glass=FrostedMaterial.Enabled(glass);
        if(glassBackdrop==null)glassBackdrop=new DockBackdrop();
        glassBackdrop.Update(hwnd,Left+36,Top+Height-backdrop.Height,baseWidth,backdrop.Height,dpi,showing,glass,strength);
        // Alpha zero is a native hit-test hole in a layered window. Retain one
        // alpha step across the whole bar so gaps receive continuous MouseMove.
        backdrop.Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));backdrop.BorderThickness=new Thickness(1);backdrop.CornerRadius=new CornerRadius(ActivityTheme.GlassRadius);backdrop.BorderBrush=FrostedMaterial.Edge();
        foreach(var tile in tiles){if(tile.Group==null){var frame=tile.Icon as Border;if(frame!=null){var glyph=frame.Child as Ui.SymbolIcon;if(glyph!=null)glyph.Foreground=new SolidColorBrush(FrostedMaterial.Ink(true));}}}
    }
    internal void ApplySettings(){CloseFlyout();State.Save();Lease.Release();lastLease=DateTime.MinValue;Rebuild();if(topBar!=null)topBar.ApplySettings();}
    internal void RequestShutdown(){
        if(shutdownRequested)return;
        if(settings!=null){settings.Close();if(settings!=null&&settings.IsVisible)return;}
        shutdownRequested=true;Application.Current.Shutdown();
    }
    internal void DisposeDock(){trayAppearance.Dispose();if(disposed)return;disposed=true;if(topBar!=null){topBar.Close();topBar=null;}if(glassBackdrop!=null){glassBackdrop.Close();glassBackdrop=null;}pulse.Stop();refresh.Stop();hoverDelay.Stop();StopMotion();CloseFlyout();if(settings!=null)settings.Close();notification.Visible=false;notification.Dispose();iconRequests.CompleteAdding();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;appbar.Dispose();Lease.Release();DockLog.Write("Dock exited; previous bars restored.");}
}
