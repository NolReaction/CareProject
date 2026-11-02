using System;
using IntercomMvp;

// Executes the production clock source, with no Unity or test-package dependency.
internal static class Program
{
    private static int assertions;
    private static int cases;
    private static void Main()
    {
        Case("tap waits for full raise, survives early release", () => {
            var c = Clock(); c.RequestFire(); Tick(c, .01f);
            Eq(c.Posture, IntercomWeaponPosture.Raising); False(c.ShotThisTick);
            Eq(Advance(c, .32f), 0); Eq(c.Posture, IntercomWeaponPosture.Raising);
            Eq(Advance(c, .02f), 1); Eq(c.Posture, IntercomWeaponPosture.Combat);
        });
        Case("held fire repeats only after interval", () => {
            var c = Clock(); int shots = Advance(c, 1.5f, true);
            Eq(shots, 3); Eq(c.Posture, IntercomWeaponPosture.Combat);
        });
        Case("release does not invent another queued automatic shot", () => {
            var c = Raised(); Tick(c, .1f, true); Eq(Advance(c, .8f), 0);
        });
        Case("new tap during cooldown is queued", () => {
            var c = Raised(); c.RequestFire(); Eq(Advance(c, .2f), 0);
            Eq(Advance(c, .21f), 1);
        });
        Case("combat hold is counted from the latest shot", () => {
            var c = Raised(); Advance(c, 1.7f); c.RequestFire(); Tick(c, .01f);
            True(c.ShotThisTick); Advance(c, 1.9f); Eq(c.Posture, IntercomWeaponPosture.Combat);
            Advance(c, .11f); Eq(c.Posture, IntercomWeaponPosture.Lowering);
            Advance(c, .51f); Eq(c.Posture, IntercomWeaponPosture.Relaxed);
        });
        Case("tap halfway down reverses and is not discarded", () => {
            var c = Raised(); Until(c, IntercomWeaponPosture.Lowering); Advance(c, .24f);
            c.RequestFire(); Tick(c, .01f); Eq(c.Posture, IntercomWeaponPosture.Raising);
            Near(c.RaiseStartNormalized, .5f, .025f); False(c.ShotThisTick);
            Eq(Advance(c, .18f), 1); Eq(c.Posture, IntercomWeaponPosture.Combat);
        });
        Case("held trigger immediately interrupts lowering", () => {
            var c = Raised(); Until(c, IntercomWeaponPosture.Lowering); Tick(c, .01f, true);
            Eq(c.Posture, IntercomWeaponPosture.Raising); True(c.RaiseStartNormalized > .95f);
            Eq(Advance(c, .02f, true), 1);
        });
        Case("large frame never creates a catch-up shot burst", () => {
            var c = Raised(); Tick(c, 20f, true); True(c.ShotThisTick);
            Tick(c, .01f, true); False(c.ShotThisTick);
        });
        Case("paused time consumes neither queued request nor timers", () => {
            var c = Clock(); c.RequestFire(); Tick(c, 0f); Eq(c.Posture, IntercomWeaponPosture.Relaxed);
            Tick(c, float.NaN); Eq(c.Posture, IntercomWeaponPosture.Relaxed);
            Tick(c, .01f); Eq(c.Posture, IntercomWeaponPosture.Raising);
        });
        Case("reset clears queued shots and posture", () => {
            var c = Raised(); c.RequestFire(); c.Reset(); Advance(c, 3f);
            Eq(c.Posture, IntercomWeaponPosture.Relaxed); False(c.ShotThisTick);
        });
        Case("external shot renews hold and requires raised weapons", () => {
            var c = Clock(); False(c.NotifyShotFired()); c = Raised(); Advance(c, 1.9f);
            True(c.NotifyShotFired()); Advance(c, 1.9f); Eq(c.Posture, IntercomWeaponPosture.Combat);
            Advance(c, .11f); Eq(c.Posture, IntercomWeaponPosture.Lowering); False(c.NotifyShotFired());
        });
        Case("rates 30/60/144 FPS all return to relaxed after one tap", () => {
            foreach (int fps in new[] {30, 60, 144}) {
                var c = Clock(); c.RequestFire(); int shots = 0;
                for (int i = 0; i < fps * 4; i++) { Tick(c, 1f / fps); if(c.ShotThisTick) shots++; }
                Eq(shots, 1); Eq(c.Posture, IntercomWeaponPosture.Relaxed);
            }
        });
        Case("current .30 second pistols deliver twenty volleys in six seconds", () => {
            foreach (int fps in new[] {30,60,120}) {
                var c=new IntercomWeaponPoseClock(.001f,.001f,.3f,2f);
                c.Tick(.001f,true);c.Tick(.001f,true);True(c.ShotThisTick);
                int volleys=0;
                for(int i=0;i<fps*6;i++){c.Tick(1f/fps,true);if(c.ShotThisTick)volleys++;}
                Eq(volleys,20);
                c.Tick(10f,true);True(c.ShotThisTick);c.Tick(.01f,true);False(c.ShotThisTick);
            }
        });
        Console.WriteLine("PASS: " + cases + " cases, " + assertions + " assertions. Production IntercomWeaponPoseClock.cs executed on .NET 8.");
    }
    private static IntercomWeaponPoseClock Clock() => new IntercomWeaponPoseClock(8f / 24f, 12f / 24f, .4f, 2f);
    private static IntercomWeaponPoseClock Raised() { var c = Clock(); c.RequestFire(); UntilShot(c); return c; }
    private static void UntilShot(IntercomWeaponPoseClock c) { for(int i=0;i<100;i++){Tick(c,.01f);if(c.ShotThisTick)return;} throw new Exception("Shot timeout"); }
    private static void Until(IntercomWeaponPoseClock c, IntercomWeaponPosture p) { for(int i=0;i<500;i++){if(c.Posture==p)return;Tick(c,.01f);}throw new Exception("Posture timeout"); }
    private static int Advance(IntercomWeaponPoseClock c, float seconds, bool held = false) {
        int steps = (int)Math.Round(seconds / .01f); int shots = 0;
        for(int i=0;i<steps;i++){Tick(c,.01f,held);if(c.ShotThisTick)shots++;}return shots;
    }
    private static void Tick(IntercomWeaponPoseClock c,float dt,bool held=false) => c.Tick(dt,held);
    private static void Case(string name,Action test){test();cases++;Console.WriteLine("PASS " + name);}
    private static void Eq<T>(T a,T b){assertions++;if(!Equals(a,b))throw new Exception("Expected " + b + ", got " + a);}
    private static void True(bool value){Eq(value,true);} private static void False(bool value){Eq(value,false);}
    private static void Near(float value,float expected,float tolerance){assertions++;if(Math.Abs(value-expected)>tolerance)throw new Exception("Expected near " + expected + ", got " + value);}
}
