using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml;
using Microsoft.Win32;

sealed class SettingsEntry {
    public string Id,Name,Keywords,Page,Group,Glyph,Target;
    public bool Direct;
    internal string[] SearchKeywords,CompactKeywords;
}

// The installed Windows catalog owns names, keywords and availability. The
// Microsoft URI reference is a refreshable activation adapter, not a settings list.
static class SettingsCatalog {
    static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ActivitiesOverview");
    static readonly string Source=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Packages\windows.immersivecontrolpanel_cw5n1h2txyewy\LocalState\DeviceSearchCache\SettingsCache.txt");
    static readonly string RouteFile=Path.Combine(Root,"settings-uris.md");
    static readonly string CacheFile=Path.Combine(Root,"settings-catalog.json");
    const string Reference="https://raw.githubusercontent.com/MicrosoftDocs/windows-dev-docs/docs/hub/apps/develop/launch/launch-settings.md";
    static SettingsEntry[] entries=new SettingsEntry[0];
    static int busy;static DateTime checkedAt=DateTime.MinValue;static string signature="";
    static FileSystemWatcher watcher;
    internal static event Action Changed;
    internal static int Count {get{return Volatile.Read(ref entries).Length;}}
    internal static int DirectCount {get{return Volatile.Read(ref entries).Count(e=>e.Direct);}}
    internal static bool IsRefreshing {get{return Volatile.Read(ref busy)!=0;}}
    internal static string Status="Loading Windows Settings…";
    sealed class Route {internal string Name,Uri,Category;}
    sealed class Saved {public string Signature;public SettingsEntry[] Entries;}
    internal static void Initialize() {
        try{if(File.Exists(CacheFile)){var saved=Json().Deserialize<Saved>(File.ReadAllText(CacheFile));if(saved!=null&&saved.Entries!=null){Prepare(saved.Entries);entries=saved.Entries;signature=saved.Signature;Status=entries.Length+" Windows settings · "+entries.Count(e=>e.Direct)+" page links";}}}catch{}
        try{string folder=Path.GetDirectoryName(Source);if(Directory.Exists(folder)){watcher=new FileSystemWatcher(folder,Path.GetFileName(Source));watcher.NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.Size|NotifyFilters.FileName;watcher.Changed+=(s,e)=>Refresh(true);watcher.Created+=(s,e)=>Refresh(true);watcher.Renamed+=(s,e)=>Refresh(true);watcher.EnableRaisingEvents=true;}}catch{}
        Refresh(false);
    }
    static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=16*1024*1024};}
    internal static void Refresh(bool force) {
        if(!force&&(DateTime.UtcNow-checkedAt).TotalMinutes<15)return;
        if(Interlocked.Exchange(ref busy,1)!=0)return;
        ThreadPool.QueueUserWorkItem(_=>{
            try {
                Directory.CreateDirectory(Root);string build="";
                using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))if(key!=null)build=Convert.ToString(key.GetValue("CurrentBuild"))+"."+key.GetValue("UBR");
                var info=new FileInfo(Source);string stamp=build+"|catalog5|"+System.Globalization.CultureInfo.CurrentUICulture.Name+"|"+(info.Exists?info.Length+"/"+info.LastWriteTimeUtc.Ticks:"xml");
                bool newBuild=!signature.StartsWith(build+"|",StringComparison.Ordinal);
                if(!File.Exists(RouteFile)||newBuild||force||(DateTime.UtcNow-File.GetLastWriteTimeUtc(RouteFile)).TotalDays>7) {
                    try{ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(Reference);request.Timeout=5000;request.ReadWriteTimeout=5000;request.UserAgent="ActivitiesOverview settings URI adapter";using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){string text=reader.ReadToEnd();if(text.Contains("ms-settings:display")&&text.Length<500000)Atomic(RouteFile,text);}}catch(Exception error){Native.Log("Settings URI refresh: "+error.GetType().Name);}
                }
                stamp+="|"+(File.Exists(RouteFile)?File.GetLastWriteTimeUtc(RouteFile).Ticks.ToString():"offline");
                if(!force&&stamp==signature&&entries.Length>0)return;
                var routes=ReadRoutes();var list=ReadInstalled();
                if(list.Count==0)list=ReadXml();
                // Resolve one route per installed page, using its page heading and
                // identifiers. Group-level results then open that broader page.
                foreach(var page in list.GroupBy(e=>e.Page)) {
                    var headings=page.Where(e=>String.IsNullOrEmpty(e.Group)).OrderBy(e=>e.Name.Length).Take(4).ToArray();
                    if(headings.Length==0)headings=page.OrderBy(e=>e.Name.Length).Take(1).ToArray();
                    string pageWords=Regex.Replace(page.Key??"","([a-z])([A-Z])","$1 $2");
                    var best=routes.Select(r=>new{Route=r,Score=RouteScore(r,headings,pageWords)}).OrderByDescending(r=>r.Score).FirstOrDefault();
                    foreach(var entry in page){entry.Direct=best!=null&&best.Score>=6;entry.Target=entry.Direct?best.Route.Uri:"ms-settings:";}
                }
                string visibility="";using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer"))if(key!=null)visibility=key.GetValue("SettingsPageVisibility") as string??"";
                var next=list.Where(e=>Allowed(e,visibility)).GroupBy(e=>e.Name+"|"+e.Target,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToArray();
                if(next.Length==0)throw new InvalidDataException("Installed Settings catalog is empty");
                Prepare(next);Volatile.Write(ref entries,next);signature=stamp;Status=next.Length+" Windows settings · "+next.Count(e=>e.Direct)+" page links";
                Atomic(CacheFile,Json().Serialize(new Saved{Signature=stamp,Entries=next}));Native.Log("Settings catalog ready: "+Status);var changed=Changed;if(changed!=null)changed();
            }catch(Exception error){Status=entries.Length>0?"Using cached Windows settings":"Windows Settings catalog unavailable";Native.Log("Settings catalog: "+error.GetType().Name+" "+error.Message);}
            finally{checkedAt=DateTime.UtcNow;Interlocked.Exchange(ref busy,0);}
        });
    }
    static void Atomic(string file,string text){string temporary=file+".tmp";File.WriteAllText(temporary,text,Encoding.UTF8);if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);}
    static string Value(IDictionary<string,object> record,string name){object boxed;if(!record.TryGetValue(name,out boxed))return "";var field=boxed as IDictionary<string,object>;object value;return field!=null&&field.TryGetValue("Value",out value)?Convert.ToString(value):"";}
    static List<SettingsEntry> ReadInstalled() {
        var result=new List<SettingsEntry>();if(!File.Exists(Source))return result;
        string data;using(var stream=new FileStream(Source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream))data=reader.ReadToEnd();
        var records=Json().DeserializeObject(data) as object[];if(records==null)return result;
        foreach(var item in records) {
            var record=item as IDictionary<string,object>;if(record==null)continue;string page=Value(record,"System.Setting.PageID"),group="";object paths;
            if(record.TryGetValue("SettingPaths",out paths)){var array=paths as object[];if(array!=null)foreach(var path in array){var p=path as IDictionary<string,object>;if(p==null)continue;string candidate=Value(p,"System.Setting.PageID");if(candidate.StartsWith("SettingsPage",StringComparison.Ordinal)){page=candidate;group=Value(p,"System.Setting.GroupID");break;}}}
            string name=Value(record,"System.Comment");if(!page.StartsWith("SettingsPage",StringComparison.Ordinal)||String.IsNullOrWhiteSpace(name))continue;
            result.Add(new SettingsEntry{Id=Value(record,"System.ParsingName"),Name=name,Page=page,Group=group,Keywords=Value(record,"System.HighKeywords"),Glyph=Value(record,"System.Setting.Glyph")});
        }return result;
    }
    [System.Runtime.InteropServices.DllImport("shlwapi.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern int SHLoadIndirectString(string input,StringBuilder output,uint size,IntPtr reserved);
    static string Resolve(string value){if(String.IsNullOrWhiteSpace(value)||!value.StartsWith("@"))return value;var output=new StringBuilder(4096);return SHLoadIndirectString(value,output,4096,IntPtr.Zero)==0?output.ToString():"";}
    static List<SettingsEntry> ReadXml(){
        var result=new List<SettingsEntry>();string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"ImmersiveControlPanel\Settings");if(!Directory.Exists(folder))return result;
        foreach(string file in Directory.GetFiles(folder,"AllSystemSettings_*.xml")){var document=new XmlDocument{XmlResolver=null};document.Load(file);foreach(XmlNode node in document.SelectNodes("//SearchableContent")){var page=node.SelectSingleNode(".//Node[@Type='Page']");var description=node.SelectSingleNode("SettingInformation/Description");if(page==null||description==null)continue;string name=Resolve(description.InnerText);if(name.Length==0)continue;var keyword=node.SelectSingleNode("SettingInformation/HighKeywords");var id=node.SelectSingleNode("Filename");result.Add(new SettingsEntry{Id=id==null?page.InnerText:id.InnerText,Name=name,Page=page.InnerText,Keywords=keyword==null?"":Resolve(keyword.InnerText)});}}return result;
    }
    static List<Route> ReadRoutes(){var routes=new List<Route>();if(!File.Exists(RouteFile))return routes;string category="";foreach(string line in File.ReadLines(RouteFile)){if(line.StartsWith("### "))category=line.Substring(4).Trim();if(!line.StartsWith("|"))continue;var cells=line.Split('|');if(cells.Length<3)continue;var match=Regex.Match(cells[2],@"`(ms-settings:[a-z0-9-]+)`");if(match.Success&&!line.Contains("Deprecated")&&!line.Contains("deprecated")&&!line.Contains("feature is removed"))routes.Add(new Route{Name=Regex.Replace(cells[1],@"[*`\[\]]"," ").Trim(),Uri=match.Groups[1].Value,Category=category});}return routes;}
    static HashSet<string> Words(string value){return new HashSet<string>(Regex.Split(SearchRank.Normalize(value??"").Replace("colour","color").Replace("aeroplane","airplane").Replace("wi fi","wifi"),@"[^\p{L}\p{N}]+").Where(t=>t.Length>1&&!new[]{"settings","setting","page","pc","change","choose","your","the","and","of","to","in","on","for"}.Contains(t)).Select(t=>t.Length>4&&t.EndsWith("s")?t.Substring(0,t.Length-1):t),StringComparer.OrdinalIgnoreCase);}
    static int RouteScore(Route route,SettingsEntry[] headings,string pageWords){
        var tokens=Words(route.Name);if(tokens.Count==0||tokens.All(t=>new[]{"advanced","other","extra","general","default"}.Contains(t)))return 0;int score=0;
        foreach(var heading in headings){var words=Words(heading.Name);int common=tokens.Count(words.Contains);int candidate=common==tokens.Count?6+common*2:common*2;if(words.SetEquals(tokens))candidate=40+tokens.Count;score=Math.Max(score,candidate);}
        foreach(var heading in headings)if((heading.Keywords??"").Split(';').Any(keyword=>Words(keyword).SetEquals(tokens)))score=Math.Max(score,18+tokens.Count);
        var page=Words(pageWords);var uri=Words(route.Uri.Replace("ms-settings:",""));int overlap=uri.Count(page.Contains);score+=overlap*3;
        if(pageWords.IndexOf("Ease Of Access",StringComparison.OrdinalIgnoreCase)>=0&&route.Category!="Ease of access")score-=8;
        if(route.Category=="Ease of access"&&pageWords.IndexOf("Ease Of Access",StringComparison.OrdinalIgnoreCase)<0)score-=5;
        if(route.Category=="Privacy"&&pageWords.IndexOf("Privacy",StringComparison.OrdinalIgnoreCase)<0&&!headings.Any(e=>e.Name.IndexOf("privacy",StringComparison.OrdinalIgnoreCase)>=0))score-=14;
        return score;
    }
    static bool Allowed(SettingsEntry entry,string policy){if(!entry.Direct||String.IsNullOrEmpty(policy))return true;int colon=policy.IndexOf(':');if(colon<0)return true;var pages=policy.Substring(colon+1).Split(';');bool listed=pages.Contains(entry.Target.Substring(12),StringComparer.OrdinalIgnoreCase);return policy.StartsWith("showonly:",StringComparison.OrdinalIgnoreCase)?listed:!listed;}
    static void Prepare(SettingsEntry[] list){foreach(var entry in list){entry.SearchKeywords=(entry.Keywords??"").Split(';').Where(p=>!String.IsNullOrWhiteSpace(p)).Select(SearchRank.Normalize).Distinct().ToArray();entry.CompactKeywords=entry.SearchKeywords.Select(p=>p.Replace(" ","")).ToArray();}}
    internal static IEnumerable<ResultItem> Search(string query){string normalized=SearchRank.Normalize(query),compact=normalized.Replace(" ","");return Volatile.Read(ref entries).Select(e=>new{Entry=e,Rank=Math.Max(SearchRank.Match(e.Name,query),KeywordRank(e,normalized,compact))}).Where(e=>e.Rank>0).OrderByDescending(e=>e.Rank).Take(16).Select(e=>new ResultItem{Setting=e.Entry,Label=e.Entry.Name,Detail="Windows Settings",Rank=e.Rank+150});}
    static int KeywordRank(SettingsEntry entry,string query,string compact){if(query.Length==0||entry.SearchKeywords==null)return 0;int best=0;var terms=query.Split(' ');for(int i=0;i<entry.SearchKeywords.Length;i++){string phrase=entry.SearchKeywords[i],flat=entry.CompactKeywords[i];if(flat==compact)return 8800;if(phrase.StartsWith(query,StringComparison.Ordinal)||flat.StartsWith(compact,StringComparison.Ordinal))best=Math.Max(best,8400);else if(terms.All(term=>phrase.Split(' ').Any(word=>word.StartsWith(term,StringComparison.Ordinal))))best=Math.Max(best,8000);else if(phrase.IndexOf(query,StringComparison.Ordinal)>=0||flat.IndexOf(compact,StringComparison.Ordinal)>=0)best=Math.Max(best,7400);}return best;}
}
