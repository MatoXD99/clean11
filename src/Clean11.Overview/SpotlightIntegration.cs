using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

partial class Overview {
    SpotlightPanel spotlight;
    bool returningFromSpotlight;
    bool spotlightChromeCleared;
    bool spotlightUnavailable;
    Action settingsCatalogChanged;
    readonly Timer settingsSearchDelay=new Timer{Interval=65};
    ResultItem[] settingMatches=new ResultItem[0];
    string settingMatchQuery="";
    int settingSearchGeneration;
    bool settingsPending;
    internal bool SpotlightActive {get{return spotlight!=null&&spotlight.IsVisible&&spotlight.Expanded;}}
    bool SpotlightHeaderVisible {get{return spotlight!=null&&spotlight.IsVisible;}}
    bool SpotlightOwnsForeground(IntPtr foreground){return spotlight!=null&&spotlight.OwnsForeground(foreground);}
    bool FocusSpotlightOverview(){
        if(diagnostic||spotlightUnavailable||closing||shuttingDown||!Visible)return false;
        try{if(spotlight==null)spotlight=new SpotlightPanel(this);if(!SpotlightActive&&search.Text.Length==0&&!browsing)spotlight.ShowOverview(SpotlightOrigin);spotlight.FocusInput();search.Visible=false;searchHint.Visible=false;return true;}
        catch(Exception error){spotlightUnavailable=true;Native.Log("Spotlight header unavailable; native input retained: "+error);return false;}
    }
    internal int SpotlightMotion {get{return state.MotionDuration;}}
    internal int SpotlightColumns {get{return state.PreviewColumns;}}
    internal bool SpotlightReduced {get{return reducedMotion;}}
    internal bool SpotlightGlass {get{return state.Glass;}}
    internal bool SpotlightGameFriendly {get{return state.GameFriendly;}}
    Rectangle SpotlightOrigin {get{var bounds=searchBox;bounds.Inflate(U(12),U(12));int center=bounds.Left+bounds.Width/2;bounds.Width=U(720);bounds.X=center-bounds.Width/2;bounds.Height=U(FavoriteItems().Count>0?124:64);bounds.Offset(Left,Top);return bounds;}}
    internal Rectangle SpotlightOverviewBounds {get{return SpotlightOrigin;}}
    internal List<ResultItem> SpotlightFavorites(){return FavoriteItems();}
    internal void OpenSpotlightFavorite(int index){OpenFavorite(index);}
    internal void UnpinSpotlightFavorite(int index){var items=FavoriteItems();if(index>=0&&index<items.Count)ToggleFavorite(items[index]);}
    internal void RenameSpotlightFavorite(int index,string text){var items=FavoriteItems();if(index<0||index>=items.Count)return;string name=text.Trim();if(name.Length==0)state.FavoriteNames.Remove(FavoriteKey(items[index]));else state.FavoriteNames[FavoriteKey(items[index])]=name;SaveState();Redraw();}
    void InitializeSpotlightCatalog(){
        if(diagnostic)return;
        settingsSearchDelay.Tick+=(s,e)=>StartSettingsSearch();
        settingsCatalogChanged=()=>{try{if(IsHandleCreated&&!shuttingDown)BeginInvoke((Action)(()=>{if(shuttingDown)return;if(spotlight!=null)spotlight.RefreshCatalogStatus();if(Visible&&search.Text.Length>0)QueueSettingsSearch();}));}catch{}};
        SettingsCatalog.Changed+=settingsCatalogChanged;SettingsCatalog.Initialize();
    }
    void QueueSettingsSearch(){
        settingsSearchDelay.Stop();settingSearchGeneration++;settingMatches=new ResultItem[0];settingMatchQuery=search.Text;settingsPending=false;
        var query=SearchQuery.Parse(search.Text);if(!diagnostic&&query.Includes("settings")&&query.Text.Length>0){settingsPending=true;settingsSearchDelay.Start();}
    }
    void StartSettingsSearch(){
        settingsSearchDelay.Stop();int generation=settingSearchGeneration;string input=search.Text,query=SearchQuery.Parse(input).Text;
        System.Threading.ThreadPool.QueueUserWorkItem(_=>{
            var matches=SettingsCatalog.Search(query).ToArray();
            try{BeginInvoke((Action)(()=>{if(shuttingDown||generation!=settingSearchGeneration||input!=search.Text)return;settingMatches=matches;settingMatchQuery=input;settingsPending=false;RebuildResults(resultNavigationUsed);Redraw();}));}catch{}
        });
    }
    void SyncSpotlightQuery(){
        if(diagnostic||!Visible||closing||shuttingDown||settingsOpen||spotlightUnavailable||search.IsImeComposing)return;
        bool searching=search.Text.Trim().Length>0||browsing;
        if(searching){
            returningFromSpotlight=false;StopFocusHandoff();
            Cursor=Cursors.Default;
            if(spotlight==null)spotlight=new SpotlightPanel(this);
            search.Visible=false;searchHint.Visible=false;
            spotlight.ShowSearch(search.Text,SpotlightOrigin,reducedMotion);SyncSpotlightResults();
        }else if(SpotlightActive&&!returningFromSpotlight){
            returningFromSpotlight=true;
            spotlight.ReturnToOverview(SpotlightOrigin,reducedMotion,()=>{returningFromSpotlight=false;if(Visible&&!closing){LayoutCards();UpdateThumbnails(1);Redraw();spotlight.FocusInput();}});
        }
    }
    void WarmSpotlight(){if(diagnostic||spotlightUnavailable)return;try{if(spotlight==null)spotlight=new SpotlightPanel(this);spotlight.WarmHidden();}catch(Exception error){spotlightUnavailable=true;Native.Log("Spotlight unavailable; native search retained: "+error);}}
    void SyncSpotlightResults(){
        if(SpotlightHeaderVisible&&!spotlight.Expanded)spotlight.RefreshOverview();
        if(!SpotlightActive||settingsOpen||returningFromSpotlight)return;
        string status=results.Count==0?(localFolderPending||indexFolderPending?"Searching…":"No results"):SearchResultCount(results.Count);
        if(localFolderPending||indexFolderPending)status+=" · Searching files…";
        else if(fileIndexUnavailable)status+=" · File index unavailable";
        if(settingsPending)status+=" · Searching settings…";
        spotlight.UpdateResults(results,selected,status);
    }
    internal void SetSpotlightQuery(string query){if(!closing&&!shuttingDown&&search.Text!=query)search.Text=query;}
    internal void SetSpotlightSelection(int index){if(index>=0&&index<results.Count){selected=index;resultNavigationUsed=true;}}
    internal void LaunchSpotlight(int index){if(index>=0&&index<results.Count){selected=index;ChooseResult(results[index]);}}
    internal bool HandleSpotlightShortcut(Keys key){return HandleSearchCommand(key);}
    internal string SpotlightKey(ResultItem item){return ResultKey(item);}
    internal Icon SpotlightIcon(ResultItem item){
        if(item.Setting!=null)return AppIcon(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"ImmersiveControlPanel\SystemSettings.exe"));
        if(item.FolderPath!=null)return AppIcon(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"));
        return AppIcon(item.App!=null?item.App.IconPath:item.Window!=null?item.Window.ProcessPath:item.FilePath??"");
    }
    internal void CloseSpotlight(){EndOverview(null);}
    internal void DismissSpotlightFocusLoss(){EndOverview(()=>{});}
    internal void SpotlightEscape(){if(settingsOpen){ExitSpotlightSettings();return;}if(search.Text.Length>0){search.Clear();LayoutCards();UpdateThumbnails(1);}else if(browsing)BrowseParent();else EndOverview(null);}
    internal void OpenSpotlightSettings(){
        if(diagnostic)return;
        string unified=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Clean11","Dock","Clean11.Settings.exe");
        if(System.IO.File.Exists(unified)){EndOverview(()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(unified){UseShellExecute=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}));return;}
        if(spotlightUnavailable){settingsOpen=true;UpdateThumbnails(1);Redraw();return;}
        StopFocusHandoff();if(openingMotion)FinishEntrance(false);settingsOpen=true;returningFromSpotlight=false;
        if(spotlight==null)spotlight=new SpotlightPanel(this);search.Visible=false;searchHint.Visible=false;
        UpdateThumbnails(1);spotlight.ShowSettings(SpotlightOrigin,reducedMotion);Redraw();
    }
    internal void ExitSpotlightSettings(){settingsOpen=false;SyncSpotlightQuery();UpdateThumbnails(1);Redraw();}
    internal void SetSpotlightPreference(string key,int value){
        if(key=="motion")state.MotionDuration=Math.Max(120,Math.Min(600,value));
        if(key=="columns")state.PreviewColumns=Math.Max(3,Math.Min(5,value));
        if(key=="reduced")state.ReducedMotion=value!=0;
        if(key=="glass")state.Glass=value!=0;
        if(key=="game")state.GameFriendly=value!=0;
        new OverviewPreferences{GlassStyle=OverviewPreferences.Load().GlassStyle,BlurRadius=OverviewPreferences.Load().BlurRadius,GlassStrength=OverviewPreferences.Load().GlassStrength,MotionDuration=state.MotionDuration,PreviewColumns=state.PreviewColumns,ReducedMotion=state.ReducedMotion,Glass=state.Glass,LiveGamePreviews=!state.GameFriendly}.Save();
        reducedMotion=state.ReducedMotion||!SystemInformation.UIEffectsEnabled;SaveState();LayoutCards();UpdateThumbnails(1);Redraw();
    }
    internal void SpotlightPower(){EndOverview(DesktopActions.PowerMenu);}
    internal void SpotlightAddPortable(string path){AppCatalog.AddPortable(path);LoadApps(true);}
    internal IEnumerable<string> SpotlightCommands(int index){
        if(index<0||index>=results.Count)return new string[0];var item=results[index];var commands=new List<string>{"Open"};
        if(FavoriteKey(item).Length>0)commands.Add(IsFavorite(item)?"Unpin":"Pin to favorites");
        if(item.App!=null||item.FilePath!=null||item.FolderPath!=null)commands.Add("Open file location");
        if(ContextTarget(item)!=null)commands.Add("Copy path");if(CanRunElevated(item))commands.Add("Run as administrator");return commands;
    }
    internal void InvokeSpotlightCommand(int index,string command){
        if(index<0||index>=results.Count)return;
        if(command=="Open"){LaunchSpotlight(index);return;}
        string action=command=="Unpin"||command=="Pin to favorites"?"pin":command=="Open file location"?"location":command=="Copy path"?"copy":"admin";
        ShowResultMenu(index,Point.Empty);int found=resultMenuItems.FindIndex(item=>item.Command==action);if(found>=0)InvokeResultMenu(found);else CloseResultMenu();SyncSpotlightResults();
    }
    void HideSpotlight(){returningFromSpotlight=false;spotlightChromeCleared=false;settingsSearchDelay.Stop();settingSearchGeneration++;settingsPending=false;if(spotlight!=null)spotlight.HidePanel();}
    void DisposeSpotlight(){settingsSearchDelay.Stop();settingsSearchDelay.Dispose();if(settingsCatalogChanged!=null)SettingsCatalog.Changed-=settingsCatalogChanged;if(spotlight!=null){spotlight.Shutdown();spotlight=null;}}
}
