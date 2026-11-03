using System;
using IntercomMvp;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Near(double x, double y, double tolerance, string message) => Check(Math.Abs(x-y) <= tolerance, $"{message}: {x} vs {y}");
    private static IntercomCameraTracking.Settings Defaults() => new IntercomCameraTracking.Settings
    {
        freeMovement=true, recenterWhenIdle=true, zoneWidth=.18, zoneHeight=.14,
        safetyWidth=.65, safetyHeight=.55, followSeconds=.3, recenterDelay=.85, recenterSeconds=1,
        idleSpeed=.04, anticipationDistance=.15, anticipationSeconds=.22, teleportDistance=8
    };
    private static double[] Rotate(double x, double y, double z, double pitch, double yaw, double roll)
    {
        double a=roll*Math.PI/180, b=pitch*Math.PI/180, c=yaw*Math.PI/180;
        double x1=x*Math.Cos(a)-y*Math.Sin(a), y1=x*Math.Sin(a)+y*Math.Cos(a);
        double y2=y1*Math.Cos(b)-z*Math.Sin(b), z2=y1*Math.Sin(b)+z*Math.Cos(b);
        return new[]{x1*Math.Cos(c)+z2*Math.Sin(c),y2,-x1*Math.Sin(c)+z2*Math.Cos(c)};
    }
    private static IntercomCameraTracking.Projection Projection(bool ortho=true, double pitch=35, double yaw=10, double roll=0, double aspect=16d/9)
    {
        var r=Rotate(1,0,0,pitch,yaw,roll); var u=Rotate(0,1,0,pitch,yaw,roll); var f=Rotate(0,0,1,pitch,yaw,roll);
        return new IntercomCameraTracking.Projection
        {
            rightX=r[0],rightY=r[1],rightZ=r[2],upX=u[0],upY=u[1],upZ=u[2],forwardX=f[0],forwardY=f[1],forwardZ=f[2],
            halfHeight=ortho?4.6:12*Math.Tan(Math.PI/6),distance=12,aspect=aspect,orthographic=ortho
        };
    }
    private static void Main()
    {
        ProjectionCases(); FreeZoneAndDamping(); RecenterAndLifecycle(); FrameRatesAndSafety(); ArcadeResponse(); AdaptiveRunning(); ImmediateReversalBraking();
        Console.WriteLine($"Camera tracking: {checks} assertions PASS. Production ground-plane projection/tracking math; no Unity Editor or rendering execution.");
    }
    private static void ProjectionCases()
    {
        var random=new Random(1194);
        foreach(bool ortho in new[]{true,false})
        foreach(double pitch in new[]{20d,35,60,89})
        foreach(double yaw in new[]{0d,10,75,180})
        foreach(double roll in new[]{0d,12})
        foreach(double aspect in new[]{4d/3,16d/9,20d/9})
        {
            var p=Projection(ortho,pitch,yaw,roll,aspect);
            for(int i=0;i<40;i++)
            {
                double x=(random.NextDouble()-.5)*24, z=(random.NextDouble()-.5)*24, fx=2, fz=-3;
                bool visible=IntercomCameraTracking.Project(x-fx,z-fz,p,out double nx,out double ny);
                bool inside=visible && Math.Abs(nx)<=.18 && Math.Abs(ny)<=.14;
                IntercomCameraTracking.Constrain(x,z,ref fx,ref fz,p,.18,.14);
                Check(IntercomCameraTracking.Project(x-fx,z-fz,p,out nx,out ny),"constrained anchor projects in front of camera");
                Check(Math.Abs(nx)<=.180000001 && Math.Abs(ny)<=.140000001,"dead zone respected for pitch/yaw/roll/aspect/projection");
                if(inside) { Near(fx,2,1e-12,"inside zone leaves camera x"); Near(fz,-3,1e-12,"inside zone leaves camera z"); }
            }
        }
        var horizontal=Projection(true,0); double ax=0,az=0;
        IntercomCameraTracking.Constrain(3,4,ref ax,ref az,horizontal,.18,.14);
        Near(ax,3,1e-12,"horizontal singularity safely follows target x"); Near(az,4,1e-12,"horizontal singularity safely follows target z");
    }
    private static void FreeZoneAndDamping()
    {
        var t=new IntercomCameraTracking(); var s=Defaults(); var p=Projection(); t.Reset(0,0);
        for(int i=1;i<=24;i++) t.Advance(2.1*i/60,0,1,0,1d/60,s,p);
        Near(t.FocusX,0,1e-9,"first .4 seconds of horizontal movement is free");
        Near(t.FocusZ,0,1e-9,"anticipation cannot move camera before crossing free zone");
        double previous=0;
        for(int i=25;i<=240;i++)
        {
            t.Advance(2.1*i/60,0,1,0,1d/60,s,p);
            Check(t.FocusX>=previous-1e-8,"camera catches forward movement monotonically");
            Check(t.FocusX-previous<.06,"no jump when crossing dead-zone border");
            previous=t.FocusX;
        }
        Check(t.FocusX>5 && t.FocusX<8,"camera followed without locking target to center");
        IntercomCameraTracking.Project(8.4-t.FocusX,-t.FocusZ,p,out double nx,out double ny);
        Check(nx>.15 && nx<.32,"running robot visibly off center");
        // Reverse: velocity is continuous; camera can briefly coast, but must reverse and settle.
        for(int i=1;i<=240;i++) t.Advance(8.4-2.1*i/60,0,-1,0,1d/60,s,p);
        Check(t.FocusX<3,"camera follows reversed travel");
        s.freeMovement=false; s.anticipationDistance=0; t.Reset(0,0);
        for(int i=1;i<=120;i++) t.Advance(2.1*i/60,0,1,0,1d/60,s,p);
        Check(t.FocusX>3.5,"legacy centered tracking remains selectable");
    }
    private static void RecenterAndLifecycle()
    {
        var s=Defaults(); var p=Projection(); var t=new IntercomCameraTracking(); t.Reset(0,0);
        for(int i=1;i<=240;i++) t.Advance(2.1*i/60,0,1,0,1d/60,s,p);
        for(int i=0;i<40;i++) { t.Advance(8.4,0,0,0,1d/60,s,p); Check(!t.Recentering,"idle recenter respects delay"); }
        for(int i=0;i<500;i++) t.Advance(8.4,0,0,0,1d/60,s,p);
        Check(t.Recentering,"idle recenter enabled after delay"); Near(t.FocusX,8.4,.00002,"idle returns authored x composition"); Near(t.FocusZ,0,.00002,"idle returns authored z composition");
        double frozenX=t.FocusX, frozenZ=t.FocusZ, idle=t.IdleSeconds;
        t.Advance(11,3,1,0,0,s,p); Near(t.FocusX,frozenX,0,"pause freezes camera"); Near(t.IdleSeconds,idle,0,"pause freezes idle timer");
        t.Advance(double.NaN,0,0,0,.1,s,p); Near(t.FocusX,frozenX,0,"invalid target ignored");
        t.Advance(100,20,1,0,1d/60,s,p); Near(t.FocusX,100,0,"teleport snaps x"); Near(t.FocusZ,20,0,"teleport snaps z");
        t.Advance(100.03,20,1,0,1d/60,s,p); Near(t.FocusX,100,1e-10,"no stale pre-teleport velocity");
        s.recenterWhenIdle=false; t.Reset(0,0);
        for(int i=1;i<=240;i++) t.Advance(2.1*i/60,0,1,0,1d/60,s,p);
        for(int i=0;i<600;i++) t.Advance(8.4,0,0,0,1d/60,s,p);
        Check(!t.Recentering && 8.4-t.FocusX>1,"disabled recenter holds off-center composition");
        t.ApplyBounds(5,0,5,0); Near(t.FocusX,5,0,"map bounds can override screen zone");
    }
    private static void FrameRatesAndSafety()
    {
        double reference=0;
        foreach(int fps in new[]{120,15,30,60,240})
        {
            var s=Defaults(); var p=Projection(); var t=new IntercomCameraTracking(); t.Reset(0,0);
            for(int i=1;i<=fps*4;i++) t.Advance(2.1*i/fps,0,1,0,1d/fps,s,p);
            if(fps==120) reference=t.FocusX;
            else Near(t.FocusX,reference,.08,"catch-up similar across 15-240 FPS");
            for(int i=0;i<fps*8;i++) t.Advance(8.4,0,0,0,1d/fps,s,p);
            Near(t.FocusX,8.4,.0001,"all frame rates settle idle");
            s.followSeconds=2; s.teleportDistance=100; t.Reset(0,0);
            for(int i=1;i<=fps*2;i++)
            {
                double x=i*15d/fps,z=i*10d/fps;
                t.Advance(x,z,1,1,1d/fps,s,p);
                IntercomCameraTracking.Project(x-t.FocusX,z-t.FocusZ,p,out double nx,out double ny);
                Check(Math.Abs(nx)<=s.safetyWidth+1e-8 && Math.Abs(ny)<=s.safetyHeight+1e-8,"outer zone prevents fast escape even with heavy damping");
            }
        }
    }
    private static void ArcadeResponse()
    {
        // V5 keeps screen freedom, but the new 3.3 m/s motor must not drag the
        // camera behind it. Compare production tracker output at identical speed.
        var old = Defaults(); var current = Defaults();
        current.zoneWidth=.16; current.zoneHeight=.12; current.followSeconds=.18;
        current.anticipationDistance=.28; current.anticipationSeconds=.14;
        current.recenterDelay=.65; current.recenterSeconds=.8;
        foreach(int fps in new[]{30,60,120,240})
        {
            var p=Projection(); var a=new IntercomCameraTracking(); var b=new IntercomCameraTracking();
            a.Reset(0,0); b.Reset(0,0);
            double previous=0;
            for(int i=1;i<=fps*3;i++)
            {
                double x=3.3*i/fps;
                a.Advance(x,0,1,0,1d/fps,old,p);
                b.Advance(x,0,1,0,1d/fps,current,p);
                if(x<1) Near(b.FocusX,0,1e-9,"arcade still permits initial free movement");
                Check(Math.Abs(b.FocusX-previous)<4d/fps,"arcade follow crosses zone without a snap");
                previous=b.FocusX;
                IntercomCameraTracking.Project(x-b.FocusX,-b.FocusZ,p,out double nx,out double ny);
                Check(Math.Abs(nx)<=current.safetyWidth && Math.Abs(ny)<=current.safetyHeight,"arcade remains inside screen safety zone");
            }
            Check(b.FocusX>a.FocusX+.5,"arcade camera has materially less lag at equal motor speed");
            Check(9.9-b.FocusX>.8,"arcade camera retains off-center character composition");
            // A stop does not instantly cancel existing camera inertia or teleport to the centre.
            double before=b.FocusX;
            for(int i=0;i<fps/2;i++) b.Advance(9.9,0,0,0,1d/fps,current,p);
            Check(!b.Recentering,"arcade idle delay avoids recenter on brief dodge pauses");
            Check(b.FocusX>=before,"camera does not recoil on stopping");
            for(int i=0;i<fps*6;i++) b.Advance(9.9,0,0,0,1d/fps,current,p);
            Near(b.FocusX,9.9,.0001,"arcade idle recovers the authored framing");
        }
    }

    private static IntercomCameraTracking.Settings AdaptiveDefaults()
    {
        var s=Defaults(); s.zoneWidth=.16; s.zoneHeight=.12; s.followSeconds=.18;
        s.anticipationDistance=.28; s.anticipationSeconds=.14;
        s.recenterDelay=.65; s.recenterSeconds=.8;
        s.adaptiveMotion=true; s.velocityFilterSeconds=.09; s.velocityLeadSeconds=.16;
        s.maximumLeadDistance=.65; s.runningSpeed=3.3; s.runningFollowSeconds=.12; s.reversalBrakeSeconds=.09;
        return s;
    }

    private static void AdaptiveRunning()
    {
        double reference=0;
        foreach(int fps in new[]{120,15,30,60,240})
        {
            var s=AdaptiveDefaults(); var p=Projection();
            var t=new IntercomCameraTracking(); var legacy=new IntercomCameraTracking();
            var old=s; old.adaptiveMotion=false; t.Reset(0,0); legacy.Reset(0,0);
            double previous=0;
            for(int i=1;i<=fps*3;i++)
            {
                double x=3.3*i/fps;
                // Deliberately stale/contradictory intent. Adaptive look is based on
                // real displacement so aiming left cannot pull it away from travel.
                t.Advance(x,0,-1,0,1d/fps,s,p);
                legacy.Advance(x,0,1,0,1d/fps,old,p);
                if(x<1) Near(t.FocusX,0,1e-9,"adaptive lead retains initial free movement");
                Check(t.FocusX>=previous-1e-8,"adaptive run follows monotonically");
                Check(t.FocusX-previous<4.8d/fps,$"adaptive crossing has no camera snap FPS={fps} frame={i} delta={t.FocusX-previous}");
                previous=t.FocusX;
            }
            Check(t.FocusX>legacy.FocusX+.35,"adaptive camera reduces run lag at unchanged hero speed");
            Check(9.9-t.FocusX>.6,"adaptive run still leaves hero off centre");
            Console.WriteLine($"Adaptive camera {fps} FPS: at 3.3 m/s, target-to-focus lag {9.9-legacy.FocusX:F3} -> {9.9-t.FocusX:F3} m; dead zone retained.");
            if(fps==120) reference=t.FocusX;
            else Near(t.FocusX,reference,.12,"adaptive run similar across 15-240 FPS");
            // A stop does not remove lead and pull the image backwards.
            for(int i=0;i<fps/2;i++)
            {
                t.Advance(9.9,0,1,0,1d/fps,s,p);
                Check(t.FocusX>=previous-1e-8,"adaptive stop has no anticipation recoil");
                previous=t.FocusX;
            }
            for(int i=0;i<fps*6;i++) t.Advance(9.9,0,1,0,1d/fps,s,p);
            Near(t.FocusX,9.9,.0002,"adaptive idle returns authored framing even with held intent");
            // Turning back must brake the previous camera travel; after a full
            // second it must already be travelling with the new direction.
            for(int i=1;i<=fps*3;i++) t.Advance(9.9+3.3*i/fps,0,1,0,1d/fps,s,p);
            double furthest=t.FocusX;
            for(int i=1;i<=fps;i++)
            {
                t.Advance(19.8-3.3*i/fps,0,-1,0,1d/fps,s,p);
                furthest=Math.Max(furthest,t.FocusX);
            }
            Check(t.FocusX<furthest-.15,"reversal brakes and changes camera travel within one second");
            double frozen=t.FocusX;
            t.Advance(100,20,0,0,0,s,p); Near(t.FocusX,frozen,0,"adaptive pause does not update motion history");
            t.Advance(100,20,0,0,1d/fps,s,p); Near(t.FocusX,100,0,"adaptive teleport clears anticipation");
            t.Advance(100.01,20,1,0,1d/fps,s,p); Near(t.FocusX,100,1e-10,"teleport leaves no stale running lead");
        }
        var cfg=AdaptiveDefaults(); var projection=Projection(); var wall=new IntercomCameraTracking(); wall.Reset(0,0);
        for(int i=0;i<600;i++) wall.Advance(0,0,1,1,1d/60,cfg,projection);
        Near(wall.FocusX,0,0,"blocked hero and held stick cannot move camera x");
        Near(wall.FocusZ,0,0,"blocked hero and held stick cannot move camera z");

        // In centred mode the maximal extra look may not exceed the authored
        // cap, even if the target is moving implausibly fast.
        cfg.freeMovement=false; cfg.followSeconds=.01; cfg.runningFollowSeconds=.01;
        cfg.anticipationSeconds=.01; cfg.teleportDistance=100;
        var capped=new IntercomCameraTracking(); capped.Reset(0,0);
        for(int i=1;i<=600;i++)
        {
            capped.Advance(i*.2,i*.2,1,1,1d/60,cfg,projection);
            double leadX=capped.FocusX-capped.AnchorX,leadZ=capped.FocusZ-capped.AnchorZ;
            Check(Math.Sqrt(leadX*leadX+leadZ*leadZ)<=cfg.maximumLeadDistance+.001,"diagonal prediction remains capped in world distance");
        }
        for(int i=0;i<120;i++) capped.Advance(120,120,1,1,1d/60,cfg,projection);
        Near(capped.FocusX,120,.00001,"centred mode releases running lead when stopped x");
        Near(capped.FocusZ,120,.00001,"centred mode releases running lead when stopped z");
        cfg=AdaptiveDefaults(); cfg.recenterWhenIdle=false;
        var parked=new IntercomCameraTracking(); parked.Reset(0,0);
        for(int i=1;i<=180;i++) parked.Advance(i*.055,0,1,0,1d/60,cfg,projection);
        for(int i=0;i<600;i++) parked.Advance(9.9,0,0,0,1d/60,cfg,projection);
        Check(9.9-parked.FocusX>.6 && !parked.Recentering,"no-recenter option preserves parked predictive framing");
    }

    private static void ImmediateReversalBraking()
    {
        foreach(int fps in new[]{30,60,120,240})
        foreach(double reverseAt in new[]{.48,.6,.8,2.0})
        {
            var s=AdaptiveDefaults(); var p=Projection();
            var quick=new IntercomCameraTracking(); var slow=new IntercomCameraTracking();
            var gentle=s; gentle.reversalBrakeSeconds=.18;
            quick.Reset(0,0); slow.Reset(0,0);
            int runFrames=(int)Math.Ceiling(fps*reverseAt);
            double x=0;
            for(int i=1;i<=runFrames;i++)
            {
                x=3.3*i/fps;
                quick.Advance(x,0,1,0,1d/fps,s,p);
                slow.Advance(x,0,1,0,1d/fps,gentle,p);
            }
            double startQuick=quick.FocusX,startSlow=slow.FocusX;
            double previous=quick.FocusX, previousDelta=double.MaxValue;
            for(int i=1;i<=Math.Ceiling(fps*.2);i++)
            {
                quick.Advance(x-3.3*i/fps,0,-1,0,1d/fps,s,p);
                slow.Advance(x-3.3*i/fps,0,-1,0,1d/fps,gentle,p);
                double delta=quick.FocusX-previous;
                Check(delta<=previousDelta+1e-9,"reversal never accelerates camera toward the stale forward goal");
                previousDelta=delta; previous=quick.FocusX;
            }
            Check(quick.FocusX-startQuick<slow.FocusX-startSlow-.001,"shorter reversal brake reduces old-direction travel immediately");
            Check(quick.FocusX-startQuick<.43,$"old-direction coast remains bounded FPS={fps} reverseAt={reverseAt} travel={quick.FocusX-startQuick}");
        }
    }
}
