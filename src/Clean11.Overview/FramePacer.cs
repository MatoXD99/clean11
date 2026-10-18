using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

sealed class FramePacer : IDisposable {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateWaitableTimerEx(IntPtr attributes,string name,uint flags,uint access);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetWaitableTimer(IntPtr timer,ref long dueTime,int period,IntPtr completion,IntPtr argument,bool resume);
    [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    [DllImport("winmm.dll")]static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")]static extern uint timeEndPeriod(uint period);
    IntPtr timer;bool fallbackPeriod;
    internal bool HighResolution {get{return timer!=IntPtr.Zero;}}
    internal FramePacer() {
        timer=CreateWaitableTimerEx(IntPtr.Zero,null,2,0x1F0003);
        if(timer==IntPtr.Zero)fallbackPeriod=timeBeginPeriod(1)==0;
    }
    internal void WaitUntil(Stopwatch clock,double deadline,CancellationToken token) {
        if(token.IsCancellationRequested)return;
        double remaining=deadline-clock.Elapsed.TotalMilliseconds;if(remaining<=0)return;
        if(timer!=IntPtr.Zero) {
            long due=-Math.Max(1,(long)(remaining*10000));
            if(SetWaitableTimer(timer,ref due,0,IntPtr.Zero,IntPtr.Zero,false)){WaitForSingleObject(timer,(uint)Math.Ceiling(remaining+5));return;}
        }
        token.WaitHandle.WaitOne(Math.Max(1,(int)Math.Ceiling(remaining)));
    }
    internal double LastPresentWaitMs {get;private set;}
    internal bool WaitForPresentedFrame(Stopwatch clock,double frameStarted,double period,CancellationToken token,Func<int> present) {
        LastPresentWaitMs=0;if(token.IsCancellationRequested)return false;
        double started=clock.Elapsed.TotalMilliseconds;bool success=present()==0;
        LastPresentWaitMs=clock.Elapsed.TotalMilliseconds-started;
        // The deadline is measured from submission, not from the end of DwmFlush.
        // A six-ms present wait must not be followed by another six-ms sleep.
        WaitUntil(clock,frameStarted+period,token);
        return success;
    }
    public void Dispose(){if(timer!=IntPtr.Zero){CloseHandle(timer);timer=IntPtr.Zero;}if(fallbackPeriod){timeEndPeriod(1);fallbackPeriod=false;}}
}
