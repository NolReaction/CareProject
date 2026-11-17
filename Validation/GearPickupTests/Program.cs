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
        { Time.time += Time.deltaTime; Call(pickups, "Update"); }
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
    private static void RarityAndSound()
    {
        int beforeClips = AudioClip.Created;
        _ = new IntercomGearPickups();
        Check(AudioClip.Created == beforeClips, "MonoBehaviour construction creates no native sound resources");
        var (mission, pickups) = Harness();
        int standardDrops = 0, runnerDrops = 0, heavyDrops = 0, standardValue = 0;
        for (uint seed = 0; seed < 4096; seed++)
        {
            pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Standard, seed);
            if (pickups.ActiveCoins > 0) { standardDrops++; Check(pickups.ActiveCoins == 2 && pickups.UncollectedGears == 4, "ordinary successful loot is two richer coins"); }
            standardValue += pickups.UncollectedGears; pickups.ClearForTransition();
            pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Runner, seed);
            if (pickups.ActiveCoins > 0) { runnerDrops++; Check(pickups.ActiveCoins == 1 && pickups.UncollectedGears == 2, "runner has one double-value coin"); }
            pickups.ClearForTransition();
            pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Heavy, seed);
            if (pickups.ActiveCoins > 0) heavyDrops++;
            Check(pickups.ActiveCoins == 2 && pickups.UncollectedGears == 8, "heavy reward always drops, with fewer physical tokens");
            pickups.ClearForTransition();
        }
        Check(standardDrops > 2867 && standardDrops < 3277 && runnerDrops == standardDrops && heavyDrops == 4096,
            "stable seed population has approximately 75 percent standard/runner and all heavy drops");
        Check(standardValue > 11468 && standardValue < 13108, "standard expected reward remains close to old three per life");
        pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Standard, 101);
        Check(pickups.ActiveCoins == 0, "known missing-loot life is repeatably empty");
        pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Standard, 101);
        Check(pickups.ActiveCoins == 0, "rerolling same seed cannot turn a missed drop into loot");
        AudioSource audio = Field<AudioSource>(pickups, "pickupAudio");
        AudioClip clip = Field<AudioClip>(pickups, "pickupClip");
        Check(audio != null && clip != null && audio.PlayCalls == 0 && !audio.playOnAwake && !audio.loop && audio.spatialBlend == 0f,
            "one reusable 2D pickup voice stays quiet until actual collection");
        Check(clip.frequency == 22050 && clip.channels == 1 && clip.samples == 2867 && clip.Data[0] == 0f && clip.Data[^1] == 0f,
            "short pickup chime has click-free endpoints and bounded mono data");
        float maximum = 0f;
        foreach (float sample in clip.Data) { Check(!float.IsNaN(sample) && !float.IsInfinity(sample), "finite chime data"); maximum = Math.Max(maximum, Math.Abs(sample)); }
        Check(maximum > .05f && maximum < .38f, "metallic chime has useful headroom");
        ExportPickupPreview(clip);
        for (int i = 0; i < 20; i++) pickups.DropProp(Vector3.zero, 1000 + i, IntercomPropKind.Crate);
        Check(pickups.ActiveCoins == 40 && pickups.UncollectedGears == 80, "crate showers are halved without changing their reward");
        Tick(pickups, .30f); Check(audio.PlayCalls == 0, "drop and bounce do not play reward sound");
        mission.playerMotor.transform.position = Vector3.zero; Tick(pickups, 3f);
        Check(pickups.CollectedGears == 80 && pickups.TotalPickups == 40 && audio.PlayCalls > 0 && audio.PlayCalls < 12,
            "magnet burst groups many token pickups into a few audible rewards");
        for (int i = 1; i < audio.PlayedTimes.Count; i++) Check(audio.PlayedTimes[i] - audio.PlayedTimes[i - 1] >= .089f,
            "pickup voices are separated by the 90ms cooldown");
        pickups.DropProp(Vector3.zero, 2000, IntercomPropKind.Relay); Tick(pickups, 2f);
        Check(audio.PlayedPitches.Count == audio.PlayCalls && audio.PlayedPitches[0] == 1f && audio.PlayedPitches.Exists(p => p > 1f),
            "batched pickups get a small changing note cadence");
        foreach (float pitch in audio.PlayedPitches) Check(pitch >= 1f && pitch < 1.27f, "pickup pitch remains subtle");
        Check(audio.volume > 0f && audio.volume <= 1f, "pickup gain is bounded");
        int played = audio.PlayCalls;
        mission.playerMotor.transform.position = new Vector3(30, 0, 30);
        pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Heavy, 100); mission.Phase = IntercomMissionPhase.Victory;
        pickups.SweepForVictory(); Tick(pickups, 2f);
        Check(audio.PlayCalls == played && !audio.isPlaying, "abstract victory settlement and fly-in remain silent");
        mission.Phase = IntercomMissionPhase.Active; mission.RunRevision++;
        pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Heavy, 123);
        mission.playerMotor.transform.position = Vector3.zero; Tick(pickups, 2f);
        Check(audio.PlayCalls > played, "new run resumes audible pickups");
        Call(pickups, "OnApplicationFocus", false); Check(!audio.isPlaying, "focus loss stops active reward voice");
        Call(pickups, "OnApplicationFocus", true); audio.PlayOneShot(clip);
        Time.timeScale = 0f; Tick(pickups, .1f); Check(!audio.isPlaying, "gameplay pause stops active reward voice"); Time.timeScale = 1f;
        audio.PlayOneShot(clip); Call(pickups, "OnApplicationPause", true); Check(!audio.isPlaying, "application pause stops voice immediately");
        Call(pickups, "OnApplicationPause", false); audio.PlayOneShot(clip);
        mission.Phase = IntercomMissionPhase.Selection; Tick(pickups, .1f); Check(!audio.isPlaying, "menu hides pickup audio");
        audio.PlayOneShot(clip); pickups.ClearForTransition(); Check(!audio.isPlaying && Field<int>(pickups, "pendingPickupValue") == 0,
            "transition clears both current and queued pickup audio");
        mission.Phase = IntercomMissionPhase.Active; mission.RunRevision++; mission.playerMotor.transform.position = Vector3.zero;
        pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Heavy, 55); int frozenValue = pickups.UncollectedGears;
        audio.PlayOneShot(clip); mission.enabled = false; Tick(pickups, 2f);
        Check(pickups.CollectedGears == 0 && pickups.UncollectedGears == frozenValue && !audio.isPlaying,
            "disabled director stops both physical collection and queued audio"); mission.enabled = true;
        audio.PlayOneShot(clip); pickups.enabled = false; Check(!audio.isPlaying, "disable cannot leave reward voice playing");
    }
    private static void ExportPickupPreview(AudioClip clip)
    {
        var root = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
        while (root != null && !System.IO.Directory.Exists(System.IO.Path.Combine(root.FullName, "Assets/IntercomMVP"))) root = root.Parent;
        if (root == null) return;
        string folder = System.IO.Path.Combine(root.FullName, "Source/Audio/Previews"); System.IO.Directory.CreateDirectory(folder);
        string path = System.IO.Path.Combine(folder, "V23_GearPickup.wav");
        using var output = new System.IO.BinaryWriter(System.IO.File.Create(path));
        int bytes = clip.Data.Length * 2;
        output.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); output.Write(36 + bytes);
        output.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); output.Write(16); output.Write((short)1); output.Write((short)1);
        output.Write(clip.frequency); output.Write(clip.frequency * 2); output.Write((short)2); output.Write((short)16);
        output.Write(System.Text.Encoding.ASCII.GetBytes("data")); output.Write(bytes);
        foreach (float value in clip.Data) output.Write((short)Math.Round(Math.Clamp(value, -1f, 1f) * short.MaxValue));
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

        var (mission, pickups) = Harness(); int objects = GameObject.Created.Count, meshes = Mesh.Created, materials = Material.Created, clips = AudioClip.Created;
        pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Standard, 100);
        Check(pickups.ActiveCoins == 2 && pickups.UncollectedGears == 4 && pickups.CollectedGears == 0, "robot drops actual coins without instant reward");
        Tick(pickups, 3);
        Check(pickups.CollectedGears == 0 && pickups.ActiveCoins == 2, "coins persist outside pickup range");
        mission.playerMotor.transform.position = Vector3.zero; Tick(pickups, 2);
        Check(pickups.CollectedGears == 4 && pickups.ActiveCoins == 0 && pickups.TotalPickups == 2, "walking close attracts and consumes tokens");
        Tick(pickups, 2); Check(pickups.CollectedGears == 4, "idle overlap cannot double credit");
        mission.playerMotor.transform.position = new Vector3(30, 0, 30);
        pickups.DropProp(Vector3.zero, 12, IntercomPropKind.Crate); pickups.DropProp(Vector3.zero, 12, IntercomPropKind.Crate);
        pickups.DropProp(Vector3.zero, 13, IntercomPropKind.Relay); pickups.DropProp(Vector3.zero, 13, IntercomPropKind.Relay);
        Check(pickups.UncollectedGears == 6, "crates/relays drop once per run even when reset or rebroken");
        pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Heavy, 101);
        Check(pickups.UncollectedGears == 14, "heavy robot keeps eight value in two rich tokens");
        Time.timeScale = 0f; mission.playerMotor.transform.position = Vector3.zero; Tick(pickups, 2);
        Check(pickups.CollectedGears == 4, "pause cannot collect coins"); Time.timeScale = 1f;
        mission.Phase = IntercomMissionPhase.Defeat; Tick(pickups, 2);
        Check(pickups.CollectedGears == 4, "defeat does not sweep missed coins");
        pickups.ClearForTransition(); Check(pickups.ActiveCoins == 0 && pickups.CollectedGears == 4, "cleanup preserves result receipt only");
        mission.RunRevision++; mission.Phase = IntercomMissionPhase.Active; pickups.DropProp(Vector3.zero, 12, IntercomPropKind.Crate);
        Check(pickups.CollectedGears == 0 && pickups.UncollectedGears == 4, "new run clears previous ledger and re-enables prop rewards");
        mission.Phase = IntercomMissionPhase.Victory; pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Runner, 100); pickups.SweepForVictory();
        Check(pickups.CollectedGears == 6 && pickups.ActiveCoins == 0, "victory draws final robot and leftover gears once");
        pickups.SweepForVictory(); Check(pickups.CollectedGears == 6, "victory sweep is idempotent"); Tick(pickups, 2);
        mission.RunRevision++; mission.Phase = IntercomMissionPhase.Active; mission.Mode = IntercomMissionMode.FreePlay;
        pickups.DropRobot(Vector3.zero, IntercomNpcArchetype.Heavy, 103); pickups.DropProp(Vector3.zero, 12, IntercomPropKind.Crate);
        Check(pickups.CollectedGears == 0 && pickups.ActiveCoins == 0, "freeplay cannot farm currency");
        Check(GameObject.Created.Count == objects && Mesh.Created == meshes && Material.Created == materials && AudioClip.Created == clips, "all runtime drops reuse prepared objects and shared native resources");

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
        Mesh ownedMesh = Field<Mesh>(lifecyclePickups, "mesh"); AudioClip ownedClip = Field<AudioClip>(lifecyclePickups, "pickupClip"); Material sharedMaterial = lifecycleMission.deathEffects.gearMaterial;
        GameObject ownedPool = Field<GameObject>(lifecyclePickups, "poolRoot"); Call(lifecyclePickups, "OnDestroy");
        Check(UnityEngine.Object.Destroyed.Contains(ownedMesh) && UnityEngine.Object.Destroyed.Contains(ownedPool) && UnityEngine.Object.Destroyed.Contains(ownedClip)
            && !UnityEngine.Object.Destroyed.Contains(sharedMaterial), "destroy releases owned mesh and pool, preserving shared material");
        var fallbackRoot = new GameObject("Fallback mission"); fallbackRoot.AddComponent<IntercomMissionDirector>();
        var fallbackPickups = fallbackRoot.AddComponent<IntercomGearPickups>(); Material ownedMaterial = Field<Material>(fallbackPickups, "fallbackMaterial");
        Check(ownedMaterial != null, "missing authored material has one owned fallback"); Call(fallbackPickups, "OnDestroy");
        Check(UnityEngine.Object.Destroyed.Contains(ownedMaterial), "owned fallback material is released");

        RarityAndSound();

        var profile = new IntercomProgressionState();
        Check(profile.TrySettle(new string('1', 32), 50, 0, false, true, 7, out var next, out var reward)
            && next.Wallet == 7 && reward.Kills == 50 && reward.GearParts == 7, "settlement pays collected gears instead of kill count");
        Check(next.TrySettle(new string('2', 32), 50, 0, false, true, 0, out var zero, out var missed)
            && zero.Wallet == 7 && missed.Total == 0, "uncollected kills cannot pay later");
        Console.WriteLine("Gear pickups: " + checks + " assertions PASS. Production ledger, pooling, motion and settlement with narrow engine contracts; no Unity rendering claim.");
    }
}
