using System;
using System.Runtime.InteropServices;
using Windows.UI.Composition;
using Windows.Graphics.Effects;
using Windows.Foundation;
using System.Numerics;

[ComImport,Guid("29E691FA-4567-4DCA-B319-D0F207EB6807"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDesktopCompositor {void CreateDesktopWindowTarget(IntPtr hwnd,[MarshalAs(UnmanagedType.Bool)]bool topmost,out IntPtr target);}
[ComImport,Guid("A1BEA8BA-D726-4663-8129-6B5E7927FFA6"),InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
interface IDesktopTarget {Visual Root{get;set;}}
[ComImport,Guid("2FC57384-A068-44D7-A331-30982FCF7177"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IEffectInterop {
    void GetEffectId(out Guid id);
    void GetNamedPropertyMapping([MarshalAs(UnmanagedType.LPWStr)]string name,out uint index,out uint mapping);
    void GetPropertyCount(out uint count);
    void GetProperty(uint index,out IntPtr value);
    void GetSource(uint index,out IGraphicsEffectSource source);
    void GetSourceCount(out uint count);
}
[ComImport,Guid("629BDBC8-D932-4FF4-96B9-8D96C5C1E858"),InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
interface IPropertyFactory {
    void CreateEmpty(out IntPtr value);void CreateUInt8(byte data,out IntPtr value);void CreateInt16(short data,out IntPtr value);void CreateUInt16(ushort data,out IntPtr value);void CreateInt32(int data,out IntPtr value);void CreateUInt32(uint data,out IntPtr value);void CreateInt64(long data,out IntPtr value);void CreateUInt64(ulong data,out IntPtr value);void CreateSingle(float data,out IntPtr value);
}
[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
public sealed class DockGaussian : IGraphicsEffect,IGraphicsEffectSource,IEffectInterop {
    public string Name{get;set;}
    internal IGraphicsEffectSource Source;
    public void GetEffectId(out Guid id){id=new Guid("1FEB6D69-2FE6-4AC9-8C58-1D7F93E7A6A5");}
    public void GetNamedPropertyMapping(string name,out uint index,out uint mapping){if(name!="BlurAmount")throw new ArgumentException();index=0;mapping=1;}
    public void GetPropertyCount(out uint count){count=3;}
    public void GetProperty(uint index,out IntPtr value){
        var factory=(IPropertyFactory)System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeMarshal.GetActivationFactory(typeof(PropertyValue));
        IntPtr raw;if(index==0)factory.CreateSingle(8,out raw);else if(index==1||index==2)factory.CreateUInt32(1,out raw);else throw new ArgumentOutOfRangeException();
        try{var id=new Guid("4BD682DD-7554-40E9-9A9B-82654EDE7E62");Marshal.ThrowExceptionForHR(Marshal.QueryInterface(raw,ref id,out value));}finally{Marshal.Release(raw);}
    }
    public void GetSource(uint index,out IGraphicsEffectSource source){if(index!=0)throw new ArgumentOutOfRangeException();source=Source;}
    public void GetSourceCount(out uint count){count=1;}
}
sealed class DockComposition : IDisposable {
    [StructLayout(LayoutKind.Sequential)]struct QueueOptions{public int Size,Thread,Apartment;}
    [DllImport("CoreMessaging.dll")]static extern int CreateDispatcherQueueController(QueueOptions options,[MarshalAs(UnmanagedType.IUnknown)]out object controller);
    static object queue;
    Compositor compositor;IDesktopTarget target;ContainerVisual root;SpriteVisual blur,tint;CompositionEffectBrush effect;CompositionLinearGradientBrush tintBrush;CompositionBackdropBrush sourceBrush;CompositionEffectFactory factory;
    internal DockComposition(IntPtr window){
        if(queue==null){object controller;int hr=CreateDispatcherQueueController(new QueueOptions{Size=12,Thread=2,Apartment=2},out controller);if(hr<0)Marshal.ThrowExceptionForHR(hr);queue=controller;}
        try{
        compositor=new Compositor();IntPtr raw;
        ((IDesktopCompositor)(object)compositor).CreateDesktopWindowTarget(window,true,out raw);
        try{target=(IDesktopTarget)Marshal.GetObjectForIUnknown(raw);}finally{Marshal.Release(raw);}
        root=compositor.CreateContainerVisual();target.Root=root;
        factory=compositor.CreateEffectFactory(new DockGaussian{Name="Blur",Source=new CompositionEffectSourceParameter("Backdrop")},new[]{"Blur.BlurAmount"});
        effect=factory.CreateBrush();sourceBrush=compositor.CreateBackdropBrush();effect.SetSourceParameter("Backdrop",sourceBrush);
        blur=compositor.CreateSpriteVisual();blur.Brush=effect;root.Children.InsertAtTop(blur);
        tintBrush=compositor.CreateLinearGradientBrush();tintBrush.StartPoint=new Vector2(0,0);tintBrush.EndPoint=new Vector2(.2f,1);tint=compositor.CreateSpriteVisual();tint.Brush=tintBrush;root.Children.InsertAtTop(tint);
        }catch{Dispose();throw;}
    }
    internal void SetVisible(bool visible){root.IsVisible=visible;}
    internal void Update(float width,float height,float radius,byte alpha){
        root.Size=blur.Size=tint.Size=new Vector2(width,height);
        effect.Properties.InsertScalar("Blur.BlurAmount",Math.Max(0,Math.Min(40,radius)));
        var color=FrostedMaterial.Tone;
        tintBrush.ColorStops.Clear();
        tintBrush.ColorStops.Add(compositor.CreateColorGradientStop(0,Windows.UI.Color.FromArgb(alpha,(byte)Math.Min(255,color.R+13),(byte)Math.Min(255,color.G+15),(byte)Math.Min(255,color.B+16))));
        tintBrush.ColorStops.Add(compositor.CreateColorGradientStop(1,Windows.UI.Color.FromArgb(alpha,color.R,color.G,color.B)));
    }
    public void Dispose(){if(target!=null)target.Root=null;if(tint!=null)tint.Dispose();if(blur!=null)blur.Dispose();if(root!=null)root.Dispose();if(effect!=null)effect.Dispose();if(sourceBrush!=null)sourceBrush.Dispose();if(factory!=null)factory.Dispose();if(tintBrush!=null)tintBrush.Dispose();var disposable=target as IDisposable;if(disposable!=null)disposable.Dispose();if(compositor!=null)compositor.Dispose();}
}
