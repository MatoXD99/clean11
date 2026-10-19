using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

static partial class TopBarAudio {
    internal sealed class Endpoint {internal string Id,Name;internal bool Default;public override string ToString(){return Name;}}
    [StructLayout(LayoutKind.Sequential)]struct PropertyKey {public Guid Id;public uint Index;}
    [StructLayout(LayoutKind.Explicit,Size=24)]struct Variant {[FieldOffset(0)]public ushort Type;[FieldOffset(8)]public IntPtr Text;}
    [DllImport("ole32.dll")]static extern int PropVariantClear(ref Variant value);
    [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IProperties {
        [PreserveSig]int GetCount(out uint count);
        [PreserveSig]int GetAt(uint index,out PropertyKey key);
        [PreserveSig]int GetValue(ref PropertyKey key,out Variant value);
    }
    [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IDevices {
        [PreserveSig]int GetCount(out uint count);
        [PreserveSig]int Item(uint index,out IDevice device);
    }
    // Windows has no public Core Audio setter for system defaults. Keep its
    // policy interface isolated here; the UI offers Windows Settings on failure.
    [ComImport,Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]class Policy {}
    [ComImport,Guid("F8679F50-850A-41CF-9C72-430F290290C8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IPolicy {
        [PreserveSig]int GetMixFormat(IntPtr id,out IntPtr format);
        [PreserveSig]int GetDeviceFormat(IntPtr id,int def,out IntPtr format);
        [PreserveSig]int ResetDeviceFormat(IntPtr id);
        [PreserveSig]int SetDeviceFormat(IntPtr id,IntPtr endpoint,IntPtr mix);
        [PreserveSig]int GetProcessingPeriod(IntPtr id,int def,IntPtr period,IntPtr minimum);
        [PreserveSig]int SetProcessingPeriod(IntPtr id,IntPtr period);
        [PreserveSig]int GetShareMode(IntPtr id,IntPtr mode);
        [PreserveSig]int SetShareMode(IntPtr id,IntPtr mode);
        [PreserveSig]int GetPropertyValue(IntPtr id,IntPtr key,IntPtr value);
        [PreserveSig]int SetPropertyValue(IntPtr id,IntPtr key,IntPtr value);
        [PreserveSig]int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)]string id,int role);
        [PreserveSig]int SetEndpointVisibility(IntPtr id,int visible);
    }
    internal static List<Endpoint> Endpoints(int flow){
        object enumerator=null;IDevice current=null;object collection=null;IntPtr pointer=IntPtr.Zero;var result=new List<Endpoint>();
        try{enumerator=new Enumerator();var api=(IEnumerator)enumerator;string selected=null;if(api.GetDefaultAudioEndpoint(flow,1,out current)>=0)current.GetId(out selected);
            Marshal.ThrowExceptionForHR(api.EnumAudioEndpoints(flow,1,out pointer));collection=Marshal.GetObjectForIUnknown(pointer);var devices=(IDevices)collection;uint count;Marshal.ThrowExceptionForHR(devices.GetCount(out count));
            for(uint i=0;i<count;i++){IDevice device=null;IProperties properties=null;try{Marshal.ThrowExceptionForHR(devices.Item(i,out device));string id;Marshal.ThrowExceptionForHR(device.GetId(out id));string name=flow==0?"Audio output":"Audio input";
                if(device.OpenPropertyStore(0,out properties)>=0){var key=new PropertyKey{Id=new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),Index=14};Variant value;if(properties.GetValue(ref key,out value)>=0){try{if(value.Type==31)name=Marshal.PtrToStringUni(value.Text);}finally{PropVariantClear(ref value);}}}
                result.Add(new Endpoint{Id=id,Name=name,Default=id==selected});
            }finally{Release(properties);Release(device);}}
        }finally{Release(collection);if(pointer!=IntPtr.Zero)Marshal.Release(pointer);Release(current);Release(enumerator);}return result;
    }
    internal static void DefaultEndpoint(string id){object policy=null;try{policy=new Policy();for(int role=0;role<3;role++)Marshal.ThrowExceptionForHR(((IPolicy)policy).SetDefaultEndpoint(id,role));}finally{Release(policy);}}
}
