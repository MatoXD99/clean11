using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Only settled preview cards use these non-layered hosts. Entrance/exit retains
// the original DWM animation path. Source application HWNDs are never modified.
sealed class RoundedPreview : Form {
    [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    IntPtr thumbnail;
    internal RoundedPreview(Form owner,IntPtr source,Action<MouseEventArgs> move,Action<MouseEventArgs> down){
        Owner=owner;Text="Activities rounded preview";FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;ShowInTaskbar=false;TopMost=true;BackColor=Color.Black;
        MouseMove+=(s,e)=>move(new MouseEventArgs(e.Button,e.Clicks,Left+e.X-owner.Left,Top+e.Y-owner.Top,e.Delta));
        MouseDown+=(s,e)=>down(new MouseEventArgs(e.Button,e.Clicks,Left+e.X-owner.Left,Top+e.Y-owner.Top,e.Delta));
        // A GDI region disables DWM's antialiased corner treatment. Let DWM
        // clip the thumbnail and its host together, without a second border.
        int corner=2,border=-2;
        GlassNative.DwmSetWindowAttribute(Handle,33,ref corner,4);
        GlassNative.DwmSetWindowAttribute(Handle,34,ref border,4);
        Native.DwmRegisterThumbnail(Handle,source,out thumbnail);
    }
    protected override CreateParams CreateParams {get{var parameters=base.CreateParams;parameters.ExStyle|=0x08000080;return parameters;}}
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override void WndProc(ref Message message){if(message.Msg==0x21){message.Result=new IntPtr(3);return;}base.WndProc(ref message);}
    internal bool Ready {get{return thumbnail!=IntPtr.Zero;}}
    internal void Place(Rectangle bounds,IntPtr behind){
        if(Bounds!=bounds)Bounds=bounds;
        var properties=new Native.Props{Flags=1|4|8|16,Destination=new Native.Rect(ClientRectangle),Opacity=255,Visible=true,ClientOnly=false};Native.DwmUpdateThumbnailProperties(thumbnail,ref properties);
        if(!Visible)Show();SetWindowPos(Handle,behind,0,0,0,0,0x0013);
    }
    internal void Fade(byte opacity,IntPtr behind){
        if(opacity==0){Hide();return;}
        // This is a non-layered DWM host: reducing thumbnail opacity exposes
        // its black backing, rather than the desktop. Keep it intact until
        // the search transition reaches its visibility boundary.
        var properties=new Native.Props{Flags=4|8,Opacity=255,Visible=true};Native.DwmUpdateThumbnailProperties(thumbnail,ref properties);
        if(!Visible)Show();SetWindowPos(Handle,behind,0,0,0,0,0x0013);
    }
    protected override void Dispose(bool disposing){if(thumbnail!=IntPtr.Zero){Native.DwmUnregisterThumbnail(thumbnail);thumbnail=IntPtr.Zero;}base.Dispose(disposing);}
}

partial class Overview {
    readonly Dictionary<IntPtr,RoundedPreview> roundedPreviews=new Dictionary<IntPtr,RoundedPreview>();
    byte previewOpacity=255;
    internal byte SpotlightPreviewOpacity {get{return previewOpacity;}}
    internal void FadeSpotlightPreviews(byte opacity){
        previewOpacity=opacity;if(!Visible||closing)return;
        foreach(var badge in previewBadges.Values){badge.Opacity=1;if(opacity==0)badge.Hide();else if(!badge.IsVisible)badge.Show();}
        foreach(var window in windows){RoundedPreview preview;if(roundedPreviews.TryGetValue(window.Hwnd,out preview))preview.Fade(opacity,Handle);else if(window.Thumbnail!=IntPtr.Zero){var properties=new Native.Props{Flags=4|8,Opacity=opacity,Visible=opacity>0};Native.DwmUpdateThumbnailProperties(window.Thumbnail,ref properties);}}
    }
    void HideRoundedPreviews(){foreach(var preview in roundedPreviews.Values)preview.Hide();foreach(var badge in previewBadges.Values)badge.Hide();}
    void ReleaseRoundedPreview(IntPtr window){ReleaseSpotlightBadge(window);RoundedPreview preview;if(roundedPreviews.TryGetValue(window,out preview)){preview.Dispose();roundedPreviews.Remove(window);}}
    void ReleaseRoundedPreviews(){foreach(var preview in roundedPreviews.Values)preview.Dispose();roundedPreviews.Clear();foreach(var badge in previewBadges.Values)badge.Close();previewBadges.Clear();}
    void UpdateRoundedPreviews(){
        bool show=!diagnostic&&Visible&&!rendering&&!closing&&!settingsOpen&&!browsing&&search.Text.Trim().Length==0;
        if(!show){HideRoundedPreviews();if(!rendering&&!closing)foreach(var window in windows)if(window.Thumbnail!=IntPtr.Zero){var hidden=new Native.Props{Flags=8,Visible=false};Native.DwmUpdateThumbnailProperties(window.Thumbnail,ref hidden);}return;}
        previewOpacity=255;
        var ids=new HashSet<IntPtr>(windows.Select(w=>w.Hwnd));foreach(var gone in roundedPreviews.Keys.Where(key=>!ids.Contains(key)).ToArray())ReleaseRoundedPreview(gone);foreach(var gone in previewBadges.Keys.Where(key=>!ids.Contains(key)).ToArray())ReleaseSpotlightBadge(gone);
        IntPtr inputBefore=Native.GetForegroundWindow();
        foreach(var window in windows){
            PlaceSpotlightBadge(window);
            if(window.Thumbnail==IntPtr.Zero)continue;
            RoundedPreview preview;
            if(!roundedPreviews.TryGetValue(window.Hwnd,out preview)){preview=new RoundedPreview(previewHost,window.Hwnd,e=>OnMouseMove(e),e=>OnMouseDown(e));if(!preview.Ready){preview.Dispose();var fallback=new Native.Props{Flags=1|4|8|16,Destination=new Native.Rect(window.Preview),Opacity=255,Visible=true,ClientOnly=false};Native.DwmUpdateThumbnailProperties(window.Thumbnail,ref fallback);continue;}roundedPreviews[window.Hwnd]=preview;}
            var bounds=window.Preview;bounds.Offset(Left,Top);preview.Place(bounds,Handle);
            var properties=new Native.Props{Flags=8,Visible=false};Native.DwmUpdateThumbnailProperties(window.Thumbnail,ref properties);
        }
        // Form.Show can activate a sibling owned form on some WinForms builds.
        // Recover only from our preview HWNDs; never override an external switch.
        IntPtr inputAfter=Native.GetForegroundWindow();
        if(inputAfter!=inputBefore&&roundedPreviews.Values.Any(preview=>preview.Handle==inputAfter)&&(inputBefore==Handle||SpotlightOwnsForeground(inputBefore))){Native.SetForegroundWindow(inputBefore);FocusSearch();}
    }
}
