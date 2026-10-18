using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

// Generate design deliverables with synthetic data; never show a window or inject input.
static class RenderDesign {
    [STAThread]static int Main(string[] args){
        try{
            Directory.CreateDirectory(args[0]);
            using(var owner=new Overview(true)){
                var apps=new List<AppItem>{
                    new AppItem{Name="PowerShell",Path="powershell.exe",Target="powershell.exe",IconPath="powershell.exe"},
                    new AppItem{Name="Windows Terminal",Path="wt.exe",Target="wt.exe",IconPath="wt.exe"},
                    new AppItem{Name="Command Prompt",Path="cmd.exe",Target="cmd.exe",IconPath="cmd.exe"}
                };
                typeof(Overview).GetField("apps",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,apps);
                var state=(OverviewState)typeof(Overview).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
                state.Favorites.Clear();foreach(var app in apps)state.Favorites.Add("app:"+app.Path);
                var cache=(Dictionary<string,Icon>)typeof(Overview).GetField("iconCache",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
                foreach(var app in apps)cache[app.IconPath]=(Icon)SystemIcons.Application.Clone();
                var items=new List<ResultItem>();foreach(var app in apps)items.Add(new ResultItem{App=app,Label=app.Name,Detail="Application"});
                var panel=new SpotlightPanel(owner);panel.WarmHidden();
                panel.SaveOffscreenPreview(Path.Combine(args[0],"Clean11-search.png"),false,items,"power");
                panel.SaveOffscreenPreview(Path.Combine(args[0],"Clean11-settings.png"),true,new ResultItem[0],"");
                panel.SaveOffscreenHeader(Path.Combine(args[0],"Clean11-overview-bar.png"));
                panel.Shutdown();
            }
            Console.WriteLine("Design previews rendered without showing windows.");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
