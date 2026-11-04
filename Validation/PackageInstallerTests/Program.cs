using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using CareProject.Packages;

static class Program
{
    static int assertions, scenarios;
    const string A = "Assets/IntercomMVP/Runtime/NewModel.cs";
    const string Old = "Assets/IntercomMVP/Runtime/OldModel.cs";
    const string Stable = "Assets/IntercomMVP/Stable.txt";
    const string Guid = "0123456789abcdef0123456789abcdef";
    static void Check(bool condition, string reason) { assertions++; if (!condition) throw new Exception(reason); }
    static void Reject(Action action, string reason)
    {
        try { action(); } catch (Exception e) { Check(e.Message.Contains(reason, StringComparison.OrdinalIgnoreCase), e.Message + " expected " + reason); return; }
        throw new Exception("Accepted invalid operation: " + reason);
    }
    sealed class Fixture : IDisposable
    {
        public string Root = Path.Combine(Path.GetTempPath(), "CareProject installer " + System.Guid.NewGuid().ToString("N"));
        public string Project, Payload;
        public Fixture()
        {
            Project = Path.Combine(Root, "Project Привет space"); Payload = Path.Combine(Root, "Payload");
            Directory.CreateDirectory(Path.Combine(Project, "Assets/IntercomMVP")); Directory.CreateDirectory(Path.Combine(Project, "Packages"));
            Directory.CreateDirectory(Path.Combine(Project, "ProjectSettings")); Directory.CreateDirectory(Payload);
            Write(Project, "ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.3.26f1\n");
            Write(Project, Stable, "keep me"); Write(Payload, A, "new model");
        }
        public PackageFile File(string root, string path) { return new PackageFile { Path = path, Sha256 = PackageInstaller.HashFile(Path.Combine(root, path)), Bytes = new FileInfo(Path.Combine(root, path)).Length }; }
        public PackageFile[] PayloadFiles() { return new[] { File(Payload, A) }; }
        public PackageFile[] Expected() { return new[] { File(Payload, A), File(Project, Stable) }; }
        public PackageResult Apply(bool verify = false, RetiredFile[] retired = null, KnownScript[] scripts = null)
        { return PackageInstaller.Apply(Payload, Project, "V19", PayloadFiles(), Expected(), retired, scripts, verify); }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    static void Write(string root, string path, string value)
    { string file = Path.Combine(root, path); Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file, value, new UTF8Encoding(false)); }
    static string Text(string root, string path) { return File.ReadAllText(Path.Combine(root, path)); }
    static string Snapshot(string root)
    {
        var data = new List<string>(); foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            data.Add(Path.GetRelativePath(root, file) + "=" + PackageInstaller.HashFile(file)); data.Sort(StringComparer.Ordinal); return string.Join("\n", data);
    }
    static RetiredFile[] Retired(Fixture f)
    { return new[] { new RetiredFile { Path = Old, KnownSha256 = new[] { PackageInstaller.HashFile(Path.Combine(f.Project, Old)) } } }; }
    static void BackupRestoreAndIdempotence()
    {
        using var f = new Fixture(); Write(f.Project, A, "my existing new-path version"); Write(f.Project, Old, "old published model");
        string before = Snapshot(f.Project); var retired = Retired(f); var check = f.Apply(true, retired);
        Check(Snapshot(f.Project) == before && check.VerifyOnly && check.BackupRoot == null, "dry-run is read only");
        var result = f.Apply(false, retired);
        Check(result.Copied == 1 && result.Removed == 1 && result.Verified == 2, "exact apply counts");
        Check(Text(f.Project, A) == "new model" && !File.Exists(Path.Combine(f.Project, Old)), "replacement and retirement actually happen");
        Check(Text(result.BackupRoot, "Files/" + A) == "my existing new-path version" && Text(result.BackupRoot, "Files/" + Old) == "old published model", "both original sources retained");
        using (var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BackupRoot, "Receipt.json"))))
            Check(json.RootElement.GetProperty("entries").GetArrayLength() == 2 && json.RootElement.GetProperty("projectRoot").GetString() == f.Project, "persisted restore receipt is valid JSON with exact project");
        var again = f.Apply(false, retired); Check(again.Copied == 0 && again.Removed == 0 && again.BackupRoot == null, "repeat install creates no new backup or mutation");
        PackageInstaller.Restore(f.Project, result.BackupRoot, result.Receipt);
        Check(Text(f.Project, A) == "my existing new-path version" && Text(f.Project, Old) == "old published model", "restore returns original bytes");
        PackageInstaller.Restore(f.Project, result.BackupRoot, result.Receipt); Check(Text(f.Project, Old) == "old published model", "restore itself is idempotent");
        scenarios++;
    }
    static void BadPayloadAndUnchangedBase()
    {
        using var f = new Fixture(); var payload = f.PayloadFiles(); var expected = f.Expected(); Write(f.Payload, A, "tampered"); string before = Snapshot(f.Project);
        Reject(() => PackageInstaller.Apply(f.Payload, f.Project, "V19", payload, expected, null, null, false), "Missing/changed file");
        Check(Snapshot(f.Project) == before && !Directory.Exists(Path.Combine(f.Project, "ProjectBackups")), "bad payload produces no project mutations");
        payload = f.PayloadFiles(); expected = f.Expected(); Write(f.Project, Stable, "edited outside payload"); before = Snapshot(f.Project);
        Reject(() => PackageInstaller.Apply(f.Payload, f.Project, "V19", payload, expected, null, null, false), "Missing/changed file");
        Check(Snapshot(f.Project) == before, "incomplete base preflight preserves edits"); scenarios++;
    }
    static void UnknownRetiredAndMissingOld()
    {
        using var f = new Fixture(); Write(f.Project, Old, "published"); var retired = Retired(f); Write(f.Project, Old, "my edited old script"); string before = Snapshot(f.Project);
        Reject(() => f.Apply(false, retired), "Old script was edited"); Check(Snapshot(f.Project) == before, "unknown retired script blocks entire update before copy");
        File.Delete(Path.Combine(f.Project, Old)); var result = f.Apply(false, retired);
        Check(result.Copied == 1 && result.Removed == 0, "absent retired sources are normal"); scenarios++;
    }
    static void CreatedFileRestoreAndEditedRestore()
    {
        using var f = new Fixture(); var result = f.Apply(); Check(File.Exists(Path.Combine(f.Project, A)), "new file created");
        Write(f.Project, A, "after-install manual edit"); string before = Snapshot(f.Project);
        Reject(() => PackageInstaller.Restore(f.Project, result.BackupRoot, result.Receipt), "changed after the update");
        Check(Snapshot(f.Project) == before, "restore does not erase post-install edits");
        Write(f.Project, A, "new model"); PackageInstaller.Restore(f.Project, result.BackupRoot, result.Receipt);
        Check(!File.Exists(Path.Combine(f.Project, A)) && Text(f.Project, Stable) == "keep me", "restore removes only package-created file"); scenarios++;
        PackageInstaller.Restore(f.Project, result.BackupRoot, result.Receipt);
        Check(!File.Exists(Path.Combine(f.Project, A)), "repeat restore accepts an already absent package-created file");
    }
    static void KnownNestedAliasAndUnknownAlias()
    {
        using var f = new Fixture(); const string alias = "Assets/Old_Package/IntercomCopy.cs";
        Write(f.Project, alias, "known old source"); Write(f.Project, alias + ".meta", "fileFormatVersion: 2\nguid: " + Guid + "\n");
        var known = new[] { new KnownScript { Path = A, Guid = Guid, KnownSha256 = new[] { PackageInstaller.HashFile(Path.Combine(f.Project, alias)) } } };
        var result = f.Apply(false, null, known);
        Check(result.Removed == 2 && !File.Exists(Path.Combine(f.Project, alias)) && !File.Exists(Path.Combine(f.Project, alias + ".meta")), "known GUID alias source+sidecar quarantined");
        Check(Text(result.BackupRoot, "Files/" + alias) == "known old source", "alias backup retained");
        PackageInstaller.Restore(f.Project, result.BackupRoot, result.Receipt); Write(f.Project, alias, "edited alias"); string before = Snapshot(f.Project);
        Reject(() => f.Apply(false, null, known), "Old script was edited"); Check(Snapshot(f.Project) == before, "edited duplicate stops before mutation"); scenarios++;
    }
    static void AliasGuidGuardAndUnknownCustom()
    {
        using var f = new Fixture(); Write(f.Project, Old, "published"); Write(f.Project, Old + ".meta", "guid: ffffffffffffffffffffffffffffffff\n");
        var retired = Retired(f); retired[0].Guid = Guid; retired[0].RequireGuid = true; string before = Snapshot(f.Project);
        Reject(() => f.Apply(false, retired), "GUID does not match"); Check(Snapshot(f.Project) == before, "legacy GUID guard preserves unknown identity");
        Write(f.Project, "Assets/MyOwnScript.cs", "class UserFeature {}"); var result = f.Apply();
        Check(Text(f.Project, "Assets/MyOwnScript.cs") == "class UserFeature {}" && File.Exists(Path.Combine(f.Project, Old)), "unlisted custom and old sources are untouched"); scenarios++;
    }
    static void UnsafePathsAndCaseCollision()
    {
        using var f = new Fixture(); var good = f.PayloadFiles()[0];
        foreach (string path in new[] { "../outside.cs", "Assets/../outside.cs", ".git/config", "Library/test", "/tmp/test", "Assets\\test.cs", "Assets/bad:stream.cs", "Assets/a./x.cs" })
        {
            var file = new PackageFile { Path = path, Sha256 = good.Sha256, Bytes = good.Bytes };
            Reject(() => PackageInstaller.Apply(f.Payload, f.Project, "V19", new[] { file }, new[] { file }, null, null, false), path.Contains(".git") || path.StartsWith("Library") ? "outside package" : "Unsafe");
        }
        var lower = new PackageFile { Path = "Assets/intercommvp/Runtime/NewModel.cs", Sha256 = good.Sha256, Bytes = good.Bytes };
        Reject(() => PackageInstaller.Apply(f.Payload, f.Project, "V19", new[] { good, lower }, f.Expected(), null, null, false), "duplicate package path");
        Check(!File.Exists(Path.Combine(f.Project, A)) && !Directory.Exists(Path.Combine(f.Project, "ProjectBackups")), "invalid manifests never mutate destination"); scenarios++;
    }
    static void WrongRootLockedAndBlockedParent()
    {
        using var f = new Fixture(); string wrong = Path.Combine(f.Root, "not a project"); Directory.CreateDirectory(wrong);
        Reject(() => PackageInstaller.Apply(f.Payload, wrong, "V19", f.PayloadFiles(), f.Expected(), null, null, false), "Unity root");
        string unityLock = Path.Combine(f.Project, "Temp/UnityLockfile"); Directory.CreateDirectory(Path.GetDirectoryName(unityLock));
        using (FileStream locked = File.Open(unityLock, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            Reject(() => f.Apply(), "Close Unity");
        File.Delete(unityLock); Write(f.Project, "Assets/IntercomMVP/Runtime", "blocking file");
        Reject(() => f.Apply(), "blocks a destination folder"); Check(!Directory.Exists(Path.Combine(f.Project, "ProjectBackups")), "preflight checks all parents before backup/copy"); scenarios++;
    }
    static void SelfRepairAndTamperedBackup()
    {
        using var f = new Fixture(); Write(f.Project, A, "installed"); Write(f.Project, Old, "known stale"); var retired = Retired(f);
        var expected = new[] { f.File(f.Project, A), f.File(f.Project, Stable) };
        var result = PackageInstaller.Apply(f.Project, f.Project, "V19", expected, expected, retired, null, false);
        Check(result.Copied == 0 && result.Removed == 1 && !File.Exists(Path.Combine(f.Project, Old)), "full-project in-place cleanup works without copying a file onto itself");
        Write(result.BackupRoot, "Files/" + Old, "corrupted backup"); string before = Snapshot(f.Project);
        Reject(() => PackageInstaller.Restore(f.Project, result.BackupRoot, result.Receipt), "Backup is missing/changed");
        Check(Snapshot(f.Project) == before, "bad backup preflight prevents partial restore"); scenarios++;
    }
    static void SymlinkBoundary()
    {
        using var f = new Fixture(); string outside = Path.Combine(f.Root, "outside"); Directory.CreateDirectory(outside);
        string parent = Path.Combine(f.Project, "Assets/IntercomMVP/Runtime");
        try { Directory.CreateSymbolicLink(parent, outside); }
        catch (PlatformNotSupportedException) { return; }
        catch (UnauthorizedAccessException) { return; }
        Reject(() => f.Apply(), "Junction/symbolic-link"); Check(Directory.GetFiles(outside).Length == 0, "junction escape writes nothing outside target"); scenarios++;
    }
    static void VersionCompatibilityAndReceipts()
    {
        foreach (string version in new[] { "V18", "V19" })
        {
            using var f = new Fixture(); Write(f.Project, A, "previous source");
            var result = PackageInstaller.Apply(f.Payload, f.Project, version, f.PayloadFiles(), f.Expected(), null, null, false);
            Check(result.Version == version && Path.GetFileName(result.BackupRoot).StartsWith(version + "_", StringComparison.Ordinal), "receipt and backup retain actual package version");
            using (var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BackupRoot, "Receipt.json"))))
                Check(receipt.RootElement.GetProperty("version").GetString() == version, "persisted receipt is not hard-coded to an earlier package");
            PackageInstaller.Restore(f.Project, result.BackupRoot, result.Receipt);
            Check(Text(f.Project, A) == "previous source", "both supported package receipts restore original bytes");
        }
        using (var f = new Fixture())
        {
            string before = Snapshot(f.Project);
            Reject(() => PackageInstaller.Apply(f.Payload, f.Project, "V20", f.PayloadFiles(), f.Expected(), null, null, false), "Unsupported package version");
            Check(before == Snapshot(f.Project) && !Directory.Exists(Path.Combine(f.Project, "ProjectBackups")), "unsupported package version fails before mutation");
        }
        Check(!PackageInstaller.SupportsVersion(null) && !PackageInstaller.SupportsVersion("V1"), "unknown restore version rejected");
        scenarios++;
    }
    static void Main()
    {
        BackupRestoreAndIdempotence(); BadPayloadAndUnchangedBase(); UnknownRetiredAndMissingOld(); CreatedFileRestoreAndEditedRestore();
        KnownNestedAliasAndUnknownAlias(); AliasGuidGuardAndUnknownCustom(); UnsafePathsAndCaseCollision(); WrongRootLockedAndBlockedParent();
        SelfRepairAndTamperedBackup(); SymlinkBoundary(); VersionCompatibilityAndReceipts();
        Console.WriteLine("Package installer: " + scenarios + " real filesystem scenarios, " + assertions + " assertions PASS. Production C#5-compatible transaction core; PowerShell/Windows GUI and Unity were not executed.");
    }
}
