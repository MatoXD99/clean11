using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

// Only transition boundaries cross processes. Each WPF process animates locally;
// there is no per-frame pipe, file write, or desktop broadcast.
static class OverviewSceneNative {
    internal const string StateTitle="Clean11 overview scene state",ListenerTitle="Clean11 overview scene listener";
    internal const string Target="Clean11.Scene.Target",Duration="Clean11.Scene.Duration",Started="Clean11.Scene.Started",Sequence="Clean11.Scene.Sequence";
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern IntPtr FindWindow(string kind,string title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string kind,string title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern bool SetProp(IntPtr window,string name,IntPtr value);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern IntPtr GetProp(IntPtr window,string name);
    [DllImport("user32.dll")]internal static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    internal static readonly uint Message=RegisterWindowMessage("Clean11.Overview.Scene.v1");
    internal static bool OwnsWindow(IntPtr window){
        var state=FindWindow(null,StateTitle);if(state==IntPtr.Zero||window==IntPtr.Zero)return false;
        uint owner,pid;GetWindowThreadProcessId(state,out owner);GetWindowThreadProcessId(window,out pid);return owner!=0&&owner==pid;
    }
    internal static bool ProcessIs(IntPtr window,string expected){
        uint pid;GetWindowThreadProcessId(window,out pid);
        try{using(var process=Process.GetProcessById((int)pid))return process.ProcessName.Equals(expected,StringComparison.OrdinalIgnoreCase);}catch{return false;}
    }
}

sealed class OverviewScenePublisher : Forms.NativeWindow,IDisposable {
    int target=-1,sequence;
    internal OverviewScenePublisher(){CreateHandle(new Forms.CreateParams{Caption=OverviewSceneNative.StateTitle,Style=unchecked((int)0x80000000),ExStyle=0x08000080});Transition(0,0);}
    internal void Transition(int value,int milliseconds){
        if(target==value)return;target=value;
        OverviewSceneNative.SetProp(Handle,OverviewSceneNative.Target,new IntPtr(value+1));
        OverviewSceneNative.SetProp(Handle,OverviewSceneNative.Duration,new IntPtr(Math.Max(0,Math.Min(600,milliseconds))));
        OverviewSceneNative.SetProp(Handle,OverviewSceneNative.Started,new IntPtr(Environment.TickCount));
        OverviewSceneNative.SetProp(Handle,OverviewSceneNative.Sequence,new IntPtr(++sequence));
        IntPtr next=IntPtr.Zero;
        while((next=OverviewSceneNative.FindWindowEx(IntPtr.Zero,next,null,OverviewSceneNative.ListenerTitle))!=IntPtr.Zero){
            if(OverviewSceneNative.ProcessIs(next,"Clean11.Dock")||OverviewSceneNative.ProcessIs(next,"Clean11.Widgets"))OverviewSceneNative.PostMessage(next,OverviewSceneNative.Message,IntPtr.Zero,IntPtr.Zero);
        }
    }
    public void Dispose(){Transition(0,0);DestroyHandle();}
}

sealed class OverviewSceneReceiver : FrameworkElement,IDisposable {
    sealed class Listener : Forms.NativeWindow,IDisposable {
        readonly Action changed;
        internal Listener(Action action){changed=action;CreateHandle(new Forms.CreateParams{Caption=OverviewSceneNative.ListenerTitle,Style=unchecked((int)0x80000000),ExStyle=0x08000080});}
        protected override void WndProc(ref Forms.Message message){if(message.Msg==OverviewSceneNative.Message)changed();base.WndProc(ref message);}
        public void Dispose(){DestroyHandle();}
    }
    static readonly DependencyProperty ProgressProperty=DependencyProperty.Register("Progress",typeof(double),typeof(OverviewSceneReceiver),new PropertyMetadata(0.0,(o,e)=>{var receiver=(OverviewSceneReceiver)o;if(!receiver.disposed)receiver.apply((double)e.NewValue);}));
    readonly Action<double> apply;readonly Action<bool> prepare;readonly Func<bool> reduced;readonly Listener listener;
    readonly DispatcherTimer recovery=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
    IntPtr source;int sequence=-1,target;bool disposed;
    internal OverviewSceneReceiver(Action<double> action,Func<bool> reducedMotion,Action<bool> preparing=null){
        apply=action;prepare=preparing;reduced=reducedMotion;listener=new Listener(Read);recovery.Tick+=(s,e)=>Read();recovery.Start();Read();
    }
    void Read(){
        if(disposed)return;
        var window=OverviewSceneNative.FindWindow(null,OverviewSceneNative.StateTitle);
        if(window==IntPtr.Zero){if(source!=IntPtr.Zero){source=IntPtr.Zero;sequence=-1;Animate(0,160);}return;}
        if(window!=source){if(!OverviewSceneNative.ProcessIs(window,"ActivitiesOverview"))return;source=window;sequence=-1;}
        int next=OverviewSceneNative.GetProp(window,OverviewSceneNative.Sequence).ToInt32();if(next==sequence)return;
        int goal=OverviewSceneNative.GetProp(window,OverviewSceneNative.Target).ToInt32()-1;
        int duration=OverviewSceneNative.GetProp(window,OverviewSceneNative.Duration).ToInt32();
        int elapsed=unchecked(Environment.TickCount-OverviewSceneNative.GetProp(window,OverviewSceneNative.Started).ToInt32());
        if(next!=OverviewSceneNative.GetProp(window,OverviewSceneNative.Sequence).ToInt32()||goal<0||goal>1||duration<0||duration>600)return;
        sequence=next;Animate(goal,Math.Max(0,duration-Math.Max(0,elapsed)));
    }
    void Animate(int goal,int duration){
        target=goal;double from=(double)GetValue(ProgressProperty);
        if(prepare!=null)prepare(goal==1);
        if(duration==0||reduced()||!Forms.SystemInformation.UIEffectsEnabled){BeginAnimation(ProgressProperty,null);SetValue(ProgressProperty,(double)target);return;}
        var animation=new DoubleAnimation(from,target,TimeSpan.FromMilliseconds(duration)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
        BeginAnimation(ProgressProperty,animation,HandoffBehavior.SnapshotAndReplace);SetValue(ProgressProperty,(double)target);
    }
    public void Dispose(){disposed=true;recovery.Stop();BeginAnimation(ProgressProperty,null);listener.Dispose();}
}
