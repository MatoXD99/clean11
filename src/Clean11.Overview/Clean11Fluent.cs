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
    internal static void Quiet(Ui.Button button) {
        button.Appearance=Ui.ControlAppearance.Transparent;button.Foreground=ActivityTheme.Brush("Muted","#A6ABB5");button.BorderThickness=new Thickness(0);button.Background=Brushes.Transparent;
        button.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='Chrome' Background='Transparent' CornerRadius='10' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Chrome' Property='Background' Value='#14FFFFFF'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Chrome' Property='Background' Value='#22FFFFFF'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Chrome' Property='Background' Value='#22FFFFFF'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        // IconButton normally uses the library's Icon slot; retain it in this simple template.
        if(button.Icon!=null){var symbol=button.Icon as Ui.SymbolIcon;string name=symbol==null?"":symbol.Symbol.ToString();if(name.StartsWith("Dismiss")||name.StartsWith("Settings"))button.Content=new TextBlock{Text=name.StartsWith("Dismiss")?"\uE711":"\uE713",FontFamily=new FontFamily("Segoe Fluent Icons"),FontSize=14,Foreground=ActivityTheme.Brush("Text","#F1F2F5")};else{button.Icon.Foreground=ActivityTheme.Brush("Muted","#A6ABB5");button.Content=button.Icon;}}
    }
    internal static Border Card(UIElement content) {
        return new Border{Child=content,Background=ActivityTheme.Brush("Card","#303030"),BorderBrush=ActivityTheme.Brush("Divider","#18FFFFFF"),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(ActivityTheme.RowRadius),Padding=new Thickness(16),Margin=new Thickness(0,0,0,6)};
    }
    internal static void Menu(ContextMenu menu) {
        Apply(menu);menu.FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");menu.FontSize=14;
        // Keep the library's complete keyboard, submenu, checkmark and icon template.
        var surface=new SolidColorBrush(FrostedMaterial.Tone);var ink=new SolidColorBrush(FrostedMaterial.Ink(false));
        menu.Resources["ApplicationBackgroundBrush"]=surface;
        menu.Resources["TextFillColorPrimaryBrush"]=ink;
        menu.Background=surface;menu.Foreground=ink;menu.BorderBrush=FrostedMaterial.Edge();
    }
}
