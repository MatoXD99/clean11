using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ui = Wpf.Ui.Controls;

// One library resource tree for every WPF surface, including detached popups.
// Native preview windows share ActivityTheme's palette and geometry.
static class Clean11Fluent {
    static bool ready;
    internal static void Ensure() {
        if(ready)return;
        if(Application.Current==null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        Application.Current.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary{Theme=Wpf.Ui.Appearance.ApplicationTheme.Dark});
        Application.Current.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        Application.Current.Resources.MergedDictionaries.Add(ActivityTheme.Resources());
        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(ActivityTheme.Brush("Accent","#60CDFF").Color,Wpf.Ui.Appearance.ApplicationTheme.Dark,false,false);
        Application.Current.Resources["ControlCornerRadius"]=new CornerRadius(ActivityTheme.ControlRadius);
        ready=true;
    }
    internal static void Apply(FrameworkElement element) {
        Ensure();
        element.Resources.MergedDictionaries.Add(ActivityTheme.Resources());
        element.UseLayoutRounding=true;
        element.SnapsToDevicePixels=true;
    }
    internal static Ui.SymbolIcon Icon(string symbol,double size) {
        Ui.SymbolRegular value;
        if(!Enum.TryParse<Ui.SymbolRegular>(symbol,out value))value=Ui.SymbolRegular.Search24;
        return new Ui.SymbolIcon{Symbol=value,FontSize=size,VerticalAlignment=VerticalAlignment.Center};
    }
    internal static Ui.Button Button(string label,Action action,bool primary) {
        var button=new Ui.Button{Content=label,Appearance=primary?Ui.ControlAppearance.Primary:Ui.ControlAppearance.Secondary,
            CornerRadius=new CornerRadius(ActivityTheme.ControlRadius),Padding=new Thickness(14,8,14,8),MinHeight=34,FontSize=14};
        button.Click+=(s,e)=>action();
        return button;
    }
    internal static Ui.Button IconButton(string label,string symbol,Action action) {
        var button=Button("",action,false);button.Appearance=Ui.ControlAppearance.Transparent;
        button.Icon=Icon(symbol,18);button.Width=34;button.Height=34;button.Padding=new Thickness(7);
        button.ToolTip=label;System.Windows.Automation.AutomationProperties.SetName(button,label);
        return button;
    }
    internal static Border Card(UIElement content) {
        return new Border{Child=content,Background=ActivityTheme.Brush("Card","#303030"),BorderBrush=ActivityTheme.Brush("Divider","#18FFFFFF"),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(ActivityTheme.RowRadius),Padding=new Thickness(16),Margin=new Thickness(0,0,0,6)};
    }
    internal static void Menu(ContextMenu menu) {
        Apply(menu);menu.FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");menu.FontSize=14;
        // Keep the library's complete keyboard, submenu, checkmark and icon template.
        menu.SetResourceReference(Control.BackgroundProperty,"ApplicationBackgroundBrush");
        menu.SetResourceReference(Control.ForegroundProperty,"TextFillColorPrimaryBrush");
    }
}
