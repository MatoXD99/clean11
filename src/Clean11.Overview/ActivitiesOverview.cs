using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

static class Native {
    internal delegate bool EnumProc(IntPtr hwnd, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left,Top,Right,Bottom; public Rectangle Box { get { return Rectangle.FromLTRB(Left,Top,Right,Bottom); } } public Rect(Rectangle r) { Left=r.Left; Top=r.Top; Right=r.Right; Bottom=r.Bottom; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Size { public int Width,Height; }
    [StructLayout(LayoutKind.Sequential)] internal struct Placement {public int Length,Flags,ShowCommand;public Point MinPosition,MaxPosition;public Rect NormalPosition;}
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
    [StructLayout(LayoutKind.Sequential,Pack=1)] internal struct Props {
        public uint Flags; public Rect Destination,Source; public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
    }
    // dwmapi.h uses one-byte packing. Only the desktop-wide prefix is consumed;
    // the remaining legacy per-window counters are reserved here (Win8.1+ uses NULL HWND).
    [StructLayout(LayoutKind.Sequential,Pack=1,Size=292)] internal struct CompositionTiming {
        public uint Size,RefreshNumerator,RefreshDenominator;public ulong RefreshPeriod;
        public uint ComposeNumerator,ComposeDenominator;public ulong VBlank,Refresh;public uint DxRefresh;
        public ulong ComposeTime,Frame;public uint DxPresent;public ulong RefreshFrame,FrameSubmitted;
        public uint DxPresentSubmitted;public ulong FrameConfirmed;public uint DxPresentConfirmed;
        public ulong RefreshConfirmed;public uint DxRefreshConfirmed;public ulong FramesLate;public uint FramesOutstanding;
        public double RefreshHz {get{return RefreshDenominator==0?0:RefreshNumerator/(double)RefreshDenominator;}}
        public double ComposeHz {get{return ComposeDenominator==0?0:ComposeNumerator/(double)ComposeDenominator;}}
    }
    [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetWindowText(IntPtr hwnd, StringBuilder title,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd,StringBuilder name,int count);
    [DllImport("user32.dll")] internal static extern bool GetWindowPlacement(IntPtr hwnd,ref Placement placement);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr hwnd,uint command);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(uint pid);
    [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint pid);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern bool GetClipCursor(out Rect rectangle);
    [DllImport("user32.dll")] internal static extern bool ClipCursor(IntPtr rectangle);
    [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr hwnd,int command);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hwnd,int command);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr hwnd,uint message,IntPtr wparam,IntPtr lparam);
    [DllImport("user32.dll")] internal static extern bool RedrawWindow(IntPtr hwnd,ref Rect update,IntPtr region,uint flags);
    [DllImport("user32.dll")] internal static extern bool GetUpdateRect(IntPtr hwnd,out Rect update,bool erase);
    [DllImport("user32.dll")] internal static extern bool ValidateRect(IntPtr hwnd,IntPtr update);
    [DllImport("user32.dll")] internal static extern bool InvalidateRect(IntPtr hwnd,ref Rect update,bool erase);
    [DllImport("user32.dll")] internal static extern IntPtr GetLastActivePopup(IntPtr hwnd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr value,string text);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out Rect value,int size);
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] internal static extern int DwmGetInt(IntPtr hwnd,int attribute,out int value,int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmRegisterThumbnail(IntPtr destination,IntPtr source,out IntPtr thumbnail);
    [DllImport("dwmapi.dll")] internal static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
    [DllImport("dwmapi.dll")] internal static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail,out Size size);
    [DllImport("dwmapi.dll")] internal static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail,ref Props properties);
    [DllImport("dwmapi.dll")] internal static extern int DwmFlush();
    [DllImport("dwmapi.dll")] static extern int DwmGetCompositionTimingInfo(IntPtr hwnd,ref CompositionTiming timing);
    internal static bool ReadCompositionTiming(out CompositionTiming timing) {
        timing=new CompositionTiming();timing.Size=(uint)Marshal.SizeOf(typeof(CompositionTiming));
        return DwmGetCompositionTimingInfo(IntPtr.Zero,ref timing)==0;
    }
    internal static Rectangle Bounds(IntPtr hwnd) { Rect r; if(DwmGetWindowAttribute(hwnd,9,out r,16)!=0) GetWindowRect(hwnd,out r); return r.Box; }
    internal static Rectangle RestoredBounds(Rect normalPosition,Rectangle monitor,Rectangle work,bool returnsMaximized) {
        if(returnsMaximized)return work;
        Rectangle normal=normalPosition.Box;
        return new Rectangle(normal.X+work.X-monitor.X,normal.Y+work.Y-monitor.Y,normal.Width,normal.Height);
    }
    internal static Rectangle PlacementBounds(IntPtr hwnd,Placement placement) {
        IntPtr monitor=MonitorFromWindow(hwnd,2);var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};
        if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info))return placement.NormalPosition.Box;
        bool returnsMaximized=(placement.Flags&2)!=0||placement.ShowCommand==3;
        return RestoredBounds(placement.NormalPosition,info.Monitor.Box,info.Work.Box,returnsMaximized);
    }
    internal static void Log(string message) { try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native.log"),DateTime.Now.ToString("s")+" "+message+Environment.NewLine); } catch {} }
}

class WindowItem {
    public IntPtr Hwnd,Thumbnail;
    public string Title,ProcessPath,ProcessName;
    public bool Minimized,Game,ClosePending,Active;
    public uint Pid; public int ZOrder;
    public DateTime CloseRequestedAt;
    public Rectangle Original,Card,Preview;
    public System.Drawing.Size SourceSize;
}
class AppItem { public string Name,Path,IconPath,Target,Arguments,Aliases; }
class ResultItem { public string Label,Detail,FolderPath,FilePath,Action,Section; public int Rank; public WindowItem Window; public AppItem App; public SettingsEntry Setting; public Rectangle Bounds,SectionBounds; }
class ResultMenuItem { public string Label,Command; public Rectangle Bounds; }
class MotionSurface {public IntPtr Thumbnail;public Rectangle From,To;public bool HasOrigin;}
class OverviewState {
    public List<string> Favorites=new List<string>(); public Dictionary<string,string> FavoriteNames=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,int> Usage=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
    public int MotionDuration=200,PreviewColumns=4;
    public bool ReducedMotion,Glass=true,GameFriendly=true;
}

static class FolderSearch {
    internal static string Canonical(string path) {
        string full=Path.GetFullPath(path);return full.Length>Path.GetPathRoot(full).Length?full.TrimEnd('\\'):full;
    }
    internal static string Normalize(string query) {
        string path=Environment.ExpandEnvironmentVariables(query.Trim().Trim('"')).Replace('/','\\');
        if(path=="~")path=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        else if(path.StartsWith("~\\"))path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),path.Substring(2));
        if(path.Length==2&&path[1]==':')path+="\\";
        return path;
    }
    internal static FileHit[] Local(string query) {
        var matches=new List<FileHit>();string path=Normalize(query);
        try {
            if(!Path.IsPathRooted(path))return matches.ToArray();
            path=Path.GetFullPath(path);
            if(Directory.Exists(path))matches.Add(new FileHit{Path=Canonical(path),IsFolder=true});
            else if(File.Exists(path))matches.Add(new FileHit{Path=Canonical(path)});
            string parent=path.EndsWith("\\")?path:Path.GetDirectoryName(path);
            string prefix=path.EndsWith("\\")?"":Path.GetFileName(path);
            if(!String.IsNullOrEmpty(parent)&&Directory.Exists(parent)) {
                foreach(string entry in Directory.EnumerateFileSystemEntries(parent).Take(4096).Where(p=>prefix.Length==0||SearchRank.Match(Path.GetFileName(p),prefix)>0).OrderByDescending(p=>SearchRank.Match(Path.GetFileName(p),prefix)).Take(48)) {
                    if(!matches.Any(m=>String.Equals(m.Path,entry,StringComparison.OrdinalIgnoreCase)))matches.Add(new FileHit{Path=entry,IsFolder=(File.GetAttributes(entry)&FileAttributes.Directory)!=0});
                }
            }
        }catch(Exception e){Native.Log("Folder lookup: "+e.GetType().Name);}
        return matches.ToArray();
    }
    [DllImport("Everything.dll",ExactSpelling=true,CharSet=CharSet.Unicode)] static extern void Everything_SetSearchW(string query);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern void Everything_SetReplyWindow(IntPtr hwnd);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern void Everything_SetReplyID(uint id);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern void Everything_SetMax(uint count);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern void Everything_SetMatchPath(bool enabled);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern void Everything_SetRequestFlags(uint flags);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern void Everything_SetMatchCase(bool enabled);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern void Everything_SetMatchWholeWord(bool enabled);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern void Everything_SetRegex(bool enabled);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern void Everything_SetOffset(uint offset);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern void Everything_SetSort(uint sort);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern bool Everything_QueryW(bool wait);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern bool Everything_IsQueryReply(uint message,IntPtr wparam,IntPtr lparam,uint id);
    [DllImport("Everything.dll",ExactSpelling=true)] static extern uint Everything_GetNumResults();
    [DllImport("Everything.dll",ExactSpelling=true)] static extern bool Everything_IsFolderResult(uint index);
    [DllImport("Everything.dll",ExactSpelling=true,CharSet=CharSet.Unicode)] static extern uint Everything_GetResultFullPathNameW(uint index,StringBuilder path,uint capacity);
    internal static string BuildQuery(string query,bool exact=false) {
        string text=Normalize(query).Replace("\"","");
        if(text.Contains("\\"))return "\""+text+"\"";
        var words=text.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
        string expression=exact?"regex:\"^"+Regex.Escape(text)+(Path.HasExtension(text)?"":"(?:\\.[^.]*)?")+"$\"":String.Join(" ",words.Select(w=>"\""+w+"\""));
        foreach(string excluded in new[]{"\\Windows\\","\\Program Files\\","\\Program Files (x86)\\","\\ProgramData\\","\\AppData\\","\\$Recycle.Bin\\","\\System Volume Information\\","\\node_modules\\","\\.git\\","\\cache\\","\\Caches\\","\\Temp\\","\\obj\\"})expression+=" !path:\""+excluded+"\"";
        expression+=" !path:\""+AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')+"\\\"";
        return expression;
    }
    static void Configure(string query,bool exact=false) {
        Everything_SetMax(256);Everything_SetOffset(0);Everything_SetSort(1);
        Everything_SetMatchCase(false);Everything_SetMatchWholeWord(false);Everything_SetRegex(false);
        Everything_SetMatchPath(Normalize(query).Contains("\\"));Everything_SetRequestFlags(3);
        Everything_SetSearchW(BuildQuery(query,exact));
    }
    internal static bool Request(IntPtr hwnd,uint id,string query,bool exact=false) {
        try {
            Configure(query,exact);Everything_SetReplyWindow(hwnd);Everything_SetReplyID(id);
            return Everything_QueryW(false);
        }catch(Exception e){Native.Log("File index unavailable: "+e.GetType().Name);return false;}
    }
    static FileHit[] Results() {
        var matches=new List<FileHit>();
        for(uint i=0;i<Math.Min(256,Everything_GetNumResults());i++) {
            var path=new StringBuilder(32768);Everything_GetResultFullPathNameW(i,path,(uint)path.Capacity);
            if(path.Length>0)matches.Add(new FileHit{Path=path.ToString(),IsFolder=Everything_IsFolderResult(i)});
        }
        return matches.ToArray();
    }
    internal static FileHit[] QueryBlocking(string query) {
        Configure(query);Everything_SetReplyWindow(IntPtr.Zero);Everything_SetReplyID(0);
        if(!Everything_QueryW(true))throw new InvalidOperationException("Everything query failed");
        return Results();
    }
    internal static bool ReadReply(ref Message message,uint id,out FileHit[] folders) {
        folders=null;
        try {
            if(!Everything_IsQueryReply((uint)message.Msg,message.WParam,message.LParam,id))return false;
            folders=Results();return true;
        }catch{return false;}
    }
}

class SearchTextBox : TextBox {
    internal Func<Keys,bool> HandleCommand;
    internal bool IsImeComposing;
    internal event Action ImeCompositionEnded;
    protected override void WndProc(ref Message message){
        if(message.Msg==0x010D)IsImeComposing=true;
        bool ended=message.Msg==0x010E;if(ended)IsImeComposing=false;
        base.WndProc(ref message);
        if(ended){var callback=ImeCompositionEnded;if(callback!=null)callback();}
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
        if(HandleCommand!=null&&HandleCommand(keyData))return true;
        return base.ProcessCmdKey(ref msg,keyData);
    }
}

