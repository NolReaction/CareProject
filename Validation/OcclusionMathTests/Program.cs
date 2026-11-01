using System;
using IntercomMvp;

internal static class Program
{
    private static int assertions;
    private static void Check(bool value, string name)
    {
        assertions++;
        if (!value) throw new Exception(name);
    }
    private static IntercomOcclusionMath.Projection Box(float x,float y,float z,float hx,float hy,float hz,bool ortho=true)
    {
        Check(IntercomOcclusionMath.ProjectBox(x,y,z,hx,hy,hz,ortho,ortho?4.6f:0.57735f,16f/9f,.05f,out var p),"box projects");
        return p;
    }
    public static void Main()
    {
        var player=Box(0,0,12,.5f,.8f,.5f);
        Check(IntercomOcclusionMath.Blocks(Box(0,0,5,1,2,1),player),"foreground house hides");
        Check(!IntercomOcclusionMath.Blocks(Box(0,0,15,1,2,1),player),"background house stays");
        Check(!IntercomOcclusionMath.Blocks(Box(5,0,5,1,2,1),player),"house beside silhouette stays");
        Check(!IntercomOcclusionMath.Blocks(Box(0,5,5,1,1,1),player),"house above silhouette stays");
        Check(IntercomOcclusionMath.Blocks(Box(.52f,0,5,.05f,.05f,.05f),player),"thin edge cover caught");
        Check(IntercomOcclusionMath.Blocks(Box(0,-.82f,5,.5f,.05f,1),player),"feet cover caught");
        Check(IntercomOcclusionMath.Blocks(Box(0,.82f,5,.5f,.05f,1),player),"head cover caught");
        var left=Box(-1,0,5,.2f,.2f,.2f);var right=Box(1,0,5,.2f,.2f,.2f);
        Check(!IntercomOcclusionMath.Blocks(left,player)&&!IntercomOcclusionMath.Blocks(right,player),"gap between parts stays clear");
        Check(IntercomOcclusionMath.Blocks(Box(0,0,2,.2f,.2f,.2f),player)
            &&IntercomOcclusionMath.Blocks(Box(0,0,7,.2f,.2f,.2f),player),"all foreground layers hide");
        Check(!IntercomOcclusionMath.ProjectBox(0,0,-2,1,1,.5f,true,4.6f,1.777f,.05f,out _),"behind camera rejected");
        Check(!IntercomOcclusionMath.ProjectBox(float.NaN,0,5,1,1,1,true,4.6f,1.777f,.05f,out _),"invalid coordinates rejected");
        Check(IntercomOcclusionMath.Blocks(Box(0,0,.02f,.2f,.2f,.2f,false),Box(0,0,12,.5f,.8f,.5f,false)),"near-plane crossing protects target");
        // Orthographic view rays must be parallel: a distant off-centre
        // obstacle cannot cover the centre just because a perspective ray
        // from the camera origin happens to cross its position.
        Check(!IntercomOcclusionMath.Blocks(Box(1,0,6,.1f,.1f,.1f),player),"parallel ortho rays preserve offset");
        Check(IntercomOcclusionMath.Blocks(Box(.25f,0,6,.1f,.1f,.1f,false),Box(.5f,0,12,.2f,.8f,.5f,false)),"perspective aligned silhouettes cover");
        // Independent ground truth: project dense points from random boxes.
        // Every projected point must lie inside the production projection.
        var random=new Random(14627);
        int boxes=0,points=0;
        for(int mode=0;mode<2;mode++)
        for(int n=0;n<1200;n++)
        {
            float x=(float)(random.NextDouble()*20-10),y=(float)(random.NextDouble()*12-6),z=(float)(random.NextDouble()*24-1);
            float hx=(float)(random.NextDouble()*3),hy=(float)(random.NextDouble()*4),hz=(float)(random.NextDouble()*3);
            bool ortho=mode==0;
            bool valid=IntercomOcclusionMath.ProjectBox(x,y,z,hx,hy,hz,ortho,ortho?4.6f:.57735f,16f/9f,.05f,out var projection);
            boxes++;
            for(int ix=0;ix<4;ix++)for(int iy=0;iy<4;iy++)for(int iz=0;iz<4;iz++)
            {
                float px=x+hx*(ix/1.5f-1),py=y+hy*(iy/1.5f-1),pz=z+hz*(iz/1.5f-1);
                if(pz<.05f)continue;
                Check(valid,"visible point must have valid box");
                float sx=px/(ortho?4.6f*16/9:pz*.57735f*16/9),sy=py/(ortho?4.6f:pz*.57735f);
                const float epsilon=.0001f;
                Check(sx>=projection.minX-epsilon&&sx<=projection.maxX+epsilon&&sy>=projection.minY-epsilon&&sy<=projection.maxY+epsilon,"projected sample contained");
                points++;
            }
        }
        Console.WriteLine($"PASS: 14 geometry scenarios; {boxes} randomized boxes / {points} independently projected points; {assertions} assertions. Production pure math, not Unity rendering.");
    }
}
