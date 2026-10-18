using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

class ProcessIdentity {internal string Name,Path;}
static class ProcessSnapshot {
    [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref uint length);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    internal static ProcessIdentity Read(uint pid) {
        IntPtr process=OpenProcess(0x1000,false,pid);if(process==IntPtr.Zero)return new ProcessIdentity{Name="",Path=""};
        try {
            var path=new StringBuilder(1024);uint length=(uint)path.Capacity;
            if(!QueryFullProcessImageName(process,0,path,ref length))return new ProcessIdentity{Name="",Path=""};
            string value=path.ToString();return new ProcessIdentity{Name=System.IO.Path.GetFileNameWithoutExtension(value),Path=value};
        }finally{CloseHandle(process);}
    }
}
