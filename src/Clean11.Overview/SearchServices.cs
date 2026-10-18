using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Windows.Forms;

// AppsFolder identities return meaningful icons only when queried in a pumping
// STA. Icon requests originate on the asynchronous worker pool, so serialize
// only these Shell calls through one hidden, background STA dispatcher.
static class ShellIconApartment {
    static readonly object gate=new object();
    static readonly ManualResetEvent ready=new ManualResetEvent(false);
    static Control dispatcher;
    static Exception startupError;
    static bool startAttempted;
    internal static int WorkerStartCount {get;private set;}
    static void EnsureStarted() {
        lock(gate)if(!startAttempted) {
            startAttempted=true;WorkerStartCount++;
            try {
                var thread=new Thread(()=>{
                    try {
                        var control=new Control();IntPtr handle=control.Handle;
                        lock(gate)dispatcher=control;
                    }catch(Exception error){lock(gate)startupError=error;}
                    finally{ready.Set();}
                    if(dispatcher!=null)Application.Run();
                });
                thread.IsBackground=true;thread.Name="Activities Overview Shell icons";thread.SetApartmentState(ApartmentState.STA);thread.Start();
            }catch(Exception error){startupError=error;ready.Set();}
        }
        if(!ready.WaitOne(5000))throw new TimeoutException("Shell icon apartment did not start.");
        if(startupError!=null)throw new InvalidOperationException("Shell icon apartment could not start.",startupError);
    }
    internal static T Invoke<T>(Func<T> action) {EnsureStarted();Control target=dispatcher;return (T)target.Invoke(action);}
}

static class SearchMode {
    internal static readonly string DataDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ActivitiesOverview");
    internal static readonly string Marker=Path.Combine(DataDirectory,"use-windows-search");
    internal static bool Windows {get{return File.Exists(Marker);}}
    internal static void SetWindows(bool enabled) {
        Directory.CreateDirectory(DataDirectory);
        if(enabled)File.WriteAllText(Marker,"Super opens Windows Search. Delete this file to return to Activities Overview.");
        else if(File.Exists(Marker))File.Delete(Marker);
    }
    internal static void OpenWindows() {
        try {Process.Start(new ProcessStartInfo("ms-search:"){UseShellExecute=true});}
        catch(Exception e){Native.Log("Windows Search launch: "+e.GetType().Name);}
    }
}

