using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

sealed class DockSettings : Window {
    internal DockSettings(DockWindow dock){
        Clean11Fluent.Apply(this);Title="Clean11 Settings";Width=760;Height=740;MinWidth=620;MinHeight=520;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=ActivityTheme.Brush("Surface","#202020");Foreground=ActivityTheme.Brush("Text","#F5F5F5");
        var panel=new StackPanel{Margin=new Thickness(24)};var tabs=new TabControl{Margin=new Thickness(12),Background=Background,Foreground=Foreground};
        Content=tabs;
        tabs.Items.Add(new TabItem{Header="Overview & search",Content=OverviewPage(dock)});
        tabs.Items.Add(new TabItem{Header="Dock",Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});
        var state=dock.State;
        panel.Children.Add(new TextBlock{Text="Dock",FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18)});
        var size=new Slider{Minimum=28,Maximum=64,Value=state.IconSize,TickFrequency=2,IsSnapToTickEnabled=true};Row(panel,"Icon size",size);
        var magnify=new Slider{Minimum=1,Maximum=1.7,Value=state.Magnification,TickFrequency=.05,IsSnapToTickEnabled=true};Row(panel,"Magnification",magnify);
        var previewColumns=new ComboBox{ItemsSource=new[]{1,2,3,4,5},SelectedItem=state.PreviewColumns};Row(panel,"Window previews per row (maximum)",previewColumns);
        var visibility=new ComboBox{ItemsSource=new[]{"Never hide","Intelligent","Auto-hide"},SelectedItem=state.Visibility=="Always"?"Never hide":state.Visibility};Row(panel,"Visibility",visibility);
        var fullscreen=Toggle(panel,"Hide in fullscreen apps and games",state.HideInFullscreen);
        panel.Children.Add(new TextBlock{Text="When on, fullscreen overrides the visibility mode. When off, your chosen mode still applies: auto-hide and intelligent hiding can still hide the dock.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,8),Foreground=ActivityTheme.Brush("Muted","#A6ABB5")});
        var monitors=new ComboBox{ItemsSource=Forms.Screen.AllScreens.Select(s=>s.DeviceName).ToArray(),SelectedItem=dock.Screen.DeviceName};Row(panel,"Display",monitors);
        var motion=Toggle(panel,"Reduce motion",state.ReducedMotion);var downloads=Toggle(panel,"Downloads stack",state.ShowDownloads);var recycle=Toggle(panel,"Recycle Bin",state.ShowRecycleBin);var tray=Toggle(panel,"Background apps button",state.ShowBackgroundApps);var takeover=Toggle(panel,"Replace the existing bottom bar while running",state.TakeOver);var startup=Toggle(panel,"Start with Windows",state.Startup);
        panel.Children.Add(Clean11Fluent.Button("Pin an app or file…",()=>{var dialog=new OpenFileDialog{Title="Add to Clean11 Dock",Filter="Apps and shortcuts|*.exe;*.lnk|All files|*.*"};if(dialog.ShowDialog(this)==true){state.Pins.Add(DockState.FromPath(dialog.FileName));dock.ApplySettings();}},false));
        panel.Children.Add(Clean11Fluent.Button("Pin a folder…",()=>{using(var dialog=new Forms.FolderBrowserDialog()){if(dialog.ShowDialog()==Forms.DialogResult.OK){state.Pins.Add(DockState.FromPath(dialog.SelectedPath));dock.ApplySettings();}}},false));
        var apply=Clean11Fluent.Button("Apply",()=>{state.PreviewColumns=(int)previewColumns.SelectedItem;state.IconSize=(int)size.Value;state.Magnification=magnify.Value;state.Visibility=(string)visibility.SelectedItem=="Never hide"?"Always":(string)visibility.SelectedItem;state.HideInFullscreen=fullscreen.IsChecked==true;state.Monitor=(string)monitors.SelectedItem;state.ReducedMotion=motion.IsChecked==true;state.ShowDownloads=downloads.IsChecked==true;state.ShowRecycleBin=recycle.IsChecked==true;state.ShowBackgroundApps=tray.IsChecked==true;state.TakeOver=takeover.IsChecked==true;state.Startup=startup.IsChecked==true;try{SetStartup(state.Startup);dock.ApplySettings();Close();}catch{dock.Notify("Could not save dock settings. Check access to your local settings folder.");}},true);apply.Margin=new Thickness(0,18,0,0);panel.Children.Add(apply);
        panel.Children.Add(new TextBlock{Text="Drag pins to reorder. Right-click an app for window actions and pin options. Exit Clean11 Dock to restore the previous bars.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,0),Foreground=ActivityTheme.Brush("Muted","#B8B8B8")});
    }
    FrameworkElement OverviewPage(DockWindow dock){
        var panel=new StackPanel{Margin=new Thickness(24)};
        panel.Children.Add(new TextBlock{Text="Overview & search",FontSize=24,FontWeight=FontWeights.SemiBold});
        var prefs=OverviewPreferences.Load();
        var duration=new Slider{Minimum=120,Maximum=600,Value=prefs.MotionDuration,TickFrequency=20,IsSnapToTickEnabled=true};Row(panel,"Animation duration (milliseconds)",duration);
        var columns=new Slider{Minimum=3,Maximum=5,Value=prefs.PreviewColumns,TickFrequency=1,IsSnapToTickEnabled=true};Row(panel,"Windows per row",columns);
        var reduced=Toggle(panel,"Reduce motion",prefs.ReducedMotion);
        var glass=Toggle(panel,"Frosted glass · search, dock & previews",prefs.Glass);
        var material=new ComboBox{ItemsSource=new[]{"Midnight","Pearl"},SelectedItem=prefs.GlassStyle};Row(panel,"Clean11 glass · Midnight / Pearl",material);
        panel.Children.Add(new TextBlock{Text="Midnight is cool smoked glass. Pearl is a luminous frost with dark text. Both keep a gentle tint for readability.",TextWrapping=TextWrapping.Wrap,Foreground=ActivityTheme.Brush("Muted","#A6ABB5"),Margin=new Thickness(0,0,0,8)});
        var strength=new Slider{Minimum=0,Maximum=100,Value=prefs.GlassStrength,TickFrequency=5,IsSnapToTickEnabled=true};Row(panel,"Glass visibility · dock, search & previews",strength);
        var strengthValue=new TextBlock{Text=prefs.GlassStrength+"%",Foreground=ActivityTheme.Brush("Muted","#A6ABB5")};panel.Children.Add(strengthValue);
        strength.ValueChanged+=(s,e)=>strengthValue.Text=((int)strength.Value)+"%";
        strength.IsEnabled=prefs.Glass;glass.Checked+=(s,e)=>strength.IsEnabled=true;glass.Unchecked+=(s,e)=>strength.IsEnabled=false;
        var radius=new Slider{Minimum=0,Maximum=40,Value=prefs.BlurRadius,TickFrequency=1,IsSnapToTickEnabled=true};Row(panel,"Blur radius · sharp to frosted",radius);
        var radiusValue=new TextBlock{Text=prefs.BlurRadius+" px",Foreground=ActivityTheme.Brush("Muted","#A6ABB5")};panel.Children.Add(radiusValue);
        radius.ValueChanged+=(s,e)=>radiusValue.Text=((int)radius.Value)+" px";
        if(!DockBackdrop.AdjustableBlurAvailable)panel.Children.Add(new TextBlock{Text="Dock is using the system blur fallback; radius applies to search and previews only.",TextWrapping=TextWrapping.Wrap,Foreground=ActivityTheme.Brush("Muted","#A6ABB5")});
        radius.IsEnabled=prefs.Glass;glass.Checked+=(s,e)=>radius.IsEnabled=true;glass.Unchecked+=(s,e)=>radius.IsEnabled=false;
        var games=Toggle(panel,"Live game previews",prefs.LiveGamePreviews);
        panel.Children.Add(new TextBlock{Text="Show live game content in the overview. Turn off to reduce GPU work while gaming.",TextWrapping=TextWrapping.Wrap,Foreground=ActivityTheme.Brush("Muted","#A6ABB5"),Margin=new Thickness(0,4,0,18)});
        var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)};
        panel.Children.Add(Clean11Fluent.Button("Save settings",()=>{try{prefs.MotionDuration=(int)duration.Value;prefs.PreviewColumns=(int)columns.Value;prefs.ReducedMotion=reduced.IsChecked==true;prefs.Glass=glass.IsChecked==true;prefs.LiveGamePreviews=games.IsChecked==true;prefs.GlassStyle=(string)material.SelectedItem;prefs.GlassStrength=(int)strength.Value;prefs.BlurRadius=(int)radius.Value;prefs.Save();dock.CloseFlyout();dock.RefreshGlass();status.Text="Saved. Dock updated; search and previews update on opening.";}catch{status.Text="Could not save settings. Your changes have not been applied.";}},true));
        panel.Children.Add(status);
        panel.Children.Add(Clean11Fluent.Button("Add a portable application…",()=>{var dialog=new OpenFileDialog{Filter="Applications and shortcuts|*.exe;*.lnk"};if(dialog.ShowDialog(this)==true){try{OverviewPreferences.AddPortable(dialog.FileName);status.Text="Application added to search.";}catch{status.Text="Could not add application.";}}},false));
        panel.Children.Add(Clean11Fluent.Button("Refresh Windows Settings index",()=>{try{OverviewPreferences.RunOverview("--refresh-settings");status.Text="Index refresh requested.";}catch{status.Text="Could not find the overview application.";}},false));
        panel.Children.Add(Clean11Fluent.Button("Power controls",()=>OverviewPreferences.RunOverview("--power-menu"),false));
        panel.Children.Add(new TextBlock{Text="Clean11 · Interface powered by WPF UI",Foreground=ActivityTheme.Brush("Muted","#A6ABB5"),Margin=new Thickness(0,24,0,8)});
        panel.Children.Add(Clean11Fluent.Button("Third-party licenses",()=>System.Diagnostics.Process.Start(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"licenses")),false));
        return new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    }
    static void Row(Panel panel,string label,FrameworkElement control){panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,8,0,6)});control.Margin=new Thickness(0,0,0,8);panel.Children.Add(control);}
    static Wpf.Ui.Controls.ToggleSwitch Toggle(Panel panel,string label,bool value){
        var row=new DockPanel{Margin=new Thickness(14,10,14,10)};
        var box=new Wpf.Ui.Controls.ToggleSwitch{IsChecked=value,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(box,Dock.Right);row.Children.Add(box);
        row.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center,FontSize=14,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,16,0)});
        panel.Children.Add(new System.Windows.Controls.Border{Background=ActivityTheme.Brush("Card","#2B2E34"),CornerRadius=new CornerRadius(ActivityTheme.RowRadius),Margin=new Thickness(0,4,0,4),Child=row});return box;
    }
    internal static void SetStartup(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(enabled)key.SetValue("Clean11Dock","\""+DockProgram.Executable+"\"");else key.DeleteValue("Clean11Dock",false);}}
}
