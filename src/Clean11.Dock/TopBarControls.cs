using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

// Work only while a panel is open. No background scanning or audio polling.
sealed class TopBarControls : StackPanel {
    sealed class RouteChoice {internal string Id,Name;public override string ToString(){return Name;}}
    readonly string kind;
    readonly Action<string> open;
    readonly StackPanel body=new StackPanel();
    readonly TextBlock status=Label("",12);
    readonly CancellationTokenSource lifetime=new CancellationTokenSource();
    bool busy,closed;

    internal TopBarControls(string page,Action<string> launch,Action back){
        kind=page;open=launch;Margin=new Thickness(4);
        var backButton=Clean11Fluent.Button("‹  Back",back,false);Clean11Fluent.Quiet(backButton);backButton.HorizontalAlignment=HorizontalAlignment.Left;backButton.Margin=new Thickness(-6,-4,0,8);Children.Add(backButton);
        var title=Label(page=="wifi"?"Wi-Fi":page=="bluetooth"?"Bluetooth":"Volume mixer",20);title.FontWeight=FontWeights.SemiBold;Children.Add(title);
        status.Margin=new Thickness(0,4,0,14);status.SetResourceReference(TextBlock.ForegroundProperty,"Muted");Children.Add(status);Children.Add(new ScrollViewer{Content=body,MaxHeight=420,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,6,0)});
        var footer=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};
        footer.Children.Add(Button("Refresh",async()=>await Reload(kind=="wifi")));
        footer.Children.Add(Button("More settings",()=>{open(page=="wifi"?"ms-settings:network-wifi":page=="bluetooth"?"ms-settings:bluetooth":"ms-settings:apps-volume");return Task.FromResult(0);}));Children.Add(footer);
        Loaded+=async(s,e)=>{if(busy||closed)return;busy=true;try{await Reload(false);}finally{busy=false;}};
        Unloaded+=(s,e)=>{closed=true;lifetime.Cancel();};
    }
    // Bridge split system WinMetadata on .NET Framework as well as the modern SDK.
    static Task<T> Await<T>(Windows.Foundation.IAsyncOperation<T> operation){
        var completion=new TaskCompletionSource<T>();
        // A Windows pairing/permission prompt may deactivate our popup. Let that
        // user-initiated request finish; closed panels ignore its visual result.
        operation.Completed=(result,state)=>{try{if(state==Windows.Foundation.AsyncStatus.Canceled)completion.TrySetCanceled();else if(state==Windows.Foundation.AsyncStatus.Error)completion.TrySetException(result.ErrorCode);else completion.TrySetResult(result.GetResults());}catch(Exception ex){completion.TrySetException(ex);}finally{result.Close();}};
        return completion.Task;
    }
    static TextBlock Label(string text,double size){var value=new TextBlock{Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};value.SetResourceReference(TextBlock.ForegroundProperty,"Text");return value;}
    Wpf.Ui.Controls.Button Button(string name,Func<Task> action){
        var button=Clean11Fluent.Button(name,()=>{},false);Clean11Fluent.Quiet(button);button.Margin=new Thickness(0,2,5,2);button.MinHeight=32;
        button.Click+=async(s,e)=>{if(busy||closed)return;busy=true;button.IsEnabled=false;try{await action();}catch(OperationCanceledException){}catch(Exception ex){if(!closed)status.Text=Error(ex);}finally{busy=false;if(!closed)button.IsEnabled=true;}};return button;
    }
    static string Error(Exception ex){var native=ex as Win32Exception;if(ex is UnauthorizedAccessException||(native!=null&&native.NativeErrorCode==5))return "Windows denied access. Check permissions in Windows Settings, then refresh. Wi-Fi discovery may require Location access.";return "Windows could not complete this request. Try Refresh or More settings. ("+ex.HResult.ToString("X8")+")";}
    StackPanel Card(string title,string detail,string symbol){
        var content=new StackPanel();var heading=new Grid();heading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});heading.ColumnDefinitions.Add(new ColumnDefinition());
        var icon=Clean11Fluent.Icon(symbol,20);icon.Margin=new Thickness(0,0,10,0);icon.SetResourceReference(Control.ForegroundProperty,"Text");heading.Children.Add(icon);
        var name=Label(title,14);name.FontWeight=FontWeights.SemiBold;Grid.SetColumn(name,1);heading.Children.Add(name);content.Children.Add(heading);
        if(detail!=null){var sub=Label(detail,12);sub.Margin=new Thickness(30,4,0,6);sub.SetResourceReference(TextBlock.ForegroundProperty,"Muted");content.Children.Add(sub);}
        var card=new Border{CornerRadius=new CornerRadius(12),Padding=new Thickness(10),Margin=new Thickness(0,0,0,6),BorderThickness=new Thickness(1),Child=content};card.SetResourceReference(Border.BackgroundProperty,"Card");card.SetResourceReference(Border.BorderBrushProperty,"Divider");body.Children.Add(card);return content;
    }
    async Task Reload(bool scan){
        if(closed)return;status.Text="Loading…";body.IsEnabled=false;
        try{if(kind=="wifi")await Wifi(scan);else if(kind=="bluetooth")await Bluetooth();else await Mixer();}
        catch(OperationCanceledException){}catch(Exception ex){if(!closed){body.Children.Clear();status.Text=Error(ex);}}
        finally{if(!closed)body.IsEnabled=true;}
    }
    async Task<bool> Radios(RadioKind type){
        var radios=await Await(Radio.GetRadiosAsync());if(closed)return false;
        var matching=radios.Where(x=>x.Kind==type).ToList();
        foreach(var radio in matching){var current=radio;bool enabled=current.State==RadioState.On;var card=Card(current.Name,enabled?"On":current.State==RadioState.Disabled?"Disabled by hardware or policy":"Off",type==RadioKind.WiFi?"Wifi124":"Bluetooth24");
            var toggle=Button(enabled?"Turn off":"Turn on",async()=>{
                var access=await Await(Radio.RequestAccessAsync());if(access!=RadioAccessStatus.Allowed){status.Text="Windows did not allow radio control. Use More settings to manage access.";return;}
                var result=await Await(current.SetStateAsync(enabled?RadioState.Off:RadioState.On));if(result!=RadioAccessStatus.Allowed){status.Text="Windows could not change this radio. Check hardware switches and device policy.";return;}await Reload(false);
            });toggle.IsEnabled=current.State!=RadioState.Disabled;card.Children.Add(toggle);
        }
        return matching.Any(x=>x.State==RadioState.On);
    }
    async Task Wifi(bool scan){
        body.Children.Clear();bool on=await Radios(RadioKind.WiFi);if(closed)return;if(!on){status.Text="Wi-Fi is off or no wireless adapter is available.";return;}
        var networks=await Task.Run(()=>TopBarWifi.Read(scan));if(scan){await Task.Delay(1600,lifetime.Token);networks=await Task.Run(()=>TopBarWifi.Read(false));}if(closed)return;
        status.Text=networks.Count==0?"No nearby networks. Refresh to try again.":"Nearby networks · Choose a network to connect";
        foreach(var item in networks){var network=item;bool saved=!String.IsNullOrEmpty(network.Profile);
            var card=Card(network.Name,(network.Connected?"Connected · ":saved?"Saved · ":"")+network.Signal+"% signal · "+(network.Secure?"Secured":"Open"),"Wifi124");
                        bool direct=TopBarWifi.CanCreate(network),revealed=false;
            var password=new PasswordBox{Margin=new Thickness(0,8,0,6),MaxLength=63,Visibility=Visibility.Collapsed};
            System.Windows.Automation.AutomationProperties.SetName(password,"Wi-Fi password");card.Children.Add(password);
            var action=Button(network.Connected?"Disconnect":saved?"Connect":direct?"Connect once":"Set up in Windows",async()=>{
                if(!saved&&!network.Connected&&!direct){open("ms-settings:network-wifi");return;}
                if(!saved&&!network.Connected&&network.Secure&&!revealed){revealed=true;password.Visibility=Visibility.Visible;password.Focus();status.Text="Enter the Wi-Fi password, then choose Connect once. It is not saved.";return;}
                if(!saved&&network.Secure&&!network.Connected&&password.Password.Length<8){status.Text="Enter an 8–63 character Wi-Fi password.";return;}
                string secret=password.Password;password.Clear();try{await Task.Run(()=>TopBarWifi.Connect(network,secret));}finally{secret=null;}if(closed)return;status.Text=network.Connected?"Disconnecting…":"Connecting…";
                // WlanConnect accepts a request; it does not report connection success.
                for(int attempt=0;attempt<6;attempt++){await Task.Delay(1000,lifetime.Token);var observed=await Task.Run(()=>TopBarWifi.Read(false));bool connected=observed.Any(x=>x.Adapter==network.Adapter&&x.Name==network.Name&&x.Connected);if(connected!=network.Connected){await Reload(false);return;}}
                await Reload(false);if(!closed)status.Text="Connection has not changed yet. Refresh or check Windows network settings.";
            });action.IsEnabled=network.Connected||!saved||network.Connectable;card.Children.Add(action);
            if(saved)card.Children.Add(Button("Forget network",()=>{Confirm(card,"Remove the saved password? You will need it to reconnect.",async()=>{await Task.Run(()=>TopBarWifi.Forget(network));await Reload(false);});return Task.FromResult(0);}));
        }
    }
    async Task Bluetooth(){
        body.Children.Clear();bool on=await Radios(RadioKind.Bluetooth);if(closed)return;if(!on){status.Text="Bluetooth is off or no Bluetooth adapter is available.";return;}
        status.Text="Looking for devices…";
        const string selector="System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\"";
        var devices=await DiscoverBluetooth(selector);if(closed)return;
        status.Text="";int count=0;
        foreach(var device in devices.Where(x=>!String.IsNullOrWhiteSpace(x.Name)).GroupBy(DeviceKey).Select(g=>g.OrderByDescending(x=>x.Pairing.IsPaired).First()).OrderByDescending(x=>x.Pairing.IsPaired).ThenBy(x=>x.Name)){
            var selected=device;object value;bool connected=device.Properties.TryGetValue("System.Devices.Aep.IsConnected",out value)&&value is bool&&(bool)value;
            var card=Card(device.Name,connected?"Connected":device.Pairing.IsPaired?"Paired":DeviceProperty(device,"System.Devices.Aep.ProtocolId").IndexOf("bb7bb05e",StringComparison.OrdinalIgnoreCase)>=0?"Available · Bluetooth LE":"Available · Bluetooth Classic","Bluetooth24");count++;
            if(device.Pairing.IsPaired){var actions=new WrapPanel();actions.Children.Add(Button(connected?"Connection options":"Connect in Windows",()=>{open("ms-settings:bluetooth");return Task.FromResult(0);}));actions.Children.Add(Button("Forget device",()=>{Confirm(card,"Remove this pairing? You will need to pair the device again.",async()=>{var result=await Await(selected.Pairing.UnpairAsync());if(closed)return;if(result.Status==DeviceUnpairingResultStatus.Unpaired||result.Status==DeviceUnpairingResultStatus.AlreadyUnpaired)await Reload(false);else status.Text="Could not remove device: "+result.Status;});return Task.FromResult(0);}));card.Children.Add(actions);}
            if(!device.Pairing.IsPaired){var pair=Button("Pair",async()=>{var result=await Await(selected.Pairing.PairAsync());if(closed)return;if(result.Status==DevicePairingResultStatus.Paired||result.Status==DevicePairingResultStatus.AlreadyPaired)await Reload(false);else status.Text="Pairing: "+result.Status+". For PIN or device-specific setup, use More settings.";});pair.IsEnabled=device.Pairing.CanPair;card.Children.Add(pair);}
        }
        status.Text=count==0?"No devices found. Make your device discoverable, then refresh.":"Your devices and nearby devices";
    }
    async Task<List<DeviceInformation>> DiscoverBluetooth(string selector){
        // Association-endpoint watchers discover unpaired Classic and LE devices too.
        var found=new Dictionary<string,DeviceInformation>();var gate=new object();
        var watcher=DeviceInformation.CreateWatcher(selector,new[]{"System.Devices.Aep.IsConnected","System.Devices.Aep.DeviceAddress","System.Devices.Aep.ProtocolId"},DeviceInformationKind.AssociationEndpoint);
        Windows.Foundation.TypedEventHandler<DeviceWatcher,DeviceInformation> added=(s,d)=>{lock(gate)found[d.Id]=d;};
        Windows.Foundation.TypedEventHandler<DeviceWatcher,DeviceInformationUpdate> updated=(s,d)=>{lock(gate){DeviceInformation item;if(found.TryGetValue(d.Id,out item))item.Update(d);}};
        Windows.Foundation.TypedEventHandler<DeviceWatcher,DeviceInformationUpdate> removed=(s,d)=>{lock(gate)found.Remove(d.Id);};
        watcher.Added+=added;watcher.Updated+=updated;watcher.Removed+=removed;
        try{watcher.Start();await Task.Delay(6000,lifetime.Token);lock(gate)return found.Values.ToList();}
        finally{watcher.Added-=added;watcher.Updated-=updated;watcher.Removed-=removed;if(watcher.Status==DeviceWatcherStatus.Started||watcher.Status==DeviceWatcherStatus.EnumerationCompleted)watcher.Stop();}
    }
    async Task Mixer(){
        var outputDevices=await Task.Run(()=>TopBarAudio.Endpoints(0));if(closed)return;var inputDevices=await Task.Run(()=>TopBarAudio.Endpoints(1));if(closed)return;var applications=await Task.Run(()=>TopBarAudio.Applications());if(closed)return;body.Children.Clear();
        await DevicePicker(0,"Default output","Speaker224",outputDevices);await DevicePicker(1,"Default input","Mic24",inputDevices);if(closed)return;float level;bool muted;
        if(TopBarAudio.Read(out level,out muted))Volume(Card("Output volume","Current default output device","Speaker224"),level,muted,x=>TopBarAudio.Set(x),x=>TopBarAudio.Mute(x));
        if(applications.Count>0){var hint=Label("App routing · Changes may take effect when the app restarts its audio stream.",12);hint.Margin=new Thickness(8,5,8,10);hint.SetResourceReference(TextBlock.ForegroundProperty,"Muted");body.Children.Add(hint);}
        foreach(var application in applications){var app=application;app.OutputEndpointId=await Task.Run(()=>TopBarAudio.AppEndpoint(app.ProcessId,0));if(closed)return;app.InputEndpointId=await Task.Run(()=>TopBarAudio.AppEndpoint(app.ProcessId,1));if(closed)return;
            string detail="PID "+app.ProcessId+(app.HasOutput?" · Audio active":"")+(app.HasInput?" · Microphone active":"");var card=Card(app.Name,detail,"MusicNote224");
            AppRoutePicker(card,app,0,"Output device",outputDevices,app.OutputEndpointId);AppRoutePicker(card,app,1,"Input device",inputDevices,app.InputEndpointId);
            foreach(var session in app.Sessions){var item=session;Volume(card,item.Level,item.Muted,x=>TopBarAudio.SetSession(item.Id,x,null),x=>TopBarAudio.SetSession(item.Id,null,x));}
        }
        status.Text=applications.Count==0?"No active app audio sessions. Start audio or microphone use to see app routing here.":"Active apps · Choose default devices above or set an app-specific route.";
    }
    static string DeviceProperty(DeviceInformation device,string key){object value;return device.Properties.TryGetValue(key,out value)&&value!=null?value.ToString():"";}
    static string DeviceKey(DeviceInformation device){string address=DeviceProperty(device,"System.Devices.Aep.DeviceAddress");return String.IsNullOrEmpty(address)?device.Id:address.Replace(":","").Replace("-","").ToUpperInvariant();}
    void Confirm(StackPanel card,string message,Func<Task> action){
        if(card.Tag!=null)return;
        var confirmation=new StackPanel{Margin=new Thickness(0,8,0,0)};card.Tag=confirmation;confirmation.Children.Add(Label(message,12));
        var actions=new WrapPanel();actions.Children.Add(Button("Confirm removal",action));actions.Children.Add(Button("Cancel",()=>{card.Children.Remove(confirmation);card.Tag=null;return Task.FromResult(0);}));confirmation.Children.Add(actions);card.Children.Add(confirmation);
    }
    Task DevicePicker(int flow,string title,string symbol,List<TopBarAudio.Endpoint> devices){
        if(closed)return Task.FromResult(0);var card=Card(title,"System-wide default for apps and calls",symbol);
        if(devices.Count==0){card.Children.Add(Label("No active device available",12));return Task.FromResult(0);}
        var picker=new ComboBox{ItemsSource=devices,SelectedItem=devices.FirstOrDefault(x=>x.Default),Margin=new Thickness(0,5,0,4),MinHeight=34,HorizontalContentAlignment=HorizontalAlignment.Stretch};
        System.Windows.Automation.AutomationProperties.SetName(picker,title);picker.ToolTip=picker.SelectedItem;picker.SelectionChanged+=(s,e)=>picker.ToolTip=picker.SelectedItem;card.Children.Add(picker);
        var apply=Button("Use selected device",async()=>{var selected=picker.SelectedItem as TopBarAudio.Endpoint;if(selected==null)return;await Task.Run(()=>TopBarAudio.DefaultEndpoint(selected.Id));await Reload(false);});card.Children.Add(apply);
        return Task.FromResult(0);
    }
    void AppRoutePicker(StackPanel card,TopBarAudio.Application app,int flow,string title,List<TopBarAudio.Endpoint> devices,string currentId){
        var label=Label(title,12);label.Margin=new Thickness(0,7,0,1);label.SetResourceReference(TextBlock.ForegroundProperty,"Muted");card.Children.Add(label);
        var choices=new List<RouteChoice>{new RouteChoice{Name="System default"}};choices.AddRange(devices.Select(x=>new RouteChoice{Id=x.Id,Name=x.Name}));
        var selected=choices.FirstOrDefault(x=>String.Equals(x.Id,currentId,StringComparison.OrdinalIgnoreCase))??choices[0];
        var picker=new ComboBox{ItemsSource=choices,SelectedItem=selected,Margin=new Thickness(0,1,0,4),MinHeight=32,HorizontalContentAlignment=HorizontalAlignment.Stretch};System.Windows.Automation.AutomationProperties.SetName(picker,app.Name+" "+title);picker.ToolTip=selected.Name;card.Children.Add(picker);
        picker.SelectionChanged+=async(s,e)=>{var choice=picker.SelectedItem as RouteChoice;if(choice==null||closed)return;var previous=selected;selected=choice;picker.IsEnabled=false;status.Text="Applying "+title.ToLowerInvariant()+" for "+app.Name+"…";try{bool changed=await Task.Run(()=>TopBarAudio.SetAppEndpoint(app.ProcessId,flow,choice.Id));if(closed)return;if(!changed){picker.SelectedItem=previous;selected=previous;status.Text="Windows could not change this app's route. Try Windows Volume mixer from More settings.";}else{picker.ToolTip=choice.Name;status.Text=String.Equals(choice.Name,"System default",StringComparison.Ordinal)?app.Name+" now follows the system "+title.ToLowerInvariant()+".":app.Name+" routed to "+choice.Name+".";}}catch(Exception ex){if(!closed)status.Text=Error(ex);}finally{if(!closed)picker.IsEnabled=true;}};
    }
    void Volume(StackPanel card,float value,bool muted,Func<float,bool> set,Func<bool,bool> mute){
        var label=Label(Math.Round(value*100)+"%"+(muted?" · Muted":""),12);label.Margin=new Thickness(0,6,0,0);card.Children.Add(label);
        var slider=new Slider{Minimum=0,Maximum=100,Value=value*100,Margin=new Thickness(0,8,0,4),SmallChange=1,LargeChange=5};System.Windows.Automation.AutomationProperties.SetName(slider,"Volume");
        // Commit on release/key-up, avoiding COM enumeration on every drag pixel.
        Action commit=()=>{if(!set((float)slider.Value/100))status.Text="Audio device or session changed. Refresh the mixer.";};
        slider.ValueChanged+=(s,e)=>label.Text=Math.Round(slider.Value)+"%"+(muted?" · Muted":"");slider.PreviewMouseLeftButtonUp+=(s,e)=>commit();slider.PreviewKeyUp+=(s,e)=>commit();card.Children.Add(slider);
        var button=Button(muted?"Unmute":"Mute",()=>Task.FromResult(0));button.Click+=(s,e)=>{if(mute(!muted)){muted=!muted;button.Content=muted?"Unmute":"Mute";label.Text=Math.Round(slider.Value)+"%"+(muted?" · Muted":"");}else status.Text="Audio device or session changed. Refresh the mixer.";};card.Children.Add(button);
    }
}
