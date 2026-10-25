using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Windows.Devices.Radios;
using Forms=System.Windows.Forms;
using Ui=Wpf.Ui.Controls;

// The top bar shares the dock host and material renderer. It never takes focus
// until the user deliberately opens a menu.
sealed class TopBarWindow : Window {
    readonly DockWindow dock;
    readonly Border leftFrame,centerFrame,rightFrame;
    readonly TopBarReservation reservation=new TopBarReservation();
    readonly DispatcherTimer timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
    readonly TextBlock active=Text("Desktop",13),clock=Text("",12),network=Text("",12),bluetooth=Text("Bluetooth",12),battery=Text("",12);
    readonly Ui.Button activeButton,networkButton,bluetoothButton,batteryButton,clockButton;
    readonly Border batteryDivider;
    readonly StackPanel left=new StackPanel{Orientation=Orientation.Horizontal},right=new StackPanel{Orientation=Orientation.Horizontal};
    DockBackdrop leftGlass,centerGlass,rightGlass;
    TopBarPopup popup;
    string popupKey,themeKey="",lastClock="";
    IntPtr handle,activeWindow;
    uint activePid;readonly uint ownPid=(uint)Process.GetCurrentProcess().Id;
    double dpi=1,overviewProgress,startupProgress=1;
    bool disposed,placing,startupMotion,bluetoothChecking;
    DateTime nextStatus=DateTime.MinValue;
    internal Forms.Screen Screen {get{return Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName==dock.State.TopBarMonitor)??Forms.Screen.PrimaryScreen;}}

    internal TopBarWindow(DockWindow owner,bool animateStartup){
        dock=owner;startupMotion=animateStartup;startupProgress=animateStartup?0:1;Clean11Fluent.Apply(this);Title="Clean11 Top Bar";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=null;ShowInTaskbar=Environment.CommandLine.Contains("--inspect-ui");ShowActivated=false;Topmost=true;Height=42;FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");
        left.Children.Add(Button("Clean11 menu","Grid24","Clean11",b=>Toggle("menu",b,MainMenu)));left.Children.Add(Divider());
        activeButton=Button("Active window actions",null,null,b=>Toggle("window",b,WindowMenu));activeButton.Content=active;activeButton.Width=170;active.FontWeight=FontWeights.SemiBold;left.Children.Add(activeButton);
        left.Children.Add(Button("File Explorer","Folder24",null,b=>dock.Open("explorer.exe")));
        networkButton=Button("Wi-Fi networks","Wifi124",null,b=>ShowIntegrated("wifi",b));networkButton.Content=IconLabel("Wifi124",network);right.Children.Add(networkButton);
        right.Children.Add(Divider());bluetoothButton=Button("Bluetooth devices","Bluetooth24",null,b=>ShowIntegrated("bluetooth",b));bluetoothButton.Content=IconLabel("Bluetooth24",bluetooth);right.Children.Add(bluetoothButton);
        right.Children.Add(Divider());right.Children.Add(Button("Volume mixer","Options24",null,b=>ShowIntegrated("mixer",b)));
        batteryDivider=Divider();right.Children.Add(batteryDivider);batteryButton=Button("Power and battery","Battery524",null,b=>dock.Open("ms-settings:powersleep"));batteryButton.Content=IconLabel("Battery524",battery);right.Children.Add(batteryButton);
        clockButton=Button("Calendar and date","Clock24",null,b=>Toggle("calendar",b,Calendar));clockButton.Content=IconLabel("Clock24",clock);
        var grid=new Grid{Margin=new Thickness(8,3,8,3)};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});grid.ColumnDefinitions.Add(new ColumnDefinition());
        leftFrame=Island(left);centerFrame=Island(clockButton);rightFrame=Island(right);leftFrame.HorizontalAlignment=HorizontalAlignment.Left;Grid.SetColumn(centerFrame,1);centerFrame.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(rightFrame,2);rightFrame.HorizontalAlignment=HorizontalAlignment.Right;grid.Children.Add(leftFrame);grid.Children.Add(centerFrame);grid.Children.Add(rightFrame);Content=grid;
        SourceInitialized+=(s,e)=>{handle=new WindowInteropHelper(this).Handle;if(!Environment.CommandLine.Contains("--inspect-ui"))DockNative.NoActivate(this);dpi=Math.Max(96,DockNative.GetDpiForWindow(handle))/96.0;HwndSource.FromHwnd(handle).AddHook(Messages);};
        reservation.Changed+=()=>{if(!disposed)Dispatcher.BeginInvoke((Action)Place);};
        timer.Tick+=(s,e)=>Tick();Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        ContentRendered+=(s,e)=>{Place();RefreshGlass();};SizeChanged+=(s,e)=>RefreshGlass();
        Closed+=(s,e)=>{disposed=true;timer.Stop();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;ClosePopup();reservation.Dispose();CloseGlass();};
        new WindowInteropHelper(this).EnsureHandle();timer.Start();
    }
    Border Island(UIElement content){return new Border{Background=new SolidColorBrush(Color.FromArgb(1,0,0,0)),BorderThickness=new Thickness(1),BorderBrush=FrostedMaterial.Edge(),CornerRadius=new CornerRadius(ActivityTheme.GlassRadius),Padding=new Thickness(3,1,3,1),VerticalAlignment=VerticalAlignment.Center,Child=content};}
    static TextBlock Text(string value,double size){var label=new TextBlock{Text=value,FontSize=size,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};label.SetResourceReference(TextBlock.ForegroundProperty,"Text");return label;}
    Border Divider(){var line=new Border{Width=1,Height=14,Margin=new Thickness(7,0,7,0),VerticalAlignment=VerticalAlignment.Center};line.SetResourceReference(Border.BackgroundProperty,"Divider");return line;}
    Ui.Button Button(string name,string symbol,string label,Action<Ui.Button> action){
        var button=Clean11Fluent.Button("",()=>{},false);Clean11Fluent.Quiet(button);button.MinHeight=0;button.Height=28;button.Padding=new Thickness(9,2,9,2);button.Margin=new Thickness(1,0,1,0);button.VerticalAlignment=VerticalAlignment.Center;button.ToolTip=name;button.Focusable=false;ToolTipService.SetInitialShowDelay(button,700);System.Windows.Automation.AutomationProperties.SetName(button,name);
        var row=new StackPanel{Orientation=Orientation.Horizontal};if(symbol!=null){var icon=Clean11Fluent.Icon(symbol,16);icon.SetResourceReference(Control.ForegroundProperty,"Text");row.Children.Add(icon);}if(label!=null){var text=Text(label,12);text.FontWeight=FontWeights.SemiBold;text.Margin=new Thickness(symbol==null?0:7,0,0,0);row.Children.Add(text);}button.Content=row;button.Click+=(s,e)=>action(button);return button;
    }
    StackPanel IconLabel(string symbol,TextBlock label){var row=new StackPanel{Orientation=Orientation.Horizontal};var icon=Clean11Fluent.Icon(symbol,15);icon.SetResourceReference(Control.ForegroundProperty,"Text");icon.Margin=new Thickness(0,0,7,0);row.Children.Add(icon);label.FontWeight=FontWeights.SemiBold;row.Children.Add(label);return row;}
    internal void ApplySettings(){ClosePopup();themeKey="";nextStatus=DateTime.MinValue;activeButton.Visibility=dock.State.TopBarShowActiveApp?Visibility.Visible:Visibility.Collapsed;networkButton.Visibility=dock.State.TopBarShowNetwork?Visibility.Visible:Visibility.Collapsed;reservation.Configure(Screen,(int)Math.Round(48*dpi),dock.State.TopBarEnabled&&dock.State.TopBarReserveSpace);Place();Tick();}
    void DisplayChanged(object sender,EventArgs e){try{Dispatcher.BeginInvoke((Action)ApplySettings);}catch{}}
    IntPtr Messages(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x02E0){dpi=(w.ToInt64()&0xffff)/96.0;Dispatcher.BeginInvoke((Action)ApplySettings);}return IntPtr.Zero;}
    void Place(){if(disposed||placing)return;placing=true;try{var bounds=reservation.Registered?reservation.Bounds:new DockNative.Rect{Left=Screen.Bounds.Left,Top=Screen.Bounds.Top,Right=Screen.Bounds.Right,Bottom=Screen.Bounds.Top+(int)(48*dpi)};Width=Math.Max(240,(bounds.Right-bounds.Left)/dpi-16);Left=bounds.Left/dpi+8;Top=bounds.Top/dpi+3-(Height+14)*overviewProgress-(Height+14)*(1-startupProgress);activeButton.Width=Math.Max(90,Math.Min(170,(Width-400)*.45));RefreshGlass();}finally{placing=false;}}
    internal void SetStartupProgress(double progress){startupProgress=Math.Max(0,Math.Min(1,progress));if(startupProgress>=1)startupMotion=false;Place();MoveGlass();}
    void Tick(){
        if(disposed)return;
        if(overviewProgress>0)return;
        bool visible=dock.State.TopBarEnabled&&!(dock.State.TopBarHideInFullscreen&&DockNative.Fullscreen(Screen,handle));
        if(!visible){ClosePopup();HideGlass();if(IsVisible)Hide();return;}
        if(!IsVisible){Show();Place();if(!startupMotion&&!dock.State.ReducedMotion)BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160)));}
        var appearance=OverviewPreferences.Appearance;string key=appearance.Glass+"|"+appearance.Strength+"|"+appearance.Radius+"|"+appearance.Light;
        if(key!=themeKey){themeKey=key;Clean11Fluent.RefreshPalette(this);leftFrame.BorderBrush=centerFrame.BorderBrush=rightFrame.BorderBrush=FrostedMaterial.Edge();RefreshGlass();if(popup!=null)popup.RefreshGlass();}
        var now=DateTime.Now;string timePattern=CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;if(dock.State.TopBarShowSeconds&&!timePattern.Contains("s")){int colon=timePattern.LastIndexOf(':');timePattern=colon<0?timePattern+":ss":timePattern.Insert(colon+1,"ss:");}string time=now.ToString("ddd d MMM, "+timePattern,CultureInfo.CurrentCulture);if(time!=lastClock){clock.Text=time;lastClock=time;clockButton.ToolTip=now.ToString("D",CultureInfo.CurrentCulture);}
        IntPtr foreground=DockNative.GetForegroundWindow();uint pid;DockNative.GetWindowThreadProcessId(foreground,out pid);
        if(foreground!=IntPtr.Zero&&pid!=ownPid){activeWindow=foreground;if(pid!=activePid){activePid=pid;string identity,path=DockNative.ProcessPath(pid,out identity);string name=System.IO.Path.GetFileNameWithoutExtension(path);active.Text=String.IsNullOrEmpty(name)?"Desktop":name.Equals("explorer",StringComparison.OrdinalIgnoreCase)?"File Explorer":name;}}
        if(DateTime.UtcNow>=nextStatus){nextStatus=DateTime.UtcNow.AddSeconds(3);activeButton.ToolTip=DockNative.Title(activeWindow);bool connected=false;try{connected=NetworkInterface.GetIsNetworkAvailable();}catch{}network.Text=connected?"Connected":"Offline";RefreshBluetoothStatus();var power=Forms.SystemInformation.PowerStatus;bool hasBattery=power.BatteryChargeStatus!=Forms.BatteryChargeStatus.Unknown&&(power.BatteryChargeStatus&Forms.BatteryChargeStatus.NoSystemBattery)==0;bool showBattery=hasBattery&&dock.State.TopBarShowBattery;batteryButton.Visibility=showBattery?Visibility.Visible:Visibility.Collapsed;batteryDivider.Visibility=showBattery?Visibility.Visible:Visibility.Collapsed;battery.Text=(power.PowerLineStatus==Forms.PowerLineStatus.Online?"Charging ":"")+Math.Round(Math.Max(0,power.BatteryLifePercent)*100)+"%";}
    }
    void RefreshBluetoothStatus(){if(bluetoothChecking)return;bluetoothChecking=true;try{var operation=Radio.GetRadiosAsync();operation.Completed=(result,state)=>{string label="Bluetooth";try{if(state==Windows.Foundation.AsyncStatus.Completed){var devices=result.GetResults().Where(x=>x.Kind==RadioKind.Bluetooth).ToList();label=devices.Count==0?"Bluetooth":devices.Any(x=>x.State==RadioState.On)?"On":"Off";}}catch{}finally{result.Close();}string value=label;Dispatcher.BeginInvoke((Action)(()=>{bluetoothChecking=false;if(!disposed)bluetooth.Text=value;}));};}catch{bluetoothChecking=false;bluetooth.Text="Bluetooth";}}
    void UpdateGlass(ref DockBackdrop material,FrameworkElement island,OverviewPreferences.AppearanceSnapshot prefs){if(material==null)material=new DockBackdrop();var origin=island.TranslatePoint(new Point(0,0),this);material.Update(handle,Left+origin.X,Top+origin.Y,island.ActualWidth,island.ActualHeight,dpi,true,FrostedMaterial.Enabled(prefs.Glass),prefs.Strength);}
    void RefreshGlass(){if(handle==IntPtr.Zero||!IsVisible||leftFrame==null||centerFrame==null||rightFrame==null)return;var prefs=OverviewPreferences.Appearance;UpdateGlass(ref leftGlass,leftFrame,prefs);UpdateGlass(ref centerGlass,centerFrame,prefs);UpdateGlass(ref rightGlass,rightFrame,prefs);}
    void MoveOne(DockBackdrop material,FrameworkElement island){if(material==null)return;var origin=island.TranslatePoint(new Point(0,0),this);material.MoveWithHost(handle,Left,Top,Left+origin.X,Top+origin.Y,dpi);}
    void MoveGlass(){MoveOne(leftGlass,leftFrame);MoveOne(centerGlass,centerFrame);MoveOne(rightGlass,rightFrame);}
    void SetGlassMotionOpacity(double opacity){if(leftGlass!=null)leftGlass.SetMotionOpacity(opacity);if(centerGlass!=null)centerGlass.SetMotionOpacity(opacity);if(rightGlass!=null)rightGlass.SetMotionOpacity(opacity);}
    void HideGlass(){if(leftGlass!=null)leftGlass.Hide();if(centerGlass!=null)centerGlass.Hide();if(rightGlass!=null)rightGlass.Hide();}
    void CloseGlass(){if(leftGlass!=null){leftGlass.Close();leftGlass=null;}if(centerGlass!=null){centerGlass.Close();centerGlass=null;}if(rightGlass!=null){rightGlass.Close();rightGlass=null;}}
    internal void PrepareOverviewScene(){ClosePopup();DockNative.SetWindowPos(handle,new IntPtr(-1),0,0,0,0,0x213);}
    internal void SetOverviewProgress(double progress){
        if(disposed)return;
        if(overviewProgress==0&&progress>0){ClosePopup();BeginAnimation(OpacityProperty,null);}
        overviewProgress=progress;IsHitTestVisible=progress==0;Opacity=progress<1?1:0;Place();
        SetGlassMotionOpacity(progress<1?1:0);MoveGlass();
        if(progress==0)Tick();
    }
    void Overview(){ClosePopup();try{OverviewPreferences.RunOverview("");}catch{dock.Notify("Could not open Clean11 Overview.");}}
    void ClosePopup(){var current=popup;popup=null;popupKey=null;if(current!=null)current.Close();}
    void Toggle(string key,FrameworkElement anchor,Func<StackPanel> content){if(popup!=null&&popupKey==key){ClosePopup();return;}ClosePopup();dock.CloseFlyout();var point=anchor.PointToScreen(new Point(anchor.ActualWidth/2,anchor.ActualHeight));var panel=content();var window=new TopBarPopup(panel,point,Screen,key=="calendar"?330:key=="mixer"?460:key=="wifi"||key=="bluetooth"?380:320);popup=window;popupKey=key;window.Closed+=(s,e)=>{if(popup==window){popup=null;popupKey=null;}};window.Show();window.Activate();window.RefreshGlass();}
    StackPanel Panel(string title,string detail){var panel=new StackPanel();var heading=Text(title,18);heading.FontWeight=FontWeights.SemiBold;heading.Margin=new Thickness(8,3,8,4);panel.Children.Add(heading);if(detail!=null){var description=Text(detail,12);description.SetResourceReference(TextBlock.ForegroundProperty,"Muted");description.Margin=new Thickness(8,0,8,12);panel.Children.Add(description);}return panel;}
    void Action(StackPanel panel,string title,string symbol,Action action){var button=Button(title,symbol,title,b=>{ClosePopup();try{action();}catch(Exception ex){DockLog.Write("Top bar action failed: "+ex.GetType().Name);dock.Notify("This action could not be completed.");}});button.Focusable=true;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Height=36;panel.Children.Add(button);}
    void Separator(StackPanel panel){var line=new Border{Height=1,Margin=new Thickness(8,7,8,7)};line.SetResourceReference(Border.BackgroundProperty,"Divider");panel.Children.Add(line);}
    StackPanel MainMenu(){var p=Panel("Clean11","A little more room to breathe.");Action(p,"Clean11 Settings","Settings24",dock.ShowSettings);Action(p,"Window overview","Apps24",Overview);Action(p,"Desktop widgets","Grid24",()=>DesktopWidgetBridge.Start(true));Action(p,"Task Manager","Pulse24",()=>dock.Open("taskmgr.exe"));Action(p,"Windows Settings","Settings24",()=>dock.Open("ms-settings:"));Separator(p);Action(p,"Lock PC","LockClosed24",()=>TopBarSystem.LockWorkStation());Action(p,"Power options","Power24",()=>ShowPower());Separator(p);Action(p,"Exit Clean11 bars","Dismiss24",dock.RequestShutdown);return p;}
    void ShowPower(){Toggle("power",left,()=>{var p=Panel("Power","Choose what happens next.");Action(p,"Sleep","WeatherMoon24",()=>ConfirmPower("Put this PC to sleep?",()=>Forms.Application.SetSuspendState(Forms.PowerState.Suspend,false,false)));Action(p,"Restart","ArrowClockwise24",()=>ConfirmPower("Restart this PC?",()=>TopBarSystem.Power("/r /t 0")));Action(p,"Shut down","Power24",()=>ConfirmPower("Shut down this PC?",()=>TopBarSystem.Power("/s /t 0")));Action(p,"Sign out","SignOut24",()=>ConfirmPower("Sign out of Windows?",()=>TopBarSystem.Power("/l")));return p;});}
    void ConfirmPower(string question,Action action){if(MessageBox.Show(question,"Clean11",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes)action();}
    StackPanel WindowMenu(){var target=activeWindow;var p=Panel(active.Text,"Actions for the active window");bool valid=target!=IntPtr.Zero&&DockNative.IsWindow(target)&&DockNative.Class(target)!="Progman"&&DockNative.Class(target)!="WorkerW";if(valid){Action(p,"Minimize","Subtract24",()=>DockNative.ShowWindowAsync(target,6));Action(p,DockNative.IsZoomed(target)?"Restore window":"Maximize window","Square24",()=>{DockNative.ShowWindowAsync(target,DockNative.IsZoomed(target)?9:3);DockNative.SetForegroundWindow(target);});Action(p,"Close window","Dismiss24",()=>DockNative.PostMessage(target,0x10,IntPtr.Zero,IntPtr.Zero));Separator(p);}Action(p,"Show all windows","Apps24",Overview);Action(p,"Task Manager","Pulse24",()=>dock.Open("taskmgr.exe"));return p;}
    void ShowIntegrated(string kind,FrameworkElement anchor){Toggle(kind,anchor,()=>new TopBarControls(kind,target=>{ClosePopup();dock.Open(target);},ClosePopup));}
    StackPanel Calendar(){var p=Panel(DateTime.Now.ToString("dddd",CultureInfo.CurrentCulture),DateTime.Now.ToString("D",CultureInfo.CurrentCulture));var calendar=new System.Windows.Controls.Calendar{SelectedDate=DateTime.Today,DisplayDate=DateTime.Today,HorizontalAlignment=HorizontalAlignment.Center,BorderThickness=new Thickness(0),Background=Brushes.Transparent};p.Children.Add(calendar);Separator(p);Action(p,"Date & time settings","Clock24",()=>dock.Open("ms-settings:dateandtime"));return p;}
}