static class SearchRank {
    static readonly ConcurrentDictionary<string,string> normalized=new ConcurrentDictionary<string,string>(StringComparer.Ordinal);
    internal static string Normalize(string value) {
        if(String.IsNullOrEmpty(value))return "";
        if(value.Length>2048)value=value.Substring(0,2048);
        if(normalized.Count>8192)normalized.Clear();
        return normalized.GetOrAdd(value,NormalizeCore);
    }
    static string NormalizeCore(string value) {
        if(String.IsNullOrEmpty(value))return "";
        value=Regex.Replace(value,"([a-z])([A-Z])","$1 $2");
        try{value=value.Normalize(NormalizationForm.FormD);}
        catch(ArgumentException) {
            // Clipboard strings can contain unmatched UTF-16 surrogates. Keep
            // valid pairs and discard only malformed code units before retrying.
            var valid=new StringBuilder(value.Length);
            for(int i=0;i<value.Length;i++) {
                char ch=value[i];
                if(Char.IsHighSurrogate(ch)){if(i+1<value.Length&&Char.IsLowSurrogate(value[i+1])){valid.Append(ch).Append(value[++i]);}continue;}
                if(!Char.IsLowSurrogate(ch)&&ch!='\uFFFE')valid.Append(ch);
            }
            // Try to preserve accent-insensitive search where possible. Some
            // additional noncharacters are rejected by NLS, so retain clean
            // UTF-16 as a best-effort fallback if normalization still fails.
            value=valid.ToString();try{value=value.Normalize(NormalizationForm.FormD);}catch(ArgumentException){}
        }
        var text=new StringBuilder();bool space=true;
        foreach(char ch in value) {
            if(CharUnicodeInfo.GetUnicodeCategory(ch)==UnicodeCategory.NonSpacingMark)continue;
            if(Char.IsLetterOrDigit(ch)){text.Append(Char.ToLowerInvariant(ch));space=false;}
            else if(!space){text.Append(' ');space=true;}
        }
        return text.ToString().Trim();
    }
    internal static string Compact(string value){return Normalize(value).Replace(" ","");}
    // Scores use separated bands so launch history cannot lift a weak match above an exact match.
    internal static int Match(string label,string query) {
        string name=Normalize(label),q=Normalize(query);if(q.Length==0||q.Length>256)return 0;
        string nc=name.Replace(" ",""),qc=q.Replace(" ","");if(nc.Length==0)return 0;
        if(nc==qc)return 12000;
        if(name.StartsWith(q)||nc.StartsWith(qc))return 11000+Math.Max(0,150-(nc.Length-qc.Length));
        var words=name.Split(' ');var terms=q.Split(' ');
        if(terms.All(t=>words.Any(w=>w.StartsWith(t))))return 10000+Math.Max(0,100-name.Length);
        if(terms.All(name.Contains))return 9000+Math.Max(0,100-name.Length);
        string acronym=String.Concat(words.Select(w=>w.Substring(0,1)));
        if(qc.Length>=2&&acronym.StartsWith(qc))return 8200;
        if(qc.Length>=4&&qc.Length<=64) {
            int tolerance=qc.Length>=8?2:1;
            if(new[]{nc,Regex.Replace(nc,@"\d+$","")}.Concat(words).Any(w=>Math.Abs(w.Length-qc.Length)<=tolerance&&Distance(w,qc,tolerance)<=tolerance))return 7000;
        }
        if(qc.Length<3)return 0;
        int at=0,first=-1,last=-1;
        foreach(char ch in qc){int found=nc.IndexOf(ch,at);if(found<0)return 0;if(first<0)first=found;last=found;at=found+1;}
        int gaps=last-first+1-qc.Length;
        // Broad subsequences used to make unrelated long names look relevant.
        // Flow's density/position scoring is now gated by a confidence floor.
        if(first>Math.Max(3,nc.Length/3)||gaps>Math.Max(1,qc.Length/2))return 0;
        var spaces=new List<int>();for(int i=0;i<name.Length;i++)if(name[i]==' ')spaces.Add(i);
        int density=FlowScoring.Calculate(qc,nc,first,spaces,last-first+1,false);
        return density<45?0:4000+Math.Min(400,density);
    }
    // Optimal string alignment includes adjacent transpositions (e.g. "braev").
    internal static int Distance(string a,string b,int limit) {
        if(Math.Abs(a.Length-b.Length)>limit)return limit+1;
        int[,] d=new int[a.Length+1,b.Length+1];
        for(int i=0;i<=a.Length;i++)d[i,0]=i;for(int j=0;j<=b.Length;j++)d[0,j]=j;
        for(int i=1;i<=a.Length;i++)for(int j=1;j<=b.Length;j++) {
            int cost=a[i-1]==b[j-1]?0:1;
            d[i,j]=Math.Min(d[i-1,j]+1,Math.Min(d[i,j-1]+1,d[i-1,j-1]+cost));
            if(i>1&&j>1&&a[i-1]==b[j-2]&&a[i-2]==b[j-1])d[i,j]=Math.Min(d[i,j],d[i-2,j-2]+1);
        }
        return d[a.Length,b.Length];
    }
    internal static int App(AppItem app,string query) {
        int score=Match(app.Name,query);
        if(String.IsNullOrWhiteSpace(app.Arguments)) {string target=app.Target??app.Path??"";if(target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))score=Math.Max(score,Match(Path.GetFileName(target),query)-150); }
        foreach(string alias in (app.Aliases??"").Split('|'))if(alias.Length>0)score=Math.Max(score,Match(alias,query)-200);
        return score;
    }
}

class FileHit {public string Path;public bool IsFolder;}

