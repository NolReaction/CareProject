using System;
using IntercomMvp;

static class Tests
{
    static int assertions, cases;
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static void Near(float actual, float expected, string message) { Check(Math.Abs(actual - expected) < 0.0001f, message + $" ({actual} vs {expected})"); }
    static IntercomTwinStickState New()
    {
        var s = new IntercomTwinStickState(); s.Configure(100, 100, 900, 100, 70, .12f, .2f); return s;
    }
    static void Case(string name, Action body) { body(); cases++; Console.WriteLine("PASS " + name); }
    static void Main()
    {
        Case("Two thumbs move and aim independently", () => {
            var s = New(); s.BeginFrame();
            Check(s.PointerDown(11, 100, 100), "Left capture"); Check(s.PointerDown(22, 900, 100), "Right capture");
            s.PointerMove(11, 100, 170); s.PointerMove(22, 970, 100); s.EndFrame();
            Near(s.Left.X, 0, "Left X"); Near(s.Left.Y, 1, "Forward movement"); Near(s.Right.X, 1, "Right aim"); Near(s.Right.Y, 0, "Right Y");
        });
        Case("Crossing thumbs cannot swap sticks", () => {
            var s = New(); s.BeginFrame(); s.PointerDown(11, 100, 100); s.PointerDown(22, 900, 100);
            s.PointerMove(11, 950, 100); s.PointerMove(22, 50, 100); s.EndFrame();
            Check(s.Left.PointerId == 11 && s.Right.PointerId == 22, "Owners remain fixed");
            Near(s.Left.X, 1, "Left clamp"); Near(s.Right.X, -1, "Right clamp");
        });
        Case("A third finger cannot steal an occupied stick", () => {
            var s = New(); s.PointerDown(1, 100, 100); s.PointerDown(2, 900, 100);
            Check(!s.PointerDown(3, 100, 150), "Third touch rejected"); s.PointerMove(3, 970, 100);
            Check(s.Left.PointerId == 1 && s.Right.PointerId == 2, "Third finger cannot change owners");
            Check(!s.Left.Active && !s.Right.Active, "Third finger cannot drive axes");
        });
        Case("Normal fire release leaves movement running and preserves tap semantics", () => {
            var s = New(); s.BeginFrame(); s.PointerDown(1, 100, 170); s.PointerDown(2, 970, 100); s.PointerUp(2);
            Check(!s.EndFrame(), "Normal release is not a system cancellation");
            Near(s.Left.Y, 1, "Move remains"); Check(!s.Right.Captured && !s.Right.Active, "Fire stops immediately");
            s.PointerUp(1); Check(!s.AnyCaptured, "Both released");
        });
        Case("A missing OS contact is released at the next poll", () => {
            var s = New(); s.PointerDown(1, 100, 170); s.PointerDown(2, 970, 100);
            s.BeginFrame(); s.PointerMove(1, 100, 170);
            Check(s.EndFrame(), "Lost OS contact signals cancellation to the weapon queue");
            Check(s.Left.Captured && s.Left.Active, "Seen contact remains"); Check(!s.Right.Captured && !s.Right.Active, "Lost contact cannot keep firing");
        });
        Case("Pointer IDs may be reused after release", () => {
            var s = New(); s.PointerDown(0, 100, 170); s.PointerUp(0);
            Check(s.PointerDown(0, 970, 100), "ID zero reused"); Check(s.Right.PointerId == 0 && s.Right.Active, "Reused ID owns new stick");
            Check(!s.Left.Captured, "Old owner gone");
        });
        Case("A touch starting outside controls cannot drift into firing", () => {
            var s = New(); Check(!s.PointerDown(5, 500, 500), "Outside rejected"); s.PointerMove(5, 970, 100);
            Check(!s.AnyCaptured, "Entering control without a new press ignored");
            Check(s.PointerDown(6, 970, 100) && s.Right.Active, "New valid press works");
        });
        Case("Dead zone prevents accidental fire but knob still follows finger", () => {
            var s = New(); s.PointerDown(1, 910, 100);
            Check(s.Right.Captured && !s.Right.Active, "Small aim movement stays silent");
            Check(s.Right.RawX > 0 && s.Right.X == 0, "Raw knob and filtered input separate");
            s.PointerMove(1, 935, 100); Near(s.Right.X, .375f, "Radial output starts smoothly after dead zone");
            s.PointerMove(1, 900, 100); Check(!s.Right.Active && s.Right.Captured, "Returning to centre stops fire without lifting");
        });
        Case("Diagonal movement is capped to the same speed", () => {
            var s = New(); s.PointerDown(1, 100, 100); s.PointerMove(1, 1000, 1000);
            float length = (float)Math.Sqrt(s.Left.X * s.Left.X + s.Left.Y * s.Left.Y);
            Near(length, 1, "Diagonal unit length"); Near(s.Left.X, .70710678f, "Diagonal X"); Near(s.Left.Y, .70710678f, "Diagonal Y");
        });
        Case("Safe-area or orientation change clears old screen coordinates", () => {
            var s = New(); s.PointerDown(1, 100, 170); s.PointerDown(2, 970, 100);
            s.Configure(120, 110, 880, 110, 75, .12f, .2f);
            Check(!s.AnyCaptured && !s.Left.Active && !s.Right.Active, "Resized layout resets controls");
            s.PointerMove(1, 100, 170); Check(!s.AnyCaptured, "Old finger cannot recapture after resize");
            Check(s.PointerDown(3, 120, 185), "New contact captures changed layout");
        });
        Case("An unchanged layout preserves two active fingers", () => {
            var s = New(); s.PointerDown(1, 100, 170); s.PointerDown(2, 970, 100);
            s.Configure(100, 100, 900, 100, 70, .12f, .2f);
            Check(s.Left.Active && s.Right.Active, "Per-frame layout refresh does not reset input");
        });
        Case("Pause or focus reset clears all state", () => {
            var s = New(); s.PointerDown(1, 100, 170); s.PointerDown(2, 970, 100); s.Reset();
            Check(!s.AnyCaptured, "No owners after reset"); Near(s.Left.X, 0, "Move cleared"); Near(s.Right.X, 0, "Aim cleared");
            s.BeginFrame(); s.PointerMove(1, 100, 170); s.PointerMove(2, 970, 100); s.EndFrame();
            Check(!s.AnyCaptured, "Held old contacts do not restart after reset");
        });
        Case("Invalid device coordinates cannot poison output", () => {
            var s = New(); Check(!s.PointerDown(1, float.NaN, 100), "NaN down ignored");
            s.BeginFrame(); s.PointerDown(2, 970, 100); s.PointerMove(2, float.PositiveInfinity, 100);
            Check(!s.Right.Captured && !s.Right.Active, "Invalid active coordinate cancels ownership");
            Check(s.EndFrame(), "Invalid OS contact signals cancellation to the weapon queue");
            bool failed = false; try { s.Configure(0, 0, 100, 0, 0, .1f, .1f); } catch (ArgumentOutOfRangeException) { failed = true; }
            Check(failed, "Invalid radius rejected");
        });
        Case("Mouse ID stays distinct from normal touch IDs", () => {
            var s = New(); s.PointerDown(int.MinValue, 100, 170); s.PointerDown(0, 970, 100);
            s.PointerUp(int.MinValue); Check(s.Right.Captured && s.Right.PointerId == 0, "Mouse release does not cancel touch");
        });
        Console.WriteLine($"PASS: {cases} cases / {assertions} assertions. Production pointer state compiled and executed; Unity UI/device runtime not executed.");
    }
}
