using System;
using IntercomMvp;

internal static class Program
{
    private static int cases, assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Near(float actual, float expected, string message, float tolerance = 0.00002f)
    { Check(Math.Abs(actual - expected) <= tolerance, message + ": expected " + expected + ", got " + actual); }
    private static void Project(float vx, float vz, float fx, float fz, float x, float y, string label)
    {
        Check(IntercomLocomotionDirection.TryProject(vx, vz, fx, fz, out float gotX, out float gotY), label + " moved");
        Near(gotX, x, label + " X"); Near(gotY, y, label + " Y");
        Near(gotX * gotX + gotY * gotY, 1f, label + " magnitude");
    }
    private static void Case(string name, Action body) { body(); cases++; Console.WriteLine("PASS " + name); }
    private static void Main()
    {
        Case("four visual headings, four world movement axes", () =>
        {
            // Columns are world +Z,+X,-Z,-X; each row is the actor's visual forward.
            float[,] expectedX = { {0,1,0,-1}, {-1,0,1,0}, {0,-1,0,1}, {1,0,-1,0} };
            float[,] expectedY = { {1,0,-1,0}, {0,1,0,-1}, {-1,0,1,0}, {0,-1,0,1} };
            float[] x = {0,1,0,-1}, z = {1,0,-1,0};
            for (int facing = 0; facing < 4; facing++)
            for (int movement = 0; movement < 4; movement++)
                Project(x[movement], z[movement], x[facing], z[facing], expectedX[facing,movement], expectedY[facing,movement], "heading " + facing + " movement " + movement);
        });
        Case("authored model -Z forward is not wrapper +Z", () =>
        {
            Project(0,-2,0,-1,0,1,"modelYaw180 forward");
            Project(0,2,0,-1,0,-1,"modelYaw180 backward");
            Project(2,0,0,-1,-1,0,"modelYaw180 left");
            Project(-2,0,0,-1,1,0,"modelYaw180 right");
        });
        Case("all diagonal signs", () =>
        {
            float d = (float)Math.Sqrt(0.5);
            Project(1,1,0,1,d,d,"NE"); Project(-1,1,0,1,-d,d,"NW");
            Project(1,-1,0,1,d,-d,"SE"); Project(-1,-1,0,1,-d,-d,"SW");
        });
        Case("diagonal heading with wall-slide movement", () =>
        {
            float d = (float)Math.Sqrt(0.5);
            // A character aiming northeast, blocked on X, actually slides north.
            Project(0,0.8f,d,d,-d,d,"north slide");
            Check(!IntercomLocomotionDirection.TryProject(0,0,d,d,out _,out _),"solid wall yields no running direction");
        });
        Case("analog speed remains independent of direction", () =>
        {
            Project(0.02f,0,0,1,1,0,"slow right");
            Project(80f,0,0,1,1,0,"fast right");
            Project(0,0.25f,0,9f,0,1,"nonunit facing normalized");
        });
        Case("zero and numerical jitter are not movement", () =>
        {
            Check(!IntercomLocomotionDirection.TryProject(0,0,0,1,out _,out _),"stopped");
            Check(!IntercomLocomotionDirection.TryProject(0.001f,0.001f,0,1,out _,out _),"jitter ignored");
            Check(!IntercomLocomotionDirection.TryProject(0.8f,0,0,0,out _,out _),"degenerate facing ignored");
        });
        Case("nonfinite movement cannot poison animator parameters", () =>
        {
            Check(!IntercomLocomotionDirection.TryProject(float.NaN,1,0,1,out _,out _),"NaN velocity");
            Check(!IntercomLocomotionDirection.TryProject(1,float.PositiveInfinity,0,1,out _,out _),"infinite velocity");
            Check(!IntercomLocomotionDirection.TryProject(1,0,float.NaN,1,out _,out _),"NaN facing");
        });
        Case("zero smoothing applies requested direction immediately", () =>
        {
            IntercomLocomotionDirection.Smooth(0,1,-1,0,1f/60f,0,out float x,out float y);
            Near(x,-1,"instant X"); Near(y,0,"instant Y");
        });
        Case("paused time preserves direction", () =>
        {
            IntercomLocomotionDirection.Smooth(1,0,0,1,0,0.06f,out float x,out float y);
            Near(x,1,"paused X"); Near(y,0,"paused Y");
        });
        Case("180-degree reversal never blends through undefined origin", () =>
        {
            float x=0,y=1;
            for(int frame=0;frame<90;frame++)
            {
                IntercomLocomotionDirection.Smooth(x,y,0,-1,1f/60f,0.06f,out x,out y);
                Near(x*x+y*y,1,"reversal unit vector");
            }
            Near(x,0,"reversal settles X"); Near(y,-1,"reversal settles Y");
        });
        Case("angle wrap takes short route across zero", () =>
        {
            float a=(float)(Math.PI/180);
            IntercomLocomotionDirection.Smooth(-(float)Math.Sin(a),(float)Math.Cos(a),
                (float)Math.Sin(a),(float)Math.Cos(a),0.06f*(float)Math.Log(2),0.06f,out float x,out float y);
            Near(x,0,"midpoint across wrap"); Near(y,1,"wrap faces forward");
        });
        Case("smoothing stable across frame rates", () =>
        {
            float expectedX=0,expectedY=0;
            foreach(int fps in new[]{30,60,144})
            {
                float x=0,y=1;
                for(int frame=0;frame<fps;frame++)
                    IntercomLocomotionDirection.Smooth(x,y,1,0,1f/fps,0.3f,out x,out y);
                if(fps==30){expectedX=x;expectedY=y;}
                else {Near(x,expectedX,"fps X",0.00003f);Near(y,expectedY,"fps Y",0.00003f);}
            }
        });
        Case("normal analogue movement retains full measured stride", () =>
        {
            foreach (float speed in new[] { .45f, .9f, 1.35f, 1.8f })
            {
                float blend = IntercomLocomotionCadence.BlendWeight(speed, 1.8f, .45f);
                Near(blend, 1f, "full stride at " + speed);
                float rate = IntercomLocomotionCadence.PlaybackRate(speed, 1.2f, blend);
                // Measured authored cycle: 0.8m of travel over 16/24 seconds.
                Near(.8f / ((16f / 24f) / rate), speed, "stride travel matches motor");
            }
            Near(IntercomLocomotionCadence.PlaybackRate(1.8f, 1.2f, 1f) * 3f, 4.5f, "full-speed contacts/sec");
        });
        Case("idle cadence and low-speed transition remain separate", () =>
        {
            Near(IntercomLocomotionCadence.BlendWeight(0f, 1.8f, .45f), 0f, "idle blend");
            Near(IntercomLocomotionCadence.PlaybackRate(0f, 1.2f, 1f), 1f, "idle remains authored rate even while blend decays");
            Near(IntercomLocomotionCadence.BlendWeight(.225f, 1.8f, .45f), .5f, "transition only at low travel speed");
            Near(IntercomLocomotionCadence.PlaybackRate(.9f, 1.2f, 1f), .75f, "half stick cadence");
            Near(IntercomLocomotionCadence.PlaybackRate(.9f, 1.2f, 0f), 1f, "zero blend retains idle cadence");
        });
        Case("blend threshold remains reachable after designer speed changes", () =>
        {
            Near(IntercomLocomotionCadence.BlendWeight(.2f, .2f, .45f), 1f, "slow motor reaches full stride");
            Near(IntercomLocomotionCadence.BlendWeight(.1f, 1.8f, -1f), 1f, "positive threshold floor");
            Near(IntercomLocomotionCadence.BlendWeight(float.NaN, 1.8f, .45f), 0f, "invalid speed");
            Near(IntercomLocomotionCadence.PlaybackRate(float.PositiveInfinity, 1.2f, 1f), 1f, "invalid rate source");
            Near(IntercomLocomotionCadence.PlaybackRate(.9f, 1.2f, float.NaN), 1f, "invalid blend");
        });
        Case("current measured gait reference and fallback agree", () =>
        {
            Near(IntercomLocomotionCadence.PlaybackRate(2.1f, 1.0f, 1f) * 3f, 6.3f, "current full-speed contacts/sec");
            Near(IntercomLocomotionCadence.PlaybackRate(1.8f, float.NaN, 1f), 1.8f, "invalid reference uses current measured gait");
        });
        Case("V5 longer rigid stroke and cadence share the same reference", () =>
        {
            float scale=IntercomLocomotionCadence.SafeStrideScale(1.32f);
            Near(IntercomLocomotionCadence.PlaybackRate(3.3f,1f*scale,1f),2.5f,"full forward playback");
            Near(IntercomLocomotionCadence.PlaybackRate(3.3f,1f*scale,1f)*3f,7.5f,"V5 contact frequency");
            Near(IntercomLocomotionCadence.PlaybackRate(3.3f*.75f,.75f*scale,1f),2.5f,"side stride uses identical cadence");
            Near(IntercomLocomotionCadence.SafeStrideScale(float.NaN),1f,"invalid scale keeps authored stride");
            Near(IntercomLocomotionCadence.SafeStrideScale(.1f),1f,"shortening cannot erase the gait");
            Near(IntercomLocomotionCadence.SafeStrideScale(20f),1.4f,"rigid reach scale stays bounded");
        });
        Case("shorter lateral stride preserves travel direction and bounded cadence", () =>
        {
            foreach(float angle in new[]{0f,45f,90f,135f,180f,225f,270f,315f})
            {
                float a=angle*(float)Math.PI/180f, dx=(float)Math.Sin(a),dy=(float)Math.Cos(a);
                IntercomLocomotionCadence.AnimationDirection(dx,dy,.75f,out float x,out float y);
                float reference=IntercomLocomotionCadence.DirectionalReference(x,y,1f,.75f);
                Near(x*x+y*y,1f,"unit animation parameter");
                Near(x*.75f/reference,dx,"physical X follows requested heading");
                Near(y/reference,dy,"physical Y follows requested heading");
                Near(IntercomLocomotionCadence.PlaybackRate(2.1f*reference,reference,1f)*3f,6.3f,"same maximum contact cadence");
                float gain=Math.Abs(x)+Math.Abs(y);
                Near((x*.75f/gain)*gain,reference*dx,"diamond-corrected stride X");
                Near((y/gain)*gain,reference*dy,"diamond-corrected stride Y");
            }
            IntercomLocomotionCadence.AnimationDirection(1,1,.75f,out float diagonalX,out float diagonalY);
            Near(diagonalX,.8f,"45-degree lateral parameter");Near(diagonalY,.6f,"45-degree forward parameter");
            Near(IntercomLocomotionCadence.DirectionalReference(1,0,1,.75f)*2.1f,1.575f,"lateral speed limit");
            Near(IntercomLocomotionCadence.DirectionalReference(diagonalX,diagonalY,1,.75f)*2.1f,1.781909f,"diagonal speed limit");
            Near(IntercomLocomotionCadence.DirectionalReference(float.NaN,1,1,.75f),1f,"invalid params fallback");
        });
        Console.WriteLine("PASS: " + cases + " cases / " + assertions + " assertions. Production direction and cadence; .NET 8, no Unity engine playback.");
    }
}
