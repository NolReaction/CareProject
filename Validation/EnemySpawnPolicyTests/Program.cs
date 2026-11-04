using System;
using IntercomMvp;

internal static class Program
{
    private static int checks;
    private static void Check(bool value,string label) { checks++; if(!value) throw new Exception(label); }
    private static void Main()
    {
        VisibleCadence(); Visibility(); Separation(); Cooldowns();
        Console.WriteLine("Enemy spawn policy: "+checks+" assertions PASS. Pure production policy, not Unity visibility or PhysX.");
    }
    private static void VisibleCadence()
    {
        for(int every=2;every<=8;every++)
        {
            int visible=0;
            for(int spawn=0;spawn<120;spawn++)
            {
                bool expected=spawn%every==every-1;
                bool actual=IntercomEnemySpawnPolicy.WantsVisible(spawn,every);
                Check(actual==expected,"visible emergence uses deterministic authored cadence");
                if(actual)visible++;
            }
            Check(visible==120/every,"visible cadence does not add surprise extra spawns");
        }
        for(int spawn=0;spawn<24;spawn++)
        {
            Check(IntercomEnemySpawnPolicy.WantsVisible(spawn,1)==IntercomEnemySpawnPolicy.WantsVisible(spawn,2),"minimum cadence keeps at least one ordinary arrival between visible arrivals");
            Check(IntercomEnemySpawnPolicy.WantsVisible(spawn,99)==IntercomEnemySpawnPolicy.WantsVisible(spawn,8),"maximum cadence remains bounded and readable");
        }
        Check(!IntercomEnemySpawnPolicy.WantsVisible(-1,3),"invalid negative life count cannot request a visible spawn");
        Check(!IntercomEnemySpawnPolicy.WantsVisible(0,0),"zero cadence disables authored visible arrivals");
        Check(!IntercomEnemySpawnPolicy.WantsVisible(1,-1),"negative cadence is disabled");
        for(int every=2;every<=8;every++)
            Check(IntercomEnemySpawnPolicy.WantsVisible(int.MaxValue,every)==(int.MaxValue%every==every-1),"cadence has no increment-overflow boundary");
    }
    private static void Visibility()
    {
        Check(IntercomEnemySpawnPolicy.IsOnScreen(640,360,1,1280,720),"forward viewport point is visible");
        foreach(var point in new[]{new[]{128f,130f},new[]{1152f,870f},new[]{1152f,130f},new[]{128f,870f}})
            Check(IntercomEnemySpawnPolicy.IsOnScreen(point[0],point[1],1,1280,1000),"readable arrival-region boundaries accepted");
        foreach(var point in new[]{new[]{127.99f,500f},new[]{1152.01f,500f},new[]{640f,129.99f},new[]{640f,870.01f},new[]{0f,0f},new[]{1280f,1000f}})
            Check(!IntercomEnemySpawnPolicy.IsOnScreen(point[0],point[1],1,1280,1000),"HUD/edge margin is not a readable arrival region");
        foreach(float depth in new[]{0f,-1f,float.NaN,float.PositiveInfinity})
            Check(!IntercomEnemySpawnPolicy.IsOnScreen(640,360,depth,1280,720),"invalid or behind-camera depth is not visible");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            Check(!IntercomEnemySpawnPolicy.IsOnScreen(invalid,360,1,1280,720),"nonfinite viewport x rejected");
            Check(!IntercomEnemySpawnPolicy.IsOnScreen(640,invalid,1,1280,720),"nonfinite viewport y rejected");
            Check(!IntercomEnemySpawnPolicy.IsOnScreen(640,360,1,invalid,720),"nonfinite display width rejected");
            Check(!IntercomEnemySpawnPolicy.IsOnScreen(640,360,1,1280,invalid),"nonfinite display height rejected");
        }
        Check(!IntercomEnemySpawnPolicy.IsOnScreen(0,0,1,0,720),"zero width is not a usable camera rectangle");
        Check(!IntercomEnemySpawnPolicy.IsOnScreen(0,0,1,1280,-1),"negative height rejected");
    }
    private static void Separation()
    {
        Check(!IntercomEnemySpawnPolicy.DistanceSafe(0,0,5.5f),"cannot emerge on the player");
        Check(IntercomEnemySpawnPolicy.DistanceSafe(3,4,5),"exact safe-distance boundary accepted");
        Check(!IntercomEnemySpawnPolicy.DistanceSafe(3,4,5.01f),"inside safety radius rejected");
        Check(IntercomEnemySpawnPolicy.DistanceSafe(-3,-4,5),"spacing does not depend on quadrant");
        Check(IntercomEnemySpawnPolicy.DistanceSafe(float.MaxValue,0,float.MaxValue),"distance math cannot overflow at finite float bounds");
        var random=new Random(1542);
        for(int i=0;i<4000;i++)
        {
            float dx=(float)(random.NextDouble()*80-40),dz=(float)(random.NextDouble()*80-40),minimum=(float)(random.NextDouble()*12);
            bool expected=(double)dx*dx+(double)dz*dz>=(double)minimum*minimum;
            Check(IntercomEnemySpawnPolicy.DistanceSafe(dx,dz,minimum)==expected,"spacing agrees with independent squared-distance geometry");
        }
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            Check(!IntercomEnemySpawnPolicy.DistanceSafe(invalid,0,5),"nonfinite x rejected");
            Check(!IntercomEnemySpawnPolicy.DistanceSafe(0,invalid,5),"nonfinite z rejected");
            Check(!IntercomEnemySpawnPolicy.DistanceSafe(10,0,invalid),"nonfinite safety distance rejected");
        }
        Check(!IntercomEnemySpawnPolicy.DistanceSafe(10,0,-1),"negative authored safety distance rejected");
    }
    private static void Cooldowns()
    {
        Check(IntercomEnemySpawnPolicy.CooldownReady(0,0),"new marker is available at valid zero time");
        Check(!IntercomEnemySpawnPolicy.CooldownReady(3.99f,4),"cooldown holds before deadline");
        Check(IntercomEnemySpawnPolicy.CooldownReady(4,4),"cooldown releases at exact deadline");
        Check(IntercomEnemySpawnPolicy.CooldownReady(120,4),"slow frame does not permanently lock marker");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            Check(!IntercomEnemySpawnPolicy.CooldownReady(invalid,4),"invalid current clock cannot release marker");
            Check(!IntercomEnemySpawnPolicy.CooldownReady(4,invalid),"invalid deadline is not treated as ready");
        }
        Check(IntercomEnemySpawnPolicy.NextReadyAt(10,-2)==10,"negative authored cooldown clamps to immediate availability");
        Check(IntercomEnemySpawnPolicy.NextReadyAt(10,100)==70,"extreme authored cooldown is bounded to one minute");
        Check(IntercomEnemySpawnPolicy.NextReadyAt(10,float.NaN)==14,"invalid cooldown uses a finite safe fallback");
        Check(IntercomEnemySpawnPolicy.NextReadyAt(float.NaN,4)==float.MaxValue,"invalid clock does not immediately reopen a marker");
        Check(IntercomEnemySpawnPolicy.NextReadyAt(float.MaxValue,60)==float.MaxValue,"deadline arithmetic cannot overflow into an immediately-ready negative value");
        for(int seconds=0;seconds<120;seconds++)
        {
            float next=IntercomEnemySpawnPolicy.NextReadyAt(seconds,4);
            Check(Math.Abs(next-seconds-4)<.00001f,"deadline is relative to actual activation time");
            Check(!IntercomEnemySpawnPolicy.CooldownReady(seconds+.1f,next),"one update cannot consume a whole cooldown");
            Check(IntercomEnemySpawnPolicy.CooldownReady(seconds+4,next),"cooldown eventually releases");
        }
    }
}
