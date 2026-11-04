using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using IntercomMvp.Editor;
using UnityEditor;
using UnityEngine;

internal static class Program
{
    private const string Alias = "Assets/IntercomMVP/Audio/Pistol.wav";
    private const string Canonical = "Assets/IntercomMVP/Content/Weapons/Pistols/Audio/Pistol.wav";
    private const string AssetGuid = "abcdef0123456789abcdef0123456789";
    private const string OtherGuid = "bc123456789012345678901234567890";
    private const string RegistryGuid = "dd5f7628d7a55161b30d1e388ce6233d";
    private static string root;
    private static int checks, groups;
    private static readonly List<string> fixtures = new List<string>();
    private static string PathFor(string path) => Path.Combine(root, path);
    private static void Put(string path, string text)
    { Directory.CreateDirectory(Path.GetDirectoryName(PathFor(path))); File.WriteAllText(PathFor(path), text); }
    private static void Check(bool condition, string label)
    { checks++; if (!condition) throw new InvalidOperationException(label); }
    private static void Registry(params object[] entries)
    {
        Put(IntercomAssetCatalog.RegistryPath, JsonSerializer.Serialize(new { revision = "architecture-v13", files = entries }));
        Put(IntercomAssetCatalog.RegistryPath + ".meta", "guid: " + RegistryGuid + "\n");
    }
    private static object Entry(string alias = Alias, string destination = Canonical, string guid = AssetGuid)
        => new { legacyPath = alias, destination, guid };
    private static void Fixture()
    {
        root = Path.Combine(Path.GetTempPath(), "careproject-catalog-" + System.Guid.NewGuid().ToString("N"));
        fixtures.Add(root); Directory.CreateDirectory(root); Application.dataPath = PathFor("Assets");
        Put(Canonical, "current resource"); Put(Canonical + ".meta", "fileFormatVersion: 2\nguid: " + AssetGuid + "\n");
        Registry(Entry());
    }
    private static string Move(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathFor(to)));
        File.Move(PathFor(from), PathFor(to)); File.Move(PathFor(from) + ".meta", PathFor(to) + ".meta");
        return to;
    }
    private static void IdentityAndCache()
    {
        groups++; Fixture();
        Check(IntercomAssetCatalog.Resolve(Alias) == Canonical, "old identity resolves current asset by GUID");
        Check(IntercomAssetCatalog.Resolve(Canonical) == Canonical, "canonical identity is indexed");
        int reads = IntercomAssetCatalog.ManifestReadCount;
        for (int i = 0; i < 1000; i++)
            Check(IntercomAssetCatalog.Resolve(Alias) == Canonical, "repeated identity lookup stays stable");
        Check(IntercomAssetCatalog.ManifestReadCount == reads, "1000 requests do not reread registry JSON");
        Check(IntercomAssetCatalog.ResolveGuid(AssetGuid.ToUpperInvariant(), Canonical) == Canonical, "valid uppercase GUID finds asset");
        const string unknown = "Assets/IntercomMVP/Content/New.asset";
        Check(IntercomAssetCatalog.Resolve(unknown) == unknown, "unknown safe path remains available for authoring");
        Check(!File.Exists(PathFor(Alias)), "lookup never creates an old placeholder");
        Check(EditorApplication.delayCall == null, "lookup never schedules migration or destructive cleanup");
    }
    private static void ManualMoves()
    {
        groups++; Fixture(); Check(IntercomAssetCatalog.Resolve(Alias) == Canonical, "initial identity lookup");
        int reads = IntercomAssetCatalog.ManifestReadCount;
        string custom = Move(Canonical, "Assets/IntercomMVP/UserChosen/Pistol.wav");
        Check(IntercomAssetCatalog.Resolve(Alias) == custom, "alias respects manual resource move");
        Check(IntercomAssetCatalog.Resolve(Canonical) == custom, "canonical identity respects manual resource move");
        string again = Move(custom, "Assets/IntercomMVP/UserChosen/Again.wav");
        Check(IntercomAssetCatalog.ResolveGuid(AssetGuid, Canonical) == again, "typed GUID lookup respects subsequent move");
        Check(IntercomAssetCatalog.ManifestReadCount == reads, "manual resource moves do not reload stable registry");
        Check(File.ReadAllText(PathFor(again)) == "current resource", "navigation preserves resource contents");
    }
    private static void InvalidationAndRegistryMove()
    {
        groups++; Fixture(); Check(IntercomAssetCatalog.Resolve(Alias) == Canonical, "cache initial registry");
        int reads = IntercomAssetCatalog.ManifestReadCount;
        const string other = "Assets/IntercomMVP/Content/Other.wav";
        Put(other, "other resource"); Put(other + ".meta", "guid: " + OtherGuid + "\n");
        Registry(Entry(destination: other, guid: OtherGuid));
        Check(IntercomAssetCatalog.Resolve(Alias) == Canonical, "registry changes wait for project invalidation");
        EditorApplication.ProjectChanged();
        Check(IntercomAssetCatalog.Resolve(Alias) == other, "project change reloads updated registry");
        Check(IntercomAssetCatalog.ManifestReadCount == reads + 1, "project change reads registry once");
        Move(IntercomAssetCatalog.RegistryPath, "Assets/IntercomMVP/UserChosen/ProjectAssets.json");
        EditorApplication.ProjectChanged();
        Check(IntercomAssetCatalog.Resolve(Alias) == other, "registry itself can move with preserved GUID");
        IntercomAssetCatalog.Invalidate();
        Check(IntercomAssetCatalog.Resolve(Alias) == other, "explicit invalidation resolves relocated registry");
    }
    private static void MissingAndBrokenRegistries()
    {
        groups++; Fixture();
        File.Delete(PathFor(IntercomAssetCatalog.RegistryPath));
        Check(IntercomAssetCatalog.Resolve(Alias) == Alias, "missing registry does not run an old migration");
        Check(IntercomAssetCatalog.ResolveGuid(AssetGuid, Canonical) == Canonical, "typed navigation still uses GUID without registry");
        Check(IntercomAssetCatalog.ResolveGuid("not-a-guid", Canonical) == Canonical, "safe canonical fallback survives missing registry");
        Fixture(); Put(IntercomAssetCatalog.RegistryPath, "{invalid json");
        int warnings = Debug.Warnings, reads = IntercomAssetCatalog.ManifestReadCount;
        Check(IntercomAssetCatalog.Resolve(Alias) == Alias, "malformed registry is ignored safely");
        Check(Debug.Warnings == warnings + 1, "malformed registry warns once");
        Check(IntercomAssetCatalog.ManifestReadCount == reads + 1, "malformed registry is read once");
        Check(IntercomAssetCatalog.Resolve(Alias) == Alias && Debug.Warnings == warnings + 1,
            "malformed registry does not repeatedly parse or log");
        Check(IntercomAssetCatalog.ManifestReadCount == reads + 1, "malformed registry failure is cached");
        Check(IntercomAssetCatalog.ResolveGuid(AssetGuid, Canonical) == Canonical, "typed navigation remains usable after JSON error");
        Registry(Entry()); EditorApplication.ProjectChanged();
        Check(IntercomAssetCatalog.Resolve(Alias) == Canonical, "corrected registry recovers on project change");
        Fixture(); Put(IntercomAssetCatalog.RegistryPath, "{\"revision\":\"unrelated\",\"files\":[]}");
        Check(IntercomAssetCatalog.Resolve(Alias) == Alias, "unrecognized registry revision is ignored");
    }
    private static void InvalidEntries()
    {
        groups++; Fixture(); Registry(Entry(destination: "../outside"));
        Check(IntercomAssetCatalog.Resolve(Alias) == Alias, "unsafe destination is not registered");
        Fixture(); Registry(Entry(guid: "invalid"));
        Check(IntercomAssetCatalog.Resolve(Alias) == Alias, "invalid GUID entry is not registered");
        Fixture(); Registry(Entry(alias: "../outside"));
        Check(IntercomAssetCatalog.Resolve("../outside") == null, "unsafe alias cannot resolve");
        Check(IntercomAssetCatalog.Resolve(Canonical) == Canonical, "valid canonical identity survives invalid alias");
        Fixture(); Registry(null, Entry());
        Check(IntercomAssetCatalog.Resolve(Alias) == Canonical, "null entry does not invalidate valid identities");
    }
    private static void RootChangesAndMissingResource()
    {
        groups++; Fixture(); Check(IntercomAssetCatalog.Resolve(Alias) == Canonical, "first project lookup");
        int reads = IntercomAssetCatalog.ManifestReadCount;
        Fixture();
        Check(IntercomAssetCatalog.Resolve(Alias) == Canonical, "new project root gets a new cache");
        Check(IntercomAssetCatalog.ManifestReadCount == reads + 1, "project root change reloads registry without event");
        File.Delete(PathFor(Canonical)); File.Delete(PathFor(Canonical) + ".meta");
        Check(IntercomAssetCatalog.Resolve(Alias) == Canonical, "missing identity falls back to its safe canonical path");
        Check(!File.Exists(PathFor(Canonical)) && !File.Exists(PathFor(Alias)), "fallback creates no missing resources");
    }
    private static void SafePathsAndFolderCreation()
    {
        groups++; Fixture();
        string[] unsafePaths = { null, "", "../escape", "/tmp/escape", "Assets/Other/file", "Assets/IntercomMVP/../outside",
            "Assets/IntercomMVP//Audio/asset", "Assets/IntercomMVP/Audio/./asset", "Assets/IntercomMVP/Audio/asset:stream",
            "Assets\\IntercomMVP\\Audio\\asset" };
        foreach (string path in unsafePaths)
        {
            Check(!IntercomAssetCatalog.SafePath(path), "unsafe path rejected: " + path);
            Check(IntercomAssetCatalog.Resolve(path) == null, "unsafe lookup cannot escape project: " + path);
            Check(IntercomAssetCatalog.ResolveGuid("not-a-guid", path) == null, "unsafe fallback cannot escape project: " + path);
            bool rejected = false;
            try { IntercomAssetCatalog.EnsureParentFolder(path); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "unsafe folder authoring rejected: " + path);
        }
        const string target = "Assets/IntercomMVP/NewFeature/Audio/Clip.wav";
        IntercomAssetCatalog.EnsureParentFolder(target);
        Check(Directory.Exists(PathFor("Assets/IntercomMVP/NewFeature/Audio")), "authoring creates valid nested folder");
        Check(!File.Exists(PathFor(target)), "folder authoring does not create dummy asset");
        Check(Path.GetFullPath(IntercomAssetCatalog.Absolute(target)) == Path.GetFullPath(PathFor(target)), "absolute path stays in active project");
        foreach (var key in IntercomProjectPaths.All)
            Check(key.Guid.Length == 32 && IntercomAssetCatalog.SafePath(key.CanonicalPath), "typed navigation has GUID and safe canonical path");
    }
    private static void Main()
    {
        try
        {
            IdentityAndCache(); ManualMoves(); InvalidationAndRegistryMove(); MissingAndBrokenRegistries();
            InvalidEntries(); RootChangesAndMissingResource(); SafePathsAndFolderCreation();
            Console.WriteLine("PASS: " + checks + " production asset catalog checks in " + groups + " groups; real registry implementation, filesystem Unity API stubs.");
        }
        finally
        {
            foreach (string path in fixtures) if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
