using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;

// One palette for the native overview, WPF search/settings and DWM clipping.
// Theme data is local, optional, validated, and loaded once per application run.
static class ActivityTheme {
    static readonly Dictionary<string,string> values=Load();
    static Dictionary<string,string> Load(){try{return new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ActivitiesTheme.json")))??new Dictionary<string,string>();}catch{return new Dictionary<string,string>();}}
    internal static int Number(string key,int fallback){string value;int number;return values.TryGetValue(key,out value)&&Int32.TryParse(value,out number)?Math.Max(4,Math.Min(40,number)):fallback;}
    internal static System.Drawing.Color Color(string key,string fallback){string value;if(!values.TryGetValue(key,out value))value=fallback;try{var c=(System.Windows.Media.Color)ColorConverter.ConvertFromString(value);return System.Drawing.Color.FromArgb(c.A,c.R,c.G,c.B);}catch{var c=(System.Windows.Media.Color)ColorConverter.ConvertFromString(fallback);return System.Drawing.Color.FromArgb(c.A,c.R,c.G,c.B);}}
    internal static SolidColorBrush Brush(string key,string fallback){var c=Color(key,fallback);System.Windows.Media.Color palette;if(FrostedMaterial.TryColor(key,out palette))c=System.Drawing.Color.FromArgb(palette.A,palette.R,palette.G,palette.B);var brush=new SolidColorBrush(System.Windows.Media.Color.FromArgb(c.A,c.R,c.G,c.B));brush.Freeze();return brush;}
    internal const int GlassRadius=8,PanelPadding=16,PopupTitleSize=16,BodySize=13,CaptionSize=12;
    internal static readonly int PanelRadius=Number("PanelRadius",16),PreviewRadius=Number("PreviewRadius",16),RowRadius=Number("RowRadius",8),ControlRadius=Number("ControlRadius",6);
    internal static readonly System.Drawing.Color Surface=Color("Surface","#202632"),Text=Color("Text","#F2F5FC"),Muted=Color("Muted","#A3AEC2"),Accent=Color("Accent","#6EB3FF"),Border=Color("Border","#536176"),Hover=Color("Hover","#303C4E"),Selected=Color("Selected","#315B89");
    internal static readonly System.Drawing.Color Danger=Color("Danger","#653D4C"),DangerHover=Color("DangerHover","#A63E53"),Warm=Color("Warm","#EFBC42"),Favorite=Color("Favorite","#ECC674");
    internal static ResourceDictionary Resources(){var resources=new ResourceDictionary();foreach(var token in new[]{new[]{"Card","#303030"},new[]{"Surface","#202632"},new[]{"GlassTint","#8E181E29"},new[]{"Text","#F2F5FC"},new[]{"Muted","#A3AEC2"},new[]{"Accent","#6EB3FF"},new[]{"Border","#41536176"},new[]{"Hover","#343F638C"},new[]{"Selected","#B52A5B96"},new[]{"SelectionBorder","#668ABEFF"},new[]{"Divider","#26FFFFFF"},new[]{"Track","#445066"},new[]{"Thumb","#526079"},new[]{"SwitchOn","#438BDC"}})resources[token[0]]=Brush(token[0],token[1]);resources["PanelRadius"]=new CornerRadius(PanelRadius);resources["RowRadius"]=new CornerRadius(RowRadius);resources["ControlRadius"]=new CornerRadius(ControlRadius);return resources;}
}
