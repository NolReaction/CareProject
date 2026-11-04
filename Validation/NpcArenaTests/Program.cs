using System;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

static class Program
{
    static int checks;
    static void Expect(bool condition, string reason)
    { checks++; if (!condition) throw new Exception(reason); }
    static void Send(IntercomNpcArena arena, string message)
    { typeof(IntercomNpcArena).GetMethod(message, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(arena, null); }
    static Transform Marker(float x) => new Transform { position = new Vector3(x, 0, 0) };
    static IntercomNpcArena Arena(int markerCount)
    {
        var arena = new IntercomNpcArena { definition = new IntercomNpcDefinition(),
            navigation = new IntercomNpcNavigation(), playerHealth = new IntercomHealth(),
            playerMotor = new IntercomPlayerMotor(), spawnPoints = new Transform[markerCount] };
        for (int i = 0; i < markerCount; i++) arena.spawnPoints[i] = Marker(i);
        return arena;
    }
    static void Main()
    {
        IntercomNpcController.Created.Clear();
        var arena = Arena(5);
        Send(arena, "OnEnable");
        Expect(IntercomNpcController.Created.Count == 0, "OnEnable cannot create actors before Start");
        Send(arena, "Start");
        Expect(arena.SpawnedCount == 3 && IntercomNpcController.Created.Count == 3, "Authored cap creates exactly three");
        Expect(arena.navigation.rebuildCount == 1, "Navigation prepared once before spawn");
        foreach (var actor in IntercomNpcController.Created)
        {
            Expect(actor.definition == arena.definition && actor.navigation == arena.navigation
                && actor.targetHealth == arena.playerHealth && actor.resetMotor == arena.playerMotor,
                "Factory gets explicit dependencies");
            Expect(actor.transform.parent == arena.transform, "Ownership stays under sandbox, outside static environment");
            Expect(actor.EmergenceCalls==1,"sandbox starts one arrival presentation on a new actor");
            Expect(Math.Abs(actor.transform.position.x - (actor.ordinal + .1f)) < .001f,
                "Factory uses projected safe spawn");
        }
        var unrelated = new IntercomNpcController();
        for (int i = 0; i < 4; i++)
        {
            Send(arena, "OnDisable");
            Expect(IntercomNpcController.Created.TrueForAll(actor => !actor.gameObject.activeSelf), "Disable suspends owned actors");
            arena.playerMotor.RespawnVersion++;
            Send(arena, "OnEnable");
            Expect(IntercomNpcController.Created.TrueForAll(actor => actor.gameObject.activeSelf), "Enable restores owned actors");
        }
        Expect(IntercomNpcController.Created.Count == 3, "Enable/reset cycle cannot duplicate factory actors");
        Expect(unrelated.gameObject.activeSelf && !unrelated.gameObject.destroyed, "Unrelated actor untouched");
        Send(arena, "OnDestroy");
        Expect(arena.SpawnedCount == 0 && IntercomNpcController.Created.TrueForAll(actor => actor.gameObject.destroyed), "Removing bootstrap cleans owned actors");
        Send(arena, "OnDestroy");
        Send(arena, "OnEnable");
        Expect(IntercomNpcController.Created.Count == 3, "Repeated cleanup cannot recreate actors");

        IntercomNpcController.Created.Clear();
        var partial = Arena(3);
        partial.spawnPoints[0] = null;
        partial.spawnPoints[1] = Marker(-1);
        partial.navigation.IsReady = true;
        Send(partial, "Start");
        Expect(partial.SpawnedCount == 1 && IntercomNpcController.Created.Count == 1, "Missing/blocked markers skipped safely");
        Expect(IntercomNpcController.Created[0].ordinal == 2, "Spawn ordinal remains tied to authored marker");
        Expect(partial.navigation.rebuildCount == 0, "Already prepared navigation not rebuilt");

        var invalid = Arena(3); invalid.playerHealth = null;
        Send(invalid, "Start"); Send(invalid, "OnDisable"); Send(invalid, "OnEnable"); Send(invalid, "OnDestroy");
        Expect(invalid.SpawnedCount == 0, "Missing dependencies produce no partial actors");
        var bounded = Arena(40); bounded.maximumActors = int.MaxValue;
        Send(bounded, "Start");
        Expect(bounded.SpawnedCount == 16, "Corrupt unbounded inspector count is capped");
        Console.WriteLine($"PASS: {checks} bootstrap lifecycle checks using production IntercomNpcArena; Unity/PhysX not simulated.");
    }
}
