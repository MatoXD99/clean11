using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Drawing=System.Drawing;
using Forms=System.Windows.Forms;

// Render the overview's wallpaper as a cached frosted material. No desktop or
// application capture: only the user's wallpaper file is read. Per-pixel window
// alpha owns the entire shape, including the material outside curved corners.
static class FrostedMaterial {
    static string signature="";
    static BitmapSource wallpaper;
    static DateTime engineChecked=DateTime.MinValue;
    static string engineImage="";static Drawing.Rectangle engineScreen;
    static Dictionary<string,object> Map(object item){return item as Dictionary<string,object>;}
    static object Field(object item,string key){var map=Map(item);object value;return map!=null&&map.TryGetValue(key,out value)?value:null;}
    static string EngineImage(Drawing.Rectangle screen){
        if(engineScreen==screen&&(DateTime.UtcNow-engineChecked).TotalSeconds<5)return engineImage;
        engineChecked=DateTime.UtcNow;engineScreen=screen;engineImage="";
        try{foreach(string processName in new[]{"wallpaper64","wallpaper32"})foreach(var process in Process.GetProcessesByName(processName))using(process){
            string config=Path.Combine(Path.GetDirectoryName(process.MainModule.FileName),"config.json");if(!File.Exists(config)||new FileInfo(config).Length>1048576)continue;
            var json=new JavaScriptSerializer{MaxJsonLength=1048576};var profiles=json.DeserializeObject(File.ReadAllText(config));object profile=Field(profiles,Environment.UserName);
            if(profile==null){var all=Map(profiles);if(all!=null)foreach(var entry in all)if(Field(entry.Value,"general")!=null){profile=entry.Value;break;}}
            var selected=Map(Field(Field(Field(profile,"general"),"wallpaperconfig"),"selectedwallpapers"));if(selected==null)continue;
            int index=Array.FindIndex(Forms.Screen.AllScreens,item=>item.Bounds==screen);object choice;selected.TryGetValue("Monitor"+Math.Max(0,index),out choice);if(choice==null&&selected.Count==1)foreach(var item in selected)choice=item.Value;
            string active=Convert.ToString(Field(choice,"file"));if(String.IsNullOrEmpty(active))continue;
            string extension=Path.GetExtension(active).ToLowerInvariant();if(extension==".jpg"||extension==".png"||extension==".jpeg"||extension==".bmp"){if(File.Exists(active))return engineImage=active;}
            string folder=Path.GetDirectoryName(active),project=Path.Combine(folder,"project.json");if(!File.Exists(project))continue;
            string preview=Convert.ToString(Field(json.DeserializeObject(File.ReadAllText(project)),"preview"));if(String.IsNullOrEmpty(preview))continue;
            string image=Path.GetFullPath(Path.Combine(folder,preview));if(File.Exists(image))return engineImage=image;
        }}catch{}return "";
    }
    static string WallpaperPath(Drawing.Rectangle screen){
        string engine=EngineImage(screen);if(!String.IsNullOrEmpty(engine))return engine;
        string cached=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","Windows","Themes","TranscodedWallpaper");
        if(File.Exists(cached))return cached;
        try{using(var key=Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))return key==null?"":Convert.ToString(key.GetValue("WallPaper",""));}catch{return "";}
    }
    internal static ImageBrush Create(Drawing.Rectangle screen){
        try{
            string path=WallpaperPath(screen);string style="10",tile="0";
            using(var key=Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop")){if(key!=null){style=Convert.ToString(key.GetValue("WallpaperStyle","10"));tile=Convert.ToString(key.GetValue("TileWallpaper","0"));}}
            string next=screen.ToString()+"|"+path+"|"+(File.Exists(path)?File.GetLastWriteTimeUtc(path).Ticks:0)+"|"+style+"|"+tile;
            if(next!=signature){
                wallpaper=Load(path,screen,style,tile);signature=next;
                Trace.WriteLine("Frosted wallpaper material cached; screen="+screen.Width+"x"+screen.Height+", available="+(wallpaper!=null));
            }
            return wallpaper==null?null:new ImageBrush(wallpaper){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.RelativeToBoundingBox,TileMode=TileMode.None};
        }catch(Exception error){Trace.WriteLine("Frosted wallpaper fallback: "+error.GetType().Name);return null;}
    }
    static BitmapSource Load(string path,Drawing.Rectangle screen,string style,string tile){
        if(!File.Exists(path))return null;
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(path);image.DecodePixelWidth=1600;image.EndInit();image.Freeze();
        double scale=Math.Min(1,1280.0/screen.Width);int width=Math.Max(1,(int)Math.Ceiling(screen.Width*scale)),height=Math.Max(1,(int)Math.Ceiling(screen.Height*scale));
        double sourceWidth=image.PixelWidth,sourceHeight=image.PixelHeight;
        var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){
            dc.DrawRectangle(Brushes.Black,null,new Rect(0,0,width,height));
            if(tile=="1"){
                double tw=sourceWidth*scale,th=sourceHeight*scale;
                for(double y=0;y<height;y+=Math.Max(1,th))for(double x=0;x<width;x+=Math.Max(1,tw))dc.DrawImage(image,new Rect(x,y,tw,th));
            }else{
                Rect area=new Rect(0,0,width,height);
                if(style=="22"){
                    var all=Forms.SystemInformation.VirtualScreen;double factor=Math.Max(all.Width/sourceWidth,all.Height/sourceHeight)*scale;
                    double iw=sourceWidth*factor,ih=sourceHeight*factor;area=new Rect((all.Width*scale-iw)/2+(all.Left-screen.Left)*scale,(all.Height*scale-ih)/2+(all.Top-screen.Top)*scale,iw,ih);
                }else if(style!="2"){
                    double factor=style=="6"?Math.Min(width/sourceWidth,height/sourceHeight):style=="0"?scale:Math.Max(width/sourceWidth,height/sourceHeight);
                    double iw=sourceWidth*factor,ih=sourceHeight*factor;area=new Rect((width-iw)/2,(height-ih)/2,iw,ih);
                }
                dc.DrawImage(image,area);
            }
        }
        var original=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);original.Render(drawing);original.Freeze();
        var blur=new System.Windows.Controls.Image{Source=original,Width=width,Height=height,Stretch=Stretch.Fill,Effect=new BlurEffect{Radius=ActivityTheme.Number("BlurRadius",28)*scale,KernelType=KernelType.Gaussian,RenderingBias=RenderingBias.Performance}};
        blur.Measure(new Size(width,height));blur.Arrange(new Rect(0,0,width,height));
        var result=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);result.Render(blur);result.Freeze();return result;
    }
    internal static void Position(ImageBrush brush,Drawing.Rectangle bounds,Drawing.Rectangle screen){if(brush==null||screen.Width<=0||screen.Height<=0)return;brush.Viewbox=new Rect((bounds.Left-screen.Left)/(double)screen.Width,(bounds.Top-screen.Top)/(double)screen.Height,bounds.Width/(double)screen.Width,bounds.Height/(double)screen.Height);}
    internal static bool Enabled(bool preference){
        if(!preference||SystemParameters.HighContrast)return false;
        try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return key==null||Convert.ToInt32(key.GetValue("EnableTransparency",1))!=0;}catch{return true;}
    }
    internal static Brush Tint(){
        int strength=60;try{strength=OverviewPreferences.Load().GlassStrength;}catch{}
        var color=ActivityTheme.Brush("Surface","#23252A").Color;
        color.A=(byte)Math.Round(255*(.94-.0064*Math.Max(0,Math.Min(100,strength))));
        var brush=new SolidColorBrush(color);brush.Freeze();return brush;
    }
    internal static Brush PanelBrush(Drawing.Rectangle bounds,Drawing.Rectangle screen,bool glass){
        var image=Enabled(glass)?Create(screen):null;if(image==null)return ActivityTheme.Brush("Surface","#202632");Position(image,bounds,screen);
        var drawing=new DrawingGroup();using(var dc=drawing.Open()){dc.DrawRectangle(image,null,new Rect(0,0,1,1));dc.DrawRectangle(Tint(),null,new Rect(0,0,1,1));}var result=new DrawingBrush(drawing){Stretch=Stretch.Fill};result.Freeze();return result;
    }
    internal static void StyleMenu(System.Windows.Controls.ContextMenu menu,bool glass){
        Clean11Fluent.Menu(menu);
    }
}
