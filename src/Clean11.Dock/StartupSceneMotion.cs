using System;
using System.Diagnostics;
using System.Windows.Media;

// A short, compositor-paced entrance shared by the dock and its related bars.
sealed class StartupSceneMotion : IDisposable {
    readonly Action<double> update;readonly Action completed;readonly int duration;
    readonly Stopwatch clock=new Stopwatch();readonly EventHandler frame;
    bool disposed;
    internal StartupSceneMotion(Action<double> apply,Action finish,int milliseconds=420){
        update=apply;completed=finish;duration=Math.Max(160,Math.Min(700,milliseconds));frame=OnFrame;
    }
    internal void Start(){if(disposed)return;clock.Restart();update(0);CompositionTarget.Rendering+=frame;}
    void OnFrame(object sender,EventArgs args){
        if(disposed)return;
        double t=Math.Min(1,clock.Elapsed.TotalMilliseconds/duration),eased=1-Math.Pow(1-t,3);
        update(eased);
        if(t>=1){Dispose();if(completed!=null)completed();}
    }
    public void Dispose(){if(disposed)return;disposed=true;CompositionTarget.Rendering-=frame;clock.Stop();}
}