sealed class TopBarPopup : Window {
    readonly DockBackdrop glass=new DockBackdrop();readonly Border frame;double dpi;IntPtr handle;bool closing;
    internal TopBarPopup(StackPanel content,Point anchor,Forms.Screen screen,double width){
        Clean11Fluent.Apply(this);Title="Clean11 Top Bar menu";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;ShowInTaskbar=Environment.CommandLine.Contains("--inspect-ui");Topmost=true;Background=Brushes.Transparent;Width=width;SizeToContent=SizeToContent.Height;FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");
        frame=new Border{CornerRadius=new CornerRadius(ActivityTheme.GlassRadius),BorderThickness=new Thickness(1),BorderBrush=FrostedMaterial.Edge(),Background=new SolidColorBrush(Color.FromArgb(1,0,0,0)),Padding=new Thickness(12),Child=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}};Content=frame;
        SourceInitialized+=(s,e)=>{handle=new WindowInteropHelper(this).Handle;dpi=Math.Max(96,DockNative.GetDpiForWindow(handle))/96.0;MaxHeight=Math.Max(150,screen.WorkingArea.Height/dpi-60);Left=Math.Max(screen.Bounds.Left/dpi+8,Math.Min((anchor.X/dpi)-width/2,screen.Bounds.Right/dpi-width-8));Top=anchor.Y/dpi+10;};
        Loaded+=(s,e)=>{Top=Math.Min(Top,screen.Bounds.Bottom/dpi-ActualHeight-8);RefreshGlass();};SizeChanged+=(s,e)=>RefreshGlass();Closing+=(s,e)=>closing=true;Deactivated+=(s,e)=>{if(!closing)Close();};PreviewKeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape){e.Handled=true;if(!closing)Close();}};Closed+=(s,e)=>glass.Close();
    }
    internal void RefreshGlass(){if(handle==IntPtr.Zero||!IsVisible)return;Clean11Fluent.RefreshPalette(this);frame.BorderBrush=FrostedMaterial.Edge();var p=OverviewPreferences.Appearance;glass.Update(handle,Left,Top,ActualWidth,ActualHeight,dpi,true,FrostedMaterial.Enabled(p.Glass),p.Strength);}
}
