using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

sealed class DockSettings : Window {
    internal DockSettings(DockWindow dock){
        Clean11Fluent.Apply(this);Title="Clean11 · Dock settings";Width=540;Height=690;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=ActivityTheme.Brush("Surface","#202020");Foreground=ActivityTheme.Brush("Text","#F5F5F5");
        var panel=new StackPanel{Margin=new Thickness(24)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};var state=dock.State;
        panel.Children.Add(new TextBlock{Text="Make the dock yours",FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18)});
        var size=new Slider{Minimum=28,Maximum=64,Value=state.IconSize,TickFrequency=2,IsSnapToTickEnabled=true};Row(panel,"Icon size",size);
        var magnify=new Slider{Minimum=1,Maximum=1.7,Value=state.Magnification,TickFrequency=.05,IsSnapToTickEnabled=true};Row(panel,"Magnification",magnify);
        var visibility=new ComboBox{ItemsSource=new[]{"Always","Intelligent","Auto-hide"},SelectedItem=state.Visibility};Row(panel,"Visibility",visibility);
        var monitors=new ComboBox{ItemsSource=Forms.Screen.AllScreens.Select(s=>s.DeviceName).ToArray(),SelectedItem=dock.Screen.DeviceName};Row(panel,"Display",monitors);
        var motion=Toggle(panel,"Reduce motion",state.ReducedMotion);var downloads=Toggle(panel,"Downloads stack",state.ShowDownloads);var recycle=Toggle(panel,"Recycle Bin",state.ShowRecycleBin);var tray=Toggle(panel,"Background apps button",state.ShowBackgroundApps);var takeover=Toggle(panel,"Replace the existing bottom bar while running",state.TakeOver);var startup=Toggle(panel,"Start with Windows",state.Startup);
        panel.Children.Add(Clean11Fluent.Button("Pin an app or file…",()=>{var dialog=new OpenFileDialog{Title="Add to Clean11 Dock",Filter="Apps and shortcuts|*.exe;*.lnk|All files|*.*"};if(dialog.ShowDialog(this)==true){state.Pins.Add(DockState.FromPath(dialog.FileName));dock.ApplySettings();}},false));
        panel.Children.Add(Clean11Fluent.Button("Pin a folder…",()=>{using(var dialog=new Forms.FolderBrowserDialog()){if(dialog.ShowDialog()==Forms.DialogResult.OK){state.Pins.Add(DockState.FromPath(dialog.SelectedPath));dock.ApplySettings();}}},false));
        var apply=Clean11Fluent.Button("Apply",()=>{state.IconSize=(int)size.Value;state.Magnification=magnify.Value;state.Visibility=(string)visibility.SelectedItem;state.Monitor=(string)monitors.SelectedItem;state.ReducedMotion=motion.IsChecked==true;state.ShowDownloads=downloads.IsChecked==true;state.ShowRecycleBin=recycle.IsChecked==true;state.ShowBackgroundApps=tray.IsChecked==true;state.TakeOver=takeover.IsChecked==true;state.Startup=startup.IsChecked==true;try{SetStartup(state.Startup);dock.ApplySettings();Close();}catch{dock.Notify("Could not save dock settings. Check access to your local settings folder.");}},true);apply.Margin=new Thickness(0,18,0,0);panel.Children.Add(apply);
        panel.Children.Add(new TextBlock{Text="Drag pins to reorder. Right-click an app for window actions and pin options. Exit Clean11 Dock to restore the previous bars.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,0),Foreground=ActivityTheme.Brush("Muted","#B8B8B8")});
    }
    static void Row(Panel panel,string label,FrameworkElement control){panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,8,0,6)});control.Margin=new Thickness(0,0,0,8);panel.Children.Add(control);}
    static CheckBox Toggle(Panel panel,string label,bool value){var box=new CheckBox{Content=label,IsChecked=value,Margin=new Thickness(0,8,0,8)};panel.Children.Add(box);return box;}
    internal static void SetStartup(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(enabled)key.SetValue("Clean11Dock","\""+DockProgram.Executable+"\"");else key.DeleteValue("Clean11Dock",false);}}
}
