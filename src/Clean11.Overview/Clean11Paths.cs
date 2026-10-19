using System;
using System.IO;
using Microsoft.Win32;

// Resolve sibling modules before considering the legacy development locations.
static class Clean11Paths {
    internal static string Root {
        get {
            string here=AppDomain.CurrentDomain.BaseDirectory;
            if(File.Exists(Path.Combine(here,"Clean11.exe")))return here;
            string parent=Path.GetDirectoryName(here.TrimEnd(Path.DirectorySeparatorChar));
            if(parent!=null&&File.Exists(Path.Combine(parent,"Clean11.exe")))return parent;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Clean11");
        }
    }
    internal static string Launcher {get{return Path.Combine(Root,"Clean11.exe");}}
    internal static string Overview {
        get {string bundled=Path.Combine(Root,"Overview","ActivitiesOverview.exe");return File.Exists(bundled)?bundled:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Tools","ActivitiesOverview","ActivitiesOverview.exe");}
    }
    internal static string Settings {get{return Path.Combine(Root,"Dock","Clean11.Settings.exe");}}
    internal static string Licenses {get{string shared=Path.Combine(Root,"licenses");return Directory.Exists(shared)?shared:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"licenses");}}
    internal static bool StartupDefault {
        get {
            using(var installed=Registry.CurrentUser.OpenSubKey(@"Software\Clean11\Installer")) {
                if(installed==null)return true;
                using(var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))return run!=null&&run.GetValue("Clean11")!=null;
            }
        }
    }
}