partial class Overview : Form {
    static readonly object steamNamesLock=new object();
    static readonly Dictionary<string,Dictionary<string,string>> steamNamesByLibrary=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
    static readonly Regex steamField=new Regex("^\\s*\"(?<key>name|installdir)\"\\s+\"(?<value>[^\"]*)\"",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    readonly SearchTextBox search=new SearchTextBox(); readonly Label searchHint=new Label();
    readonly PreviewHost previewHost;
    static readonly Color TransparentBackdrop=Color.FromArgb(1,0,1);
    readonly System.Windows.Forms.Timer windowWatcher=new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer folderDelay=new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer folderTimeout=new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer appRefreshDelay=new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer focusSettleTimer=new System.Windows.Forms.Timer();
    readonly Stopwatch focusHandoffClock=new Stopwatch();
    bool focusHandoffArmed;
    int focusRecoveryAttempts;
    long lastFocusRecovery;
    readonly List<FileSystemWatcher> appWatchers=new List<FileSystemWatcher>();
    FileHit[] localFolders=new FileHit[0],indexedFolders=new FileHit[0];
    int appIndexLoading;
    DateTime lastAppRefresh=DateTime.MinValue;
    bool fileIndexUnavailable;
    bool shuttingDown;
    uint folderGeneration,folderReplyId;
    bool localFolderPending,indexFolderPending,openFolderWhenReady,resultNavigationUsed,fileExactPhase;
    int resultScroll;
    readonly OverviewState state=new OverviewState();
    readonly string statePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ActivitiesOverview","state.json");
    bool settingsOpen,browsing;
    bool resultMenuOpen;
    int resultMenuTarget=-1,resultMenuHover=-1;
    Rectangle resultMenuBounds;
    readonly List<ResultMenuItem> resultMenuItems=new List<ResultMenuItem>();
    string browsePath="";
    string[] browseEntries=new string[0];
    Thread motionThread;
    CancellationTokenSource motionCancellation;
    int motionEpoch;
    readonly List<WindowItem> windows=new List<WindowItem>();
    List<AppItem> apps=new List<AppItem>();
    List<ResultItem> results=new List<ResultItem>();
    readonly Dictionary<string,Icon> iconCache=new Dictionary<string,Icon>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> pendingIcons=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,DateTime> iconRetryAfter=new Dictionary<string,DateTime>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,string> windowAppNames=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> pendingAppNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,Font> fontCache=new Dictionary<string,Font>();
    Rectangle searchBox,closeBox,settingsBox,desktop,favoriteArea,settingsPanel,browseBackBox;
    float scale=1;

    bool closing,appsReady,openingMotion,reducedMotion,pointerOwnershipConfirmed,measureSettledPaint;
    volatile bool rendering;
    int selected=-1,hover=-1,hoverClose=-1;
    IntPtr previous;
    Action afterClose;
    WindowItem exitWindow;
    Rectangle exitSearchBounds;
    int closeGeneration,completedCloseGeneration;
    int frames,motionPaintCoalesces;
    double thumbnailMs,paintMs,motionPaintMs;
    Bitmap chromeCache;
    Bitmap backgroundCache;
    bool backgroundGlass=true;
    bool chromeDirty=true;
    readonly Color foreground=ActivityTheme.Text;
    readonly bool diagnostic;
    public Overview(bool diagnosticMode=false) {
        diagnostic=diagnosticMode;
        Text="Clean11 Overview";
        FormBorderStyle=FormBorderStyle.None; StartPosition=FormStartPosition.Manual;
        ShowInTaskbar=Environment.CommandLine.Contains("--inspect-ui"); TopMost=true; AutoScaleMode=AutoScaleMode.None;
        DoubleBuffered=true; KeyPreview=true; BackColor=TransparentBackdrop;TransparencyKey=TransparentBackdrop;
        previewHost=new PreviewHost(e=>OnMouseMove(e),e=>OnMouseDown(e),e=>OnMouseWheel(e));
        Owner=previewHost;
        VisibleChanged+=(s,e)=>{if(!Visible)previewHost.HideRendering();};
        search.BorderStyle=BorderStyle.None; search.BackColor=ActivityTheme.Surface; search.ForeColor=foreground;
        search.HandleCommand=HandleSearchCommand;
        search.ImeCompositionEnded+=()=>{try{BeginInvoke((Action)(()=>{SyncSpotlightQuery();Redraw();}));}catch{}};
        search.GotFocus+=(s,e)=>Redraw();search.LostFocus+=(s,e)=>{Redraw();if(Visible&&!closing)Native.Log("Search focus lost; foreground="+Native.GetForegroundWindow());};
        search.TextChanged+=(s,e)=> { if(resultMenuOpen)CloseResultMenu();if(openingMotion) FinishEntrance(false); QueueFolderSearch();QueueSettingsSearch();RebuildResults();SyncSpotlightQuery();UpdateThumbnails(1); Redraw(); };
        Controls.Add(search); search.Visible=false;
        searchHint.Text="Search apps, files, folders and windows…";searchHint.ForeColor=ActivityTheme.Muted;
        searchHint.BackColor=search.BackColor;searchHint.AutoSize=false;searchHint.TextAlign=ContentAlignment.MiddleLeft;
        searchHint.Click+=(s,e)=>search.Focus();Controls.Add(searchHint);searchHint.BringToFront();searchHint.Visible=false;
        search.TextChanged+=(s,e)=>{searchHint.Visible=Visible&&!SpotlightHeaderVisible&&search.Text.Length==0;};
        MouseMove+=(s,e)=> {if(rendering||SpotlightActive)return; int next=Hit(e.Location),nextClose=HitWindowClose(e.Location),nextMenu=HitResultMenu(e.Location); if(next!=hover||nextClose!=hoverClose||nextMenu!=resultMenuHover) {int previous=hover;hover=next;hoverClose=nextClose;resultMenuHover=nextMenu;Cursor=next>=0||nextClose>=0||nextMenu>=0||closeBox.Contains(e.Location)||settingsBox.Contains(e.Location)||favoriteArea.Contains(e.Location)?Cursors.Hand:Cursors.Default;if(!TryPaintSearchHover(previous))Redraw();} };
        MouseDown+=(s,e)=> {
            if(rendering)return;
            if(SpotlightActive){if(e.Button==MouseButtons.Left)EndOverview(null);return;}
            if(e.Button==MouseButtons.Right) {
                if(resultMenuOpen&&resultMenuBounds.Contains(e.Location))return;
                if(search.Text.Trim().Length==0){int pin=HitFavorite(e.Location);if(pin>=0){RenameFavorite(pin);return;}}
                int result=Hit(e.Location);if(result>=0){ShowResultMenu(result,e.Location);return;}
                CloseResultMenu();return;
            }
            if(e.Button!=MouseButtons.Left)return;
            if(resultMenuOpen) {
                int menuItem=HitResultMenu(e.Location);
                if(menuItem>=0){InvokeResultMenu(menuItem);return;}
                CloseResultMenu();
            }
            if(closeBox.Contains(e.Location)){EndOverview(null);return;}
            if(settingsBox.Contains(e.Location)){OpenSpotlightSettings();return;}
            if(settingsOpen){if(!HandleSettingsClick(e.Location)){settingsOpen=false;UpdateThumbnails(1);Redraw();}return;}
            if(browsing&&browseBackBox.Contains(e.Location)){BrowseParent();return;}
            if(search.Text.Trim().Length==0&&!browsing&&favoriteArea.Contains(e.Location)){int favorite=HitFavorite(e.Location);if(favorite>=0){OpenFavorite(favorite);return;}}
            int star=HitFavoriteToggle(e.Location);if(star>=0){ToggleFavorite(results[star]);return;}
            int closeIndex=HitWindowClose(e.Location);if(closeIndex>=0){RequestWindowClose(windows[closeIndex]);return;}
            int index=Hit(e.Location);if(index>=0)Choose(index);else if(searchBox.Contains(e.Location))search.Focus();else EndOverview(null);
        };
        windowWatcher.Interval=250;windowWatcher.Tick+=(s,e)=>RefreshClosedWindows();
        folderDelay.Interval=120;folderDelay.Tick+=(s,e)=>StartFolderSearch();
        folderTimeout.Interval=1800;folderTimeout.Tick+=(s,e)=>{folderTimeout.Stop();fileIndexUnavailable=indexFolderPending;indexFolderPending=false;folderReplyId=0;ApplyFolderResults();};
        appRefreshDelay.Interval=700;appRefreshDelay.Tick+=(s,e)=>{appRefreshDelay.Stop();LoadApps(true);};
        focusSettleTimer.Interval=25;focusSettleTimer.Tick+=(s,e)=>SettleOpeningFocus();
        MouseWheel+=(s,e)=> {if(search.Text.Trim().Length==0)return;CloseResultMenu();resultNavigationUsed=true;resultScroll=Math.Max(0,Math.Min(Math.Max(0,results.Count-1),resultScroll-Math.Sign(e.Delta)*3));selected=Math.Max(resultScroll,Math.Min(selected,resultScroll+ResultRows()-1));LayoutResults();EnsureResultVisible(selected);Redraw();};
        FormClosing+=(s,e)=> {e.Cancel=true;EndOverview(null);};
        if(!diagnostic){LoadState();apps=AppCatalog.ReadCache();appsReady=apps.Count>0;WatchAppCatalog();InitializeSpotlightCatalog();}
    }
    int U(float value) { return (int)Math.Round(value*scale); }
    bool MotionActive {get{return rendering&&motionThread!=null;}}
    internal bool TransitionActive {get{return rendering;}}
    internal int CloseSequence {get{return Volatile.Read(ref closeGeneration);}}
    internal static bool IsToggleSignalCurrent(bool transitionAtReceipt,int receivedCloseSequence,bool transitionAtDispatch,int currentCloseSequence) {
        return !transitionAtReceipt&&!transitionAtDispatch&&receivedCloseSequence==currentCloseSequence;
    }
    void Redraw() {SyncSpotlightResults();if(SpotlightActive){if(!spotlightChromeCleared){spotlightChromeCleared=true;chromeDirty=true;if(!MotionActive)Invalidate();}return;}spotlightChromeCleared=false;chromeDirty=true;if(!MotionActive)Invalidate();}
    bool TryPaintSearchHover(int previous) {
        if(MotionActive||closing||settingsOpen||(!browsing&&search.Text.Trim().Length==0)||chromeDirty||chromeCache==null||chromeCache.Size!=ClientSize)return false;
        // Hover changes only row chrome. Rebuilding the monitor-sized bitmap on
        // the mouse thread stalls pointer delivery, especially on wide displays.
        using(var region=new Region(Rectangle.Empty)) {
            foreach(int index in new[]{previous,hover})if(index>=0&&index<results.Count&&!results[index].Bounds.IsEmpty) {
                var bounds=results[index].Bounds;bounds.Inflate(U(3),U(3));region.Union(bounds);
            }
            if(resultMenuOpen){var bounds=resultMenuBounds;bounds.Inflate(U(3),U(3));region.Union(bounds);}
            region.Intersect(ClientRectangle);
            using(var surface=Graphics.FromImage(chromeCache)) {
                surface.SetClip(region,CombineMode.Intersect);PaintChrome(surface);
            }
            Invalidate(region);
        }
        return true;
    }
    Rectangle FrameDamageBounds() {return new Rectangle(Math.Max(0,ClientSize.Width-2),Math.Max(0,ClientSize.Height-2),Math.Min(2,ClientSize.Width),Math.Min(2,ClientSize.Height));}
    bool CanRetainMotionPaint(Rectangle pending) {
        return MotionActive&&chromeCache!=null&&chromeCache.Size==ClientSize&&pending.Width>0&&pending.Height>0&&!FrameDamageBounds().Contains(pending);
    }
    bool CoalesceMotionPaint() {
        if(!Visible||!MotionActive)return false;
        Native.Rect pending;if(!Native.GetUpdateRect(Handle,out pending,false)||!CanRetainMotionPaint(pending.Box))return false;
        Rectangle damage=FrameDamageBounds();
        // Static chrome was fully painted before motion. Activation/exposure can
        // invalidate the whole HWND again; its cached pixels are still valid.
        // Retain those pixels and commit only the tiny frame region until settle.
        if(!Native.ValidateRect(Handle,IntPtr.Zero))return false;
        var update=new Native.Rect(damage);if(!Native.InvalidateRect(Handle,ref update,false))return false;
        motionPaintCoalesces++;return true;
    }
    bool TryPaintWindowMetadata(bool settling) {
        if(rendering||closing||settingsOpen||browsing||search.Text.Trim().Length>0||chromeCache==null||chromeCache.Size!=ClientSize||(!settling&&chromeDirty))return false;
        var timing=Stopwatch.StartNew();
        using(var region=MetadataPatchRegion()) {
            using(var surface=Graphics.FromImage(chromeCache)) {
                var state=surface.Save();surface.SetClip(region,CombineMode.Intersect);PaintChrome(surface);surface.Restore(state);
            }
            chromeDirty=false;Invalidate(region);
        }
        if(settling){measureSettledPaint=Visible;Native.Log("Entrance metadata cache patch ms="+timing.Elapsed.TotalMilliseconds.ToString("F1"));}
        return true;
    }
    internal Region MetadataPatchRegion() {
        var region=new Region(Rectangle.Empty);
        var field=searchBox;field.Inflate(U(2),U(2));region.Union(field);
        region.Union(new Rectangle(favoriteArea.Left-U(2),favoriteArea.Top-U(28),favoriteArea.Width+U(4),favoriteArea.Height+U(32)));
        foreach(var window in windows) {
            var preview=window.Preview;
            region.Union(new Rectangle(preview.Left-U(4),preview.Top-U(38),preview.Width+U(8),U(38)));
            // WindowAppName captions extend eight pixels below the preview and
            // have a 28-pixel body; leave a little antialiasing margin as well.
            region.Union(new Rectangle(preview.Left-U(4),preview.Bottom,preview.Width+U(8),U(40)));
            var outer=preview;outer.Inflate(U(4),U(4));
            using(var border=new Region(outer)){border.Exclude(preview);region.Union(border);}
            var close=WindowCloseBox(window.Card);close.Inflate(U(2),U(2));region.Union(close);
        }
        region.Intersect(ClientRectangle);
        return region;
    }
    void RedrawWindowMetadata() {if(!TryPaintWindowMetadata(false))Redraw();}
    void LoadState() {
        try {
            if(!File.Exists(statePath))return;
            var saved=new JavaScriptSerializer().Deserialize<OverviewState>(File.ReadAllText(statePath));
            if(saved==null)return;
            state.FavoriteNames=saved.FavoriteNames??new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);state.Favorites=saved.Favorites??new List<string>();state.Usage=saved.Usage??new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            state.MotionDuration=Math.Max(120,Math.Min(600,saved.MotionDuration));state.PreviewColumns=Math.Max(3,Math.Min(5,saved.PreviewColumns));
            state.ReducedMotion=saved.ReducedMotion;state.Glass=saved.Glass;state.GameFriendly=saved.GameFriendly;
        }catch(Exception e){Native.Log("Settings read: "+e.GetType().Name);}
    }
    void LoadUnifiedPreferences(){
        try{var p=OverviewPreferences.Load();state.MotionDuration=p.MotionDuration;state.PreviewColumns=p.PreviewColumns;state.ReducedMotion=p.ReducedMotion;state.Glass=p.Glass;state.GameFriendly=!p.LiveGamePreviews;}catch(Exception e){Native.Log("Unified preferences: "+e.GetType().Name);}
    }
    void SaveState() {
        try {Directory.CreateDirectory(Path.GetDirectoryName(statePath));File.WriteAllText(statePath,new JavaScriptSerializer().Serialize(state));}
        catch(Exception e){Native.Log("Settings save: "+e.GetType().Name);}
    }
    void MigrateAppHistory() {
        Func<string,string> migrate=key=>{
            if(!key.StartsWith("app:",StringComparison.OrdinalIgnoreCase))return key;
            string path=key.Substring(4);
            if(path.StartsWith("shell:AppsFolder\\",StringComparison.OrdinalIgnoreCase))path=path.Substring("shell:AppsFolder\\".Length);
            path=AppCatalog.ResolvePath(path);
            var app=apps.FirstOrDefault(a=>String.Equals(a.Path,path,StringComparison.OrdinalIgnoreCase)||String.Equals(a.Target,path,StringComparison.OrdinalIgnoreCase));
            return app==null?key:"app:"+app.Path;
        };
        bool changed=state.Favorites.Concat(state.Usage.Keys).Any(key=>key!=migrate(key));
        if(!changed)return;
        state.Favorites=state.Favorites.Select(migrate).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        state.Usage=state.Usage.GroupBy(pair=>migrate(pair.Key),StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>Math.Min(1000,group.Sum(pair=>pair.Value)),StringComparer.OrdinalIgnoreCase);
        SaveState();
    }
    string FavoriteKey(ResultItem result) {
        if(result==null)return "";
        if(result.App!=null)return "app:"+result.App.Path;
        if(result.FolderPath!=null)return "folder:"+result.FolderPath;
        if(result.FilePath!=null)return "file:"+result.FilePath;
        return "";
    }
    bool IsFavorite(ResultItem result) {string key=FavoriteKey(result);return key.Length>0&&state.Favorites.Contains(key,StringComparer.OrdinalIgnoreCase);}
    void ToggleFavorite(ResultItem result) {
        string key=FavoriteKey(result);if(key.Length==0)return;
        int at=state.Favorites.FindIndex(x=>String.Equals(x,key,StringComparison.OrdinalIgnoreCase));
        if(at>=0)state.Favorites.RemoveAt(at);else state.Favorites.Insert(0,key);
        SaveState();Redraw();
    }
    List<ResultItem> FavoriteItems() {
        var items=new List<ResultItem>();
        foreach(string key in state.Favorites.Take(8)) {
            int split=key.IndexOf(':');if(split<0)continue;string kind=key.Substring(0,split),value=key.Substring(split+1);
            if(kind=="app") {var app=apps.FirstOrDefault(a=>String.Equals(a.Path,value,StringComparison.OrdinalIgnoreCase));if(app!=null)items.Add(new ResultItem{App=app,Label=app.Name,Detail="Favorite"});}
            else if(kind=="folder"&&Directory.Exists(value))items.Add(new ResultItem{FolderPath=value,Label=Path.GetFileName(value.TrimEnd('\\')),Detail="Favorite"});
            else if(kind=="file"&&File.Exists(value))items.Add(new ResultItem{FilePath=value,Label=Path.GetFileName(value),Detail="Favorite"});
        }
        foreach(var item in items){string name;if(state.FavoriteNames.TryGetValue(FavoriteKey(item),out name)&&!String.IsNullOrWhiteSpace(name))item.Label=name;}return items;
    }
    string UsageKey(ResultItem result) {return result.App!=null?"app:"+result.App.Path:result.FolderPath!=null?"folder:"+result.FolderPath:result.FilePath!=null?"file:"+result.FilePath:"";}
    int UsageScore(ResultItem result) {int n;return UsageKey(result).Length>0&&state.Usage.TryGetValue(UsageKey(result),out n)?Math.Min(180,n*8):0;}
    void RecordUse(ResultItem result) {
        string key=UsageKey(result);if(key.Length==0)return;int n;state.Usage.TryGetValue(key,out n);state.Usage[key]=Math.Min(1000,n+1);SaveState();
    }
    int HitFavorite(Point p) {var items=FavoriteItems();for(int i=0;i<items.Count;i++)if(FavoriteBounds(i).Contains(p))return i;return -1;}
    Rectangle FavoriteToggleBounds(Rectangle bounds) {return new Rectangle(bounds.Right-U(38),bounds.Top+(bounds.Height-U(22))/2,U(22),U(22));}
    int HitFavoriteToggle(Point p) {for(int i=0;i<results.Count;i++)if(!results[i].Bounds.IsEmpty&&FavoriteKey(results[i]).Length>0&&FavoriteToggleBounds(results[i].Bounds).Contains(p))return i;return -1;}
    Rectangle FavoriteBounds(int index) {int count=Math.Max(1,FavoriteItems().Count);int width=Math.Min(U(132),(favoriteArea.Width-U(8)*(count-1))/count);int total=count*width+(count-1)*U(8);return new Rectangle(favoriteArea.Left+(favoriteArea.Width-total)/2+index*(width+U(8)),favoriteArea.Top,width,favoriteArea.Height);}
    void RenameFavorite(int index) {
        var items=FavoriteItems();if(index<0||index>=items.Count)return;StopFocusHandoff();var item=items[index];
        using(var dialog=new Form())using(var field=new TextBox())using(var save=new Button())using(var cancel=new Button()) {
            dialog.BackColor=ActivityTheme.Surface;dialog.ForeColor=ActivityTheme.Text;field.BackColor=ActivityTheme.Hover;field.ForeColor=ActivityTheme.Text;
            save.BackColor=ActivityTheme.Selected;save.ForeColor=ActivityTheme.Text;save.FlatStyle=FlatStyle.Flat;cancel.BackColor=ActivityTheme.Surface;cancel.ForeColor=ActivityTheme.Text;cancel.FlatStyle=FlatStyle.Flat;
            dialog.Text="Rename pinned app";dialog.FormBorderStyle=FormBorderStyle.FixedDialog;
            dialog.StartPosition=FormStartPosition.CenterParent;dialog.ClientSize=new Size(380,116);
            dialog.MinimizeBox=false;dialog.MaximizeBox=false;dialog.ShowInTaskbar=false;
            field.SetBounds(18,18,344,28);field.Text=item.Label;field.MaxLength=80;
            save.Text="Save";save.SetBounds(184,68,84,30);save.DialogResult=DialogResult.OK;
            cancel.Text="Cancel";cancel.SetBounds(278,68,84,30);cancel.DialogResult=DialogResult.Cancel;
            dialog.Controls.AddRange(new Control[]{field,save,cancel});dialog.AcceptButton=save;dialog.CancelButton=cancel;
            dialog.Shown+=(s,e)=>{field.Focus();field.SelectAll();};
            if(dialog.ShowDialog(this)==DialogResult.OK){string value=field.Text.Trim();if(value.Length==0)state.FavoriteNames.Remove(FavoriteKey(item));else state.FavoriteNames[FavoriteKey(item)]=value;SaveState();Redraw();}
        }
        FocusSearch();
    }
    void OpenFavorite(int index) {var items=FavoriteItems();if(index>=0&&index<items.Count)ChooseResult(items[index]);}
    void BrowseInto(string path) {
        if(!Directory.Exists(path))return;
        browsePath=FolderSearch.Canonical(path);browsing=true;settingsOpen=false;search.Clear();
        try {browseEntries=Directory.EnumerateDirectories(browsePath).OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).Take(80).Concat(Directory.EnumerateFiles(browsePath).OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).Take(80)).ToArray();}
        catch(Exception e){browseEntries=new string[0];Native.Log("Browse folder: "+e.GetType().Name);}
        RebuildResults();Redraw();search.Focus();
    }
    void BrowseParent() {
        if(!browsing)return;string parent=Path.GetDirectoryName(browsePath.TrimEnd('\\'));
        if(String.IsNullOrEmpty(parent)||String.Equals(parent,browsePath,StringComparison.OrdinalIgnoreCase)){browsing=false;browsePath="";browseEntries=new string[0];}
        else BrowseInto(parent);
        RebuildResults();Redraw();
    }
    bool HandleSettingsClick(Point p) {
        if(!settingsPanel.Contains(p))return false;
        int row=(p.Y-settingsPanel.Top-U(62))/SettingsRowHeight();if(row<0||row>7)return true;
        if(row==0)state.MotionDuration=state.MotionDuration<300?300:state.MotionDuration<400?400:200;
        else if(row==1)state.PreviewColumns=state.PreviewColumns>=5?3:state.PreviewColumns+1;
        else if(row==2)state.ReducedMotion=!state.ReducedMotion;
        else if(row==3)return true; // Wallpaper is always untinted; no full-screen backdrop.
        else if(row==4)state.GameFriendly=!state.GameFriendly;
        else if(row==5){settingsOpen=false;EndOverview(DesktopActions.PowerMenu);return true;}
        else if(row==6){
            using(var dialog=new OpenFileDialog{Title="Add a portable application",Filter="Applications and shortcuts|*.exe;*.lnk",CheckFileExists=true})
                if(dialog.ShowDialog(this)==DialogResult.OK){AppCatalog.AddPortable(dialog.FileName);LoadApps(true);}
        }
        else if(row==7){settingsOpen=false;search.Focus();}
        SaveState();reducedMotion=state.ReducedMotion||!SystemInformation.UIEffectsEnabled;LayoutCards();UpdateThumbnails(1);Redraw();return true;
    }
    void StopMotion() {
        Interlocked.Increment(ref motionEpoch);
        var cancellation=motionCancellation;motionCancellation=null;
        var worker=motionThread;motionThread=null;
        if(cancellation!=null){try{cancellation.Cancel();}catch(ObjectDisposedException){}if(worker==null)cancellation.Dispose();}
    }
    internal static double MotionMilliseconds(int openingDuration,bool reverse) {
        int opening=Math.Max(120,Math.Min(600,openingDuration));
        return reverse?Math.Round(opening*11.0/15.0):opening;
    }
    // WindowScatter's ease-out starts moving immediately and settles softly.
    internal static double MotionCurve(double progress) {double p=Math.Max(0,Math.Min(1,progress));return 1-Math.Pow(1-p,3);}
    void StartMotion() {
        StopMotion();
        motionPaintMs=0;motionPaintCoalesces=0;
        var cancellation=new CancellationTokenSource();motionCancellation=cancellation;
        var token=cancellation.Token;int epoch=Volatile.Read(ref motionEpoch);
        var surfaces=CaptureSurfaces();bool reverse=closing;IntPtr renderHost=previewHost.Handle;
        double duration=MotionMilliseconds(state.MotionDuration,reverse);
        UpdateMotionBadges(reverse?1:0);
        int badgeFramePending=0;
        motionThread=new Thread(()=> {
            Native.CompositionTiming compositionBefore;bool hasComposition=Native.ReadCompositionTiming(out compositionBefore);
            double targetHz=hasComposition&&compositionBefore.RefreshHz>20?compositionBefore.RefreshHz:165;
            var clock=Stopwatch.StartNew();int count=0,presentBatches=0,presentFailures=0;double updates=0,last=0,worstGap=0,worstGapAt=0,presentWaitMs=0;
            var intervals=new List<double>(128);
            try {
              using(var pacer=new FramePacer()) {
                while(!token.IsCancellationRequested&&epoch==Volatile.Read(ref motionEpoch)) {
                    double now=clock.Elapsed.TotalMilliseconds;
                    if(count>0){double gap=now-last;if(gap>worstGap){worstGap=gap;worstGapAt=now;}intervals.Add(gap);}last=now;
                    double raw=Math.Min(1,now/duration);
                    // The exit follows the entrance path backward in time. Using
                    // 1-ease(raw) instead makes the reverse motion stall, then snap.
                    double eased=MotionCurve(reverse?1-raw:raw);
                    long updateStart=Stopwatch.GetTimestamp();ApplySurfaces(surfaces,eased,true);
                    updates+=(Stopwatch.GetTimestamp()-updateStart)*1000.0/Stopwatch.Frequency;count++;
                    if(Interlocked.CompareExchange(ref badgeFramePending,1,0)==0){
                        double badgeProgress=eased;
                        BeginInvoke((Action)(()=>{try{if(!shuttingDown&&!token.IsCancellationRequested&&epoch==Volatile.Read(ref motionEpoch))UpdateMotionBadges(badgeProgress);}finally{Interlocked.Exchange(ref badgeFramePending,0);}}));
                    }
                    // As in WindowScatter, DWM presents the whole batch and paces
                    // the worker itself. No second timer wait after a real vsync,
                    // and no synchronous GDI/UI-thread painting during native motion.
                    double period=1000.0/targetHz;
                    if(surfaces.Length>0&&!token.IsCancellationRequested) {
                        var damage=new Native.Rect(new Rectangle(0,0,2,2));Native.RedrawWindow(renderHost,ref damage,IntPtr.Zero,0x101);double started=clock.Elapsed.TotalMilliseconds;int status=Native.DwmFlush();
                        double waited=clock.Elapsed.TotalMilliseconds-started;presentWaitMs+=waited;
                        if(status==0)presentBatches++;else presentFailures++;
                        // Remote/inactive composition may return immediately: avoid spinning.
                        if(status!=0||waited<0.5)pacer.WaitUntil(clock,now+period,token);
                    }else pacer.WaitUntil(clock,now+period,token);
                    if(raw>=1)break;
                }
                if(token.IsCancellationRequested||epoch!=Volatile.Read(ref motionEpoch))return;
                Native.CompositionTiming compositionAfter;bool compositionValid=Native.ReadCompositionTiming(out compositionAfter);
                BeginInvoke((Action)(()=>{
                    if(shuttingDown||token.IsCancellationRequested||epoch!=Volatile.Read(ref motionEpoch)||!rendering)return;
                    frames=count;thumbnailMs=updates;
                    intervals.Sort();
                    double median=intervals.Count>0?intervals[intervals.Count/2]:0;
                    double p95=intervals.Count>0?intervals[Math.Min(intervals.Count-1,(int)(intervals.Count*.95))]:0;
                    Native.Log("Compositor motion: target Hz="+targetHz+", submitted updates="+count+", duration ms="+duration+", median gap ms="+median.ToString("F1")+", p95 gap ms="+p95.ToString("F1")+", max gap ms="+worstGap.ToString("F1")+", max gap at ms="+worstGapAt.ToString("F1")+", UI paint during motion ms="+motionPaintMs.ToString("F1")+", retained paint coalesces="+motionPaintCoalesces);
                    Native.Log("Presentation synchronization: successful batches="+presentBatches+", failures="+presentFailures+", total wait ms="+presentWaitMs.ToString("F1")+" (wait completion does not identify individual panel frames)");
                    Native.Log("Renderer: separate non-layered DWM host; cubic ease-out; aspect-locked scaling; tiny native host commits; live wallpaper="+previewHost.LiveWallpaper);
                    if(hasComposition&&compositionValid)Native.Log("DWM desktop timing: refresh Hz="+compositionAfter.RefreshHz.ToString("F3")+", compose Hz="+compositionAfter.ComposeHz.ToString("F3")+", composed delta="+(compositionAfter.Frame-compositionBefore.Frame)+", confirmed delta="+(compositionAfter.FrameConfirmed-compositionBefore.FrameConfirmed)+", late delta="+(compositionAfter.FramesLate-compositionBefore.FramesLate)+" (global counters, not panel FPS)");
                    if(reverse)CompleteClose();else FinishEntrance();
                }));
              }
            }catch(Exception error){Native.Log("Render error: "+error);try{BeginInvoke((Action)(()=>{if(!shuttingDown&&!token.IsCancellationRequested&&epoch==Volatile.Read(ref motionEpoch))Abort();}));}catch{}}
            finally {
                bool cancelled=token.IsCancellationRequested;cancellation.Dispose();
                // A cancelled worker can finish one in-flight DWM call after the
                // UI settled the transition. Reapply the current idle state once,
                // without ever joining the compositor thread from the UI thread.
                if(cancelled)try{BeginInvoke((Action)(()=>{if(!shuttingDown&&Visible&&!rendering&&Volatile.Read(ref motionEpoch)==epoch+1)UpdateThumbnails(1);}));}catch{}
            }
        });
        motionThread.IsBackground=true;motionThread.Priority=ThreadPriority.AboveNormal;motionThread.Name="Activities compositor animation";motionThread.Start();
    }
    Font FontAt(float size,bool bold=false) {
        float pixels=size*scale;string key=pixels.ToString("R")+":"+bold;Font font;
        if(!fontCache.TryGetValue(key,out font)){font=new Font("Segoe UI",pixels,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);fontCache[key]=font;}return font;
    }
    void WatchAppCatalog() {
        Directory.CreateDirectory(SearchMode.DataDirectory);
        foreach(string directory in AppCatalog.ShortcutDirectories.Concat(new[]{SearchMode.DataDirectory}))try {
            if(!Directory.Exists(directory))continue;
            var watcher=new FileSystemWatcher(directory,directory==SearchMode.DataDirectory?"portable-apps.json":"*.lnk");
            watcher.IncludeSubdirectories=directory!=SearchMode.DataDirectory;
            watcher.NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.DirectoryName;
            FileSystemEventHandler changed=(s,e)=>ScheduleAppRefresh();
            watcher.Created+=changed;watcher.Changed+=changed;watcher.Deleted+=changed;watcher.Renamed+=(s,e)=>ScheduleAppRefresh();
            watcher.Error+=(s,e)=>ScheduleAppRefresh();watcher.EnableRaisingEvents=true;appWatchers.Add(watcher);
        }catch(Exception e){Native.Log("App watcher: "+e.GetType().Name);}
    }
    void ScheduleAppRefresh() {
        try{if(!shuttingDown&&IsHandleCreated)BeginInvoke((Action)(()=>{if(shuttingDown||IsDisposed)return;lastAppRefresh=DateTime.MinValue;appRefreshDelay.Stop();appRefreshDelay.Start();}));}catch{}
    }
    public void LoadApps(bool force=false) {
        if(shuttingDown||IsDisposed||Disposing)return;
        if(!force&&(DateTime.UtcNow-lastAppRefresh).TotalSeconds<30)return;
        if(Interlocked.CompareExchange(ref appIndexLoading,1,0)!=0){appRefreshDelay.Start();return;}
        var thread=new Thread(()=> {
            try {
                var timer=Stopwatch.StartNew();var list=AppCatalog.Build();
                if(list.Count>0)AppCatalog.SaveCache(list);
                BeginInvoke((Action)(()=>{
                    if(shuttingDown||IsDisposed||Disposing)return;
                    lastAppRefresh=DateTime.UtcNow;
                    if(list.Count>0){apps=list;appsReady=true;MigrateAppHistory();}
                    if(Visible&&!openingMotion){RebuildResults(resultNavigationUsed);Redraw();}
                    Native.Log("Application index ready: "+apps.Count+", pgAdmin="+apps.Any(a=>SearchRank.App(a,"pgadmin")>0)+", build ms="+timer.ElapsedMilliseconds);
                }));
            }catch(Exception e){Native.Log("App catalogue refresh: "+e.GetType().Name);}
            finally{Interlocked.Exchange(ref appIndexLoading,0);}
        });thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();
    }
    public void Toggle() {
        Native.Log("Toggle received: visible="+Visible+", opening="+openingMotion+", closing="+closing+", rendering="+rendering);
        if(rendering){Native.Log("Toggle ignored while overview transition is active");return;}
        if(Visible)EndOverview(null); else BeginOverview();
    }
    void ReadWindows() {
        ReleaseThumbnails();windows.Clear();
        uint ownPid=(uint)Process.GetCurrentProcess().Id;var processes=new Dictionary<uint,ProcessIdentity>();
        Native.EnumWindows((hwnd,data)=> {
            if(hwnd==Handle || !Native.IsWindowVisible(hwnd))return true;
            long ex=Native.GetWindowLongPtr(hwnd,-20).ToInt64();
            bool owned=Native.GetWindow(hwnd,4)!=IntPtr.Zero;
            var className=new StringBuilder(128);Native.GetClassName(hwnd,className,className.Capacity);
            long style=Native.GetWindowLongPtr(hwnd,-16).ToInt64();
            bool dialog=className.ToString()=="#32770"||(owned&&(style&0x00C00000)==0x00C00000);
            if(((ex&0x80)!=0&&!dialog)||((ex&0x40000)==0&&owned&&!dialog))return true;
            int cloaked; if(Native.DwmGetInt(hwnd,14,out cloaked,4)==0&&cloaked!=0)return true;
            var title=new StringBuilder(1024);Native.GetWindowText(hwnd,title,title.Capacity);if(title.Length==0)return true;
            uint pid;Native.GetWindowThreadProcessId(hwnd,out pid);
            if(pid==ownPid)return true;ProcessIdentity process;
            if(!processes.TryGetValue(pid,out process)){process=ProcessSnapshot.Read(pid);processes[pid]=process;}
            if(process.Name.Equals("Clean11.Dock",StringComparison.OrdinalIgnoreCase)||process.Name.Equals("seelen-ui",StringComparison.OrdinalIgnoreCase)||process.Name.Equals("Flow.Launcher",StringComparison.OrdinalIgnoreCase))return true;
            string processPath=process.Path;bool game=IsGameProcess(process.Name,processPath);
            Rectangle rect=Native.Bounds(hwnd);bool minimized=Native.IsIconic(hwnd);
            if(minimized){var placement=new Native.Placement{Length=Marshal.SizeOf(typeof(Native.Placement))};if(Native.GetWindowPlacement(hwnd,ref placement)&&placement.NormalPosition.Right>placement.NormalPosition.Left&&placement.NormalPosition.Bottom>placement.NormalPosition.Top)rect=Native.PlacementBounds(hwnd,placement);}
            if(!minimized&&(rect.Width<80||rect.Height<60||!rect.IntersectsWith(desktop)))return true;
            string processName=process.Name;
            var item=new WindowItem{Hwnd=hwnd,Pid=pid,ZOrder=windows.Count,Title=title.ToString(),ProcessPath=processPath,ProcessName=processName,Original=rect,Minimized=minimized,Game=game,Active=hwnd==previous};
            windows.Add(item);AppIcon(processPath);WindowAppName(item);return true;
        },IntPtr.Zero);
        // Keep stable application groups; foreground/title changes must not reorder cards.
        windows.Sort((a,b)=> {
            int group=String.Compare(a.ProcessName,b.ProcessName,StringComparison.OrdinalIgnoreCase);
            return group!=0?group:a.Hwnd.ToInt64().CompareTo(b.Hwnd.ToInt64());
        });
        // DWM composes later registrations over earlier ones. Preserve desktop z-order.
        foreach(var item in windows.OrderByDescending(w=>w.ZOrder)) {
            // A live high-refresh game surface can make DWM copy/compose the game
            // throughout the overview. Keep its switch/close card without that surface.
            if(item.Minimized||(item.Game&&state.GameFriendly))continue;
            if(Native.DwmRegisterThumbnail(previewHost.Handle,item.Hwnd,out item.Thumbnail)==0) {
                Native.Size size;if(Native.DwmQueryThumbnailSourceSize(item.Thumbnail,out size)==0&&(!item.Minimized||(size.Width>=80&&size.Height>=60)))item.SourceSize=new System.Drawing.Size(size.Width,size.Height);
            }
        }
    }
    void BeginOverview() {
        if(rendering)return;
        try {
            LoadUnifiedPreferences();HideSpotlight();RecoverSpotlight();previewOpacity=255;SettingsCatalog.Refresh(false);
            previous=Native.GetForegroundWindow();desktop=Screen.FromPoint(Cursor.Position).Bounds;
            Bounds=desktop;scale=Math.Max(1,Native.GetDpiForWindow(Handle)/96f);
            reducedMotion=state.ReducedMotion||!SystemInformation.UIEffectsEnabled;
            StopMotion();ReleaseOwnPointerCapture();ReleaseThumbnails();windows.Clear();pointerOwnershipConfirmed=false;
            exitWindow=null;exitSearchBounds=Rectangle.Empty;
            search.Text=""; selected=-1; hover=hoverClose=-1; closing=false;afterClose=null;settingsOpen=false;browsing=false;browsePath="";measureSettledPaint=false;
            Native.SendMessage(search.Handle,0x1501,new IntPtr(1),"");
            LayoutCards();RebuildResults();openingMotion=rendering=true;search.Visible=diagnostic||spotlightUnavailable;searchHint.Visible=false;chromeDirty=true;
            focusHandoffArmed=true;focusRecoveryAttempts=0;lastFocusRecovery=-100;focusHandoffClock.Restart();focusSettleTimer.Start();
            // Prepare native surfaces at their desktop positions before exposing the
            // wallpaper host. Showing it first produces a wallpaper-only flash.
            var preparation=Stopwatch.StartNew();var phase=Stopwatch.StartNew();
            previewHost.Prepare(desktop);double wallpaperPreparationMs=phase.Elapsed.TotalMilliseconds;
            phase.Restart();ReadWindows();double windowDiscoveryMs=phase.Elapsed.TotalMilliseconds;
            phase.Restart();LayoutCards();RebuildResults();UpdateThumbnails(0);double layoutMs=phase.Elapsed.TotalMilliseconds;
            phase.Restart();EnsurePaintCache();using(var surface=Graphics.FromImage(chromeCache))PaintChrome(surface);chromeDirty=false;double chromeCacheMs=phase.Elapsed.TotalMilliseconds;
            // Build captions before either the search or native window motion starts.
            foreach(var window in windows){PlaceSpotlightBadge(window,0);}
            UpdateMotionBadges(0);
            previewHost.Show();Show();Activate();bool foreground=Native.SetForegroundWindow(Handle);FocusSearch();Update();
            Native.Log("Prepared complete scene before show; total ms="+preparation.Elapsed.TotalMilliseconds.ToString("F1")+", wallpaper ms="+wallpaperPreparationMs.ToString("F1")+", windows ms="+windowDiscoveryMs.ToString("F1")+", layout-and-thumbnails ms="+layoutMs.ToString("F1")+", chrome-cache ms="+chromeCacheMs.ToString("F1")+", foreground="+foreground);
            frames=0;thumbnailMs=paintMs=motionPaintMs=0;StartMotion();
            windowWatcher.Start();
            Native.Log("Opened: "+windows.Count+" windows, "+windows.Count(w=>w.Thumbnail!=IntPtr.Zero)+" live DWM surfaces");
            if(windows.Any(w=>w.Game))Native.Log("Game-friendly overview: game live surfaces omitted="+windows.Count(w=>w.Game));
            if(reducedMotion)FinishEntrance();
        }catch(Exception e){Native.Log("Open failed: "+e);StopFocusHandoff();StopMotion();ReleaseThumbnails();Hide();rendering=false;}
    }
    void LayoutCards() {
        int left=Math.Max(U(70),(ClientSize.Width-U(2680))/2), width=ClientSize.Width-left-U(70);
        int searchWidth=Math.Min(U(760),Math.Max(U(300),ClientSize.Width-U(48)));
        searchBox=new Rectangle((ClientSize.Width-searchWidth)/2,U(56),searchWidth,U(60));
        closeBox=new Rectangle(searchBox.Right-U(40),searchBox.Top+U(13),U(28),U(28));
        settingsBox=new Rectangle(searchBox.Right-U(78),searchBox.Top+U(13),U(28),U(28));
        favoriteArea=new Rectangle(searchBox.Left+U(16),U(128),searchBox.Width-U(32),U(76));
        settingsPanel=new Rectangle((ClientSize.Width-Math.Min(U(760),ClientSize.Width-U(48)))/2,U(142),Math.Min(U(760),ClientSize.Width-U(48)),Math.Min(U(550),ClientSize.Height-U(200)));
        browseBackBox=new Rectangle(searchBox.Left,U(153),searchBox.Width,U(34));
        search.Bounds=new Rectangle(searchBox.Left+U(46),searchBox.Top+U(16),searchBox.Width-U(150),U(30));
        Font searchFont=FontAt(19);if(search.Font!=searchFont)search.Font=searchFont;searchHint.Font=searchFont;searchHint.Bounds=search.Bounds;
        var live=windows.Where(w=>!w.Minimized).ToArray();
        var minimized=windows.Where(w=>w.Minimized).ToArray();
        int gap=U(14),columns=Math.Max(1,width/U(294));
        int rows=(int)Math.Ceiling(minimized.Length/(double)columns);
        int rowHeight=rows==0?0:Math.Min(U(112),Math.Max(U(48),(ClientSize.Height-U(360))/Math.Max(1,rows+2)));
        int reserved=rows==0?0:rows*(rowHeight+gap)+U(30);
        var layout=WindowLayout.Arrange(live.Select(w=>WindowLayout.SourceSize(w.Original.Size,w.SourceSize)).ToArray(),
            new Rectangle(left,U(278),Math.Max(1,width),Math.Max(1,ClientSize.Height-U(365)-reserved)),U(28),U(68),U(34),state.PreviewColumns);
        for(int i=0;i<live.Length;i++) {
            live[i].Card=layout.Footprints[i];live[i].Preview=layout.Previews[i];
            var preview=live[i].Preview;preview.Offset(0,U(34));live[i].Preview=preview;
        }
        int startY=live.Length==0?Math.Max(U(240),(ClientSize.Height-reserved)/2):ClientSize.Height-U(80)-reserved;
        for(int i=0;i<minimized.Length;i++) {
            int row=i/columns,count=Math.Min(columns,minimized.Length-row*columns);
            int cardWidth=Math.Min(U(280),(width-gap*(count-1))/count);
            int startX=(ClientSize.Width-(count*cardWidth+(count-1)*gap))/2;
            var bounds=new Rectangle(startX+(i%columns)*(cardWidth+gap),startY+U(30)+row*(rowHeight+gap),cardWidth,rowHeight);
            minimized[i].Card=minimized[i].Preview=bounds;
        }
    }

    internal static Rectangle Fit(System.Drawing.Size source,Rectangle bounds) {
        if(source.Width<=0||source.Height<=0)return bounds;
        double factor=Math.Min((double)bounds.Width/source.Width,(double)bounds.Height/source.Height);
        int width=Math.Max(1,(int)Math.Round(source.Width*factor)),height=Math.Max(1,(int)Math.Round(source.Height*factor));
        return new Rectangle(bounds.Left+(bounds.Width-width)/2,bounds.Top+(bounds.Height-height)/2,width,height);
    }
    internal static Rectangle Mix(Rectangle a,Rectangle b,double t) {
        if(t<=0)return a;if(t>=1)return b;
        int width=Math.Max(1,(int)Math.Round(a.Width+(b.Width-a.Width)*t));
        // Match WindowScatter: derive height from width, never interpolate the
        // two axes independently (which can warp windows during the transition).
        double aspect=a.Height>0?a.Width/(double)a.Height:1;
        return new Rectangle((int)Math.Round(a.X+(b.X-a.X)*t),(int)Math.Round(a.Y+(b.Y-a.Y)*t),
            width,Math.Max(1,(int)Math.Round(width/Math.Max(.001,aspect))));
    }
    MotionSurface[] CaptureSurfaces() {
        return windows.Where(w=>w.Thumbnail!=IntPtr.Zero&&(!closing||exitSearchBounds.IsEmpty||w==exitWindow)).Select(w=> {
            Rectangle original=w.Original;original.Offset(-desktop.Left,-desktop.Top);
            bool valid=(!w.Minimized||(closing&&w==exitWindow))&&original.Width>0&&original.Height>0&&original.IntersectsWith(ClientRectangle);
            Rectangle preview=closing&&w==exitWindow&&!exitSearchBounds.IsEmpty?Fit(original.Size,exitSearchBounds):w.Preview;
            return new MotionSurface{Thumbnail=w.Thumbnail,From=original,To=preview,HasOrigin=valid};
        }).ToArray();
    }
    static void ApplySurfaces(MotionSurface[] surfaces,double t,bool visible) {
        foreach(var surface in surfaces) {
            Rectangle destination=surface.HasOrigin?Mix(surface.From,surface.To,t):surface.To;
            var props=new Native.Props{Flags=1|4|8|16,Destination=new Native.Rect(destination),Opacity=(byte)(surface.HasOrigin?255:Math.Round(255*t)),Visible=visible,ClientOnly=false};
            Native.DwmUpdateThumbnailProperties(surface.Thumbnail,ref props);
        }
    }
    void UpdateThumbnails(double t) {
        // Search owns preview visibility for both expansion and collapse.
        if(SpotlightActive&&spotlight.IsAnimating&&!closing)return;
        if(Visible&&!rendering&&!closing&&!diagnostic){UpdateRoundedPreviews();return;}
        ApplySurfaces(CaptureSurfaces(),t,search.Text.Trim().Length==0&&!browsing&&!settingsOpen);
        UpdateRoundedPreviews();
    }
    void FocusSearch() {
        if(shuttingDown||IsDisposed||!Visible||closing||settingsOpen)return;
        IntPtr focused=Native.GetForegroundWindow();
        if(focused!=Handle&&!SpotlightOwnsForeground(focused)){FocusSpotlightOverview(false);return;}
        if(FocusSpotlightOverview()){EnsurePointerInput();return;}
        search.Visible=true;searchHint.Visible=search.Text.Length==0;if(ActiveControl!=search)ActiveControl=search;
        if(!search.Focused)search.Focus();
        EnsurePointerInput();
    }
    void ReleaseOwnPointerCapture(){if(Capture)Capture=false;if(search.Capture)search.Capture=false;}
    void EnsurePointerInput() {
        if(pointerOwnershipConfirmed||!Visible||closing||(Native.GetForegroundWindow()!=Handle&&!SpotlightOwnsForeground(Native.GetForegroundWindow())))return;
        pointerOwnershipConfirmed=true;ReleaseOwnPointerCapture();Native.Rect clip;
        Native.Log("Opening foreground input confirmed; search="+search.Focused);
        // Release a stale game-sized confinement only after this panel truly
        // owns foreground input. The game can reapply its own clip on return.
        if(desktop.Width>0&&Native.GetClipCursor(out clip)&&!clip.Box.Contains(desktop)) {
            bool released=Native.ClipCursor(IntPtr.Zero);Native.Log("Opening cursor confinement released="+released);
        }
    }
    void StopFocusHandoff(){focusHandoffArmed=false;focusSettleTimer.Stop();focusHandoffClock.Stop();}
    bool IntentionalFocusChange() {
        if(Native.GetAsyncKeyState(1)<0||Native.GetAsyncKeyState(2)<0||Native.GetAsyncKeyState(4)<0)return true;
        if(Native.GetAsyncKeyState(0x12)<0)return true; // Alt+Tab/Alt+Esc: always yield.
        bool win=Native.GetAsyncKeyState(0x5B)<0||Native.GetAsyncKeyState(0x5C)<0;
        return win&&new[]{9,0x44,0x45,0x4C,0x53}.Any(key=>Native.GetAsyncKeyState(key)<0);
    }
    void SettleOpeningFocus() {
        if(!focusHandoffArmed)return;
        if(shuttingDown||!Visible||closing||settingsOpen||focusHandoffClock.ElapsedMilliseconds>600||IntentionalFocusChange()){StopFocusHandoff();return;}
        IntPtr foreground=Native.GetForegroundWindow();
        if(SpotlightOwnsForeground(foreground)){EnsurePointerInput();return;}
        if(foreground==Handle){FocusSearch();return;}
        bool allowed=foreground==previous||foreground==IntPtr.Zero;
        if(!allowed)try {
            uint pid;Native.GetWindowThreadProcessId(foreground,out pid);
            allowed=String.Equals(ProcessSnapshot.Read(pid).Name,"seelen-ui",StringComparison.OrdinalIgnoreCase);
        }catch{}
        if(!allowed){StopFocusHandoff();return;}
        if(focusRecoveryAttempts>=5||focusHandoffClock.ElapsedMilliseconds-lastFocusRecovery<50)return;
        lastFocusRecovery=focusHandoffClock.ElapsedMilliseconds;focusRecoveryAttempts++;
        bool granted=Native.SetForegroundWindow(Handle);
        if(Native.GetForegroundWindow()==Handle)FocusSearch();
        Native.Log("Opening focus recovery: granted="+granted+", search="+search.Focused);
    }
    protected override void OnActivated(EventArgs e) {
        base.OnActivated(e);FocusSearch();
    }
    protected override void OnDeactivate(EventArgs e) {
        base.OnDeactivate(e);
        if(focusHandoffArmed&&IntentionalFocusChange()){StopFocusHandoff();return;}
        if(focusHandoffArmed&&!closing&&!shuttingDown)try{BeginInvoke((Action)SettleOpeningFocus);}catch{}
    }
    void FinishEntrance(bool focusSearch=true) {
        if(shuttingDown||closing||!openingMotion)return;
        // Typing must not re-select the textbox or activate a form mid-TextChanged.
        // Claim any final focus while redraws are still deferred by MotionActive.
        if(focusSearch&&Visible)FocusSearch();
        StopMotion();rendering=false;openingMotion=false;UpdateThumbnails(1);
        if(!TryPaintWindowMetadata(true))Redraw();
        if((DateTime.UtcNow-lastAppRefresh).TotalSeconds>=30)appRefreshDelay.Start();
        Native.Log("Search focus="+(search.Focused||SpotlightOwnsForeground(Native.GetForegroundWindow()))+", shared Spotlight input="+SpotlightHeaderVisible);
        Native.Log("Entrance complete; rendered ticks="+frames+", DWM updates ms="+(int)thumbnailMs+", paint ms="+(int)paintMs);
    }
    void EndOverview(Action next) {
        if(closing){if(next!=null)afterClose+=next;return;}
        if(!Visible){HideSpotlight();if(next!=null)next();return;}
        StopFocusHandoff();if(openingMotion)FinishEntrance(false);
        closing=true;
        HideSpotlight();
        StopFocusHandoff();if(openingMotion)FinishEntrance(false);
        afterClose=next;closing=true;rendering=true;search.Visible=false;searchHint.Visible=false;
        Interlocked.Increment(ref closeGeneration);
        // A search result has no large preview to animate back from.
        if(reducedMotion||(search.Text.Trim().Length>0&&exitWindow==null)){CompleteClose();return;}
        ApplySurfaces(CaptureSurfaces(),1,true);
        // Present the replacement before removing the settled preview hosts.
        // Otherwise the desktop is exposed for a frame at the start of exit.
        Native.DwmFlush();
        HideRoundedPreviews();
        Redraw();Update();
        StartMotion();
    }
    void CompleteClose() {
        int generation=Volatile.Read(ref closeGeneration);
        if(generation==completedCloseGeneration)return;
        completedCloseGeneration=generation;
        StopFocusHandoff();
        ReleaseOwnPointerCapture();
        folderDelay.Stop();folderTimeout.Stop();folderGeneration++;folderReplyId=0;openFolderWhenReady=false;
        windowWatcher.Stop();StopMotion();HideSpotlight();rendering=false;openingMotion=false;Hide();ReleaseThumbnails();closing=false;
        Native.Log("Overview hidden");
        Action next=afterClose;afterClose=null;
        Native.Log("Overview close completed; selected="+(exitWindow==null?"none":exitWindow.ProcessName+"/"+exitWindow.Hwnd)+", generation="+generation);
        exitWindow=null;exitSearchBounds=Rectangle.Empty;
        if(next!=null)next();else if(Native.IsWindow(previous))Native.SetForegroundWindow(previous);
    }
    public void Abort() {if(shuttingDown||IsDisposed)return;closing=true;HideSpotlight();ReleaseOwnPointerCapture();StopFocusHandoff();folderDelay.Stop();folderTimeout.Stop();folderGeneration++;folderReplyId=0;openFolderWhenReady=localFolderPending=indexFolderPending=false;windowWatcher.Stop();StopMotion();Hide();ReleaseThumbnails();rendering=false;closing=false;openingMotion=false;}
    void ReleaseThumbnails() {ReleaseRoundedPreviews();foreach(var w in windows)if(w.Thumbnail!=IntPtr.Zero){Native.DwmUnregisterThumbnail(w.Thumbnail);w.Thumbnail=IntPtr.Zero;}}
    static int Score(string label,string query) {
        return SearchRank.Match(label,query);
    }
    void QueueFolderSearch() {
        if(diagnostic||shuttingDown||IsDisposed||Disposing)return;
        folderDelay.Stop();folderTimeout.Stop();folderGeneration++;folderReplyId=0;
        localFolders=indexedFolders=new FileHit[0];resultScroll=0;openFolderWhenReady=resultNavigationUsed=false;fileIndexUnavailable=false;
        localFolderPending=indexFolderPending=!browsing&&SearchQuery.Parse(search.Text).NeedsFiles;
        if(localFolderPending)folderDelay.Start();
    }
    void StartFolderSearch() {
        if(shuttingDown||IsDisposed||Disposing||!IsHandleCreated)return;folderDelay.Stop();
        string query=SearchQuery.Parse(search.Text).Text;uint generation=folderGeneration;
        if(query.Length==0)return;
        folderReplyId=0xA1000000u+generation*2u;
        fileExactPhase=!Path.IsPathRooted(FolderSearch.Normalize(query));
        indexFolderPending=FolderSearch.Request(Handle,folderReplyId,query,fileExactPhase);
        fileIndexUnavailable=!indexFolderPending;
        folderTimeout.Start();
        ThreadPool.QueueUserWorkItem(_=> {
            FileHit[] matches=FolderSearch.Local(query);
            try {BeginInvoke((Action)(()=>{if(shuttingDown||IsDisposed||Disposing||generation!=folderGeneration)return;localFolders=matches;localFolderPending=false;ApplyFolderResults();}));}catch{}
        });
    }
    void ApplyFolderResults() {
        RebuildResults(resultNavigationUsed);Redraw();
        if(!localFolderPending&&!indexFolderPending) {
            folderTimeout.Stop();bool open=openFolderWhenReady;openFolderWhenReady=false;
            if(open&&results.Any(r=>r.Action==null)&&Visible)Choose(0);
        }
    }
    protected override void WndProc(ref Message message) {
        if(message.Msg==SuperKeyDispatcher.ToggleMessage){Toggle();message.Result=IntPtr.Zero;return;}
        if(message.Msg==0x000F&&Visible&&MotionActive)CoalesceMotionPaint();
        if(message.Msg==0x000F&&measureSettledPaint&&!MotionActive) {
            measureSettledPaint=false;var paint=Stopwatch.StartNew();base.WndProc(ref message);
            Native.Log("Entrance final WM_PAINT ms="+paint.Elapsed.TotalMilliseconds.ToString("F1"));return;
        }
        if(message.Msg==0x004A&&folderReplyId!=0) {
            FileHit[] matches;
            if(FolderSearch.ReadReply(ref message,folderReplyId,out matches)) {
                indexedFolders=fileExactPhase?matches:indexedFolders.Concat(matches).GroupBy(h=>h.Path,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToArray();
                if(fileExactPhase){
                    fileExactPhase=false;folderReplyId=0;uint generation=folderGeneration;
                    // Everything delivers replies with synchronous WM_COPYDATA.
                    // Query again only after returning its first reply, avoiding
                    // a re-entrant request while the sender is still waiting.
                    BeginInvoke((Action)(()=>{
                        if(shuttingDown||IsDisposed||generation!=folderGeneration)return;
                        folderReplyId=0xA1000000u+generation*2u+1u;
                        indexFolderPending=FolderSearch.Request(Handle,folderReplyId,SearchQuery.Parse(search.Text).Text,false);
                        fileIndexUnavailable=!indexFolderPending;folderTimeout.Stop();if(indexFolderPending)folderTimeout.Start();ApplyFolderResults();
                    }));
                } else indexFolderPending=false;
                fileIndexUnavailable=false;ApplyFolderResults();message.Result=new IntPtr(1);return;
            }
        }
        base.WndProc(ref message);
    }
    int ResultRows(){return Math.Max(1,(ClientSize.Height-U(320))/U(84));}
    void LayoutResults() {
        resultScroll=Math.Max(0,Math.Min(resultScroll,Math.Max(0,results.Count-1)));
        int left=searchBox.Left;int panelWidth=searchBox.Width;
        for(int i=0;i<results.Count;i++){results[i].Bounds=Rectangle.Empty;results[i].SectionBounds=Rectangle.Empty;}
        int y=U(browsing?218:164),bottom=ClientSize.Height-U(92);string previousSection=null;
        for(int i=resultScroll;i<results.Count;i++) {
            string section=String.IsNullOrEmpty(results[i].Section)?ResultSection(results[i]):results[i].Section;
            if(i==resultScroll||!String.Equals(section,previousSection,StringComparison.Ordinal)) {
                if(y+U(25)>bottom)break;
                results[i].SectionBounds=new Rectangle(left+U(14),y,panelWidth-U(28),U(22));y+=U(25);
            }
            if(y+U(58)>bottom)break;
            results[i].Bounds=new Rectangle(left+U(6),y,panelWidth-U(12),U(58));y+=U(66);previousSection=section;
        }
        if(resultMenuOpen)PositionResultMenu();
    }
    static string ResultSection(ResultItem result) {return result.App!=null?"Apps":result.Setting!=null?"Windows Settings":result.FolderPath!=null?"Folders":result.FilePath!=null?"Files":result.Window!=null?"Open windows":"Actions";}
    static int ResultSectionOrder(ResultItem result) {return result.App!=null?0:result.Setting!=null?1:result.FolderPath!=null?2:result.FilePath!=null?3:result.Window!=null?4:5;}
    void EnsureResultVisible(int index) {
        if(index<0||index>=results.Count)return;
        int guard=results.Count+1;
        while(results[index].Bounds.IsEmpty&&guard-->0) {
            if(index<resultScroll)resultScroll=index;
            else if(resultScroll<results.Count-1)resultScroll++;
            else break;
            LayoutResults();
        }
    }
    string ContextTarget(ResultItem item) {
        if(item==null)return null;
        if(item.FolderPath!=null)return item.FolderPath;
        if(item.FilePath!=null)return item.FilePath;
        if(item.Window!=null)return item.Window.ProcessPath;
        if(item.App!=null)return !String.IsNullOrWhiteSpace(item.App.Target)?item.App.Target:item.App.Path;
        return null;
    }
    bool CanRunElevated(ResultItem item) {
        string path=ContextTarget(item);if(String.IsNullOrWhiteSpace(path)||!File.Exists(path))return false;
        string extension=Path.GetExtension(path);return extension.Equals(".exe",StringComparison.OrdinalIgnoreCase)||extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase);
    }
    void ShowResultMenu(int index,Point point) {
        if(index<0||index>=results.Count){CloseResultMenu();return;}
        selected=index;resultMenuTarget=index;resultMenuItems.Clear();
        resultMenuItems.Add(new ResultMenuItem{Label=results[index].Window!=null?"Switch to window":"Open",Command="open"});
        string target=ContextTarget(results[index]);
        bool hasLocation=(!String.IsNullOrWhiteSpace(target)&&!target.StartsWith("shell:AppsFolder\\",StringComparison.OrdinalIgnoreCase)&&File.Exists(target))||(results[index].FolderPath!=null&&Directory.Exists(results[index].FolderPath));
        if(hasLocation)resultMenuItems.Add(new ResultMenuItem{Label="Open file location",Command="location"});
        if(CanRunElevated(results[index]))resultMenuItems.Add(new ResultMenuItem{Label="Run as administrator",Command="admin"});
        if(!String.IsNullOrWhiteSpace(target))resultMenuItems.Add(new ResultMenuItem{Label="Copy path",Command="copy"});
        if(FavoriteKey(results[index]).Length>0)resultMenuItems.Add(new ResultMenuItem{Label=IsFavorite(results[index])?"Unpin":"Pin to favorites",Command="pin"});
        resultMenuOpen=true;resultMenuHover=-1;resultMenuBounds=new Rectangle(point.X,point.Y,1,1);LayoutResults();PositionResultMenu();Redraw();
    }
    void PositionResultMenu() {
        if(!resultMenuOpen)return;
        int width=U(238),rowHeight=U(38),height=U(16)+resultMenuItems.Count*rowHeight;
        int x=resultMenuBounds.X,y=resultMenuBounds.Y;
        if(x+width>ClientSize.Width-U(8))x=ClientSize.Width-width-U(8);
        if(y+height>ClientSize.Height-U(8))y=ClientSize.Height-height-U(8);
        x=Math.Max(U(8),x);y=Math.Max(U(8),y);
        resultMenuBounds=new Rectangle(x,y,width,height);
        for(int i=0;i<resultMenuItems.Count;i++)resultMenuItems[i].Bounds=new Rectangle(x+U(6),y+U(8)+i*rowHeight,width-U(12),rowHeight);
    }
    int HitResultMenu(Point point) {return resultMenuOpen?resultMenuItems.FindIndex(item=>item.Bounds.Contains(point)):-1;}
    void CloseResultMenu() {if(!resultMenuOpen)return;resultMenuOpen=false;resultMenuTarget=-1;resultMenuHover=-1;resultMenuItems.Clear();resultMenuBounds=Rectangle.Empty;Redraw();}
    void InvokeResultMenu(int index) {
        if(index<0||index>=resultMenuItems.Count||resultMenuTarget<0||resultMenuTarget>=results.Count){CloseResultMenu();return;}
        string command=resultMenuItems[index].Command;ResultItem item=results[resultMenuTarget];string target=ContextTarget(item);
        CloseResultMenu();
        if(command=="open"){ChooseResult(item);return;}
        if(command=="pin"){ToggleFavorite(item);return;}
        if(command=="copy") {try{Clipboard.SetText(target??"");}catch(Exception e){Native.Log("Copy path failed: "+e.GetType().Name);}return;}
        if(command=="location") {string location=target;EndOverview(()=>{try{Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+location+"\""){UseShellExecute=true});}catch(Exception e){Native.Log("Open file location failed: "+e.GetType().Name);}});return;}
        if(command=="admin") {string elevated=target;string arguments=item.App==null?"":item.App.Arguments??"";EndOverview(()=>{try{Process.Start(new ProcessStartInfo(elevated,arguments){UseShellExecute=true,Verb="runas"});}catch(Exception e){Native.Log("Elevated launch failed: "+e.GetType().Name);}});}
    }
    void RebuildResults(bool preserveSelection=false) {
        ResultItem prior=preserveSelection&&selected>=0&&selected<results.Count?results[selected]:null;
        var parsed=SearchQuery.Parse(search.Text);string query=parsed.Text;results.Clear();selected=-1;
        if(browsing) {
            var entries=browseEntries.Select(path=>Directory.Exists(path)?new ResultItem{FolderPath=path,Label=Path.GetFileName(path.TrimEnd('\\')),Detail="Folder"}:new ResultItem{FilePath=path,Label=Path.GetFileName(path),Detail="File"})
                .Where(item=>query.Length==0||Score(item.Label+" "+(item.FolderPath??item.FilePath),query)>0)
                .OrderBy(item=>item.FolderPath==null?1:0).ThenBy(item=>item.Label,StringComparer.OrdinalIgnoreCase).Take(80);
            results.AddRange(entries);foreach(var result in results)result.Section=ResultSection(result);if(results.Count>0)selected=0;LayoutResults();return;
        }
        if(query.Length==0){LayoutResults();return;}
        string normalized=FolderSearch.Normalize(query).TrimEnd('\\');
        var appTargets=new HashSet<string>(apps.SelectMany(a=>new[]{a.Path,a.Target}).Where(p=>!String.IsNullOrWhiteSpace(p)),StringComparer.OrdinalIgnoreCase);
        foreach(var hit in localFolders.Concat(indexedFolders).GroupBy(f=>f.Path,StringComparer.OrdinalIgnoreCase).Select(g=>g.First())) {
            if(!parsed.Includes(hit.IsFolder?"folders":"files"))continue;
            if(!hit.IsFolder&&appTargets.Contains(hit.Path))continue;
            string name=Path.GetFileName(hit.Path.TrimEnd('\\'));if(String.IsNullOrEmpty(name))name=hit.Path;
            bool exact=String.Equals(hit.Path.TrimEnd('\\'),normalized,StringComparison.OrdinalIgnoreCase);
            int rank=exact?15000:Score(name,query);if(rank<=0)continue;
            results.Add(new ResultItem{FolderPath=hit.IsFolder?hit.Path:null,FilePath=hit.IsFolder?null:hit.Path,Label=name,Detail=hit.IsFolder?"Folder":"File",Rank=rank});
        }
        if(parsed.Includes("windows"))results.AddRange(windows.Select(w=>new{Item=w,Value=Math.Max(Score(w.Title,query),Score(w.ProcessName,query)-500)}).Where(w=>w.Value>0).OrderByDescending(w=>w.Value).Take(8).Select(w=>new ResultItem{Window=w.Item,Label=w.Item.Title,Detail="Open window",Rank=w.Value-100}));
        if(parsed.Includes("apps"))results.AddRange(apps.Select(a=>new{Item=a,Value=SearchRank.App(a,query)}).Where(a=>a.Value>0).OrderByDescending(a=>a.Value).Take(16).Select(a=>new ResultItem{App=a.Item,Label=a.Item.Name,Detail="Application",Rank=a.Value+500}));
        if(parsed.Includes("settings")&&settingMatchQuery==search.Text)results.AddRange(settingMatches);
        string calculation;
        if(parsed.Scope.Length==0&&QuickCalculator.TryCalculate(query,out calculation))results.Add(new ResultItem{Label=calculation,Detail="Copy answer",Action="calculate",Rank=20000,FilePath=null});
        if(parsed.Scope.Length==0&&new[]{"power","shutdown","shut down","restart","sleep","lock","sign out"}.Contains(query.ToLowerInvariant()))results.Add(new ResultItem{Label="Power menu",Detail="Sleep · Restart · Shut down",Action="power-menu",Rank=13000});
        results=results.OrderByDescending(r=>r.Rank+UsageScore(r)+(IsFavorite(r)?100:0)).ThenBy(ResultSectionOrder).ThenBy(r=>r.Label,StringComparer.OrdinalIgnoreCase).Take(48).ToList();
        for(int i=0;i<results.Count;i++)results[i].Section=i==0?"Best match":ResultSection(results[i]);
        if(results.Count>0)selected=0;
        if(prior!=null){string key=ResultKey(prior);int found=results.FindIndex(r=>ResultKey(r)==key);if(found>=0){selected=found;if(selected<resultScroll)resultScroll=selected;}}
        LayoutResults();
        EnsureResultVisible(selected);
        SyncSpotlightResults();
    }
    static string ResultKey(ResultItem r) {
        return r.App!=null?"app:"+AppCatalog.Identity(r.App):r.Setting!=null?"setting:"+r.Setting.Id:r.Window!=null?"window:"+r.Window.Hwnd:r.FolderPath!=null?"folder:"+r.FolderPath.ToLowerInvariant():r.FilePath!=null?"file:"+r.FilePath.ToLowerInvariant():"action:"+r.Action;
    }
    internal static string SearchResultCount(int count){return count==1?"1 result":count+" results";}
    int Hit(Point p) {
        if(search.Text.Trim().Length>0||browsing)return results.FindIndex(r=>r.Bounds.Contains(p));
        if(openingMotion)return -1;
        return windows.FindIndex(w=>w.Card.Contains(p));
    }
    internal static bool IsGameProcess(string name,string path) {
        if(new[]{"wallpaper32","wallpaper64","wallpaperui","ui32","ui64","blender","aseprite","losslessscaling"}.Contains(name.ToLowerInvariant()))return false;
        return new[]{"cs2","dota2","hl2","valorant-win64-shipping","overwatch","fortniteclient-win64-shipping"}.Contains(name.ToLowerInvariant()) ||
            path.IndexOf("\\steamapps\\common\\",StringComparison.OrdinalIgnoreCase)>=0;
    }
    Rectangle WindowCloseBox(Rectangle bounds) {return search.Text.Trim().Length>0||browsing?new Rectangle(bounds.Right-U(36),bounds.Top+U(6),U(28),U(28)):new Rectangle(bounds.Right-U(28),bounds.Top,U(28),U(28));}
    int HitWindowClose(Point p) {
        if(rendering)return -1;
        if(search.Text.Trim().Length==0)return windows.FindIndex(w=>WindowCloseBox(w.Card).Contains(p));
        var result=results.FirstOrDefault(r=>r.Window!=null&&!r.Bounds.IsEmpty&&WindowCloseBox(r.Bounds).Contains(p));
        return result==null?-1:windows.IndexOf(result.Window);
    }
    void RequestWindowClose(WindowItem window) {
        if(window.ClosePending||!Native.IsWindow(window.Hwnd))return;
        uint pid;Native.GetWindowThreadProcessId(window.Hwnd,out pid);if(pid!=window.Pid)return;
        // WM_CLOSE is the same polite request as a title-bar X. Never kill a process.
        if(Native.PostMessage(window.Hwnd,0x0010,IntPtr.Zero,IntPtr.Zero)) {
            window.ClosePending=true;window.CloseRequestedAt=DateTime.UtcNow;Redraw();Native.Log("Normal window-close request posted");
        }
    }
    void RefreshClosedWindows() {
        if(!Visible||rendering)return;
        foreach(var window in windows.Where(w=>w.ClosePending).ToArray()) {
            IntPtr popup=Native.GetLastActivePopup(window.Hwnd);
            if(popup!=IntPtr.Zero&&popup!=window.Hwnd&&Native.IsWindowVisible(popup)) {
                window.ClosePending=false;
                // Let save/confirmation dialogs be seen and answered normally.
                EndOverview(()=>Native.SetForegroundWindow(popup));return;
            }
            if((DateTime.UtcNow-window.CloseRequestedAt).TotalSeconds>3){window.ClosePending=false;Redraw();}
        }
        bool changed=false;
        foreach(var window in windows.ToArray()) {
            uint pid;Native.GetWindowThreadProcessId(window.Hwnd,out pid);
            if(!Native.IsWindow(window.Hwnd)||pid!=window.Pid||!Native.IsWindowVisible(window.Hwnd)) {
                if(window.Thumbnail!=IntPtr.Zero)Native.DwmUnregisterThumbnail(window.Thumbnail);
                ReleaseRoundedPreview(window.Hwnd);windows.Remove(window);changed=true;
            }
        }
        if(changed){hover=hoverClose=-1;LayoutCards();RebuildResults();UpdateThumbnails(1);Redraw();}
    }
    void DrawWindowClose(Graphics g,Rectangle bounds,WindowItem window) {
        var box=WindowCloseBox(bounds);int index=windows.IndexOf(window);
        FillRounded(g,box,U(ActivityTheme.ControlRadius),index==hoverClose?ActivityTheme.DangerHover:ActivityTheme.Danger);
        using(var pen=new Pen(foreground,Math.Max(1,scale))) {
            if(window.ClosePending)g.DrawLine(pen,box.Left+U(9),box.Top+U(14),box.Right-U(9),box.Top+U(14));
            else {
                g.DrawLine(pen,box.Left+U(10),box.Top+U(10),box.Right-U(10),box.Bottom-U(10));
                g.DrawLine(pen,box.Right-U(10),box.Top+U(10),box.Left+U(10),box.Bottom-U(10));
            }
        }
    }
    void Choose(int index) {
        if(search.Text.Trim().Length>0||browsing) {
            if(index<0||index>=results.Count)return;var r=results[index];
            ChooseResult(r);
        }else if(index>=0&&index<windows.Count)ChooseWindow(windows[index]);
    }
    void ChooseResult(ResultItem r) {
        if(r.Setting!=null){var target=r.Setting.Target;if(!Regex.IsMatch(target??"",@"^ms-settings:[a-z0-9-]*$"))return;EndOverview(()=>{try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)});}catch(Exception error){Native.Log("Settings launch failed: "+error.GetType().Name);}});return;}
        if(r.Action=="calculate"){try{Clipboard.SetText(r.Label);}catch{}return;}
        if(r.Action=="power-menu"){EndOverview(DesktopActions.PowerMenu);return;}
        if(r.Action=="windows-search"){EndOverview(SearchMode.OpenWindows);return;}
        if(r.Window!=null){ChooseWindow(r.Window);return;}
        if(r.FolderPath!=null) {
            if(browsing){RecordUse(r);BrowseInto(r.FolderPath);return;}
            RecordUse(r);EndOverview(()=>{try{if(Directory.Exists(r.FolderPath))Process.Start(new ProcessStartInfo(r.FolderPath){UseShellExecute=true});}catch(Exception e){Native.Log("Open folder failed: "+e.GetType().Name);}});return;
        }
        if(r.FilePath!=null){RecordUse(r);string file=r.FilePath;EndOverview(()=>{try{Process.Start(new ProcessStartInfo(file){UseShellExecute=true});}catch(Exception e){Native.Log("Open file failed: "+e.GetType().Name);}});return;}
        if(r.App!=null){RecordUse(r);EndOverview(()=>{try {Process.Start(new ProcessStartInfo(r.App.Path){UseShellExecute=true});}catch(Exception e){Native.Log("Launch failed: "+e.GetType().Name);}});}
    }
    void ChooseWindow(WindowItem window) {
        if(closing||exitWindow!=null||!Native.IsWindow(window.Hwnd))return;
        exitWindow=window;
        Native.Log("Window selection accepted once: "+window.ProcessName+" hwnd="+window.Hwnd+" minimized="+window.Minimized+" search="+(search.Text.Trim().Length>0||browsing));
        exitSearchBounds=Rectangle.Empty;
        if(search.Text.Trim().Length>0||browsing) {
            var result=results.FirstOrDefault(r=>r.Window==window&&!r.Bounds.IsEmpty);
            if(result!=null)exitSearchBounds=result.Bounds;
            if(SpotlightActive){var bounds=spotlight.SelectedRowBounds();if(!bounds.IsEmpty)exitSearchBounds=bounds;}
        }
        // Register the selected surface last so it expands above the others.
        // Its real HWND is restored/focused only after the compositor animation.
        if(window.Thumbnail!=IntPtr.Zero) {
            Native.DwmUnregisterThumbnail(window.Thumbnail);window.Thumbnail=IntPtr.Zero;
            Native.DwmRegisterThumbnail(previewHost.Handle,window.Hwnd,out window.Thumbnail);
        }
        EndOverview(()=> {
            if(!Native.IsWindow(window.Hwnd))return;
            // Restore without activating; the single explicit foreground request
            // below avoids competing activation from ShowWindowAsync(SW_RESTORE).
            if(Native.IsIconic(window.Hwnd))Native.ShowWindow(window.Hwnd,4); // SW_SHOWNOACTIVATE
            Native.SetForegroundWindow(window.Hwnd);
            Native.Log("Selected window foreground request sent once: "+window.ProcessName+" hwnd="+window.Hwnd);
        });
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys key) {
        if(HandleSearchCommand(key))return true;
        return base.ProcessCmdKey(ref msg,key);
    }
    internal static int FavoriteShortcutIndex(Keys key) {
        if((key&Keys.Modifiers)!=Keys.Control)return -1;
        Keys digit=key&Keys.KeyCode;
        if(digit>=Keys.D1&&digit<=Keys.D8)return (int)digit-(int)Keys.D1;
        if(digit>=Keys.NumPad1&&digit<=Keys.NumPad8)return (int)digit-(int)Keys.NumPad1;
        return -1;
    }
    bool HandleSearchCommand(Keys key) {
        if(key==(Keys.Control|Keys.Shift|Keys.W)){EndOverview(SearchMode.OpenWindows);return true;}
        int favoriteIndex=FavoriteShortcutIndex(key);if(favoriteIndex>=0){OpenFavorite(favoriteIndex);return true;}
        if(key==Keys.Escape){SpotlightEscape();return true;}
        if(key==(Keys.Control|Keys.D)){if(selected>=0&&selected<results.Count)ToggleFavorite(results[selected]);return true;}
        if(key==(Keys.Control|Keys.Enter)){if(selected>=0&&selected<results.Count){var r=results[selected];string target=r.FolderPath??r.FilePath??(r.App!=null&&File.Exists(r.App.IconPath)?r.App.IconPath:null);if(target!=null){EndOverview(()=>{try{Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+target+"\""){UseShellExecute=true});}catch{}});}}return true;}
        if(key==Keys.Tab){if(browsing){if(selected>=0&&selected<results.Count&&results[selected].FolderPath!=null)BrowseInto(results[selected].FolderPath);else if(selected>=0&&selected<results.Count&&results[selected].FilePath!=null)Choose(selected);}else if(selected>=0&&selected<results.Count&&results[selected].FolderPath!=null)BrowseInto(results[selected].FolderPath);return true;}
        if(key==Keys.Back&&browsing&&search.Text.Length==0){BrowseParent();return true;}
        if(key==Keys.Down||key==Keys.Up){if(openingMotion)FinishEntrance();int count=search.Text.Trim().Length>0||browsing?results.Count:windows.Count;selected=count==0?-1:Math.Max(0,Math.Min(count-1,selected+(key==Keys.Down?1:-1)));if(search.Text.Trim().Length>0||browsing){CloseResultMenu();resultNavigationUsed=true;if(selected<resultScroll)resultScroll=selected;else if(selected>=resultScroll+ResultRows())resultScroll=selected-ResultRows()+1;LayoutResults();EnsureResultVisible(selected);}Redraw();return true;}
        if(key==Keys.Enter){if(search.Text.Trim().Length>0&&(results.Count==0||(!resultNavigationUsed&&localFolderPending&&Path.IsPathRooted(FolderSearch.Normalize(SearchQuery.Parse(search.Text).Text))))&&(localFolderPending||indexFolderPending)&&!resultNavigationUsed)openFolderWhenReady=true;else Choose(selected<0?0:selected);return true;}
        return false;
    }
    protected override void OnKeyPress(KeyPressEventArgs e) {
        if(!search.Focused&&!Char.IsControl(e.KeyChar)){if(openingMotion)FinishEntrance();search.Focus();search.SelectedText=e.KeyChar.ToString();e.Handled=true;}
        base.OnKeyPress(e);
    }
    void LabelText(Graphics g,string text,Rectangle box,float size,Color color,bool bold=false) {
        if(box.Width<=0||box.Height<=0||!g.IsVisible(box))return;
        // Opaque local backing avoids color-key/ClearType fringes on bright wallpaper.
        
        g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using(var brush=new SolidBrush(color))using(var format=new StringFormat(StringFormat.GenericTypographic)) {
            format.Trimming=StringTrimming.EllipsisCharacter;format.FormatFlags=StringFormatFlags.NoWrap;
            format.LineAlignment=StringAlignment.Center;
            g.DrawString(text,FontAt(size,bold),brush,box,format);
        }
    }
    void CenteredText(Graphics g,string text,Rectangle box,float size,Color color) {
        using(var brush=new SolidBrush(color))using(var format=new StringFormat(StringFormat.GenericTypographic)) {
            format.Trimming=StringTrimming.EllipsisCharacter;format.FormatFlags=StringFormatFlags.NoWrap;
            format.Alignment=StringAlignment.Center;format.LineAlignment=StringAlignment.Center;
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.DrawString(text,FontAt(size),brush,box,format);
        }
    }
    void Caption(Graphics g,string text,Rectangle box,float size,Color color,bool bold=false) {
        if(box.Width<=0||box.Height<=0)return;
        int width=Math.Min(box.Width,(int)Math.Ceiling(g.MeasureString(text,FontAt(size,bold)).Width)+U(16));
        var panel=new Rectangle(box.Left,box.Top,width,box.Height);FillRounded(g,panel,Math.Min(U(8),box.Height/2),ActivityTheme.Surface);
        LabelText(g,text,new Rectangle(box.Left+U(8),box.Top,Math.Max(1,width-U(16)),box.Height),size,color,bold);
    }
    Icon AppIcon(string path) {
        if(String.IsNullOrWhiteSpace(path))return null;
        Icon cached;if(iconCache.TryGetValue(path,out cached))return cached;
        DateTime retry;if(iconRetryAfter.TryGetValue(path,out retry)&&DateTime.UtcNow<retry)return null;
        if(pendingIcons.Add(path))ThreadPool.QueueUserWorkItem(_=>{
            Icon loaded=SearchIcons.Load(path);
            try{BeginInvoke((Action)(()=>{
                if(shuttingDown||IsDisposed||Disposing){if(loaded!=null)loaded.Dispose();return;}
                pendingIcons.Remove(path);
                if(loaded!=null){iconCache[path]=loaded;iconRetryAfter.Remove(path);}
                else{iconRetryAfter[path]=DateTime.UtcNow.AddSeconds(5);Native.Log("Icon unavailable; retry allowed: "+path);}
                if(Visible)RedrawWindowMetadata();
            }));}
            catch{if(loaded!=null)loaded.Dispose();}
        });
        return null;
    }
    string WindowAppName(WindowItem window) {
        string path=window.ProcessPath;
        var app=apps.FirstOrDefault(a=>!String.IsNullOrWhiteSpace(path)&&String.Equals(a.Target,path,StringComparison.OrdinalIgnoreCase)&&String.IsNullOrWhiteSpace(a.Arguments))
            ??apps.FirstOrDefault(a=>!String.IsNullOrWhiteSpace(path)&&String.Equals(a.Target,path,StringComparison.OrdinalIgnoreCase));
        if(app!=null&&!String.IsNullOrWhiteSpace(app.Name))return app.Name;
        string fallback=String.IsNullOrWhiteSpace(window.ProcessName)?"Application":window.ProcessName;
        if(String.IsNullOrWhiteSpace(path))return fallback;
        string cached;if(windowAppNames.TryGetValue(path,out cached))return cached;
        // Metadata can involve slow storage. Resolve once without delaying Super.
        if(pendingAppNames.Add(path))ThreadPool.QueueUserWorkItem(_=>{
            string name=fallback;
            try{name=SteamAppName(path);if(String.IsNullOrWhiteSpace(name)){var info=FileVersionInfo.GetVersionInfo(path);if(!String.IsNullOrWhiteSpace(info.FileDescription))name=info.FileDescription.Trim();else if(!String.IsNullOrWhiteSpace(info.ProductName))name=info.ProductName.Trim();}}catch{}
            try{BeginInvoke((Action)(()=>{if(shuttingDown||IsDisposed||Disposing)return;pendingAppNames.Remove(path);windowAppNames[path]=name;if(Visible)RedrawWindowMetadata();}));}catch{}
        });
        return fallback;
    }
    internal static string SteamAppName(string executablePath) {
        const string marker="\\steamapps\\common\\";
        int common=executablePath.IndexOf(marker,StringComparison.OrdinalIgnoreCase);if(common<0)return null;
        string steamApps=executablePath.Substring(0,common)+"\\steamapps";
        string relative=executablePath.Substring(common+marker.Length);
        int separator=relative.IndexOf('\\');string installDirectory=separator<0?relative:relative.Substring(0,separator);
        if(String.IsNullOrWhiteSpace(installDirectory))return null;
        Dictionary<string,string> names;
        lock(steamNamesLock)if(!steamNamesByLibrary.TryGetValue(steamApps,out names)) {
            names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            try{foreach(string manifest in Directory.GetFiles(steamApps,"appmanifest_*.acf",SearchOption.TopDirectoryOnly)) {
                string manifestName=null,folder=null;
                foreach(string line in File.ReadLines(manifest)) {
                    Match field=steamField.Match(line);if(!field.Success)continue;
                    if(field.Groups["key"].Value.Equals("name",StringComparison.OrdinalIgnoreCase))manifestName=field.Groups["value"].Value;
                    else if(field.Groups["key"].Value.Equals("installdir",StringComparison.OrdinalIgnoreCase))folder=field.Groups["value"].Value;
                    if(manifestName!=null&&folder!=null)break;
                }
                if(!String.IsNullOrWhiteSpace(folder)&&!String.IsNullOrWhiteSpace(manifestName))names[folder]=manifestName;
            }}catch{}
            steamNamesByLibrary[steamApps]=names;
        }
        string display;return names.TryGetValue(installDirectory,out display)?display:null;
    }
    void DrawWindowHeader(Graphics g,WindowItem window) {
        if(HasSpotlightBadge(window))return;
        string name=WindowAppName(window);int captionWidth=Math.Min(window.Preview.Width,(int)Math.Ceiling(g.MeasureString(name,FontAt(13,true)).Width)+U(48));var box=new Rectangle(window.Preview.Left+(window.Preview.Width-captionWidth)/2,window.Preview.Top-U(34),captionWidth,U(28));
        FillRounded(g,box,U(ActivityTheme.RowRadius),ActivityTheme.Surface);int iconSize=Math.Min(U(22),Math.Max(1,box.Width));
        var iconBox=new Rectangle(box.Left+U(6),box.Top+(box.Height-iconSize)/2,iconSize,iconSize);
        Icon icon=AppIcon(window.ProcessPath);
        if(icon!=null)g.DrawIcon(icon,iconBox);
        else {
            FillRounded(g,iconBox,Math.Max(1,U(5)),ActivityTheme.Selected);
            string initial=WindowAppName(window).Substring(0,1).ToUpperInvariant();
            LabelText(g,initial,iconBox,12,foreground,true);
        }
        int nameLeft=iconBox.Right+U(8),nameWidth=box.Right-nameLeft;
        if(nameWidth>0)LabelText(g,name,new Rectangle(nameLeft,box.Top,Math.Max(1,nameWidth-U(6)),box.Height),13,foreground,true);
        // Window titles remain searchable; avoid detached caption strips below cards.
    }
    static GraphicsPath Rounded(Rectangle bounds,int radius) {
        var path=new GraphicsPath();int d=Math.Max(2,radius*2);path.AddArc(bounds.Left,bounds.Top,d,d,180,90);path.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);path.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);path.CloseFigure();return path;
    }
    void FillRounded(Graphics g,Rectangle bounds,int radius,Color color) {using(var path=Rounded(bounds,radius))using(var brush=new SolidBrush(color))g.FillPath(brush,path);}
    void StrokeRounded(Graphics g,Rectangle bounds,int radius,Color color,float width) {using(var path=Rounded(bounds,radius))using(var pen=new Pen(color,width))g.DrawPath(pen,path);}
    void DrawStar(Graphics g,Rectangle bounds,bool filled,Color color) {
        var points=new PointF[10];float cx=bounds.Left+bounds.Width/2f,cy=bounds.Top+bounds.Height/2f;
        for(int i=0;i<10;i++){double angle=-Math.PI/2+i*Math.PI/5;float r=(i%2==0?bounds.Width*.48f:bounds.Width*.22f);points[i]=new PointF(cx+(float)Math.Cos(angle)*r,cy+(float)Math.Sin(angle)*r);}
        using(var path=new GraphicsPath()){path.AddPolygon(points);if(filled)using(var brush=new SolidBrush(color))g.FillPath(brush,path);else using(var pen=new Pen(color,Math.Max(1,scale)))g.DrawPath(pen,path);}
    }
    int SettingsRowHeight(){return Math.Max(U(28),Math.Min(U(58),(settingsPanel.Height-U(74))/8));}
    void DrawSettings(Graphics g) {
        FillRounded(g,settingsPanel,U(ActivityTheme.PanelRadius),ActivityTheme.Surface);StrokeRounded(g,settingsPanel,U(ActivityTheme.PanelRadius),ActivityTheme.Border,Math.Max(1,scale));
        LabelText(g,"Overview settings",new Rectangle(settingsPanel.Left+U(24),settingsPanel.Top+U(18),settingsPanel.Width-U(48),U(30)),19,foreground,true);
        string[] names={"Animation duration","Windows per row","Reduced motion","Desktop wallpaper","Live game previews","Power menu","Add a portable application","Done"};
        string[] values={state.MotionDuration+" ms","Up to "+state.PreviewColumns,state.ReducedMotion?"On":"Off",previewHost.LiveWallpaper?"Live · no tint":"Original · no tint",state.GameFriendly?"Off":"On","Open power controls","Choose .exe or shortcut","Close settings"};
        for(int i=0;i<names.Length;i++) {
            int height=SettingsRowHeight();var row=new Rectangle(settingsPanel.Left+U(18),settingsPanel.Top+U(62)+i*height,settingsPanel.Width-U(36),height-U(6));
            FillRounded(g,row,U(ActivityTheme.RowRadius),ActivityTheme.Hover);
            LabelText(g,names[i],new Rectangle(row.Left+U(14),row.Top,row.Width-U(180),row.Height),14,foreground,i==0);
            LabelText(g,values[i]+"   ›",new Rectangle(row.Right-U(160),row.Top,U(142),row.Height),13,ActivityTheme.Accent);
        }
        LabelText(g,"Super opens Activities · app:, file:, folder: and window: narrow your search",new Rectangle(settingsPanel.Left+U(12),settingsPanel.Bottom+U(8),settingsPanel.Width-U(24),U(30)),11,ActivityTheme.Muted);
    }
    void DrawResultIcon(Graphics g,ResultItem result) {
        string iconPath=result.App!=null?result.App.IconPath:result.Window!=null?result.Window.ProcessPath:result.FilePath??"";
        Rectangle box=new Rectangle(result.Bounds.Left+U(14),result.Bounds.Top+(result.Bounds.Height-U(32))/2,U(32),U(32));
        if(result.FolderPath!=null){using(var brush=new SolidBrush(ActivityTheme.Warm)){g.FillRectangle(brush,box.Left,box.Top+U(5),U(14),U(7));g.FillRectangle(brush,box.Left,box.Top+U(10),box.Width,U(22));}return;}
        Icon icon=AppIcon(iconPath);
        if(icon!=null){g.DrawIcon(icon,box);return;}
        using(var brush=new SolidBrush(ActivityTheme.Hover))g.FillEllipse(brush,box);
        string initials=String.Concat((result.Label??"?").Split(new[]{' ','-','_'},StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s=>s.Substring(0,1).ToUpperInvariant()));
        LabelText(g,initials,new Rectangle(box.Left,box.Top,box.Width,box.Height),12,foreground,true);
    }
    protected override void OnPaintBackground(PaintEventArgs e) {}
    void EnsurePaintCache() {
        if(chromeCache==null||chromeCache.Width!=ClientSize.Width||chromeCache.Height!=ClientSize.Height) {
            if(chromeCache!=null)chromeCache.Dispose();
            chromeCache=new Bitmap(Math.Max(1,ClientSize.Width),Math.Max(1,ClientSize.Height),System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            if(backgroundCache!=null)backgroundCache.Dispose();
            backgroundCache=new Bitmap(chromeCache.Width,chromeCache.Height,System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            PaintBackgroundCache();
            chromeDirty=true;
        }
        else if(backgroundGlass!=state.Glass)PaintBackgroundCache();
    }
    public void WarmBackground() {
        if(Visible||shuttingDown)return;
        Bounds=Screen.FromPoint(Cursor.Position).Bounds;scale=Math.Max(1,Native.GetDpiForWindow(Handle)/96f);
        LayoutCards();EnsurePaintCache();
        using(var surface=Graphics.FromImage(chromeCache))PaintChrome(surface);
        chromeDirty=false;Native.Log("Paint caches warmed while hidden");WarmSpotlight();LoadApps(true);
    }
    protected override void OnPaint(PaintEventArgs e) {
        var timing=Stopwatch.StartNew();EnsurePaintCache();
        if(chromeDirty&&!MotionActive) {using(var surface=Graphics.FromImage(chromeCache))PaintChrome(surface);chromeDirty=false;}
        Rectangle clip=Rectangle.Intersect(e.ClipRectangle,ClientRectangle);
        if(clip.Width>0&&clip.Height>0) {
            e.Graphics.CompositingMode=CompositingMode.SourceCopy;
            e.Graphics.DrawImage(chromeCache,clip,clip,GraphicsUnit.Pixel);
        }
        paintMs+=timing.Elapsed.TotalMilliseconds;
        if(MotionActive)motionPaintMs+=timing.Elapsed.TotalMilliseconds;
    }
    void PaintChrome(Graphics g) {
        g.SmoothingMode=SmoothingMode.AntiAlias;
        g.CompositingMode=CompositingMode.SourceCopy;
        using(var clear=new SolidBrush(TransparentBackdrop))g.FillRectangle(clear,ClientRectangle);
        g.CompositingMode=CompositingMode.SourceOver;
        if(closing||SpotlightActive)return;
        // DWM owns all preview pixels. GDI only draws the surrounding interface.
        int left=Math.Max(U(70),(ClientSize.Width-U(2680))/2);
        bool showPins=search.Text.Trim().Length==0&&!browsing&&!settingsOpen&&FavoriteItems().Count>0;
        var launcher=new Rectangle(searchBox.Left-U(12),searchBox.Top-U(12),searchBox.Width+U(24),showPins?U(174):U(84));
        if(search.Text.Trim().Length>0||browsing){var rows=results.Where(r=>!r.Bounds.IsEmpty).ToList();int bottom=rows.Count==0?U(260):rows.Max(r=>r.Bounds.Bottom)+U(12);launcher.Height=bottom-launcher.Top;}
        if(spotlightUnavailable||diagnostic){
        FillRounded(g,launcher,U(ActivityTheme.PanelRadius),ActivityTheme.Surface);
        StrokeRounded(g,launcher,U(ActivityTheme.PanelRadius),ActivityTheme.Border,Math.Max(1,scale));
        if(showPins)using(var divider=new Pen(ActivityTheme.Border))g.DrawLine(divider,launcher.Left+U(24),U(120),launcher.Right-U(24),U(120));
        LabelText(g,"⚙",settingsBox,18,ActivityTheme.Muted);
        LabelText(g,"×",closeBox,22,ActivityTheme.Muted);
        // Draw the search symbol directly: font fallback and ellipsis can leave
        // an extra mark beside a glyph squeezed into a narrow text rectangle.
        using(var pen=new Pen(foreground,Math.Max(1.5f,1.6f*scale))) {
            pen.StartCap=pen.EndCap=LineCap.Round;
            g.DrawEllipse(pen,searchBox.Left+U(20),searchBox.Top+U(20),U(13),U(13));
            g.DrawLine(pen,searchBox.Left+U(22),searchBox.Top+U(32),searchBox.Left+U(17),searchBox.Top+U(38));
        }
        if(search.Text.Trim().Length==0)LabelText(g,"Search apps, files, folders and windows…",new Rectangle(searchBox.Left+U(46),searchBox.Top,searchBox.Width-U(150),searchBox.Height),18,ActivityTheme.Muted);
        if(!search.Visible&&search.Text.Trim().Length>0)LabelText(g,search.Text,search.Bounds,19,foreground);
        }
        if(browsing) {
            FillRounded(g,browseBackBox,U(ActivityTheme.ControlRadius),ActivityTheme.Hover);
            LabelText(g,"‹   "+browsePath,new Rectangle(browseBackBox.Left+U(8),browseBackBox.Top,browseBackBox.Width-U(16),browseBackBox.Height),12,ActivityTheme.Text);
        }
        if(search.Text.Trim().Length>0||browsing) {
            string status=browsing?"Folder contents":SearchResultCount(results.Count(r=>r.Action==null))+(localFolderPending||indexFolderPending?" · Searching files…":fileIndexUnavailable?" · File index unavailable":" · Local search");
            Caption(g,status,new Rectangle(searchBox.Left+U(4),browsing?browseBackBox.Bottom+U(3):searchBox.Bottom+U(8),searchBox.Width,U(23)),11,ActivityTheme.Muted);
            for(int i=0;i<results.Count;i++) {
                if(!results[i].SectionBounds.IsEmpty) {
                    LabelText(g,results[i].Section??ResultSection(results[i]),results[i].SectionBounds,10,ActivityTheme.Muted,true);
                }
                if(results[i].Bounds.IsEmpty||!g.IsVisible(results[i].Bounds))continue;
                var r=results[i];FillRounded(g,r.Bounds,U(ActivityTheme.RowRadius),i==selected?ActivityTheme.Selected:i==hover?ActivityTheme.Hover:ActivityTheme.Surface);
                if(i==selected) {
                    StrokeRounded(g,r.Bounds,U(ActivityTheme.RowRadius),ActivityTheme.Accent,Math.Max(1,scale));
                }
                DrawResultIcon(g,r);
                string context=r.FolderPath??r.FilePath??(r.App!=null?(String.IsNullOrWhiteSpace(r.App.Target)?"Installed application":Path.GetFileName(r.App.Target)):r.Action=="calculate"?"Enter to copy result":r.Action=="power-menu"?"Choose a power action":r.Action!=null?"Windows Search":r.Window!=null?WindowAppName(r.Window):null);
                if(context!=null) {
                    LabelText(g,r.Label,new Rectangle(r.Bounds.Left+U(58),r.Bounds.Top+U(5),r.Bounds.Width-U(222),U(24)),15,foreground);
                    LabelText(g,context,new Rectangle(r.Bounds.Left+U(58),r.Bounds.Top+U(29),r.Bounds.Width-U(222),U(22)),11,ActivityTheme.Muted);
                }else LabelText(g,r.Label,new Rectangle(r.Bounds.Left+U(58),r.Bounds.Top+U(4),r.Bounds.Width-U(222),r.Bounds.Height-U(8)),15,foreground);
                if(FavoriteKey(r).Length>0)DrawStar(g,FavoriteToggleBounds(r.Bounds),IsFavorite(r),IsFavorite(r)?ActivityTheme.Favorite:ActivityTheme.Muted);
                LabelText(g,r.Detail,new Rectangle(r.Bounds.Right-U(160),r.Bounds.Top,U(110),r.Bounds.Height),11,ActivityTheme.Muted);
                if(r.Window!=null)DrawWindowClose(g,r.Bounds,r.Window);
            }
            if(results.Count==0)Caption(g,localFolderPending||indexFolderPending?"Searching files…":appsReady?"No matching apps, files or folders":"Indexing applications…",new Rectangle(searchBox.Left,U(210),searchBox.Width,U(40)),14,foreground);
        }else if(settingsOpen){}
        else if(!browsing) {
            var favorites=FavoriteItems();
            if(favorites.Count>0&&(spotlightUnavailable||diagnostic)) {

                for(int i=0;i<favorites.Count;i++) {
                    Rectangle tile=FavoriteBounds(i);
                    DrawResultIcon(g,new ResultItem{App=favorites[i].App,FolderPath=favorites[i].FolderPath,FilePath=favorites[i].FilePath,Label=favorites[i].Label,Bounds=new Rectangle(tile.Left+(tile.Width-U(60))/2,tile.Top+U(4),U(60),U(40))});
                    CenteredText(g,favorites[i].Label,new Rectangle(tile.Left+U(8),tile.Top+U(46),Math.Max(1,tile.Width-U(16)),U(24)),11,foreground);
                }
            }

            for(int i=0;i<windows.Count;i++) {
                var w=windows[i];
                if(w.Minimized&&!diagnostic) {OverviewBadge minimizedBadge;if(previewBadges.TryGetValue(w.Hwnd,out minimizedBadge))minimizedBadge.SetHighlighted(i==selected||i==hover);continue;}
                if(w.Thumbnail==IntPtr.Zero) {
                    FillRounded(g,w.Preview,U(ActivityTheme.PreviewRadius),ActivityTheme.Surface);
                    LabelText(g,w.Game?"Game · live preview off":"Preview unavailable",w.Preview,12,ActivityTheme.Muted);
                }
                if(!openingMotion&&!closing) {
                    DrawWindowHeader(g,w);
                    bool highlighted=i==selected||i==hover||i==hoverClose;
                    // Antialiased strokes on a colour-keyed window leave an
                    // opaque fringe. Native previews own their corner edges;
                    // selection is displayed on the separate alpha label.
                    OverviewBadge badge;if(previewBadges.TryGetValue(w.Hwnd,out badge))badge.SetHighlighted(highlighted);

                    if((highlighted||w.ClosePending)&&!HasSpotlightBadge(w))DrawWindowClose(g,w.Card,w);
                }
            }
        }
        if(settingsOpen)DrawSettings(g);
        if(resultMenuOpen)DrawResultMenu(g);

    }
    void DrawResultMenu(Graphics g) {
        if(resultMenuBounds.IsEmpty)return;
        FillRounded(g,resultMenuBounds,U(ActivityTheme.RowRadius),ActivityTheme.Surface);
        StrokeRounded(g,resultMenuBounds,U(ActivityTheme.RowRadius),ActivityTheme.Border,Math.Max(1,scale));
        for(int i=0;i<resultMenuItems.Count;i++) {
            Rectangle row=resultMenuItems[i].Bounds;
            if(i==resultMenuHover)FillRounded(g,row,U(ActivityTheme.ControlRadius),ActivityTheme.Hover);
            LabelText(g,resultMenuItems[i].Label,new Rectangle(row.Left+U(12),row.Top,row.Width-U(24),row.Height),12,foreground);
        }
    }
    void PaintBackgroundCache() {
        if(backgroundCache==null)return;
        using(var surface=Graphics.FromImage(backgroundCache))surface.Clear(TransparentBackdrop);
        backgroundGlass=state.Glass;chromeDirty=true;
    }
    protected override void Dispose(bool disposing) {
        if(disposing&&!shuttingDown) {
            shuttingDown=true;folderGeneration++;folderReplyId=0;
            StopFocusHandoff();foreach(var timer in new[]{windowWatcher,folderDelay,folderTimeout,appRefreshDelay,focusSettleTimer}){timer.Stop();timer.Dispose();}
            foreach(var watcher in appWatchers)watcher.Dispose();appWatchers.Clear();
            DisposeSpotlight();StopMotion();ReleaseThumbnails();
            Owner=null;previewHost.Dispose();
            if(chromeCache!=null){chromeCache.Dispose();chromeCache=null;}
            if(backgroundCache!=null){backgroundCache.Dispose();backgroundCache=null;}
            foreach(var icon in iconCache.Values)if(icon!=null)icon.Dispose();iconCache.Clear();
        }
        base.Dispose(disposing);
        if(disposing){foreach(var font in fontCache.Values)font.Dispose();fontCache.Clear();}
    }
}

static class Program {
    static string PipeName(){return "ActivitiesLive"+WindowsIdentity.GetCurrent().User.Value.Replace("-","");}
    static void Signal(byte action) {
        try{using(var pipe=new NamedPipeClientStream(".",PipeName(),PipeDirection.Out)){
            pipe.Connect(action==1?1500:600);
            if(action==1){uint pid;bool allowed=Native.GetNamedPipeServerProcessId(pipe.SafePipeHandle,out pid)&&Native.AllowSetForegroundWindow(pid);Native.Log("Foreground permission handoff="+allowed);}
            pipe.WriteByte(action);
        }}catch{}
    }
    [STAThread] static int Main(string[] args) {
        if(args.Contains("--use-windows-search")){SearchMode.SetWindows(true);Signal(3);return 0;}
        if(args.Contains("--use-activities-search")){SearchMode.SetWindows(false);return 0;}
        if(args.Contains("--shutdown")){Signal(3);return 0;}
        if(args.Contains("--power-menu")){DesktopActions.PowerMenu();return 0;}
        if(args.Contains("--refresh-settings")){SettingsCatalog.Refresh(true);while(SettingsCatalog.IsRefreshing)System.Threading.Thread.Sleep(100);return 0;}
        if(args.Contains("--self-test"))return SelfTest();
        if(SearchMode.Windows){if(!args.Contains("--background"))SearchMode.OpenWindows();return 0;}
        string pipeName=PipeName();
        bool created;using(var mutex=new Mutex(true,"Local\\"+pipeName,out created)) {
            if(!created){if(!args.Contains("--background"))Signal(1);return 0;}
            return RunResident(pipeName,args.Contains("--background"));
        }
    }
    // Keep WinForms/Overview types out of the short-lived signaling entry point:
    // JIT-compiling that entry point used to load the full UI stack on every Super.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int SelfTest() {
            var fit=Overview.Fit(new System.Drawing.Size(900,1600),new Rectangle(100,200,320,180));
            if(fit.Height!=180||fit.Width!=101||fit.X!=209)return 1;
            var a=new Rectangle(-2560,0,1280,1440);var b=new Rectangle(100,200,320,180);
            if(Overview.Mix(a,b,0)!=a||Overview.Mix(a,b,1)!=b)return 2;
            if(!Overview.IsGameProcess("cs2","")||!Overview.IsGameProcess("example","D:\\Steam\\steamapps\\common\\Example\\game.exe")||Overview.IsGameProcess("brave","C:\\Brave\\brave.exe"))return 3;
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test.txt"),"Passed DPI-independent rectangle fitting, transition endpoints and game-process classification checks.");return 0;
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int RunResident(string pipeName,bool background) {
            // Shell launches without an explicit "Start in" directory inherit
            // our process directory. Never pass along a caller's transient
            // project/build folder. Shell shortcuts retain their own Start in.
            string launchDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if(!String.IsNullOrWhiteSpace(launchDirectory)&&Directory.Exists(launchDirectory)) {
                Directory.SetCurrentDirectory(launchDirectory);
                Native.Log("Default launch directory="+launchDirectory);
            }
            Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var form=new Overview();IntPtr handle=form.Handle;
            using(var superKey=new SuperKeyDispatcher(handle)) {
            var server=new Thread(()=> {while(true)try {using(var pipe=new NamedPipeServerStream(pipeName,PipeDirection.In,1)){pipe.WaitForConnection();int action=pipe.ReadByte();bool transition=action==1&&form.TransitionActive;int closeSequence=form.CloseSequence;Native.Log("Pipe action received="+action+", transition-at-receipt="+transition+", close-sequence="+closeSequence);if(action==1){if(transition)Native.Log("Queued Super toggle discarded because it arrived during a transition");else form.BeginInvoke((Action)(()=>{bool current=Overview.IsToggleSignalCurrent(transition,closeSequence,form.TransitionActive,form.CloseSequence);if(!current){Native.Log("Queued Super toggle discarded because the overview state changed before dispatch");return;}form.Toggle();}));}else if(action==2)form.BeginInvoke((Action)form.Abort);else if(action==3)form.BeginInvoke((Action)(()=>{form.Abort();Application.ExitThread();}));}}catch{Thread.Sleep(200);}});
            server.IsBackground=true;server.Start();
            Application.ThreadException+=(s,e)=> {Native.Log("UI error: "+e.Exception);form.Abort();};
            if(background)form.BeginInvoke((Action)form.WarmBackground);else form.BeginInvoke((Action)form.Toggle);
            Application.Run();return 0;
            }
    }
}
