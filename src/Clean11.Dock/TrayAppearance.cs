using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Windows;

// The tray keeps its existing shell actions; Clean11 owns its appearance.
sealed class TrayAppearance : IDisposable {
    const string Title="Clean11 Background Apps";
    readonly DockWindow dock;
    DockBackdrop backdrop;DateTime next=DateTime.MinValue;IntPtr inspectionWindow,inspectionStyle;
    internal TrayAppearance(DockWindow owner){dock=owner;}
    internal static void Sync(){
        try{
            string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"com.seelen.seelen-ui","widgets","tray-toggle-fix");
            string metadata=Path.Combine(directory,"metadata.yml"),template=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tray.css");
            if(!File.Exists(metadata)||!File.ReadAllText(metadata).Contains("@local/system-tray")||!File.Exists(template))return;
            var tone=FrostedMaterial.Tone;var ink=FrostedMaterial.Ink(false);var muted=FrostedMaterial.Ink(true);
            string css=":root{--clean11-Text:"+Hex(ink)+";--clean11-Muted:"+Hex(muted)+";--clean11-Hover:#839cb426;--clean11-Selected:#839cb43a;--clean11-Border:#a0b8cc45;}\n"+File.ReadAllText(template);
            string target=Path.Combine(directory,"style.css");bool changed=!File.Exists(target)||File.ReadAllText(target)!=css;
            if(changed)File.WriteAllText(target,css);
            string toggleTemplate=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tray-toggle.js"),togglePath=Path.Combine(directory,"toggle.js");
            if(File.Exists(toggleTemplate)&&File.Exists(togglePath)){
                string toggle=File.ReadAllText(toggleTemplate);
                if(File.ReadAllText(togglePath)!=toggle){File.WriteAllText(togglePath,toggle,new System.Text.UTF8Encoding(false));changed=true;}
            }
            string sourcePath=Path.Combine(directory,"entry.ts");
            if(File.Exists(sourcePath)){
                string source=File.ReadAllText(sourcePath);
                const string focusHandler="await widget.window.onFocusChanged";
                const string contextHandler="document.addEventListener('contextmenu',event=>{const target=event.target;if(target instanceof Element&&target.closest('.system-tray-item'))toggle.preserveContextMenu();},true);\n";
                if(!source.Contains("toggle.preserveContextMenu()")&&source.Contains(focusHandler)){
                    source=source.Replace(focusHandler,contextHandler+focusHandler);changed=true;
                }
                if(!source.Contains("await widget.window.setTitle('"+Title+"')")){
                    source+=Environment.NewLine+"await widget.window.setTitle('"+Title+"');"+Environment.NewLine;changed=true;
                }
                if(changed)File.WriteAllText(sourcePath,source,new System.Text.UTF8Encoding(false));
            }
            // Give the compatibility HWND a stable identity without altering tray actions.
            string bundle=Path.Combine(directory,"bundle.js");if(File.Exists(bundle)){
                string script=File.ReadAllText(bundle);
                if(!script.Contains(Title)&&script.Contains("await Xs.ready({show:!1})")){
                    script=script.Replace("await Xs.ready({show:!1})","await Xs.window.setTitle(\""+Title+"\");await Xs.ready({show:!1})");File.WriteAllText(bundle,script);changed=true;
                }
            }
            string cli=ShellLease.Seelen();if(changed&&cli!=null)Process.Start(new ProcessStartInfo(cli,"resource load widget \""+metadata+"\""){UseShellExecute=false,CreateNoWindow=true});
        }catch(Exception error){DockLog.Write("Tray appearance sync failed: "+error.GetType().Name);}
    }
    static string Hex(System.Windows.Media.Color c){return "#"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2");}
    internal void Update(){
        if(DateTime.UtcNow<next)return;next=DateTime.UtcNow.AddMilliseconds(100);
        IntPtr found=IntPtr.Zero;
        DockNative.EnumWindows((window,p)=>{if(DockNative.Title(window)==Title&&DockNative.IsWindowVisible(window)){uint pid;DockNative.GetWindowThreadProcessId(window,out pid);try{using(var process=Process.GetProcessById((int)pid))if(process.ProcessName=="seelen-ui")found=window;}catch{}}return true;},IntPtr.Zero);
        DockNative.Rect rect;
        if(found==IntPtr.Zero||!DockNative.GetWindowRect(found,out rect)){if(backdrop!=null)backdrop.Hide();return;}
        rect=dock.PositionBackgroundAppsPopup(found,rect);
        if(Environment.CommandLine.Contains("--inspect-ui")&&inspectionWindow!=found){
            if(inspectionWindow!=IntPtr.Zero)DockNative.SetLong(inspectionWindow,-20,inspectionStyle);
            inspectionWindow=found;inspectionStyle=DockNative.GetLong(found,-20);DockNative.SetLong(found,-20,new IntPtr((inspectionStyle.ToInt64()&~0x80)|0x40000));
        }
        if(backdrop==null)backdrop=new DockBackdrop();
        double dpi=Math.Max(96,DockNative.GetDpiForWindow(found))/96.0;var prefs=OverviewPreferences.Load();
        backdrop.Update(found,rect.Left/dpi,rect.Top/dpi,rect.Width/dpi,rect.Height/dpi,dpi,true,FrostedMaterial.Enabled(prefs.Glass),prefs.GlassStrength);
    }
    public void Dispose(){if(inspectionWindow!=IntPtr.Zero){DockNative.SetLong(inspectionWindow,-20,inspectionStyle);inspectionWindow=IntPtr.Zero;}if(backdrop!=null){backdrop.Close();backdrop=null;}}
}
