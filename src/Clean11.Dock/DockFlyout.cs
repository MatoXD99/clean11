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
        // DWM owns the outer corner and border together. A second WPF rounded
        // border had a different radius and left a square wedge at each corner.
        Content=new Border{Padding=new Thickness(14),Background=Background,Child=body};
        SourceInitialized+=(s,e)=>{DockNative.NoActivate(this);int round=2,dark=1;var h=new WindowInteropHelper(this).Handle;DockNative.DwmSetWindowAttribute(h,33,ref round,4);DockNative.DwmSetWindowAttribute(h,20,ref dark,4);var edge=ActivityTheme.Hover;int borderColor=edge.R|(edge.G<<8)|(edge.B<<16);DockNative.DwmSetWindowAttribute(h,34,ref borderColor,4);};
        ContentRendered+=(s,e)=>Place();Closed+=(s,e)=>{closed=true;foreach(var h in thumbnails)DockNative.DwmUnregisterThumbnail(h);thumbnails.Clear();};
    }
    void Place(){if(closed)return;var handle=new WindowInteropHelper(this).Handle;if(handle==IntPtr.Zero)return;double scale=Math.Max(96,DockNative.GetDpiForWindow(handle))/96.0;if(Double.IsNaN(anchor.X)||Double.IsNaN(anchor.Y)||Double.IsInfinity(anchor.X)||Double.IsInfinity(anchor.Y))return;var b=dock.Screen.WorkingArea;Left=Math.Max(b.Left/scale+8,Math.Min((b.Right/scale)-ActualWidth-8,anchor.X/scale-ActualWidth/2));Top=Math.Max(b.Top/scale+8,anchor.Y/scale-ActualHeight-8);
        bool glass=true;try{glass=OverviewPreferences.Load().Glass;}catch{}
        Background=FrostedMaterial.PanelBrush(new System.Drawing.Rectangle((int)(Left*scale),(int)(Top*scale),(int)(ActualWidth*scale),(int)(ActualHeight*scale)),dock.Screen.Bounds,glass);
        ((Border)Content).Background=Background;
    }
    internal bool Contains(int x,int y){DockNative.Rect r;return !closed&&DockNative.GetWindowRect(new WindowInteropHelper(this).Handle,out r)&&x>=r.Left&&x<=r.Right&&y>=r.Top&&y<=r.Bottom;}
    static Brush PreviewCardBrush(){
        bool glass=true;try{glass=OverviewPreferences.Load().Glass;}catch{}
        if(!FrostedMaterial.Enabled(glass))return ActivityTheme.Brush("Card","#2B2E34");
        return new SolidColorBrush(Color.FromArgb(72,43,46,52));
    }
    sealed class WindowPreview {
        internal DockAppWindow Window;
        internal Border Surface;
        internal TextBlock Placeholder;
        internal IntPtr Thumbnail;
    }
    readonly List<WindowPreview> previews=new List<WindowPreview>();
    ScrollViewer windowScroll;
    internal DockFlyout(DockWindow owner,DockGroup group,Point point):this(owner,point){
        GroupKey=group.Key;Width=360;
        var header=new DockPanel{Margin=new Thickness(2,0,2,12)};
        var count=new TextBlock{Text=group.Windows.Count+" open",Foreground=ActivityTheme.Brush("Muted","#A6ABB5"),FontSize=12,VerticalAlignment=VerticalAlignment.Center};
        DockPanel.SetDock(count,Dock.Right);header.Children.Add(count);
        header.Children.Add(new TextBlock{Text=group.Name,FontSize=15,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis});body.Children.Add(header);
        var list=new Grid();windowScroll=new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false};body.Children.Add(windowScroll);
        foreach(var item in group.Windows){
            var window=item;
            var cardBody=new StackPanel();
            var card=new Border{Background=PreviewCardBrush(),BorderBrush=ActivityTheme.Brush("Divider","#18FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(ActivityTheme.RowRadius),Padding=new Thickness(8),Margin=new Thickness(4,0,4,8),Child=cardBody};
            var titleRow=new Grid{Margin=new Thickness(0,0,0,6)};titleRow.ColumnDefinitions.Add(new ColumnDefinition());titleRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var title=new TextBlock{Text=window.Title,TextTrimming=TextTrimming.CharacterEllipsis,FontSize=12,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};
            var open=Clean11Fluent.Button("",()=>{DockNative.Activate(window,false);dock.CloseFlyout();},false);Clean11Fluent.Quiet(open);open.Content=title;open.HorizontalContentAlignment=HorizontalAlignment.Stretch;open.Padding=new Thickness(3,0,5,0);open.MinWidth=0;open.MinHeight=28;open.ToolTip=window.Title;titleRow.Children.Add(open);
            var close=Clean11Fluent.IconButton("Close "+window.Title,"Dismiss16",()=>{DockNative.PostMessage(window.Handle,0x10,IntPtr.Zero,IntPtr.Zero);dock.CloseFlyout();});Clean11Fluent.Quiet(close);close.Width=28;close.Height=28;close.MinHeight=28;close.Padding=new Thickness(4);Grid.SetColumn(close,1);titleRow.Children.Add(close);cardBody.Children.Add(titleRow);
            var placeholder=new TextBlock{Text=DockNative.IsIconic(window.Handle)?"Minimized window":"Preview unavailable",Foreground=ActivityTheme.Brush("Muted","#A6ABB5"),FontSize=12,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            var surface=new Border{Height=146,Background=ActivityTheme.Brush("Surface","#23252A"),CornerRadius=new CornerRadius(ActivityTheme.ControlRadius),Child=placeholder,Cursor=System.Windows.Input.Cursors.Hand,ToolTip="Switch to "+window.Title};
            surface.MouseLeftButtonUp+=(s,e)=>{DockNative.Activate(window,false);dock.CloseFlyout();};
            card.MouseEnter+=(s,e)=>card.BorderBrush=ActivityTheme.Brush("Accent","#60CDFF");
            card.MouseLeave+=(s,e)=>card.BorderBrush=ActivityTheme.Brush("Divider","#18FFFFFF");
            cardBody.Children.Add(surface);list.Children.Add(card);previews.Add(new WindowPreview{Window=window,Surface=surface,Placeholder=placeholder});
        }
        SourceInitialized+=(s,e)=>{
            var destination=new WindowInteropHelper(this).Handle;double scale=Math.Max(96,DockNative.GetDpiForWindow(destination))/96.0;
            double availableWidth=Math.Max(180,dock.Screen.WorkingArea.Width/scale-16);
            int columns=Math.Max(1,Math.Min(Math.Min(owner.State.PreviewColumns,group.Windows.Count),(int)((availableWidth-46)/260)));
            Width=Math.Min(availableWidth,columns*288+46);
            for(int column=0;column<columns;column++)list.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            for(int row=0;row<(group.Windows.Count+columns-1)/columns;row++)list.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            for(int index=0;index<list.Children.Count;index++){Grid.SetRow(list.Children[index],index/columns);Grid.SetColumn(list.Children[index],index%columns);}
            double available=Math.Max(180,(anchor.Y-dock.Screen.WorkingArea.Top)/scale-24);
            MaxHeight=Math.Min(760,available);windowScroll.MaxHeight=Math.Max(100,MaxHeight-64);
            // Register once per window. Hover never reallocates thumbnail resources.
            foreach(var preview in previews){IntPtr thumbnail;if(DockNative.DwmRegisterThumbnail(destination,preview.Window.Handle,out thumbnail)==0){preview.Thumbnail=thumbnail;thumbnails.Add(thumbnail);}}
        };
        windowScroll.ScrollChanged+=(s,e)=>UpdateWindowPreviews();
        LayoutUpdated+=(s,e)=>UpdateWindowPreviews();
        ContentRendered+=(s,e)=>{Place();UpdateWindowPreviews();};
    }
    void UpdateWindowPreviews(){
        if(closed||windowScroll==null)return;
        var source=PresentationSource.FromVisual(this);if(source==null||source.CompositionTarget==null)return;
        var transform=source.CompositionTarget.TransformToDevice;
        var viewport=windowScroll.TranslatePoint(new Point(0,0),this);
        var clip=new Rect(viewport.X,viewport.Y,Math.Max(0,windowScroll.ViewportWidth),Math.Max(0,windowScroll.ViewportHeight));
        foreach(var preview in previews){
            if(preview.Thumbnail==IntPtr.Zero)continue;
            DockNative.NativeSize size;
            var props=new DockNative.ThumbnailProperties{Flags=8,Visible=false};
            if(DockNative.IsWindow(preview.Window.Handle)&&DockNative.DwmQueryThumbnailSourceSize(preview.Thumbnail,out size)==0&&size.Width>0&&size.Height>0){
                var point=preview.Surface.TranslatePoint(new Point(4,4),this);
                double width=Math.Max(0,preview.Surface.ActualWidth-8),height=Math.Max(0,preview.Surface.ActualHeight-8);
                double factor=Math.Min(width/size.Width,height/size.Height);
                if(factor>0){
                    var fitted=new Rect(point.X+(width-size.Width*factor)/2,point.Y+(height-size.Height*factor)/2,size.Width*factor,size.Height*factor);
                    var visible=Rect.Intersect(fitted,clip);
                    if(!visible.IsEmpty&&visible.Width>=1&&visible.Height>=1){
                        var top=transform.Transform(visible.TopLeft);var bottom=transform.Transform(visible.BottomRight);
                        props=new DockNative.ThumbnailProperties{Flags=1|2|4|8|16,Visible=true,Opacity=255,ClientOnly=false,
                            Destination=new DockNative.Rect{Left=(int)Math.Ceiling(top.X),Top=(int)Math.Ceiling(top.Y),Right=(int)Math.Floor(bottom.X),Bottom=(int)Math.Floor(bottom.Y)},
                            Source=new DockNative.Rect{Left=(int)Math.Round((visible.Left-fitted.Left)/factor),Top=(int)Math.Round((visible.Top-fitted.Top)/factor),Right=(int)Math.Round((visible.Right-fitted.Left)/factor),Bottom=(int)Math.Round((visible.Bottom-fitted.Top)/factor)}};
                    }
                }
            }
            int result=DockNative.DwmUpdateThumbnailProperties(preview.Thumbnail,ref props);
            preview.Placeholder.Visibility=result==0&&props.Visible?Visibility.Hidden:Visibility.Visible;
        }
    }
    internal DockFlyout(DockWindow owner,string path,Point point):this(owner,point){
        body.Children.Add(new TextBlock{Text=System.IO.Path.GetFileName(path),FontSize=16,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)});
        var list=new StackPanel();body.Children.Add(new ScrollViewer{Content=list,MaxHeight=350,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});var status=new TextBlock{Text="Loading folder…",Margin=new Thickness(8)};list.Children.Add(status);
        body.Children.Add(Clean11Fluent.Button("Open folder",()=>{dock.Open(path);dock.CloseFlyout();},false));
        ThreadPool.QueueUserWorkItem(_=>{string[] items;try{items=Directory.EnumerateFileSystemEntries(path).Take(200).OrderByDescending(Directory.Exists).ThenBy(System.IO.Path.GetFileName,StringComparer.CurrentCultureIgnoreCase).Take(60).ToArray();}catch{items=null;}Dispatcher.BeginInvoke((Action)(()=>{if(closed)return;list.Children.Clear();if(items==null||items.Length==0)list.Children.Add(new TextBlock{Text=items==null?"Folder is unavailable.":"This folder is empty.",Margin=new Thickness(8)});else foreach(string entry in items){string target=entry;var button=Clean11Fluent.Button(System.IO.Path.GetFileName(entry),()=>{dock.Open(target);dock.CloseFlyout();},false);Clean11Fluent.Quiet(button);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,2,0,2);list.Children.Add(button);}UpdateLayout();Place();}));});
    }
}
