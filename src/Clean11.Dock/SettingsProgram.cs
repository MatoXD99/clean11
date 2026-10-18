using System;
using System.Diagnostics;
using System.IO;
static class SettingsProgram {
    [STAThread] static void Main(){
        string dock=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Clean11.Dock.exe");
        if(File.Exists(dock))Process.Start(new ProcessStartInfo(dock,"--settings"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)});
    }
}
