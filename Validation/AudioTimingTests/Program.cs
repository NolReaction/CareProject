using System;
using IntercomMvp;

internal static class Program
{
    private static int cases;
    private static int assertions;
    private static void Main()
    {
        Case("starting mid-stride does not invent a contact", () => {
            var c = new IntercomFootstepClock();
            Check(!c.Tick(.32, 1, 0, true));
            Check(!c.Tick(.45, 1, 0, true));
            Check(c.Tick(.51, 1, 0, true));
        });
        Case("left and right contacts alternate through loop boundaries", () => {
            var c = new IntercomFootstepClock();
            c.Tick(.49, 1, 0, true);
            Check(c.Tick(.5, 1, 0, true));
            Check(!c.Tick(.51, 1, 0, true));
            Check(c.Tick(1.0, 1, 0, true));
            Check(!c.Tick(1.01, 1, 0, true));
            Check(c.Tick(1.5, 1, 0, true));
        });
        Case("standing, wall collision and airborne movement suppress footsteps", () => {
            foreach (double stopped in new[] { .5, 1.0, 1.5 })
            {
                var c = new IntercomFootstepClock();
                c.Tick(stopped - .01, 1, 0, true);
                Check(!c.Tick(stopped + .01, 1, 0, false));
                Check(!c.Tick(stopped + .03, 1, 0, true));
                Check(c.Tick(stopped + .5, 1, 0, true));
            }
        });
        Case("long frame does not replay missed steps", () => {
            var c = new IntercomFootstepClock();
            c.Tick(.1, 1, 0, true);
            Check(!c.Tick(10.1, 1, 0, true));
            Check(!c.Tick(10.4, 1, 0, true));
            Check(c.Tick(10.5, 1, 0, true));
        });
        Case("rebind and controller-state changes resynchronize silently", () => {
            var c = new IntercomFootstepClock();
            c.Tick(.49, 1, 0, true);
            Check(!c.Tick(.51, 2, 0, true));
            Check(c.Tick(1.0, 2, 0, true));
            Check(!c.Tick(.4, 2, 0, true));
            Check(c.Tick(.5, 2, 0, true));
        });
        Case("explicit pause and respawn reset cannot produce a delayed step", () => {
            var c = new IntercomFootstepClock();
            c.Tick(.49, 1, 0, true);
            c.Reset();
            Check(!c.Tick(1.51, 1, 0, true));
            Check(c.Tick(2.0, 1, 0, true));
        });
        Case("phase offset follows replacement run clips", () => {
            var c = new IntercomFootstepClock();
            c.Tick(.19, 1, .2, true);
            Check(c.Tick(.21, 1, .2, true));
            Check(!c.Tick(.3, 1, .2, true));
            Check(c.Tick(.71, 1, .2, true));
        });
        Case("zero elapsed animation never repeats a contact", () => {
            var c = new IntercomFootstepClock();
            c.Tick(.49, 1, 0, true);
            Check(c.Tick(.5, 1, 0, true));
            for (int i = 0; i < 30; i++) Check(!c.Tick(.5, 1, 0, true));
        });
        Case("non-finite phases reset safely", () => {
            foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var c = new IntercomFootstepClock();
                c.Tick(.49, 1, 0, true);
                Check(!c.Tick(bad, 1, 0, true));
                Check(!c.Tick(.51, 1, 0, true));
                Check(!c.Tick(.6, 1, bad, true));
            }
        });
        Case("faster run contacts remain consistent at mobile frame rates", () => {
            foreach (int fps in new[] { 30, 60, 120 })
            {
                var c = new IntercomFootstepClock();
                int contacts = 0;
                // Authored 16/24 s cycle played at 2.25x: 6.75 contacts / second.
                for (int frame = 0; frame <= fps * 8; frame++)
                    if (c.Tick((double)frame / fps * 3.375, 1, 0, true)) contacts++;
                Check(contacts == 54);
            }
        });
        Console.WriteLine($"{cases} scenarios / {assertions} assertions passed");
    }

    private static void Check(bool value)
    {
        assertions++;
        if (!value) throw new Exception("Assertion " + assertions + " failed");
    }

    private static void Case(string name, Action run)
    {
        run(); cases++; Console.WriteLine("PASS " + name);
    }
}
