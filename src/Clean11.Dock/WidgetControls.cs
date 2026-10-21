using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

// Settings exchanges commands with the widget host; it never writes its data
// file or receives the private contents of notes/tasks.
sealed class WidgetCommand { public string Action="snapshot",Id="",Kind="",Size="";public bool Value; }
sealed class WidgetSummary { public string Id,Kind,Size;public bool Visible; }
sealed class WidgetSnapshot {
    public bool Enabled,Editing;public string Error="";
    public List<WidgetSummary> Cards=new List<WidgetSummary>();
}

sealed class WidgetControlServer : IDisposable {
    internal static string PipeName {get{return "Clean11.Widgets.Control."+Process.GetCurrentProcess().SessionId;}}
    readonly DesktopWidgetHost host;readonly Dispatcher dispatcher;
    readonly object gate=new object();NamedPipeServerStream pending;volatile bool disposed;
    internal WidgetControlServer(DesktopWidgetHost owner){
        host=owner;dispatcher=Application.Current.Dispatcher;
        var thread=new Thread(Run){IsBackground=true,Name="Clean11 widget controls"};thread.Start();
    }
    static NamedPipeServerStream NewPipe(){
#if NET8_0_OR_GREATER
        return new NamedPipeServerStream(PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
#else
        var security=new PipeSecurity();var sid=WindowsIdentity.GetCurrent().User;
        security.SetAccessRuleProtection(true,false);
        security.AddAccessRule(new PipeAccessRule(sid,PipeAccessRights.FullControl,AccessControlType.Allow));
        return new NamedPipeServerStream(PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,security);
#endif
    }
    void Run(){
        while(!disposed){
            try{using(var pipe=NewPipe()){
                lock(gate){if(disposed)return;pending=pipe;}
                pipe.WaitForConnection();
                using(var reader=new StreamReader(pipe,new UTF8Encoding(false),false,4096,true))
                using(var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true}){
                    var read=reader.ReadLineAsync();if(!read.Wait(2500))continue;
                    string line=read.Result;if(String.IsNullOrEmpty(line)||line.Length>4096)continue;
                    var json=new JavaScriptSerializer{MaxJsonLength=65536};
                    var request=json.Deserialize<WidgetCommand>(line);if(request==null)continue;
                    var result=(WidgetSnapshot)dispatcher.Invoke((Func<WidgetSnapshot>)(()=>ProcessCommand(request)));
                    writer.WriteLine(json.Serialize(result));
                }
            }}catch(Exception error){if(!disposed){DockLog.Write("Widget controls: "+error.GetType().Name);Thread.Sleep(150);}}
            finally{lock(gate)pending=null;}
        }
    }
    WidgetSnapshot ProcessCommand(WidgetCommand request){
        var state=host.Store.Widgets.FirstOrDefault(x=>x.Id==request.Id);
        switch(request.Action){
            case "add":host.Add(request.Kind);break;
            case "enabled":host.SetEnabled(request.Value);break;
            case "editing":host.SetEditing(request.Value);break;
            case "visible":if(state!=null&&state.Visible!=request.Value)host.Toggle(state);break;
            case "size":if(state!=null&&new[]{"Small","Medium","Large"}.Contains(request.Size)){
                state.Size=request.Size;var window=host.Windows.FirstOrDefault(x=>x.State==state);
                if(window!=null)window.ResizeCard();host.Changed();}break;
        }
        return new WidgetSnapshot{Enabled=host.Store.Enabled,Editing=host.Editing,Error=host.SaveError,
            Cards=host.Store.Widgets.Select(x=>new WidgetSummary{Id=x.Id,Kind=x.Kind,Size=x.Size,Visible=x.Visible}).ToList()};
    }
    public void Dispose(){disposed=true;lock(gate){if(pending!=null)pending.Dispose();}}
}

