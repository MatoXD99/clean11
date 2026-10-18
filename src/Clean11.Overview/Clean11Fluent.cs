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
        RefreshPalette(element);
        element.UseLayoutRounding=true;
        element.SnapsToDevicePixels=true;
    }
    internal static void RefreshPalette(FrameworkElement element){
        for(int i=element.Resources.MergedDictionaries.Count-1;i>=0;i--)if(element.Resources.MergedDictionaries[i] is Wpf.Ui.Markup.ThemesDictionary)element.Resources.MergedDictionaries.RemoveAt(i);
        element.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary{Theme=FrostedMaterial.Light?Wpf.Ui.Appearance.ApplicationTheme.Light:Wpf.Ui.Appearance.ApplicationTheme.Dark});
        var dictionary=ActivityTheme.Resources();foreach(object key in dictionary.Keys)element.Resources[key]=dictionary[key];
        element.Resources["TextFillColorPrimaryBrush"]=dictionary["Text"];
        element.Resources["TextFillColorSecondaryBrush"]=dictionary["Muted"];
        element.Resources["ControlFillColorDefaultBrush"]=dictionary["Card"];
        element.Resources["ControlFillColorSecondaryBrush"]=dictionary["Hover"];
        element.Resources["ControlFillColorTertiaryBrush"]=dictionary["Selected"];
        element.Resources["ControlStrokeColorDefaultBrush"]=dictionary["Divider"];
        var accent=FrostedMaterial.Light?new SolidColorBrush(Color.FromRgb(20,105,164)):ActivityTheme.Brush("Accent","#60CDFF");
        element.Resources["AccentFillColorDefaultBrush"]=accent;element.Resources["AccentFillColorSecondaryBrush"]=accent;
        element.Resources["TextOnAccentFillColorPrimaryBrush"]=FrostedMaterial.Light?Brushes.White:Brushes.Black;
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
        button.Appearance=Ui.ControlAppearance.Transparent;button.SetResourceReference(Control.ForegroundProperty,"Muted");button.BorderThickness=new Thickness(0);button.Background=Brushes.Transparent;
        button.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='Chrome' Background='Transparent' CornerRadius='{DynamicResource RowRadius}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Chrome' Property='Background' Value='{DynamicResource Hover}'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Chrome' Property='Background' Value='{DynamicResource Selected}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Chrome' Property='Background' Value='{DynamicResource Selected}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        // IconButton normally uses the library's Icon slot; retain it in this simple template.
        if(button.Icon!=null){var symbol=button.Icon as Ui.SymbolIcon;string name=symbol==null?"":symbol.Symbol.ToString();if(name.StartsWith("Dismiss")||name.StartsWith("Settings")||name.StartsWith("Subtract"))button.Content=new TextBlock{Text=name.StartsWith("Dismiss")?"\uE711":name.StartsWith("Subtract")?"\uE921":"\uE713",FontFamily=new FontFamily("Segoe Fluent Icons"),FontSize=14,Foreground=ActivityTheme.Brush("Text","#F1F2F5")};else{button.Icon.Foreground=ActivityTheme.Brush("Muted","#A6ABB5");button.Content=button.Icon;}var glyph=button.Content as TextBlock;if(glyph!=null)glyph.SetResourceReference(TextBlock.ForegroundProperty,"Text");}
    }
    internal static Border Card(UIElement content) {
        return new Border{Child=content,Background=ActivityTheme.Brush("Card","#303030"),BorderBrush=ActivityTheme.Brush("Divider","#18FFFFFF"),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(ActivityTheme.RowRadius),Padding=new Thickness(16),Margin=new Thickness(0,0,0,6)};
    }
    static Border MenuFrame(DependencyObject root){
        Border best=null;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){
            var child=VisualTreeHelper.GetChild(root,i);var border=child as Border;
            if(border!=null&&border.BorderThickness.Left>0&&border.ActualWidth>80&&border.ActualHeight>60)best=border;
            var nested=MenuFrame(child);if(nested!=null&&(best==null||nested.ActualWidth*nested.ActualHeight>best.ActualWidth*best.ActualHeight))best=nested;
        }return best;
    }
    internal static void Menu(ContextMenu menu) {
        Apply(menu);menu.FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");menu.FontSize=14;
        // Keep the library's complete keyboard, submenu, checkmark and icon template.
        var surface=new SolidColorBrush(FrostedMaterial.Tone);var ink=new SolidColorBrush(FrostedMaterial.Ink(false));
        menu.Resources["ApplicationBackgroundBrush"]=surface;
        menu.Resources["TextFillColorPrimaryBrush"]=ink;
        menu.Background=surface;menu.Foreground=ink;menu.BorderBrush=FrostedMaterial.Edge();
        DockBackdrop backdrop=null;
        Action refreshGlass=()=>{
            if(!menu.IsOpen)return;var source=System.Windows.PresentationSource.FromVisual(menu) as System.Windows.Interop.HwndSource;if(source==null)return;
            GlassNative.Rect bounds;if(!GlassNative.GetWindowRect(source.Handle,out bounds))return;
            double scale=source.CompositionTarget.TransformToDevice.M11;var prefs=OverviewPreferences.Load();
            menu.Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));menu.Resources["ApplicationBackgroundBrush"]=menu.Background;menu.Resources["SolidBackgroundFillColorBaseBrush"]=menu.Background;
            var frame=MenuFrame(menu);FrameworkElement area=frame??(FrameworkElement)menu;
            var point=area.PointToScreen(new Point(0,0));if(frame!=null){frame.CornerRadius=new CornerRadius(ActivityTheme.GlassRadius);frame.Background=new SolidColorBrush(Color.FromArgb(1,0,0,0));frame.BorderBrush=FrostedMaterial.Edge();}
            if(backdrop==null)backdrop=new DockBackdrop();backdrop.Update(source.Handle,point.X/scale,point.Y/scale,area.ActualWidth,area.ActualHeight,scale,true,FrostedMaterial.Enabled(prefs.Glass),prefs.GlassStrength);
        };
        var glassTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};glassTimer.Tick+=(s,e)=>refreshGlass();
        menu.Opened+=(s,e)=>{glassTimer.Start();menu.Dispatcher.BeginInvoke(refreshGlass,System.Windows.Threading.DispatcherPriority.Loaded);};
        menu.Closed+=(s,e)=>{glassTimer.Stop();if(backdrop!=null){backdrop.Close();backdrop=null;}};
    }
}
