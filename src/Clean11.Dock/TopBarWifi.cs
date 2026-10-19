using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// Native WLAN keeps saved credentials in Windows, never in Clean11 settings.
static class TopBarWifi {
    internal sealed class Network {
        internal Guid Adapter;
        internal string Name, Profile;
        internal uint Signal,Auth,Cipher;internal string SsidHex;
        internal bool Connected, Secure, Connectable;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Interface {
        public Guid Id;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Description;
        public uint State;
    }
    [StructLayout(LayoutKind.Sequential)]struct Ssid {
        public uint Length;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] Bytes;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Available {
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Profile;
        public Ssid Ssid;public uint BssType,BssCount;
        public int Connectable;public uint Reason,PhyCount;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=8)]public uint[] Phys;
        public int More;public uint Quality;public int Security;public uint Auth,Cipher,Flags,Reserved;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Connection {
        public uint Mode;[MarshalAs(UnmanagedType.LPWStr)]public string Profile;
        public IntPtr Ssid,Bssid;public uint BssType,Flags;
    }
    [DllImport("wlanapi.dll")]static extern uint WlanOpenHandle(uint version,IntPtr reserved,out uint negotiated,out IntPtr handle);
    [DllImport("wlanapi.dll")]static extern uint WlanCloseHandle(IntPtr handle,IntPtr reserved);
    [DllImport("wlanapi.dll")]static extern void WlanFreeMemory(IntPtr memory);
    [DllImport("wlanapi.dll")]static extern uint WlanEnumInterfaces(IntPtr handle,IntPtr reserved,out IntPtr list);
    [DllImport("wlanapi.dll")]static extern uint WlanGetAvailableNetworkList(IntPtr handle,ref Guid adapter,uint flags,IntPtr reserved,out IntPtr list);
    [DllImport("wlanapi.dll")]static extern uint WlanScan(IntPtr handle,ref Guid adapter,IntPtr ssid,IntPtr ie,IntPtr reserved);
    [DllImport("wlanapi.dll",CharSet=CharSet.Unicode)]static extern uint WlanConnect(IntPtr handle,ref Guid adapter,ref Connection connection,IntPtr reserved);
    [DllImport("wlanapi.dll")]static extern uint WlanDisconnect(IntPtr handle,ref Guid adapter,IntPtr reserved);
    [DllImport("wlanapi.dll",CharSet=CharSet.Unicode)]static extern uint WlanDeleteProfile(IntPtr handle,ref Guid adapter,string profile,IntPtr reserved);
    internal static void Forget(Network network){IntPtr h=Open();try{Guid id=network.Adapter;Check(WlanDeleteProfile(h,ref id,network.Profile,IntPtr.Zero));}finally{WlanCloseHandle(h,IntPtr.Zero);}}
    static void Check(uint error){if(error!=0)throw new Win32Exception((int)error);}
    static IntPtr Open(){IntPtr handle;uint version;Check(WlanOpenHandle(2,IntPtr.Zero,out version,out handle));return handle;}
    static IEnumerable<T> Entries<T>(IntPtr list){int count=Marshal.ReadInt32(list),size=Marshal.SizeOf(typeof(T));if(count<0||count>4096)throw new InvalidOperationException();for(int i=0;i<count;i++)yield return (T)Marshal.PtrToStructure(IntPtr.Add(list,8+i*size),typeof(T));}
    internal static List<Network> Read(bool scan){
        var result=new List<Network>();IntPtr h=Open(),interfaces=IntPtr.Zero;
        try{Check(WlanEnumInterfaces(h,IntPtr.Zero,out interfaces));foreach(var adapter in Entries<Interface>(interfaces)){
            Guid id=adapter.Id;IntPtr networks=IntPtr.Zero;
            try{if(scan)Check(WlanScan(h,ref id,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero));Check(WlanGetAvailableNetworkList(h,ref id,0,IntPtr.Zero,out networks));
                foreach(var item in Entries<Available>(networks)){if(item.Ssid.Length==0||item.Ssid.Length>32)continue;result.Add(new Network{Adapter=id,Name=Encoding.UTF8.GetString(item.Ssid.Bytes,0,(int)item.Ssid.Length),SsidHex=BitConverter.ToString(item.Ssid.Bytes,0,(int)item.Ssid.Length).Replace("-",""),Auth=item.Auth,Cipher=item.Cipher,Profile=item.Profile,Signal=item.Quality,Connected=(item.Flags&1)!=0,Secure=item.Security!=0,Connectable=item.Connectable!=0});}
            }finally{if(networks!=IntPtr.Zero)WlanFreeMemory(networks);}
        }}finally{if(interfaces!=IntPtr.Zero)WlanFreeMemory(interfaces);WlanCloseHandle(h,IntPtr.Zero);}
        return result.OrderByDescending(x=>x.Connected).ThenByDescending(x=>!String.IsNullOrEmpty(x.Profile)).ThenByDescending(x=>x.Signal).GroupBy(x=>x.Adapter+"|"+x.Name).Select(x=>x.First()).ToList();
    }
    internal static bool CanCreate(Network n){return (!n.Secure&&n.Auth==1&&n.Cipher==0)||(n.Auth==7&&n.Cipher==4);}
    internal static void Connect(Network network,string password){IntPtr h=Open();try{Guid id=network.Adapter;if(network.Connected){Check(WlanDisconnect(h,ref id,IntPtr.Zero));return;}
        var p=new Connection{Mode=0,Profile=network.Profile,BssType=1};
        if(String.IsNullOrEmpty(network.Profile)){
            if(!CanCreate(network))throw new InvalidOperationException("Set up this authentication type in Windows first.");
            if(network.Secure&&(String.IsNullOrEmpty(password)||password.Length<8||password.Length>63))throw new ArgumentException("Use an 8–63 character Wi-Fi password.");
            // Temporary profile: Windows owns authentication; Clean11 never saves credentials.
            p.Mode=1;p.Profile="<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>Clean11 connection</name><SSIDConfig><SSID><hex>"+network.SsidHex+"</hex></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>manual</connectionMode><MSM><security><authEncryption><authentication>"+(network.Secure?"WPA2PSK":"open")+"</authentication><encryption>"+(network.Secure?"AES":"none")+"</encryption><useOneX>false</useOneX></authEncryption>"+(network.Secure?"<sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>"+System.Security.SecurityElement.Escape(password)+"</keyMaterial></sharedKey>":"")+"</security></MSM></WLANProfile>";
        }
        Check(WlanConnect(h,ref id,ref p,IntPtr.Zero));
    }finally{WlanCloseHandle(h,IntPtr.Zero);}}
}
