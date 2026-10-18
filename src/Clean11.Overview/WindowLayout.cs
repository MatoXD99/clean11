using System;
using System.Drawing;

class PreviewLayout {
    internal Rectangle[] Previews,Footprints;
    internal double Scale;
}

static class WindowLayout {
    internal static Size SourceSize(Size source,Size original) {
        if(source.Width>0&&source.Height>0)return source;
        if(original.Width>0&&original.Height>0)return original;
        return new Size(640,480);
    }
    // Every surface uses one scale. Packing changes position, never relative size
    // or aspect ratio. Caption and close-button space live outside the surface.
    internal static PreviewLayout Arrange(Size[] sources,Rectangle area,int gap,int caption,int closeGutter,int maxPerRow) {
        var result=new PreviewLayout{Previews=new Rectangle[sources.Length],Footprints=new Rectangle[sources.Length]};
        if(sources.Length==0)return result;
        area.Inflate(-3,-3);area.Width=Math.Max(1,area.Width);area.Height=Math.Max(1,area.Height);
        int maxRows=Math.Max(1,(area.Height+gap)/Math.Max(1,caption+gap+1));
        maxPerRow=Math.Max(maxPerRow,(sources.Length+maxRows-1)/maxRows);
        int[] breaks;double minimum=0,maximum=.78;
        for(int iteration=0;iteration<34;iteration++) {
            double scale=(minimum+maximum)/2;
            if(Pack(sources,area.Width,gap,caption,closeGutter,maxPerRow,scale,out breaks)<=area.Height)minimum=scale;
            else maximum=scale;
        }
        result.Scale=minimum;int height=Pack(sources,area.Width,gap,caption,closeGutter,maxPerRow,minimum,out breaks);
        int y=area.Top+Math.Max(0,(area.Height-height)/2);
        for(int start=0;start<sources.Length;) {
            int end=breaks[start];if(end<=start)end=start+1;
            int rowWidth=0,rowHeight=0;
            for(int i=start;i<end;i++){Size size=Scaled(sources[i],minimum);rowWidth+=size.Width+closeGutter+(i>start?gap:0);rowHeight=Math.Max(rowHeight,size.Height+caption);}
            int x=area.Left+Math.Max(0,(area.Width-rowWidth)/2);
            for(int i=start;i<end;i++) {
                Size size=Scaled(sources[i],minimum);int top=y+(rowHeight-caption-size.Height)/2;
                result.Previews[i]=new Rectangle(x,top,size.Width,size.Height);
                result.Footprints[i]=new Rectangle(x,top,size.Width+closeGutter,size.Height+caption);
                x+=size.Width+closeGutter+gap;
            }
            y+=rowHeight+gap;start=end;
        }
        return result;
    }
    static Size Scaled(Size source,double scale) {
        source=SourceSize(source,Size.Empty);
        return new Size(Math.Max(1,(int)Math.Round(source.Width*scale)),Math.Max(1,(int)Math.Round(source.Height*scale)));
    }
    static int Pack(Size[] sources,int width,int gap,int caption,int gutter,int maxPerRow,double scale,out int[] breaks) {
        int n=sources.Length;breaks=new int[n];var heights=new int[n+1];heights[n]=0;
        for(int i=n-1;i>=0;i--) {
            heights[i]=Int32.MaxValue/4;int rowWidth=0,rowHeight=0;
            for(int j=i;j<n&&j<i+maxPerRow;j++) {
                Size size=Scaled(sources[j],scale);rowWidth+=size.Width+gutter+(j>i?gap:0);
                if(rowWidth>width)break;
                rowHeight=Math.Max(rowHeight,size.Height+caption);
                int candidate=rowHeight+(j<n-1?gap:0)+heights[j+1];
                if(candidate<=heights[i]){heights[i]=candidate;breaks[i]=j+1;}
            }
        }
        return heights[0];
    }
}
