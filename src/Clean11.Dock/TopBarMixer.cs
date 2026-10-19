using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

static partial class TopBarAudio {
    internal sealed class Session {
        internal string Id,Name;internal float Level;internal bool Muted;
    }
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
    static void VisitSessions(Action<IControl,ISimpleVolume> action){
        object enumerator=null,manager=null;IDevice device=null;ISessions sessions=null;
        try{enumerator=new Enumerator();Marshal.ThrowExceptionForHR(((IEnumerator)enumerator).GetDefaultAudioEndpoint(0,1,out device));Guid id=typeof(IManager).GUID;Marshal.ThrowExceptionForHR(device.Activate(ref id,23,IntPtr.Zero,out manager));Marshal.ThrowExceptionForHR(((IManager)manager).GetSessionEnumerator(out sessions));int count;Marshal.ThrowExceptionForHR(sessions.GetCount(out count));
            for(int i=0;i<count;i++){IControl control=null;try{if(sessions.GetSession(i,out control)<0)continue;int state;if(control.GetState(out state)<0||state==2)continue;action(control,(ISimpleVolume)control);}catch(COMException){}finally{Release(control);}}
        }finally{Release(sessions);Release(manager);Release(device);Release(enumerator);}
    }
    internal static List<Session> Sessions(){var result=new List<Session>();VisitSessions((control,volume)=>{
        string id;uint pid;float level;bool muted;
        if(control.GetSessionInstanceIdentifier(out id)<0||control.GetProcessId(out pid)<0||volume.GetMasterVolume(out level)<0||volume.GetMute(out muted)<0)return;
        string name=control.IsSystemSoundsSession()==0?"System sounds":"App audio";
        if(pid!=0){try{using(var process=Process.GetProcessById((int)pid))name=process.ProcessName;}catch(ArgumentException){}catch(System.ComponentModel.Win32Exception){}}
        result.Add(new Session{Id=id,Name=name,Level=level,Muted=muted});
    });return result;}
    internal static bool SetSession(string id,float? level,bool? muted){bool changed=false;try{VisitSessions((control,volume)=>{string current;if(control.GetSessionInstanceIdentifier(out current)<0||current!=id)return;Guid context=Guid.Empty;int hr=level.HasValue?volume.SetMasterVolume(Math.Max(0,Math.Min(1,level.Value)),ref context):volume.SetMute(muted.Value,ref context);changed=hr>=0;});}catch(COMException){}return changed;}
}
