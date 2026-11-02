using System;
using System.Collections.Generic;
using IntercomMvp;

internal static class Program
{
    private static int assertions;
    private static void Check(bool value, string message)
    {
        assertions++;
        if (!value) throw new Exception(message);
    }
    private static void Near(double actual, double expected, double tolerance, string message)
        => Check(Math.Abs(actual - expected) <= tolerance, message + $" ({actual} vs {expected})");

    private static void Main()
    {
        StationaryCadence();
        GatesAndReversal();
        InvalidAndReset();
        RigidPivotGeometry();
        Console.WriteLine($"Turn step scheduler and pivot geometry: {assertions} assertions passed. Production pure math; no Unity renderer, skinned rig or physics execution.");
    }

    private static void StationaryCadence()
    {
        foreach (int fps in new[] { 15, 30, 60, 120, 240 })
        foreach (float target in new[] { 100f, -100f, 180f, -180f, 25f })
        {
            var state = new IntercomTurnStepState();
            state.Reset(0f);
            int started = 0, landed = 0, gathers = 0;
            bool previousLeft = false;
            float completion = 0f;
            int lastId = 0;
            for (int frame = 0; frame < fps * 5; frame++)
            {
                state.Advance(target, 0f, true, 1f / fps);
                Check(state.Enabled, "stationary mode remains enabled");
                Check(state.Phase >= 0f && state.Phase <= 1f, "phase bounded");
                Check(state.LowerYaw >= -180f && state.LowerYaw <= 180f, "heading normalized");
                if (state.StartedThisTick)
                {
                    Check(state.StepId == lastId + 1, "new step increments identity exactly once");
                    lastId = state.StepId;
                    if (started > 0) Check(previousLeft != state.SwingLeft, "support transfers alternate boots");
                    else Check(state.SwingLeft == (target < 0f && target != -180f), "first swing leads turn direction");
                    previousLeft = state.SwingLeft;
                    started++;
                    Check(Math.Abs(state.LastStepYawDelta) <= 40f, "turn step size bounded");
                    if (state.IsGatherStep)
                    {
                        gathers++;
                        Check(state.LastStepYawDelta == 0f, "gather step moves opposite boot without additional heading");
                    }
                }
                if (state.LandedThisTick)
                {
                    landed++;
                    completion = (frame + 1f) / fps;
                    Check(!state.Active && state.Phase == 1f, "landing final pose is observable before next transfer");
                    Check(landed <= started, "landing cannot precede a started step");
                }
            }
            int expected = (int)Math.Ceiling(Math.Abs(target) / 40f) + 1;
            Check(started == expected && landed == expected, "heading catch-up and one final gather, without endless idle steps");
            Check(gathers == 1, "one gather completes both boot orientations");
            Near(IntercomTurnStepState.DeltaAngle(state.LowerYaw, target), 0f, 0.001f, "turn reaches requested heading");
            Near(completion, expected * .18f, 1.01f / fps, "cadence remains within one rendered frame across frame rates");
        }
        var quiet = new IntercomTurnStepState();
        quiet.Reset(179f);
        for (int i = 0; i < 100; i++) quiet.Advance(-176f + (float)Math.Sin(i) * 2f, 0f, true, .016f);
        Check(quiet.StepId == 0 && !quiet.Active, "small wraparound aim jitter does not shuffle boots");
    }

    private static void GatesAndReversal()
    {
        var state = new IntercomTurnStepState();
        state.Reset(0);
        state.Advance(90, .2f, true, .02f);
        Check(!state.Enabled && !state.Active, "mode does not enter between speed thresholds");
        state.Reset(0);
        state.Advance(90, .1f, true, .02f);
        Check(state.Enabled && state.Active, "slow enough enters turn mode");
        state.Advance(90, .2f, true, .02f);
        Check(state.Enabled && state.Active, "speed hysteresis keeps ongoing support transfer");
        state.Advance(90, .3f, true, .02f);
        Check(!state.Enabled && !state.Active && !state.LandedThisTick, "walking cancels without a fabricated landing");
        Near(state.LowerYaw, 90, .001f, "moving mode hands heading to authored locomotion");

        state.Reset(0);
        state.Advance(90, 0, true, .04f);
        bool firstSwing = state.SwingLeft;
        int firstId = state.StepId;
        float previous = state.LowerYaw;
        while (!state.LandedThisTick)
        {
            state.Advance(-90, 0, true, .02f);
            Check(state.StepId == firstId && state.SwingLeft == firstSwing, "reversal does not swap support in midair");
            Check(state.LowerYaw >= previous, "captured current step finishes before reversal");
            previous = state.LowerYaw;
        }
        Near(state.LowerYaw, 40f, .001f, "first endpoint remains captured after reversed input");
        state.Advance(-90, 0, true, .02f);
        Check(state.StartedThisTick && state.SwingLeft != firstSwing && state.LastStepYawDelta < 0f, "next boot follows reversed heading");
        for (int i = 0; i < 100; i++) state.Advance(-90, 0, true, .02f);
        Near(state.LowerYaw, -90f, .001f, "reversed sequence settles");
        Check(!state.Active, "reversal leaves no permanent turn state");

        state.Reset(0);
        state.Advance(90, 0, true, .02f);
        float phase = state.Phase, yaw = state.LowerYaw;
        state.Advance(90, 0, true, 0f);
        Check(state.Active && state.Phase == phase && state.LowerYaw == yaw && !state.LandedThisTick, "paused time preserves phase without contact");
        state.Advance(90, 0, false, .02f);
        Check(!state.Enabled && !state.Active && !state.LandedThisTick, "airborne cancels without contact");
    }

