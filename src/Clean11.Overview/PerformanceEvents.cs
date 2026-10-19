using System;
using System.Diagnostics.Tracing;

// Timing markers contain no queries, titles, paths, or user identifiers.
[EventSource(Name="Clean11-Performance",Guid="415FBC52-EA39-4F51-B9D8-C6A4681D7402")]
sealed class PerformanceEvents : EventSource {
    internal static readonly PerformanceEvents Log=new PerformanceEvents();
    [Event(1,Level=EventLevel.Informational)] public void TransitionBegin(string area){if(IsEnabled())WriteEvent(1,area);}
    [Event(2,Level=EventLevel.Verbose)] public void Frame(string area,double progress,double workMilliseconds){if(IsEnabled())WriteEvent(2,area,progress,workMilliseconds);}
    [Event(3,Level=EventLevel.Informational)] public void TransitionEnd(string area,double milliseconds){if(IsEnabled())WriteEvent(3,area,milliseconds);}
    [Event(4,Level=EventLevel.Informational)] public void Input(string action){if(IsEnabled())WriteEvent(4,action);}
}
