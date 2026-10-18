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
    readonly List<IntPtr> thumbnails=new List<IntPtr>(); volatile bool closed; DockBackdrop popupBackdrop;
    DockFlyout(DockWindow owner,Point point){
        dock=owner;anchor=point;Clean11Fluent.Apply(this);Title="Clean11 dock preview";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=Environment.CommandLine.Contains("--inspect-ui");ShowActivated=false;Topmost=true;Width=340;SizeToContent=SizeToContent.Height;MaxHeight=520;
        Background=ActivityTheme.Brush("Surface","#202020");Foreground=new SolidColorBrush(FrostedMaterial.Ink(false));
        // DWM owns the outer corner and border together. A second WPF rounded
        // border had a different radius and left a square wedge at each corner.
        Content=new Border{Padding=new Thickness(14),Background=Background,Child=body};
        SourceInitialized+=(s,e)=>{if(!Environment.CommandLine.Contains("--inspect-ui"))DockNative.NoActivate(this);if(!AllowsTransparency)DockPopupMaterial.PrepareContentHost(new WindowInteropHelper(this).Handle);int round=2,dark=1;var h=new WindowInteropHelper(this).Handle;DockNative.DwmSetWindowAttribute(h,33,ref round,4);DockNative.DwmSetWindowAttribute(h,20,ref dark,4);var edge=ActivityTheme.Hover;int borderColor=edge.R|(edge.G<<8)|(edge.B<<16);DockNative.DwmSetWindowAttribute(h,34,ref borderColor,4);};
        ContentRendered+=(s,e)=>Place();Closed+=(s,e)=>{closed=true;if(popupBackdrop!=null)popupBackdrop.Close();foreach(var h in thumbnails)DockNative.DwmUnregisterThumbnail(h);thumbnails.Clear();};
    }
    void Place(){if(closed)return;var handle=new WindowInteropHelper(this).Handle;if(handle==IntPtr.Zero)return;double scale=Math.Max(96,DockNative.GetDpiForWindow(handle))/96.0;if(Double.IsNaN(anchor.X)||Double.IsNaN(anchor.Y)||Double.IsInfinity(anchor.X)||Double.IsInfinity(anchor.Y))return;var b=dock.Screen.WorkingArea;Left=Math.Max(b.Left/scale+8,Math.Min((b.Right/scale)-ActualWidth-8,anchor.X/scale-ActualWidth/2));Top=Math.Max(b.Top/scale+8,anchor.Y/scale-ActualHeight-8);
        var prefs=OverviewPreferences.Load();
        // The blur lives in a separate HWND below this content window. Never
        // attach a backdrop brush to the HWND that paints text or thumbnails.
        if(popupBackdrop==null)popupBackdrop=new DockBackdrop();
        Background=Brushes.Transparent;
        popupBackdrop.Update(handle,Left,Top,ActualWidth,ActualHeight,scale,true,FrostedMaterial.Enabled(prefs.Glass),prefs.GlassStrength);
        ((Border)Content).Background=Background;
    }
    internal bool Contains(int x,int y){DockNative.Rect r;return !closed&&DockNative.GetWindowRect(new WindowInteropHelper(this).Handle,out r)&&x>=r.Left&&x<=r.Right&&y>=r.Top&&y<=r.Bottom;}
    static Brush PreviewCardBrush(){
        bool glass=true;try{glass=OverviewPreferences.Load().Glass;}catch{}
        if(!FrostedMaterial.Enabled(glass))return ActivityTheme.Brush("Card","#2B2E34");
        var tone=FrostedMaterial.Tone;tone.A=65;return new SolidColorBrush(tone);
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
        var count=new TextBlock{Text=group.Windows.Count+" open",Foreground=new SolidColorBrush(FrostedMaterial.Ink(true)),FontSize=12,VerticalAlignment=VerticalAlignment.Center};
        DockPanel.SetDock(count,Dock.Right);header.Children.Add(count);
        header.Children.Add(new TextBlock{Text=group.Name,FontSize=15,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis});body.Children.Add(header);
        var list=new Grid();windowScroll=new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false};body.Children.Add(windowScroll);
        foreach(var item in group.Windows){
            var window=item;
            var cardBody=new StackPanel();
            var card=new Border{Background=PreviewCardBrush(),BorderBrush=ActivityTheme.Brush("Divider","#18FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(ActivityTheme.RowRadius),Padding=new Thickness(8),Margin=new Thickness(4,0,4,8),Child=cardBody};
            var titleRow=new Grid{Margin=new Thickness(0,0,0,6)};titleRow.ColumnDefinitions.Add(new ColumnDefinition());titleRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var title=new TextBlock{Text=window.Title,TextTrimming=TextTrimming.CharacterEllipsis,FontSize=12,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};
            var open=Clean11Fluent.Button("",()=>{DockNative.Activate(window,false);dock.CloseFlyout();},false);Clean11Fluent.Quiet(open);open.Foreground=Foreground;open.Content=title;open.HorizontalContentAlignment=HorizontalAlignment.Stretch;open.Padding=new Thickness(3,0,5,0);open.MinWidth=0;open.MinHeight=28;open.ToolTip=window.Title;titleRow.Children.Add(open);
            var close=Clean11Fluent.IconButton("Close "+window.Title,"Dismiss16",()=>{DockNative.PostMessage(window.Handle,0x10,IntPtr.Zero,IntPtr.Zero);dock.CloseFlyout();});Clean11Fluent.Quiet(close);close.Foreground=Foreground;close.Width=28;close.Height=28;close.MinHeight=28;close.Padding=new Thickness(4);Grid.SetColumn(close,1);titleRow.Children.Add(close);cardBody.Children.Add(titleRow);
            var placeholder=new TextBlock{Text=DockNative.IsIconic(window.Handle)?"Minimized window":"Preview unavailable",Foreground=new SolidColorBrush(FrostedMaterial.Ink(true)),FontSize=12,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
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
    sealed class FolderEntry {
        internal string Path,Name,Detail; internal DateTime Modified;
    }
    static string FileSize(long size){
        if(size<1024)return size+" B";
        if(size<1048576)return (size/1024.0).ToString("0.#")+" KB";
        if(size<1073741824)return (size/1048576.0).ToString("0.#")+" MB";
        return (size/1073741824.0).ToString("0.#")+" GB";
    }
    internal DockFlyout(DockWindow owner,string path,Point point):this(owner,point){
        AllowsTransparency=true;Background=Brushes.Transparent;Width=440;MaxHeight=620;
        var outer=(Border)Content;outer.Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));outer.BorderThickness=new Thickness(0);outer.Padding=new Thickness(16);
        var header=new DockPanel{Margin=new Thickness(2,0,2,14)};
        var open=Clean11Fluent.Button("Open folder",()=>{dock.Open(path);dock.CloseFlyout();},false);Clean11Fluent.Quiet(open);open.Foreground=Foreground;open.Padding=new Thickness(10,6,10,6);DockPanel.SetDock(open,Dock.Right);header.Children.Add(open);
        var heading=new StackPanel();heading.Children.Add(new TextBlock{Text=System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar)),FontSize=18,FontWeight=FontWeights.SemiBold});
        var subtitle=new TextBlock{Text="Recent files",FontSize=12,Foreground=new SolidColorBrush(FrostedMaterial.Ink(true)),Margin=new Thickness(0,4,0,0)};heading.Children.Add(subtitle);header.Children.Add(heading);body.Children.Add(header);
        var list=new StackPanel();var scroll=new ScrollViewer{Content=list,MaxHeight=480,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};body.Children.Add(scroll);
        list.Children.Add(new TextBlock{Text="Loading recent files…",Margin=new Thickness(8,20,8,20),Foreground=new SolidColorBrush(FrostedMaterial.Ink(true))});
        SourceInitialized+=(s,e)=>{double scale=Math.Max(96,DockNative.GetDpiForWindow(new WindowInteropHelper(this).Handle))/96.0;Width=Math.Min(440,dock.Screen.WorkingArea.Width/scale-16);MaxHeight=Math.Max(160,Math.Min(620,(anchor.Y-dock.Screen.WorkingArea.Top)/scale-24));scroll.MaxHeight=Math.Max(60,MaxHeight-100);};
        var worker=new Thread(()=>{
            try{
            var entries=new List<FolderEntry>();bool failed=false;
            try{foreach(string entry in Directory.EnumerateFileSystemEntries(path)){
                if(closed)return;
                try{var info=new FileInfo(entry);var attributes=info.Attributes;if((attributes&(FileAttributes.Hidden|FileAttributes.System))!=0)continue;
                    bool directory=(attributes&FileAttributes.Directory)!=0;
                    string type=directory?"Folder":String.IsNullOrEmpty(info.Extension)?"File":info.Extension.TrimStart('.').ToUpperInvariant()+" file";
                    entries.Add(new FolderEntry{Path=entry,Name=info.Name,Modified=info.LastWriteTime,Detail=type+(directory?"":" · "+FileSize(info.Length))+" · "+info.LastWriteTime.ToString("d MMM yyyy")});
                }catch{}
            }}catch{failed=true;}
            var recent=entries.OrderByDescending(x=>x.Modified).Take(40).ToArray();
            var pictures=new List<Image>();
            if(closed)return;
            Dispatcher.Invoke((Action)(()=>{
                if(closed)return;list.Children.Clear();subtitle.Text=failed?"Folder unavailable":entries.Count==0?"No files yet":"Newest first · "+(entries.Count>40?"40 of ":"")+entries.Count+" items";
                if(recent.Length==0)list.Children.Add(new TextBlock{Text=failed?"This folder could not be opened.":"Your downloaded files will appear here.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8,20,8,20),Foreground=new SolidColorBrush(FrostedMaterial.Ink(true))});
                foreach(var entry in recent){
                    var item=entry;var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(64)});row.ColumnDefinitions.Add(new ColumnDefinition());
                    var picture=new Image{Width=48,Height=48,Stretch=Stretch.Uniform};pictures.Add(picture);
                    var art=new Border{Width=56,Height=56,CornerRadius=new CornerRadius(8),Background=new SolidColorBrush(Color.FromArgb(18,128,152,176)),Child=picture,HorizontalAlignment=HorizontalAlignment.Left};row.Children.Add(art);
                    var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,4,0)};
                    labels.Children.Add(new TextBlock{Text=item.Name,FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(FrostedMaterial.Ink(false)),TextTrimming=TextTrimming.CharacterEllipsis});
                    labels.Children.Add(new TextBlock{Text=item.Detail,FontSize=11,Foreground=new SolidColorBrush(FrostedMaterial.Ink(true)),Margin=new Thickness(0,5,0,0),TextTrimming=TextTrimming.CharacterEllipsis});Grid.SetColumn(labels,1);row.Children.Add(labels);
                    var button=Clean11Fluent.Button("",()=>{dock.Open(item.Path);dock.CloseFlyout();},false);Clean11Fluent.Quiet(button);button.Content=row;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(8);button.Margin=new Thickness(0,0,0,4);button.ToolTip=item.Name+"\n"+item.Detail;System.Windows.Automation.AutomationProperties.SetName(button,item.Name);list.Children.Add(button);
                }
                UpdateLayout();Place();
            }));
            for(int i=0;i<recent.Length&&!closed;i++){
                var image=DockNative.FilePreview(recent[i].Path);int index=i;
                if(closed)return;
                Dispatcher.BeginInvoke((Action)(()=>{if(!closed&&index<pictures.Count)pictures[index].Source=image;}));
            }
            }catch{if(!closed)DockLog.Write("Folder preview loading was interrupted.");}
        });worker.IsBackground=true;worker.SetApartmentState(ApartmentState.STA);worker.Start();
    }
}