static class WidgetControlClient {
    static readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
    internal static async Task<WidgetSnapshot> Send(WidgetCommand request){
        await gate.WaitAsync();
        try{using(var pipe=new NamedPipeClientStream(".",WidgetControlServer.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous)){
            await Task.Run(()=>pipe.Connect(400));
            var json=new JavaScriptSerializer{MaxJsonLength=65536};
            using(var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true})
            using(var reader=new StreamReader(pipe,new UTF8Encoding(false),false,4096,true)){
                await writer.WriteLineAsync(json.Serialize(request));
                var read=reader.ReadLineAsync();if(await Task.WhenAny(read,Task.Delay(2000))!=read)throw new TimeoutException();
                string reply=await read;if(String.IsNullOrEmpty(reply)||reply.Length>65536)throw new IOException("No widget response.");
                var snapshot=json.Deserialize<WidgetSnapshot>(reply);if(snapshot==null)throw new IOException("Invalid widget response.");return snapshot;
            }
        }}finally{gate.Release();}
    }
}

sealed class WidgetGalleryPage : StackPanel {
    readonly CheckBox visible=new CheckBox(),editing=new CheckBox();
    readonly TextBlock count=DesktopWidgetWindow.Text("Connecting to your widgets…",12,true),status=DesktopWidgetWindow.Text("Widget changes save automatically on this PC.",12,true);
    readonly Wpf.Ui.Controls.Button retry;readonly StackPanel saved=new StackPanel();readonly DispatcherTimer poll=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
    bool syncing,refreshing,active;int pending;string savedKey="";WidgetSnapshot current;
    internal WidgetGalleryPage(){
        var controls=new WrapPanel{Margin=new Thickness(0,0,0,22)};
        visible.Content="Show desktop widgets";visible.Margin=new Thickness(0,0,26,0);visible.SetResourceReference(CheckBox.ForegroundProperty,"Text");
        editing.Content="Edit layout";editing.SetResourceReference(CheckBox.ForegroundProperty,"Text");
        visible.Checked+=(s,e)=>SetBoolean("enabled",true);visible.Unchecked+=(s,e)=>SetBoolean("enabled",false);
        editing.Checked+=(s,e)=>SetBoolean("editing",true);editing.Unchecked+=(s,e)=>SetBoolean("editing",false);
        controls.Children.Add(visible);controls.Children.Add(editing);Children.Add(controls);
        var cards=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};
        foreach(string kind in DesktopWidgetStore.Kinds){
            string selected=kind;var content=new StackPanel();
            string example=kind=="clock"?DateTime.Now.ToString("HH:mm"):kind=="calendar"?DateTime.Today.ToString("MMM d"):kind=="todo"?"○  Make room for today":"A thought worth keeping.";
            var preview=DesktopWidgetWindow.Text(example,23);preview.Margin=new Thickness(0,0,0,20);content.Children.Add(preview);
            var title=DesktopWidgetWindow.Text(DesktopWidgetWindow.DisplayName(kind),15);title.FontWeight=FontWeights.SemiBold;content.Children.Add(title);
            var detail=DesktopWidgetWindow.Text(kind=="clock"?"Your time, at a glance.":kind=="calendar"?"A little perspective on your month.":kind=="todo"?"Small steps, kept close.":"A quiet space for your thoughts.",12,true);detail.Margin=new Thickness(0,6,0,14);content.Children.Add(detail);
            var add=DesktopWidgetWindow.SmallButton("+ Add widget",()=>Command(new WidgetCommand{Action="add",Kind=selected}));add.HorizontalAlignment=HorizontalAlignment.Left;content.Children.Add(add);
            var card=new Border{Child=content,Padding=new Thickness(20),Margin=new Thickness(0,0,12,12),CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1)};
            card.SetResourceReference(Border.BackgroundProperty,"Card");card.SetResourceReference(Border.BorderBrushProperty,"Divider");cards.Children.Add(card);
        }
        Children.Add(cards);
        var section=DesktopWidgetWindow.Text("YOUR WIDGETS",11,true);section.Margin=new Thickness(2,16,0,6);Children.Add(section);
        count.Margin=new Thickness(2,0,0,12);Children.Add(count);Children.Add(saved);
        status.Margin=new Thickness(2,16,0,0);status.TextWrapping=TextWrapping.Wrap;Children.Add(status);
        var help=DesktopWidgetWindow.Text("Enable Edit layout, then drag a card’s header on your desktop. Hidden cards keep their notes and tasks.",12,true);help.Margin=new Thickness(2,10,0,4);Children.Add(help);
        retry=DesktopWidgetWindow.SmallButton("Reconnect widgets",()=>Connect());retry.Visibility=Visibility.Collapsed;retry.HorizontalAlignment=HorizontalAlignment.Left;retry.Margin=new Thickness(0,8,0,0);Children.Add(retry);
        poll.Tick+=(s,e)=>Refresh();Loaded+=(s,e)=>{active=true;Connect();poll.Start();};
        Unloaded+=(s,e)=>{active=false;poll.Stop();if(current!=null&&current.Editing)Command(new WidgetCommand{Action="editing",Value=false});};
    }
    void ShowError(string message){status.Text=message;retry.Visibility=Visibility.Visible;}
    void SetBoolean(string action,bool value){if(!syncing)Command(new WidgetCommand{Action=action,Value=value});}
    async void Connect(){
        try{DesktopWidgetBridge.Start(false);}catch(Exception){ShowError("Could not start widgets. Use Reconnect to try again.");return;}
        for(int i=0;i<6&&active;i++){try{Render(await WidgetControlClient.Send(new WidgetCommand()));return;}catch(Exception){}await Task.Delay(250);}
        if(active)ShowError("Widgets are unavailable. Use Reconnect to try again.");
    }
    async void Refresh(){
        if(!active||refreshing||pending>0)return;refreshing=true;
        try{Render(await WidgetControlClient.Send(new WidgetCommand()));}catch(Exception){if(active)ShowError("Widgets are temporarily unavailable.");}finally{refreshing=false;}
    }
    async void Command(WidgetCommand request){
        pending++;try{Render(await WidgetControlClient.Send(request));}catch(Exception){if(active)ShowError("That change could not be sent. Reconnect widgets and try again.");}finally{pending--;}
    }
    void Render(WidgetSnapshot snapshot){
        current=snapshot;if(!active)return;retry.Visibility=Visibility.Collapsed;syncing=true;visible.IsChecked=snapshot.Enabled;editing.IsChecked=snapshot.Editing;syncing=false;
        count.Text=snapshot.Cards.Count+" cards · "+(snapshot.Enabled?snapshot.Cards.Count(x=>x.Visible):0)+" on desktop";
        status.Text=String.IsNullOrEmpty(snapshot.Error)?"Widget changes save automatically on this PC.":snapshot.Error;
        string key=String.Join("|",snapshot.Cards.Select(x=>x.Id+":"+x.Kind+":"+x.Size+":"+x.Visible));if(key==savedKey)return;savedKey=key;saved.Children.Clear();
        foreach(var card in snapshot.Cards){
            var item=card;var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,18,0)};labels.Children.Add(DesktopWidgetWindow.Text(DesktopWidgetWindow.DisplayName(item.Kind),14));labels.Children.Add(DesktopWidgetWindow.Text(item.Visible?"On your desktop":"Hidden · Content kept",12,true));row.Children.Add(labels);
            var size=new ComboBox{ItemsSource=new[]{"Small","Medium","Large"},SelectedItem=item.Size,Width=112,MinHeight=32,Margin=new Thickness(0,0,14,0),VerticalAlignment=VerticalAlignment.Center};
            System.Windows.Automation.AutomationProperties.SetName(size,DesktopWidgetWindow.DisplayName(item.Kind)+" widget size");
            size.SelectionChanged+=(s,e)=>{if(size.SelectedItem!=null)Command(new WidgetCommand{Action="size",Id=item.Id,Size=(string)size.SelectedItem});};Grid.SetColumn(size,1);row.Children.Add(size);
            var toggle=DesktopWidgetWindow.SmallButton(item.Visible?"Hide":"Show",()=>Command(new WidgetCommand{Action="visible",Id=item.Id,Value=!item.Visible}));toggle.MinWidth=56;Grid.SetColumn(toggle,2);row.Children.Add(toggle);
            var frame=new Border{Child=row,Padding=new Thickness(16,12,16,12),Margin=new Thickness(0,0,12,8),CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1)};frame.SetResourceReference(Border.BackgroundProperty,"Card");frame.SetResourceReference(Border.BorderBrushProperty,"Divider");saved.Children.Add(frame);
        }
    }
}
