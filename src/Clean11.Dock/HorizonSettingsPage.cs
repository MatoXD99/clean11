using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

sealed class HorizonSettingsPage : StackPanel {
    readonly HorizonService service=HorizonService.Instance;
    readonly Image preview=new Image{Stretch=Stretch.UniformToFill};
    readonly TextBlock previewTitle=DesktopWidgetWindow.Text("",16),previewTime=DesktopWidgetWindow.Text("",12,true),message=DesktopWidgetWindow.Text("",12,true),now=DesktopWidgetWindow.Text("",12,true);
    readonly Wpf.Ui.Controls.ToggleSwitch enabled=new Wpf.Ui.Controls.ToggleSwitch();
    readonly ComboBox mode=new ComboBox{ItemsSource=new[]{"Dynamic","Always light","Always dark"},Width=180,MinHeight=34};
    readonly Slider time=new Slider{Minimum=0,Maximum=23.75,TickFrequency=.25,IsSnapToTickEnabled=true,Value=DateTime.Now.TimeOfDay.TotalHours};
    readonly Wpf.Ui.Controls.Button restore;
    readonly DispatcherTimer poll=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
    readonly Dictionary<string,Border> tiles=new Dictionary<string,Border>();
    readonly Dictionary<string,BitmapSource> images=new Dictionary<string,BitmapSource>();
    bool syncing,busy;string previewId="";
    internal HorizonSettingsPage(){
        Margin=new Thickness(0,0,12,0);
        var hero=new Grid{Height=244};hero.Children.Add(preview);
        var wash=new Border{Background=new LinearGradientBrush(Colors.Transparent,Color.FromArgb(220,9,18,34),new Point(0,.2),new Point(0,1))};hero.Children.Add(wash);
        var caption=new StackPanel{Margin=new Thickness(20),VerticalAlignment=VerticalAlignment.Bottom};previewTitle.Foreground=Brushes.White;previewTitle.FontWeight=FontWeights.SemiBold;previewTime.Foreground=new SolidColorBrush(Color.FromRgb(221,230,241));previewTime.Margin=new Thickness(0,5,0,0);caption.Children.Add(previewTitle);caption.Children.Add(previewTime);hero.Children.Add(caption);
        var frame=Card(hero,0);frame.SizeChanged+=(s,e)=>frame.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,frame.ActualWidth),Math.Max(0,frame.ActualHeight)),16,16);Children.Add(frame);
        var timeBox=new StackPanel();var line=new DockPanel();var label=DesktopWidgetWindow.Text("Preview a time of day",14);label.FontWeight=FontWeights.SemiBold;line.Children.Add(label);var local=DesktopWidgetWindow.SmallButton("Use current time",()=>{time.Value=Math.Min(23.75,DateTime.Now.TimeOfDay.TotalHours);UpdatePreview();});local.HorizontalAlignment=HorizontalAlignment.Right;line.Children.Add(local);timeBox.Children.Add(line);time.Margin=new Thickness(0,12,0,0);System.Windows.Automation.AutomationProperties.SetName(time,"Wallpaper preview time");time.ValueChanged+=(s,e)=>UpdatePreview();timeBox.Children.Add(time);Children.Add(Card(timeBox,16));
        var gallery=new System.Windows.Controls.Primitives.UniformGrid{Columns=4,Margin=new Thickness(0,2,0,14)};
        foreach(var phase in HorizonSchedule.Phases){var item=phase;var content=new StackPanel();content.Children.Add(new Image{Source=ImageFor(item,true),Height=61,Stretch=Stretch.UniformToFill,Margin=new Thickness(0,0,0,7)});content.Children.Add(DesktopWidgetWindow.Text(item.Name,12));content.Children.Add(DesktopWidgetWindow.Text(item.TimeLabel,10,true));var button=Clean11Fluent.Button("",()=>{time.Value=item.Starts;UpdatePreview();},false);Clean11Fluent.Quiet(button);button.Content=content;button.Padding=new Thickness(7);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;System.Windows.Automation.AutomationProperties.SetName(button,item.Name+" wallpaper preview");var tile=new Border{Child=button,CornerRadius=new CornerRadius(12),BorderThickness=new Thickness(1),Margin=new Thickness(0,0,8,8)};tile.SetResourceReference(Border.BorderBrushProperty,"Divider");gallery.Children.Add(tile);tiles[item.Id]=tile;}Children.Add(gallery);
        AddRow("Use Clean11 Horizon","One scene, eight lighting states. Turning this off restores your previous Windows wallpaper.",enabled);
        AddRow("Lighting","Dynamic follows the local clock. Light and dark keep a single state all day.",mode);
        var enabledRow=Children[Children.Count-2];var modeRow=Children[Children.Count-1];Children.Remove(enabledRow);Children.Remove(modeRow);Children.Insert(1,enabledRow);Children.Insert(2,modeRow);
        now.Margin=new Thickness(2,8,0,0);Children.Add(now);message.Margin=new Thickness(2,8,0,0);message.TextWrapping=TextWrapping.Wrap;Children.Add(message);
        var help=DesktopWidgetWindow.Text("Previewing does not change your desktop. The schedule uses this PC’s time zone and fixed local hours, without location access. Keep Clean11 running for automatic changes.",12,true);help.Margin=new Thickness(2,12,0,14);Children.Add(help);
        restore=DesktopWidgetWindow.SmallButton("Restore previous wallpaper",()=>Run(service.Restore()));restore.HorizontalAlignment=HorizontalAlignment.Left;Children.Add(restore);
        enabled.Checked+=(s,e)=>{if(!syncing)Run(service.SetEnabled(true));};enabled.Unchecked+=(s,e)=>{if(!syncing)Run(service.SetEnabled(false));};mode.SelectionChanged+=(s,e)=>{if(!syncing&&mode.SelectedItem!=null)Run(service.SetMode((string)mode.SelectedItem));};
        Loaded+=(s,e)=>{Sync();UpdatePreview();poll.Start();};Unloaded+=(s,e)=>poll.Stop();poll.Tick+=(s,e)=>Sync();
    }
    static Border Card(UIElement content,double padding){var frame=new Border{Child=content,CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),Padding=new Thickness(padding),Margin=new Thickness(0,0,0,10)};frame.SetResourceReference(Border.BackgroundProperty,"Card");frame.SetResourceReference(Border.BorderBrushProperty,"Divider");return frame;}
    void AddRow(string title,string description,FrameworkElement control){var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var labels=new StackPanel{Margin=new Thickness(0,0,20,0),VerticalAlignment=VerticalAlignment.Center};var name=DesktopWidgetWindow.Text(title,14);name.FontWeight=FontWeights.SemiBold;labels.Children.Add(name);var detail=DesktopWidgetWindow.Text(description,12,true);detail.Margin=new Thickness(0,4,0,0);labels.Children.Add(detail);row.Children.Add(labels);control.VerticalAlignment=VerticalAlignment.Center;System.Windows.Automation.AutomationProperties.SetName(control,title);Grid.SetColumn(control,1);row.Children.Add(control);Children.Add(Card(row,18));}
    BitmapSource ImageFor(HorizonPhase phase,bool thumbnail=false){string key=phase.Id+(thumbnail?"-thumb":"");BitmapSource image;if(images.TryGetValue(key,out image))return image;if(!File.Exists(phase.FileName))return null;try{var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=thumbnail?220:1100;bitmap.UriSource=new Uri(phase.FileName);bitmap.EndInit();bitmap.Freeze();images[key]=bitmap;return bitmap;}catch{return null;}}
    void UpdatePreview(){var phase=HorizonSchedule.At(time.Value);if(previewId!=phase.Id){previewId=phase.Id;preview.Source=ImageFor(phase);}previewTitle.Text="Clean11 Horizon · "+phase.Name;previewTime.Text=DateTime.Today.AddHours(time.Value).ToString("HH:mm")+" preview · Desktop follows your selected lighting mode";foreach(var pair in tiles){if(pair.Key==phase.Id)pair.Value.SetResourceReference(Border.BackgroundProperty,"Selected");else pair.Value.Background=Brushes.Transparent;}}
    async void Run(Task<HorizonSnapshot> operation){busy=true;Sync();try{await operation;}catch(Exception){message.Text="Horizon could not complete this change. Try again.";}finally{busy=false;Sync();}}
    void Sync(){
        var state=service.Snapshot;syncing=true;enabled.IsChecked=state.Enabled;mode.SelectedItem=state.Mode;syncing=false;enabled.IsEnabled=state.Ready&&state.Available&&!busy;mode.IsEnabled=state.Ready&&!busy;restore.IsEnabled=state.CanRestore&&!busy;message.Text=busy?"Updating your wallpaper…":state.Message;
        var next=HorizonSchedule.Next(DateTime.Now.TimeOfDay.TotalHours);now.Text="Local time "+DateTime.Now.ToString("HH:mm")+" · "+(state.Enabled?"Now: "+state.Phase:"Horizon off")+(state.Enabled&&state.Mode=="Dynamic"?" · Next: "+next.Name+" at "+next.TimeLabel:"");
    }
}
