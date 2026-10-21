using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Hidden-window checks. No input injection, wallpaper writes, or user files.
static class RenderingRegression {
    const BindingFlags Static=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static object Call(Type type,object target,string method,params object[] args){return type.GetMethod(method,target==null?Static:Instance).Invoke(target,args);}
    static object Field(Type type,object target,string name){return type.GetField(name,target==null?Static:Instance).GetValue(target);}
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    static void Settle(Type type,object panel){type.GetField("firstFrameMilliseconds",Instance).SetValue(panel,0.0);Thread.Sleep(650);Call(type,panel,"Animate");}
    [STAThread] static int Main(string[] args){
        try{
            string overviewPath=Path.GetFullPath(args[0]),dockPath=Path.GetFullPath(args[1]);
            AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>{
                string file=Path.Combine(Path.GetDirectoryName(overviewPath),new AssemblyName(e.Name).Name+".dll");
                return File.Exists(file)?Assembly.LoadFrom(file):null;
            };
            var overview=Assembly.LoadFrom(overviewPath);var dock=Assembly.LoadFrom(dockPath);
            Call(overview.GetType("Clean11Fluent"),null,"Ensure");
            var native=overview.GetType("Overview");
            var a=new Rectangle(-1920,0,1920,1080);var b=new Rectangle(20,90,480,270);
            Check((Rectangle)Call(native,null,"Mix",a,b,0.0)==a,"Entrance endpoint");
            Check((Rectangle)Call(native,null,"Mix",a,b,1.0)==b,"Exit endpoint");
            double last=-1;
            for(int i=0;i<=100;i++){double p=(double)Call(native,null,"MotionCurve",i/100.0);Check(p>=last&&p>=0&&p<=1,"Monotonic animation curve");last=p;}
            Check((bool)Call(native,null,"IsToggleSignalCurrent",true,4,false,4),"Reversal signal remains current");
            Check(!(bool)Call(native,null,"IsToggleSignalCurrent",true,4,false,5),"Stale reversal is rejected");

            var material=overview.GetType("FrostedMaterial");Call(material,null,"Invalidate");
            var first=(ImageBrush)Call(material,null,"Create",new Rectangle(0,0,1920,1080));
            Check(first!=null,"Wallpaper material available");
            Call(material,null,"Create",new Rectangle(1920,0,1280,720));
            var reused=(ImageBrush)Call(material,null,"Create",new Rectangle(0,0,1920,1080));
            Check(Object.ReferenceEquals(first.ImageSource,reused.ImageSource),"Alternating displays reuse frozen material");
            Check(first.ImageSource.IsFrozen,"Cached wallpaper is immutable");
            for(int i=0;i<7;i++)Call(material,null,"Create",new Rectangle(i*20,0,1000+i*20,700));
            Check(((IDictionary)Field(material,null,"materials")).Count<=4,"Material entry bound");
            Check((long)Field(material,null,"cacheBytes")<=32L*1024*1024,"Material memory bound");
            Call(material,null,"Invalidate");Check(((IDictionary)Field(material,null,"materials")).Count==0,"Wallpaper invalidation clears all displays");

            using(var owner=(System.Windows.Forms.Form)Activator.CreateInstance(native,Instance,null,new object[]{true},null)){
                var handle=owner.Handle;var panelType=overview.GetType("SpotlightPanel");
                var panel=(Window)Activator.CreateInstance(panelType,Instance,null,new object[]{owner},null);
                try{
                    new System.Windows.Interop.WindowInteropHelper(panel).EnsureHandle();
                    var compact=new Rectangle(100,50,720,64);var expanded=new Rectangle(60,180,800,474);
                    var root=(Grid)Field(panelType,panel,"root");var clip=(RectangleGeometry)root.Clip;
                    Call(panelType,panel,"MoveTo",expanded,compact,false,null);
                    double fixedWidth=root.Width,fixedHeight=root.Height;
                    Call(panelType,panel,"Position",new Rectangle(80,100,760,260));
                    Check(root.Width==fixedWidth&&root.Height==fixedHeight,"Search body layout stays fixed during morph");
                    Check(clip.Rect.Width>0&&clip.Rect.Height>0,"Search clip follows visible bounds");
                    Settle(panelType,panel);
                    Check(!(bool)panelType.GetProperty("IsAnimating",Instance).GetValue(panel,null),"Expansion completes");
                    Check(Double.IsNaN(root.Width)&&Double.IsNaN(root.Height),"Expansion releases fixed layout");
                    bool done=false;
                    Call(panelType,panel,"MoveTo",compact,expanded,false,(Action)(()=>done=true));Settle(panelType,panel);
                    Check(done,"Collapse completion callback");
                    Call(panelType,panel,"MoveTo",expanded,compact,false,null);
                    Call(panelType,panel,"Position",new Rectangle(90,90,745,190));
                    var current=(Rectangle)Call(panelType,panel,"Current");
                    Check(current.Width==745,"Interrupted transition retains presented bounds");
                    Call(panelType,panel,"MoveTo",compact,current,false,null);Settle(panelType,panel);
                    Check(!(bool)panelType.GetProperty("IsAnimating",Instance).GetValue(panel,null),"Reversed transition completes");
                    Call(panelType,panel,"MoveTo",expanded,compact,true,null);
                    Check(!(bool)panelType.GetProperty("IsAnimating",Instance).GetValue(panel,null),"Reduced motion settles immediately");
                    Call(panelType,panel,"HidePanel");Check(!panel.IsVisible,"Hidden search releases rendering callbacks");
                    native.GetField("openingMotion",Instance).SetValue(owner,true);native.GetField("rendering",Instance).SetValue(owner,true);
                    Call(panelType,panel,"ShowOverview",compact,false);
                    Check((bool)Field(panelType,panel,"enteringOverview"),"Overview header uses the shared entrance timeline");
                    Call(panelType,panel,"UpdateOverviewEntrance",.5);Check(root.Opacity==.5,"Header opacity follows preview progress");
                    Call(panelType,panel,"UpdateOverviewEntrance",1.0);Check(!(bool)panelType.GetProperty("IsAnimating",Instance).GetValue(panel,null),"Shared header entrance settles");
                    native.GetField("openingMotion",Instance).SetValue(owner,false);native.GetField("rendering",Instance).SetValue(owner,false);
                }finally{Call(panelType,panel,"Shutdown");}
                var badgeType=overview.GetType("OverviewBadge");
                var badge=(Window)Activator.CreateInstance(badgeType,Instance,null,new object[]{owner,(Action<System.Windows.Forms.MouseEventArgs>)(_=>{}),(Action<System.Windows.Forms.MouseEventArgs>)(_=>{})},null);
                try{
                    int closed=0;
                    Call(badgeType,badge,"PlaceMinimized",new Rectangle(40,80,280,112),"Sample app","Sample minimized window",SystemIcons.Application,(Action)(()=>{}),(Action)(()=>closed++));
                    Call(badgeType,badge,"SetVisibilityProgress",.5);
                    var backdrop=(Window)Field(badgeType,badge,"glassBackdrop");
                    Check(backdrop.Opacity==1,"Minimized glass fades through composition, not native window opacity");
                    Call(badgeType,badge,"SetVisibilityProgress",0.0);Check(!badge.IsVisible&&!backdrop.IsVisible,"Minimized card hides both surfaces");
                    Call(badgeType,badge,"SetVisibilityProgress",1.0);Check(badge.IsVisible&&backdrop.IsVisible,"Minimized card returns with its glass");
                    ((System.Windows.Controls.Primitives.ButtonBase)Field(badgeType,badge,"closeButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    Check(closed==1,"Minimized close action fires once");
                }finally{badge.Close();}
            }
            var schedule=dock.GetType("HorizonSchedule");
            string[] phases={"night","predawn","sunrise","morning","midday","afternoon","sunset","twilight","night"};
            double[] hours={0,5,6.5,8,11,15,18,20,22};
            for(int i=0;i<hours.Length;i++){
                var phase=Call(schedule,null,"At",hours[i]);Check((string)Field(phase.GetType(),phase,"Id")==phases[i],"Horizon local-time boundary");
            }
            Check((bool)Call(schedule,null,"UseDefault",false,true),"New installation uses Horizon");
            Check(!(bool)Call(schedule,null,"UseDefault",true,true),"Existing wallpaper preference preserved");
            Check(!(bool)Call(schedule,null,"UseDefault",false,false),"Missing artwork cannot become default");
            var wallpaperNative=dock.GetType("HorizonNative");
            using(var reader=(IDisposable)Activator.CreateInstance(wallpaperNative,true)){
                Check(((IList)Call(wallpaperNative,reader,"Monitors")).Count>0,"Native wallpaper monitor enumeration");
                Call(wallpaperNative,reader,"Matches",Path.Combine(Path.GetDirectoryName(dockPath),"Wallpapers","Horizon","night.png"));
            }
            Console.WriteLine("PASS: "+checks+" rendering, animation, cache and wallpaper checks. WPF tier="+(RenderCapability.Tier>>16)+". Hidden API checks do not measure displayed FPS.");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
