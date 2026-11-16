using System;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
    private static void Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static T Field<T>(object target, string name)
        => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Tick(IntercomGearPickups pickups, float seconds)
    {
        Time.deltaTime = .02f;
        for (int i = 0; i < seconds / .02f; i++)
            pickups.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pickups, null);
    }
    private static (IntercomMissionDirector, IntercomGearPickups) Harness()
    {
        var root = new GameObject("Mission");
        var mission = root.AddComponent<IntercomMissionDirector>();
        mission.deathEffects = root.AddComponent<IntercomRobotDeathEffects>(); mission.deathEffects.gearMaterial = new Material();
        mission.playerMotor = new GameObject("Hero").AddComponent<IntercomPlayerMotor>(); mission.playerMotor.transform.position = new Vector3(30, 0, 30);
        var pickups = root.AddComponent<IntercomGearPickups>(); pickups.mission = mission;
        return (mission, pickups);
    }
    private static void Main()
    {
        var ledger = new IntercomGearCollectionState();
        Check(!ledger.TryDrop(0, out _, out _) && !ledger.TryDrop(-2, out _, out _), "invalid loot refused");
        for (int i = 0; i < 300; i++) Check(ledger.TryDrop(1, out _, out _), "loot fits bounded ledger");
        Check(ledger.ActiveCount == 96 && ledger.ActiveValue == 300 && ledger.CollectedGears == 0, "full pool preserves value by stacking, no automatic payout");
        int sum = 0;
        for (int i = 0; i < 96; i++) { Check(ledger.TryCollect(i, out int v), "live token can be consumed"); sum += v; Check(!ledger.TryCollect(i, out _), "duplicate collection refused"); }
        Check(sum == 300 && ledger.CollectedGears == 300 && ledger.ActiveCount == 0, "all stacked currency collected exactly once");
        ledger.BeginRun(); ledger.TryDrop(int.MaxValue, out int largeSlot, out _);
        Check(ledger.ActiveValue == 10000 && ledger.TryCollect(largeSlot, out _) && !ledger.TryDrop(1, out _, out _), "per-run bounds avoid overflow");
        ledger.BeginRun(); ledger.TryDrop(8, out _, out _); ledger.ClearUncollected();
        Check(ledger.ActiveValue == 0 && ledger.CollectedGears == 0, "abandoned coins do not become money");

        var (mission, pickups) = Harness(); int objects = GameObject.Created.Count, meshes = Mesh.Created, materials = Material.Created;
        pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Standard, 100);
        Check(pickups.ActiveCoins == 3 && pickups.UncollectedGears == 3 && pickups.CollectedGears == 0, "robot drops actual coins without instant reward");
        Tick(pickups, 3);
        Check(pickups.CollectedGears == 0 && pickups.ActiveCoins == 3, "coins persist outside pickup range");
        mission.playerMotor.transform.position = Vector3.zero; Tick(pickups, 2);
        Check(pickups.CollectedGears == 3 && pickups.ActiveCoins == 0 && pickups.TotalPickups == 3, "walking close attracts and consumes tokens");
        Tick(pickups, 2); Check(pickups.CollectedGears == 3, "idle overlap cannot double credit");
        mission.playerMotor.transform.position = new Vector3(30, 0, 30);
        pickups.DropProp(Vector3.zero, 12, IntercomPropKind.Crate); pickups.DropProp(Vector3.zero, 12, IntercomPropKind.Crate);
        pickups.DropProp(Vector3.zero, 13, IntercomPropKind.Relay); pickups.DropProp(Vector3.zero, 13, IntercomPropKind.Relay);
        Check(pickups.UncollectedGears == 6, "crates/relays drop once per run even when reset or rebroken");
        pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Heavy, 101);
        Check(pickups.UncollectedGears == 14, "heavy robot has four double-value coins");
        Time.timeScale = 0f; mission.playerMotor.transform.position = Vector3.zero; Tick(pickups, 2);
        Check(pickups.CollectedGears == 3, "pause cannot collect coins"); Time.timeScale = 1f;
        mission.Phase = IntercomMissionPhase.Defeat; Tick(pickups, 2);
        Check(pickups.CollectedGears == 3, "defeat does not sweep missed coins");
        pickups.ClearForTransition(); Check(pickups.ActiveCoins == 0 && pickups.CollectedGears == 3, "cleanup preserves result receipt only");
        mission.RunRevision++; mission.Phase = IntercomMissionPhase.Active; pickups.DropProp(Vector3.zero, 12, IntercomPropKind.Crate);
        Check(pickups.CollectedGears == 0 && pickups.UncollectedGears == 4, "new run clears previous ledger and re-enables prop rewards");
        mission.Phase = IntercomMissionPhase.Victory; pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Runner, 102); pickups.SweepForVictory();
        Check(pickups.CollectedGears == 6 && pickups.ActiveCoins == 0, "victory draws final robot and leftover gears once");
        pickups.SweepForVictory(); Check(pickups.CollectedGears == 6, "victory sweep is idempotent"); Tick(pickups, 2);
        mission.RunRevision++; mission.Phase = IntercomMissionPhase.Active; mission.Mode = IntercomMissionMode.FreePlay;
        pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Heavy, 103); pickups.DropProp(Vector3.zero, 12, IntercomPropKind.Crate);
        Check(pickups.CollectedGears == 0 && pickups.ActiveCoins == 0, "freeplay cannot farm currency");
        Check(GameObject.Created.Count == objects && Mesh.Created == meshes && Material.Created == materials, "all runtime drops reuse prepared objects and shared native resources");

        var (lifecycleMission, lifecyclePickups) = Harness(); lifecyclePickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Standard, 300);
        Transform firstCoin = Field<GameObject>(lifecyclePickups, "poolRoot").transform.children[0];
        Vector3 frozen = firstCoin.position; Call(lifecyclePickups, "OnApplicationFocus", false); Tick(lifecyclePickups, 1);
        Check((firstCoin.position - frozen).sqrMagnitude == 0f && lifecyclePickups.CollectedGears == 0, "focus loss freezes visual motion and collection");
        Call(lifecyclePickups, "OnApplicationFocus", true); Call(lifecyclePickups, "OnApplicationPause", true); Tick(lifecyclePickups, 1);
        Check((firstCoin.position - frozen).sqrMagnitude == 0f && lifecyclePickups.CollectedGears == 0, "application pause freezes visual motion and collection");
        Call(lifecyclePickups, "OnApplicationPause", false); Tick(lifecyclePickups, .4f);
        Check((firstCoin.position - frozen).sqrMagnitude > 0f, "focus and pause recovery resume coin motion");
        lifecyclePickups.enabled = false;
        Check(lifecyclePickups.ActiveCoins == 0 && !firstCoin.gameObject.activeSelf && lifecyclePickups.CollectedGears == 0, "disabled component clears missed coins without credit");
        Mesh ownedMesh = Field<Mesh>(lifecyclePickups, "mesh"); Material sharedMaterial = lifecycleMission.deathEffects.gearMaterial;
        GameObject ownedPool = Field<GameObject>(lifecyclePickups, "poolRoot"); Call(lifecyclePickups, "OnDestroy");
        Check(UnityEngine.Object.Destroyed.Contains(ownedMesh) && UnityEngine.Object.Destroyed.Contains(ownedPool)
            && !UnityEngine.Object.Destroyed.Contains(sharedMaterial), "destroy releases owned mesh and pool, preserving shared material");
        var fallbackRoot = new GameObject("Fallback mission"); fallbackRoot.AddComponent<IntercomMissionDirector>();
        var fallbackPickups = fallbackRoot.AddComponent<IntercomGearPickups>(); Material ownedMaterial = Field<Material>(fallbackPickups, "fallbackMaterial");
        Check(ownedMaterial != null, "missing authored material has one owned fallback"); Call(fallbackPickups, "OnDestroy");
        Check(UnityEngine.Object.Destroyed.Contains(ownedMaterial), "owned fallback material is released");

        var profile = new IntercomProgressionState();
        Check(profile.TrySettle(new string('1', 32), 50, 0, false, true, 7, out var next, out var reward)
            && next.Wallet == 7 && reward.Kills == 50 && reward.GearParts == 7, "settlement pays collected gears instead of kill count");
        Check(next.TrySettle(new string('2', 32), 50, 0, false, true, 0, out var zero, out var missed)
            && zero.Wallet == 7 && missed.Total == 0, "uncollected kills cannot pay later");
        Console.WriteLine("Gear pickups: " + checks + " assertions PASS. Production ledger, pooling, motion and settlement with narrow engine contracts; no Unity rendering claim.");
    }
}