static class SearchIcons {
    [StructLayout(LayoutKind.Sequential)] struct ShellSize {public int Width,Height;public ShellSize(int width,int height){Width=width;Height=height;}}
    [ComImport,Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory {[PreserveSig] int GetImage(ShellSize size,uint flags,out IntPtr bitmap);}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct FileInfo {
        public IntPtr Icon;public int Index;public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string Type;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SHGetFileInfo(string path,uint attributes,out FileInfo info,uint size,uint flags);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)]static extern int SHCreateItemFromParsingName(string path,IntPtr bind,ref Guid iid,out IShellItemImageFactory factory);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint ExtractIconEx(string path,int index,out IntPtr large,out IntPtr small,uint count);
    [DllImport("ole32.dll")]static extern int CoInitializeEx(IntPtr reserved,uint mode);
    [DllImport("ole32.dll")]static extern void CoUninitialize();
    [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr handle);
    static Icon ShellAppIcon(string path) {
        int initialized=CoInitializeEx(IntPtr.Zero,0);
        if(initialized<0&&initialized!=unchecked((int)0x80010106))return null;
        try {
            Guid iid=new Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B");IShellItemImageFactory factory;
            if(SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out factory)<0||factory==null)return null;
            try {
                IntPtr bitmap;int result=factory.GetImage(new ShellSize(64,64),0x4|0x80,out bitmap);
                if(result<0||bitmap==IntPtr.Zero)return null;
                try {
                    using(Bitmap image=Bitmap.FromHbitmap(bitmap)) {
                        IntPtr handle=image.GetHicon();if(handle==IntPtr.Zero)return null;
                        try{using(var borrowed=Icon.FromHandle(handle))return (Icon)borrowed.Clone();}
                        finally{DestroyIcon(handle);}
                    }
                } finally {DeleteObject(bitmap);}
            } finally {Marshal.FinalReleaseComObject(factory);}
        } catch{return null;}
        finally {if(initialized>=0)CoUninitialize();}
    }
    static Icon ExecutableIcon(string path) {
        IntPtr large=IntPtr.Zero,small=IntPtr.Zero;
        try {
            ExtractIconEx(path,0,out large,out small,1);
            IntPtr handle=large!=IntPtr.Zero?large:small;
            if(handle!=IntPtr.Zero)using(var icon=Icon.FromHandle(handle))return (Icon)icon.Clone();
        }catch{}
        finally{if(large!=IntPtr.Zero)DestroyIcon(large);if(small!=IntPtr.Zero&&small!=large)DestroyIcon(small);}
        return null;
    }
    internal static Icon Load(string path) {
        try {
            if(path.StartsWith("shell:AppsFolder\\",StringComparison.OrdinalIgnoreCase))return ShellIconApartment.Invoke(()=>ShellAppIcon(path));
            string extension=Path.GetExtension(path);bool executable=extension.Equals(".exe",StringComparison.OrdinalIgnoreCase)||extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase);
            if(executable){Icon large=ShellIconApartment.Invoke(()=>ShellAppIcon(path));if(large!=null)return large;}
            if(extension.Equals(".exe",StringComparison.OrdinalIgnoreCase)||extension.Equals(".dll",StringComparison.OrdinalIgnoreCase)||extension.Equals(".ico",StringComparison.OrdinalIgnoreCase)) {
                Icon extracted=ExecutableIcon(path);if(extracted!=null)return extracted;
            }
            // Shell lookups require COM on the calling thread, including pool
            // threads. Balance S_OK/S_FALSE; an existing STA is already usable.
            int initialized=CoInitializeEx(IntPtr.Zero,0);
            try {
                FileInfo info;SHGetFileInfo(executable?path:extension,0x80,out info,(uint)Marshal.SizeOf(typeof(FileInfo)),0x100u|(executable?0u:0x1u|0x10u));
                if(info.Icon!=IntPtr.Zero)try{using(var icon=Icon.FromHandle(info.Icon))return (Icon)icon.Clone();}finally{DestroyIcon(info.Icon);}
            }finally{if(initialized>=0)CoUninitialize();}
        }catch{}
        return null;
    }
}

