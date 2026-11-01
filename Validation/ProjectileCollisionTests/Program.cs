using System;
using IntercomMvp;
class Program
{
    static int checks;
    static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    static void Main()
    {
        // NonAlloc ordering must never decide which of a thin wall and target receives a bullet.
        var random = new Random(8443);
        for (int trial = 0; trial < 400; trial++)
        {
            float[] distances = { .01f, .42f, .87f, 1.15f };
            bool[] owners = { true, false, false, false };
            int[] indices = { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--) { int j = random.Next(i + 1); int temp = indices[i]; indices[i] = indices[j]; indices[j] = temp; }
            var choice = new IntercomProjectileHitChoice(1.2f); // 36m/s at30fps spans a thin wall eachframe.
            for (int i = 0; i < 4; i++) choice.Consider(indices[i], distances[indices[i]], owners[indices[i]]);
            Check(choice.Index == 1, "wall before target regardless of return order");
        }
        var empty = new IntercomProjectileHitChoice(1f);
        empty.Consider(0, 0f, true); Check(empty.Index == -1, "owner never blocks");
        empty.Consider(1, 1.01f, false); Check(empty.Index == -1, "range limit");
        empty.Consider(2, -1f, false); empty.Consider(3, float.NaN, false); empty.Consider(4, float.PositiveInfinity, false);
        Check(empty.Index == -1, "invalid distance rejected");
        empty.Consider(5, 0f, false); Check(empty.Index == 5, "surface at origin is valid");
        empty.Consider(6, 0f, false); Check(empty.Index == 5, "equal distance stable");
        Check(IntercomProjectileHitChoice.IsComplete(0,64), "empty query valid");
        Check(IntercomProjectileHitChoice.IsComplete(63,64), "below capacity valid");
        Check(!IntercomProjectileHitChoice.IsComplete(64,64), "saturated array cannot prove nearest hit");
        Check(!IntercomProjectileHitChoice.IsComplete(65,64), "overflow rejected");
        Check(!IntercomProjectileHitChoice.IsComplete(-1,64), "negativecount rejected");
        Check(!IntercomProjectileHitChoice.IsComplete(0,0), "zero capacity rejected");
        Console.WriteLine("Projectile hit selection: " + checks + " assertions passed. Pure selection logic; no Unity PhysX simulation.");
    }
}