    private static void InvalidAndReset()
    {
        var state = new IntercomTurnStepState();
        state.Reset(0);
        state.Advance(90, 0, true, .02f);
        state.Reset(-170);
        Check(state.StepId == 0 && !state.Active && !state.Enabled && !state.LandedThisTick, "teleport/reset clears all contact ownership");
        Near(state.LowerYaw, -170, .001f, "reset replaces heading");
        state.Advance(float.NaN, 0, true, .02f);
        Check(state.StepId == 0 && state.LowerYaw == -170, "invalid heading cannot invent a turn");
        state.Advance(0, float.NaN, true, .02f);
        Check(!state.Active && !state.LandedThisTick && !float.IsNaN(state.LowerYaw), "invalid speed cancels safely");
        state.Reset(0);
        state.stepDuration = float.NaN;
        state.maxStepDegrees = float.PositiveInfinity;
        state.startDegrees = float.NaN;
        state.finishDegrees = float.NaN;
        for (int i = 0; i < 50; i++)
        {
            state.Advance(90, 0, true, i % 2 == 0 ? 100f : .02f);
            Check(float.IsFinite(state.Phase) && state.Phase >= 0 && state.Phase <= 1, "malformed settings/stalled frame keep bounded phase");
            Check(float.IsFinite(state.LowerYaw), "malformed settings keep finite yaw");
        }
        Check(!state.Active && Math.Abs(state.LowerYaw - 90) < .001f, "bounded hitch catch-up still completes");
        Near(IntercomTurnStepState.DeltaAngle(179, -179), 2, .001f, "shortest positive wraparound");
        Near(IntercomTurnStepState.DeltaAngle(-179, 179), -2, .001f, "shortest negative wraparound");
        Check(float.IsFinite(IntercomTurnStepState.DeltaAngle(float.MaxValue, -float.MaxValue)), "extreme finite angles do not overflow subtraction");
        IntercomTurnStepState.PlanarPivotOffset(float.NaN, 5, 1, 1, 30, out float x, out float z);
        Check(x == 0 && z == 5, "invalid position helper returns finite safe components");
    }

    private static void RigidPivotGeometry()
    {
        // Independent homogeneous 2D rotation verifies that translating the pelvis
        // around the stance ankle never requires moving the foot's local joint.
        var random = new Random(1831);
        for (int i = 0; i < 4000; i++)
        {
            float bx = (float)(random.NextDouble() * 40 - 20), bz = (float)(random.NextDouble() * 40 - 20);
            double heading = random.NextDouble() * Math.PI * 2;
            double localX = (random.NextDouble() < .5 ? -1 : 1) * (.09 + random.NextDouble() * .08);
            double localZ = random.NextDouble() * .08 - .04;
            double ox = Math.Cos(heading) * localX + Math.Sin(heading) * localZ;
            double oz = -Math.Sin(heading) * localX + Math.Cos(heading) * localZ;
            float px = bx + (float)ox, pz = bz + (float)oz;
            float turn = (float)(random.NextDouble() * 80 - 40);
            IntercomTurnStepState.PlanarPivotOffset(bx, bz, px, pz, turn, out float nx, out float nz);
            double nextHeading = heading + turn * Math.PI / 180;
            double ankleX = nx + Math.Cos(nextHeading) * localX + Math.Sin(nextHeading) * localZ;
            double ankleZ = nz - Math.Sin(nextHeading) * localX + Math.Cos(nextHeading) * localZ;
            Near(ankleX, px, .000005, "planted ankle X remains fixed");
            Near(ankleZ, pz, .000005, "planted ankle Z remains fixed");
            double oldLength = Math.Sqrt(((double)bx-px)*(bx-px)+((double)bz-pz)*(bz-pz));
            double newLength = Math.Sqrt(((double)nx-px)*(nx-px)+((double)nz-pz)*(nz-pz));
            Near(newLength, oldLength, .000005, "rigid support distance preserved without stretching local link");
            double travel = Math.Sqrt(((double)nx-bx)*(nx-bx)+((double)nz-bz)*(nz-bz));
            double predicted = 2*oldLength*Math.Abs(Math.Sin(turn*Math.PI/360));
            Near(travel, predicted, .000005, "body translation obeys bounded rigid arc");
        }
    }
}
