using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

static partial class TopBarAudio {
    internal sealed class Session {
        internal string Id,Name;internal uint ProcessId;internal float Level;internal bool Muted;
    }
    internal sealed class Application {internal uint ProcessId;internal string Name;internal bool HasOutput,HasInput;internal string OutputEndpointId,InputEndpointId;internal List<Session> Sessions=new List<Session>();}
    [ComImport,Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IManager {
        [PreserveSig]int GetAudioSessionControl(ref Guid id,uint flags,out IntPtr control);
        [PreserveSig]int GetSimpleAudioVolume(ref Guid id,uint flags,out IntPtr volume);
        [PreserveSig]int GetSessionEnumerator(out ISessions sessions);
    }
    [ComImport,Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ISessions {
        [PreserveSig]int GetCount(out int count);
        [PreserveSig]int GetSession(int index,out IControl control);
    }
    // IAudioSessionControl2 includes all inherited IAudioSessionControl slots.
    [ComImport,Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IControl {
        [PreserveSig]int GetState(out int state);
        [PreserveSig]int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)]out string name);
        [PreserveSig]int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string name,ref Guid context);
        [PreserveSig]int GetIconPath([MarshalAs(UnmanagedType.LPWStr)]out string path);
        [PreserveSig]int SetIconPath([MarshalAs(UnmanagedType.LPWStr)]string path,ref Guid context);
        [PreserveSig]int GetGroupingParam(out Guid grouping);
        [PreserveSig]int SetGroupingParam(ref Guid grouping,ref Guid context);
        [PreserveSig]int RegisterAudioSessionNotification(IntPtr callback);
        [PreserveSig]int UnregisterAudioSessionNotification(IntPtr callback);
        [PreserveSig]int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
        [PreserveSig]int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
        [PreserveSig]int GetProcessId(out uint pid);
        [PreserveSig]int IsSystemSoundsSession();
        [PreserveSig]int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)]bool optOut);
    }
    [ComImport,Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ISimpleVolume {
        [PreserveSig]int SetMasterVolume(float level,ref Guid context);
        [PreserveSig]int GetMasterVolume(out float level);
        [PreserveSig]int SetMute([MarshalAs(UnmanagedType.Bool)]bool muted,ref Guid context);
        [PreserveSig]int GetMute([MarshalAs(UnmanagedType.Bool)]out bool muted);
    }
    static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
    static void VisitSessionControls(int flow,Action<IControl> action){
        object enumerator=null,collection=null;IntPtr pointer=IntPtr.Zero;
        try{enumerator=new Enumerator();Marshal.ThrowExceptionForHR(((IEnumerator)enumerator).EnumAudioEndpoints(flow,1,out pointer));collection=Marshal.GetObjectForIUnknown(pointer);var endpoints=(IDevices)collection;uint endpointCount;Marshal.ThrowExceptionForHR(endpoints.GetCount(out endpointCount));
            for(uint endpointIndex=0;endpointIndex<endpointCount;endpointIndex++){IDevice device=null;object manager=null;ISessions sessions=null;try{Marshal.ThrowExceptionForHR(endpoints.Item(endpointIndex,out device));Guid iid=typeof(IManager).GUID;Marshal.ThrowExceptionForHR(device.Activate(ref iid,23,IntPtr.Zero,out manager));Marshal.ThrowExceptionForHR(((IManager)manager).GetSessionEnumerator(out sessions));int count;Marshal.ThrowExceptionForHR(sessions.GetCount(out count));
                for(int i=0;i<count;i++){IControl control=null;try{if(sessions.GetSession(i,out control)<0)continue;int state;if(control.GetState(out state)<0||state==2)continue;action(control);}catch(COMException){}finally{Release(control);}}
            }catch(COMException){}finally{Release(sessions);Release(manager);Release(device);}}
        }finally{Release(collection);if(pointer!=IntPtr.Zero)Marshal.Release(pointer);Release(enumerator);}
    }
    internal static List<Session> Sessions(){var result=new List<Session>();var seen=new HashSet<string>(StringComparer.Ordinal);VisitSessionControls(0,control=>{
        var volume=control as ISimpleVolume;if(volume==null)return;string id;uint pid;float level;bool muted;
        if(control.GetSessionInstanceIdentifier(out id)<0||!seen.Add(id)||control.GetProcessId(out pid)<0||volume.GetMasterVolume(out level)<0||volume.GetMute(out muted)<0)return;
        string name="App audio";if(pid!=0){try{using(var process=Process.GetProcessById((int)pid))name=process.ProcessName;}catch(ArgumentException){}catch(System.ComponentModel.Win32Exception){}}
        result.Add(new Session{Id=id,Name=name,ProcessId=pid,Level=level,Muted=muted});
    });return result;}
    static List<uint> ProcessIds(int flow){var result=new HashSet<uint>();VisitSessionControls(flow,control=>{uint pid;if(control.GetProcessId(out pid)>=0&&pid>0)result.Add(pid);});return result.OrderBy(x=>x).ToList();}
    internal static List<Application> Applications(){var sessions=Sessions();var inputs=new HashSet<uint>(ProcessIds(1));var apps=new List<Application>();foreach(uint pid in sessions.Where(x=>x.ProcessId>0).Select(x=>x.ProcessId).Concat(inputs).Distinct().OrderBy(x=>x)){
        var app=new Application{ProcessId=pid,HasInput=inputs.Contains(pid)};app.Sessions=sessions.Where(x=>x.ProcessId==pid).ToList();app.HasOutput=app.Sessions.Count>0;app.Name=app.Sessions.Select(x=>x.Name).FirstOrDefault();
        if(String.IsNullOrEmpty(app.Name)){try{using(var process=Process.GetProcessById((int)pid))app.Name=process.ProcessName;}catch(ArgumentException){}catch(System.ComponentModel.Win32Exception){}}if(String.IsNullOrEmpty(app.Name))app.Name="App";
        apps.Add(app);
    }return apps.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(x=>x.ProcessId).ToList();}
    internal static bool SetSession(string id,float? level,bool? muted){bool changed=false;try{VisitSessionControls(0,control=>{var volume=control as ISimpleVolume;if(volume==null)return;string current;if(control.GetSessionInstanceIdentifier(out current)<0||current!=id)return;Guid context=Guid.Empty;int hr=level.HasValue?volume.SetMasterVolume(Math.Max(0,Math.Min(1,level.Value)),ref context):volume.SetMute(muted.Value,ref context);changed=hr>=0;});}catch(COMException){}return changed;}

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int SetPersistedEndpointDelegate(IntPtr factory,uint processId,int flow,int role,IntPtr deviceId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GetPersistedEndpointDelegate(IntPtr factory,uint processId,int flow,int role,out IntPtr deviceId);
    [DllImport("combase.dll",PreserveSig=true)]static extern int RoGetActivationFactory(IntPtr className,ref Guid iid,out IntPtr factory);
    [DllImport("combase.dll",CharSet=CharSet.Unicode,PreserveSig=true)]static extern int WindowsCreateString(string value,uint length,out IntPtr valueString);
    [DllImport("combase.dll",PreserveSig=true)]static extern int WindowsDeleteString(IntPtr valueString);
    [DllImport("combase.dll",PreserveSig=true)]static extern IntPtr WindowsGetStringRawBuffer(IntPtr valueString,out uint length);
    [DllImport("combase.dll",PreserveSig=true)]static extern int RoInitialize(uint initType);
    [DllImport("combase.dll")]static extern void RoUninitialize();
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct NativeVersion {internal uint Size,Major,Minor,Build,Platform;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]internal string ServicePack;}
    [DllImport("ntdll.dll",CharSet=CharSet.Unicode)]static extern int RtlGetVersion(ref NativeVersion version);
    const string PolicyClass="Windows.Media.Internal.AudioPolicyConfig",MmDevicePrefix=@"\\?\SWD#MMDEVAPI#",RenderSuffix="#{e6327cad-dcec-4949-ae8a-991e976a79d2}",CaptureSuffix="#{2eef81be-33fa-4800-9670-1cd474972c3f}";
    const int SetEndpointSlot=25,GetEndpointSlot=26;
    static int NativeBuild(){var version=new NativeVersion();version.Size=(uint)Marshal.SizeOf(typeof(NativeVersion));int hr=RtlGetVersion(ref version);if(hr<0)Marshal.ThrowExceptionForHR(hr);return (int)version.Build;}
    static IntPtr CreatePolicyFactory(out bool modern){int build=NativeBuild();modern=build>=21390;Guid iid=modern?new Guid("ab3d4648-e242-459f-b02f-541c70306324"):new Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258");IntPtr className=IntPtr.Zero,factory=IntPtr.Zero;try{Marshal.ThrowExceptionForHR(WindowsCreateString(PolicyClass,(uint)PolicyClass.Length,out className));int hr=RoGetActivationFactory(className,ref iid,out factory);if(hr<0){DockLog.Write("Audio policy factory activation failed; Windows build="+build+", interface="+(modern?"21H2+":"legacy")+", HRESULT=0x"+hr.ToString("X8"));Marshal.ThrowExceptionForHR(hr);}return factory;}catch{if(factory!=IntPtr.Zero)Marshal.Release(factory);throw;}finally{if(className!=IntPtr.Zero)WindowsDeleteString(className);}}
    static T VtableMethod<T>(IntPtr factory,int slot)where T:class{IntPtr vtable=Marshal.ReadIntPtr(factory);IntPtr method=Marshal.ReadIntPtr(vtable,slot*IntPtr.Size);return (T)(object)Marshal.GetDelegateForFunctionPointer(method,typeof(T));}
    static int SetRoute(IntPtr factory,uint pid,int flow,int role,IntPtr endpoint){return VtableMethod<SetPersistedEndpointDelegate>(factory,SetEndpointSlot)(factory,pid,flow,role,endpoint);}
    static int GetRoute(IntPtr factory,uint pid,int flow,int role,out IntPtr endpoint){return VtableMethod<GetPersistedEndpointDelegate>(factory,GetEndpointSlot)(factory,pid,flow,role,out endpoint);}
    static string ReadRoute(IntPtr factory,uint pid,int flow){for(int role=1;role>=0;role--){IntPtr value=IntPtr.Zero;int hr=GetRoute(factory,pid,flow,role,out value);if(hr==unchecked((int)0x80070490))continue;if(hr<0)Marshal.ThrowExceptionForHR(hr);if(value==IntPtr.Zero)continue;try{uint length;IntPtr text=WindowsGetStringRawBuffer(value,out length);if(text==IntPtr.Zero||length==0)continue;return Marshal.PtrToStringUni(text,(int)length);}finally{WindowsDeleteString(value);}}return null;}
    static bool InitializeWinRt(){int hr=RoInitialize(1);if(hr==0||hr==1)return true;if(hr==unchecked((int)0x80010106))return false;Marshal.ThrowExceptionForHR(hr);return false;}
    internal static string AppEndpoint(uint pid,int flow){IntPtr factory=IntPtr.Zero;bool uninitialize=false;try{uninitialize=InitializeWinRt();bool modern;factory=CreatePolicyFactory(out modern);string value=ReadRoute(factory,pid,flow);if(String.IsNullOrEmpty(value))return null;if(value.StartsWith(MmDevicePrefix,StringComparison.OrdinalIgnoreCase))value=value.Substring(MmDevicePrefix.Length);string suffix=flow==0?RenderSuffix:CaptureSuffix;if(value.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))value=value.Substring(0,value.Length-suffix.Length);return value;}catch(Exception ex){DockLog.Write("Per-app audio route read failed: "+ex);return null;}finally{if(factory!=IntPtr.Zero)Marshal.Release(factory);if(uninitialize)RoUninitialize();}}
    internal static bool SetAppEndpoint(uint pid,int flow,string endpointId){IntPtr factory=IntPtr.Zero,value=IntPtr.Zero;bool uninitialize=false;try{uninitialize=InitializeWinRt();bool modern;factory=CreatePolicyFactory(out modern);if(!String.IsNullOrEmpty(endpointId)){string suffix=flow==0?RenderSuffix:CaptureSuffix;string packed=MmDevicePrefix+endpointId+suffix;Marshal.ThrowExceptionForHR(WindowsCreateString(packed,(uint)packed.Length,out value));}bool ok=true;for(int role=0;role<3;role++){int hr=SetRoute(factory,pid,flow,role,value);if(hr<0){ok=false;break;}}return ok;}catch(Exception ex){DockLog.Write("Per-app audio route change failed: "+ex);return false;}finally{if(value!=IntPtr.Zero)WindowsDeleteString(value);if(factory!=IntPtr.Zero)Marshal.Release(factory);if(uninitialize)RoUninitialize();}}
}
