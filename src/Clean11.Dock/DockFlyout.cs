using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

sealed class DockFlyout : Window {
    internal string GroupKey;
    readonly DockWindow dock; readonly Point anchor; readonly StackPanel body=new StackPanel();
    readonly List<IntPtr> thumbnails=new List<IntPtr>(); bool closed;
    DockFlyout(DockWindow owner,Point point){
        dock=owner;anchor=point;Clean11Fluent.Apply(this);Title="Clean11 dock preview";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;Width=340;SizeToContent=SizeToContent.Height;MaxHeight=520;
        Background=ActivityTheme.Brush("Surface","#202020");Foreground=ActivityTheme.Brush("Text","#F5F5F5");
        Content=new Border{Padding=new Thickness(14),BorderThickness=new Thickness(1),BorderBrush=ActivityTheme.Brush("Border","#505050"),Child=body};
        SourceInitialized+=(s,e)=>{DockNative.NoActivate(this);int round=2,dark=1;var h=new WindowInteropHelper(this).Handle;DockNative.DwmSetWindowAttribute(h,33,ref round,4);DockNative.DwmSetWindowAttribute(h,20,ref dark,4);};
        ContentRendered+=(s,e)=>Place();Closed+=(s,e)=>{closed=true;foreach(var h in thumbnails)DockNative.DwmUnregisterThumbnail(h);thumbnails.Clear();};
    }
    void Place(){if(closed)return;var handle=new WindowInteropHelper(this).Handle;if(handle==IntPtr.Zero)return;double scale=Math.Max(96,DockNative.GetDpiForWindow(handle))/96.0;if(Double.IsNaN(anchor.X)||Double.IsNaN(anchor.Y)||Double.IsInfinity(anchor.X)||Double.IsInfinity(anchor.Y))return;var b=dock.Screen.WorkingArea;Left=Math.Max(b.Left/scale+8,Math.Min((b.Right/scale)-ActualWidth-8,anchor.X/scale-ActualWidth/2));Top=Math.Max(b.Top/scale+8,anchor.Y/scale-ActualHeight-8);}
    internal bool Contains(int x,int y){DockNative.Rect r;return !closed&&DockNative.GetWindowRect(new WindowInteropHelper(this).Handle,out r)&&x>=r.Left&&x<=r.Right&&y>=r.Top&&y<=r.Bottom;}
    internal DockFlyout(DockWindow owner,DockGroup group,Point point):this(owner,point){
        GroupKey=group.Key;body.Children.Add(new TextBlock{Text=group.Name,FontSize=16,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)});
        // One live preview avoids DWM thumbnails drawing outside a scrolling viewport.
        var preview=new Border{Height=150,Background=Brushes.Black,Margin=new Thickness(0,0,0,8),Child=new TextBlock{Text="Window preview",Foreground=Foreground,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
        body.Children.Add(preview);
        var list=new StackPanel();var scroll=new ScrollViewer{Content=list,MaxHeight=270,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};body.Children.Add(scroll);
        IntPtr thumbnail=IntPtr.Zero;
        Action<DockAppWindow> show=window=>{
            if(closed)return;if(thumbnail!=IntPtr.Zero){DockNative.DwmUnregisterThumbnail(thumbnail);thumbnails.Remove(thumbnail);thumbnail=IntPtr.Zero;}
            var destination=new WindowInteropHelper(this).Handle;if(destination==IntPtr.Zero)return;
            if(DockNative.DwmRegisterThumbnail(destination,window.Handle,out thumbnail)!=0)return;thumbnails.Add(thumbnail);
            var transform=PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;Point p=preview.TranslatePoint(new Point(0,0),this);double width=preview.ActualWidth*transform.M11,height=preview.ActualHeight*transform.M22;
            DockNative.NativeSize size;if(DockNative.DwmQueryThumbnailSourceSize(thumbnail,out size)==0&&size.Width>0&&size.Height>0){double factor=Math.Min(width/size.Width,height/size.Height);double w=size.Width*factor,h=size.Height*factor;p.X=p.X*transform.M11+(width-w)/2;p.Y=p.Y*transform.M22+(height-h)/2;width=w;height=h;}else{p=transform.Transform(p);}
            var props=new DockNative.ThumbnailProperties{Flags=1|4|8|16,Destination=new DockNative.Rect{Left=(int)p.X,Top=(int)p.Y,Right=(int)(p.X+width),Bottom=(int)(p.Y+height)},Opacity=255,Visible=true,ClientOnly=false};DockNative.DwmUpdateThumbnailProperties(thumbnail,ref props);
        };
        foreach(var item in group.Windows){var window=item;var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var button=Clean11Fluent.Button(window.Title,()=>{DockNative.Activate(window,false);dock.CloseFlyout();},false);Clean11Fluent.Quiet(button);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,2,4,2);button.ToolTip=window.Title;button.MouseEnter+=(s,e)=>show(window);row.Children.Add(button);var close=Clean11Fluent.IconButton("Close window","Dismiss16",()=>{DockNative.PostMessage(window.Handle,0x10,IntPtr.Zero,IntPtr.Zero);dock.CloseFlyout();});Clean11Fluent.Quiet(close);Grid.SetColumn(close,1);row.Children.Add(close);list.Children.Add(row);}
        ContentRendered+=(s,e)=>{if(group.Windows.Count>0)show(group.Windows[0]);};
    }
    internal DockFlyout(DockWindow owner,string path,Point point):this(owner,point){
        body.Children.Add(new TextBlock{Text=System.IO.Path.GetFileName(path),FontSize=16,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)});
        var list=new StackPanel();body.Children.Add(new ScrollViewer{Content=list,MaxHeight=350,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});var status=new TextBlock{Text="Loading folder…",Margin=new Thickness(8)};list.Children.Add(status);
        body.Children.Add(Clean11Fluent.Button("Open folder",()=>{dock.Open(path);dock.CloseFlyout();},false));
        ThreadPool.QueueUserWorkItem(_=>{string[] items;try{items=Directory.EnumerateFileSystemEntries(path).Take(200).OrderByDescending(Directory.Exists).ThenBy(System.IO.Path.GetFileName,StringComparer.CurrentCultureIgnoreCase).Take(60).ToArray();}catch{items=null;}Dispatcher.BeginInvoke((Action)(()=>{if(closed)return;list.Children.Clear();if(items==null||items.Length==0)list.Children.Add(new TextBlock{Text=items==null?"Folder is unavailable.":"This folder is empty.",Margin=new Thickness(8)});else foreach(string entry in items){string target=entry;var button=Clean11Fluent.Button(System.IO.Path.GetFileName(entry),()=>{dock.Open(target);dock.CloseFlyout();},false);Clean11Fluent.Quiet(button);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,2,0,2);list.Children.Add(button);}UpdateLayout();Place();}));});
    }
}