static class AppCatalog {
    static readonly string CachePath=Path.Combine(SearchMode.DataDirectory,"apps-cache.json");
    internal static readonly string PortablePath=Path.Combine(SearchMode.DataDirectory,"portable-apps.json");
    internal static readonly string[] ShortcutDirectories={
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
    };
    [DllImport("shell32.dll")] static extern int SHGetKnownFolderPath(ref Guid folder,uint flags,IntPtr token,out IntPtr path);
    internal static string ResolvePath(string value) {
        if(String.IsNullOrWhiteSpace(value))return "";
        value=Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        if(value.StartsWith("{"))try {
            int end=value.IndexOf('}');Guid id=new Guid(value.Substring(0,end+1));IntPtr p;
            if(SHGetKnownFolderPath(ref id,0,IntPtr.Zero,out p)==0) {
                try {value=Path.Combine(Marshal.PtrToStringUni(p),value.Substring(end+1).TrimStart('\\','/'));}
                finally {Marshal.FreeCoTaskMem(p);}
            }
        }catch{}
        return value;
    }
    static void Release(object obj){if(obj!=null&&Marshal.IsComObject(obj))try{Marshal.FinalReleaseComObject(obj);}catch{}}
    internal static bool IsCommandHost(string path) {return new[]{"powershell","pwsh","cmd","wscript","cscript","rundll32","mshta"}.Contains(Path.GetFileNameWithoutExtension(path??"").ToLowerInvariant());}
    static bool IsDocument(string path){return new[]{".html",".htm",".pdf",".chm",".txt",".url"}.Contains(Path.GetExtension(path).ToLowerInvariant());}
    internal static List<AppItem> ReadCache() {
        try {return (new JavaScriptSerializer().Deserialize<List<AppItem>>(File.ReadAllText(CachePath))??new List<AppItem>()).Where(a=>!IsCommandHost(a.Target)||(!String.IsNullOrWhiteSpace(a.Path)&&a.Path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))||(!String.IsNullOrWhiteSpace(a.Arguments)&&!String.IsNullOrWhiteSpace(a.Name))).ToList();}
        catch{return new List<AppItem>();}
    }
    internal static void SaveCache(List<AppItem> apps) {
        try {
            Directory.CreateDirectory(SearchMode.DataDirectory);string temporary=CachePath+".tmp";
            File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(apps));
            if(File.Exists(CachePath))File.Replace(temporary,CachePath,null);else File.Move(temporary,CachePath);
        }catch(Exception e){Native.Log("App cache write: "+e.GetType().Name);}
    }
    internal static string[] PortableApps() {
        try{return new JavaScriptSerializer().Deserialize<string[]>(File.ReadAllText(PortablePath))??new string[0];}
        catch{return new string[0];}
    }
    internal static void AddPortable(string path) {
        if(!File.Exists(path)||!new[]{".exe",".lnk"}.Contains(Path.GetExtension(path).ToLowerInvariant()))return;
        Directory.CreateDirectory(SearchMode.DataDirectory);
        File.WriteAllText(PortablePath,new JavaScriptSerializer().Serialize(PortableApps().Concat(new[]{path}).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
    }
    internal static string Identity(AppItem app) {
        return (String.IsNullOrWhiteSpace(app.Target)?app.Path:app.Target).Trim().TrimEnd('\\').ToLowerInvariant()+"|"+(app.Arguments??"").Trim().ToLowerInvariant();
    }
    internal static List<AppItem> Deduplicate(IEnumerable<AppItem> input) {
        var result=new List<AppItem>();var targets=new Dictionary<string,AppItem>(StringComparer.OrdinalIgnoreCase);
        foreach(var app in input) {
            if(String.IsNullOrWhiteSpace(app.Name)||String.IsNullOrWhiteSpace(app.Path))continue;
            // Shell AppsFolder omits arguments for some script-backed links.
            // Such an entry is not a reliable identity for the interpreter.
            if(IsCommandHost(app.Target)&&String.IsNullOrWhiteSpace(app.Arguments)&&!app.Path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))continue;
            if(Regex.IsMatch(app.Name,@"\b(uninstall|uninstaller)\b",RegexOptions.IgnoreCase))continue;
            string key=Identity(app);AppItem existing;
            if(targets.TryGetValue(key,out existing)) {
                existing.Aliases=(existing.Aliases??"")+"|"+app.Name+"|"+app.Aliases;
                if(String.IsNullOrEmpty(existing.IconPath))existing.IconPath=app.IconPath;
                continue;
            }
            targets[key]=app;result.Add(app);
        }
        return result.GroupBy(a=>SearchRank.Compact(a.Name)+"|"+(a.Arguments??""),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).OrderBy(a=>a.Name,StringComparer.OrdinalIgnoreCase).ToList();
    }
    internal static List<AppItem> Build() {
        var apps=new List<AppItem>();
        // Seed real system tools before shell catalogue entries. An AppsFolder
        // script entry can expose its interpreter without its arguments.
        string system=Environment.GetFolderPath(Environment.SpecialFolder.System);
        AddKnown(apps,Path.Combine(system,@"WindowsPowerShell\v1.0\powershell.exe"),"Windows PowerShell","powershell|power shell|ps|windows powershell");
        AddKnown(apps,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"SysWOW64\WindowsPowerShell\v1.0\powershell.exe"),"Windows PowerShell (x86)","powershell x86|power shell 32bit");
        AddKnown(apps,Path.Combine(system,"cmd.exe"),"Command Prompt","cmd|command prompt|console");
        AddKnown(apps,Path.Combine(system,@"WindowsPowerShell\v1.0\PowerShell_ISE.exe"),"Windows PowerShell ISE","powershell ise|ise");
        string powerShellRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PowerShell");
        if(Directory.Exists(powerShellRoot))foreach(string directory in Directory.GetDirectories(powerShellRoot).OrderByDescending(x=>x))AddKnown(apps,Path.Combine(directory,"pwsh.exe"),"PowerShell 7","pwsh|powershell 7|power shell 7");
        object shellObject=null,folderObject=null,itemsObject=null,shortcutShell=null;
        try {
            shellObject=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));dynamic shell=shellObject;
            folderObject=shell.NameSpace("shell:AppsFolder");dynamic folder=folderObject;itemsObject=folder.Items();dynamic items=itemsObject;
            for(int i=0;i<(int)items.Count;i++) {
                object itemObject=null;
                try {
                    itemObject=items.Item(i);dynamic item=itemObject;string name=(string)item.Name,raw=(string)item.Path;
                    if(String.IsNullOrWhiteSpace(name)||String.IsNullOrWhiteSpace(raw))continue;
                    if(IsDocument(raw))continue;
                    string target="";try{target=ResolvePath((string)item.ExtendedProperty("System.Link.TargetParsingPath"));}catch{}
                    string resolved=ResolvePath(raw);if(!File.Exists(target))target=File.Exists(resolved)?resolved:"";
                    if(IsDocument(target)||IsCommandHost(target))continue;
                    string launch=File.Exists(resolved)?resolved:raw.StartsWith("shell:",StringComparison.OrdinalIgnoreCase)?raw:"shell:AppsFolder\\"+raw;
                    apps.Add(new AppItem{Name=name,Path=launch,Target=target,IconPath=String.IsNullOrWhiteSpace(target)?launch:target,Aliases=Path.GetFileNameWithoutExtension(target)});
                }catch(Exception e){Native.Log("Skipped app entry: "+e.GetType().Name);}finally{Release(itemObject);}
            }
        }catch(Exception e){Native.Log("Registered app scan: "+e.GetType().Name);}
        finally{Release(itemsObject);Release(folderObject);Release(shellObject);}
        try {
            shortcutShell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            foreach(string dir in ShortcutDirectories) {
                string[] links;try{links=Directory.GetFiles(dir,"*.lnk",SearchOption.AllDirectories);}catch{continue;}
                foreach(string file in links)AddShortcut(apps,file,shortcutShell);
            }
            foreach(string file in PortableApps()) {
                if(!File.Exists(file))continue;
                if(Path.GetExtension(file).Equals(".lnk",StringComparison.OrdinalIgnoreCase))AddShortcut(apps,file,shortcutShell);
                else apps.Add(Executable(file,Path.GetFileNameWithoutExtension(file)));
            }
        }catch(Exception e){Native.Log("Shortcut scan: "+e.GetType().Name);}finally{Release(shortcutShell);}
        foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})try {
            using(var root=RegistryKey.OpenBaseKey(hive,view))using(var paths=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths")) {
                if(paths==null)continue;
                foreach(string keyName in paths.GetSubKeyNames())try {
                    using(var key=paths.OpenSubKey(keyName)) {
                        string path=ResolvePath(key.GetValue("") as string);
                        if(File.Exists(path)&&Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase))apps.Add(Executable(path,Path.GetFileNameWithoutExtension(path)));
                    }
                }catch{}
            }
        }catch{}
        return Deduplicate(apps);
    }
    static void AddKnown(List<AppItem> apps,string file,string name,string aliases) {
        if(File.Exists(file))apps.Add(new AppItem{Name=name,Path=file,Target=file,IconPath=file,Aliases=aliases});
    }
    static AppItem Executable(string file,string fallback) {
        string name=fallback;try{string description=FileVersionInfo.GetVersionInfo(file).FileDescription;if(!String.IsNullOrWhiteSpace(description))name=description.Trim();}catch{}
        return new AppItem{Name=name,Path=file,Target=file,IconPath=file,Aliases=Path.GetFileNameWithoutExtension(file)};
    }
    static void AddShortcut(List<AppItem> apps,string file,object shell) {
        object shortcutObject=null;
        try {
            dynamic shortcuts=shell;shortcutObject=shortcuts.CreateShortcut(file);dynamic shortcut=shortcutObject;
            string target=ResolvePath((string)shortcut.TargetPath),arguments=(string)shortcut.Arguments;
            if(IsDocument(target))return;
            // Keep shell/advertised shortcuts with no ordinary file target; ignore broken local targets.
            if(Path.IsPathRooted(target)&&!File.Exists(target)&&!Directory.Exists(target))return;
            string alias=Path.GetFileNameWithoutExtension(target);
            if(new[]{"powershell","pwsh","cmd","wscript","cscript","rundll32","mshta"}.Contains(alias.ToLowerInvariant()))alias="";
            apps.Add(new AppItem{Name=Path.GetFileNameWithoutExtension(file),Path=file,Target=target,Arguments=arguments,IconPath=File.Exists(target)?target:file,Aliases=alias});
        }catch{}finally{Release(shortcutObject);}
    }
}
