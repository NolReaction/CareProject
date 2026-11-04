// External package maintenance only. This file is outside Assets and is not a Unity script.
// Compatible with Windows PowerShell 5.1 Add-Type and the standalone .NET regression project.
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace CareProject.Packages
{
    public sealed class PackageFile { public string Path; public string Sha256; public long Bytes; }
    public sealed class RetiredFile { public string Path; public string[] KnownSha256; public string Guid; public bool RequireGuid; }
    public sealed class KnownScript { public string Path; public string Guid; public string[] KnownSha256; }
    public sealed class ReceiptEntry { public string Path; public bool Existed; public string PreviousSha256; public string InstalledSha256; }
    public sealed class PackageResult
    {
        public string Version, ProjectRoot, BackupRoot;
        public int Copied, Removed, Unchanged, Verified;
        public bool VerifyOnly;
        public ReceiptEntry[] Receipt;
    }

    public static class PackageInstaller
    {
        private static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;
        private static readonly Regex HashPattern = new Regex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);
        private static readonly Regex GuidPattern = new Regex("^guid: ([0-9a-fA-F]{32})\\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        private sealed class Operation { public string Relative, Source, Target, Before, After; public bool Remove; }

        public static bool SupportsVersion(string version)
        { return version == "V18" || version == "V19"; }

        public static string HashFile(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream file = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }

        private static string Root(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Project/package path is empty.");
            string full = System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            if (full.Length == 0 || !Directory.Exists(full)) throw new InvalidOperationException("Directory does not exist: " + path);
            RejectReparse(full);
            return full;
        }

        private static string Normalize(string relative)
        {
            if (String.IsNullOrWhiteSpace(relative) || relative.IndexOf('\\') >= 0 || relative.IndexOf(':') >= 0
                || relative.StartsWith("/", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(relative))
                throw new InvalidOperationException("Unsafe package path: " + relative);
            string[] parts = relative.Split('/');
            foreach (string part in parts)
                if (part.Length == 0 || part == "." || part == ".." || part.EndsWith(".", StringComparison.Ordinal)
                    || part.EndsWith(" ", StringComparison.Ordinal) || part.IndexOfAny(new char[] { '<', '>', '"', '|', '?', '*', '\r', '\n', '\t' }) >= 0)
                    throw new InvalidOperationException("Unsafe package path: " + relative);
            string first = parts[0];
            bool folder = first == "Assets" || first == "Packages" || first == "ProjectSettings" || first == "Tools"
                || first == "BlenderTools" || first == "Source" || first == "Docs" || first == "Validation";
            bool rootFile = parts.Length == 1 && (first == "README.md" || first == "START_HERE.md" || first == "MOBILE_TESTING.md"
                || first == "PROJECT_FILES.json" || first == ".gitignore" || first == ".gitattributes"
                || first == "RepairProject.ps1" || first == "RepairProject.cmd");
            if ((!folder || parts.Length < 2) && !rootFile) throw new InvalidOperationException("Path is outside package-owned files: " + relative);
            return relative;
        }

        private static void RejectReparse(string path)
        {
            string current = System.IO.Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Junction/symbolic-link paths are not updated: " + current);
                current = System.IO.Path.GetDirectoryName(current);
            }
        }

        private static string At(string root, string relative)
        {
            Normalize(relative);
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            if (!full.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path escaped project: " + relative);
            RejectReparse(full);
            string parent = System.IO.Path.GetDirectoryName(full);
            while (!Paths.Equals(parent, root))
            {
                if (File.Exists(parent)) throw new InvalidOperationException("A file blocks a destination folder: " + relative);
                parent = System.IO.Path.GetDirectoryName(parent);
            }
            return full;
        }

        private static void VerifyFile(string root, PackageFile entry)
        {
            string path = At(root, entry.Path);
            if (!File.Exists(path) || new FileInfo(path).Length != entry.Bytes || !Paths.Equals(HashFile(path), entry.Sha256))
                throw new InvalidOperationException("Missing/changed file: " + entry.Path + ". Use the full current project package in a new folder if this is an incomplete base.");
        }

        private static Dictionary<string, PackageFile> Index(PackageFile[] entries)
        {
            if (entries == null || entries.Length == 0) throw new InvalidOperationException("Package inventory is empty.");
            Dictionary<string, PackageFile> result = new Dictionary<string, PackageFile>(Paths);
            foreach (PackageFile entry in entries)
            {
                if (entry == null || !HashPattern.IsMatch(entry.Sha256 ?? "") || entry.Bytes < 0) throw new InvalidOperationException("Invalid package hash/size.");
                Normalize(entry.Path);
                if (result.ContainsKey(entry.Path)) throw new InvalidOperationException("Case-insensitive duplicate package path: " + entry.Path);
                result.Add(entry.Path, entry);
            }
            return result;
        }

        private static void ValidateProject(string project)
        {
            foreach (string folder in new string[] { "Assets/IntercomMVP", "Packages", "ProjectSettings" })
                if (!Directory.Exists(System.IO.Path.Combine(project, folder))) throw new InvalidOperationException("Select the CareProject Unity root containing Assets, Packages and ProjectSettings: " + project);
            if (!File.Exists(System.IO.Path.Combine(project, "ProjectSettings/ProjectVersion.txt")))
                throw new InvalidOperationException("ProjectVersion.txt is missing; this is not a complete Unity project root.");
            string unityLock = System.IO.Path.Combine(project, "Temp/UnityLockfile");
            if (File.Exists(unityLock))
            {
                try { using (FileStream held = File.Open(unityLock, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } }
                catch (IOException) { throw new InvalidOperationException("Close Unity for this project before updating."); }
            }
        }

        private static bool Accepted(string hash, string[] known)
        {
            if (known != null) foreach (string candidate in known) if (Paths.Equals(hash, candidate)) return true;
            return false;
        }

        private static string ReadGuid(string path)
        {
            if (!File.Exists(path)) return "";
            Match match = GuidPattern.Match(File.ReadAllText(path));
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : "";
        }

        private static IEnumerable<string> AssetScriptMetas(string directory)
        {
            foreach (string path in Directory.GetFiles(directory, "*.cs.meta")) { RejectReparse(path); yield return path; }
            foreach (string child in Directory.GetDirectories(directory))
            {
                RejectReparse(child);
                foreach (string path in AssetScriptMetas(child)) yield return path;
            }
        }

        private static void Retire(string project, string relative, string[] hashes, string guid, bool requireGuid,
            Dictionary<string, PackageFile> expected, Dictionary<string, Operation> operations)
        {
            Normalize(relative);
            if (!relative.StartsWith("Assets/", StringComparison.Ordinal) || !(relative.EndsWith(".cs", StringComparison.Ordinal) || relative.EndsWith(".cs.meta", StringComparison.Ordinal)))
                throw new InvalidOperationException("Only known Intercom script files can be retired: " + relative);
            if (expected.ContainsKey(relative)) throw new InvalidOperationException("Retirement conflicts with current inventory: " + relative);
            string file = At(project, relative);
            if (!File.Exists(file)) return;
            string hash = HashFile(file);
            if (!Accepted(hash, hashes)) throw new InvalidOperationException("Old script was edited; preserved without changes: " + relative);
            if (requireGuid && !Paths.Equals(ReadGuid(relative.EndsWith(".meta", StringComparison.Ordinal) ? file : file + ".meta"), guid))
                throw new InvalidOperationException("Old script GUID does not match; preserved: " + relative);
            if (!operations.ContainsKey(relative)) operations.Add(relative, new Operation { Relative = relative, Target = file, Before = hash, Remove = true });
        }

        public static PackageResult Apply(string payloadRoot, string projectRoot, string version, PackageFile[] payloadFiles,
            PackageFile[] expectedFiles, RetiredFile[] retiredFiles, KnownScript[] knownScripts, bool verifyOnly)
        {
            if (!SupportsVersion(version)) throw new InvalidOperationException("Unsupported package version.");
            string payload = Root(payloadRoot), project = Root(projectRoot);
            ValidateProject(project);
            Dictionary<string, PackageFile> source = Index(payloadFiles), expected = Index(expectedFiles);
            Dictionary<string, Operation> operations = new Dictionary<string, Operation>(Paths);
            // Complete source and destination preflight happens before any backup or mutation.
            foreach (PackageFile entry in source.Values)
            {
                if (!expected.ContainsKey(entry.Path) && entry.Path != "PROJECT_FILES.json") throw new InvalidOperationException("Payload is outside project inventory: " + entry.Path);
                if (expected.ContainsKey(entry.Path) && (!Paths.Equals(expected[entry.Path].Sha256, entry.Sha256) || expected[entry.Path].Bytes != entry.Bytes))
                    throw new InvalidOperationException("Payload/full inventory disagree: " + entry.Path);
                VerifyFile(payload, entry);
                string destination = At(project, entry.Path);
                if (Directory.Exists(destination)) throw new InvalidOperationException("A directory blocks a destination file: " + entry.Path);
                string before = File.Exists(destination) ? HashFile(destination) : null;
                if (!Paths.Equals(before, entry.Sha256))
                {
                    if (before != null && (File.GetAttributes(destination) & FileAttributes.ReadOnly) != 0)
                        throw new InvalidOperationException("Destination is read-only: " + entry.Path);
                    operations.Add(entry.Path, new Operation { Relative = entry.Path, Source = At(payload, entry.Path), Target = destination, Before = before, After = entry.Sha256 });
                }
            }
            foreach (PackageFile entry in expected.Values) if (!source.ContainsKey(entry.Path)) VerifyFile(project, entry);
            if (retiredFiles != null) foreach (RetiredFile old in retiredFiles)
                Retire(project, old.Path, old.KnownSha256, old.Guid, old.RequireGuid, expected, operations);

            Dictionary<string, KnownScript> byGuid = new Dictionary<string, KnownScript>(Paths);
            if (knownScripts != null) foreach (KnownScript script in knownScripts)
            {
                Normalize(script.Path);
                if (String.IsNullOrEmpty(script.Guid) || script.Guid.Length != 32 || !script.Path.StartsWith("Assets/IntercomMVP/", StringComparison.Ordinal) || !script.Path.EndsWith(".cs", StringComparison.Ordinal))
                    throw new InvalidOperationException("Invalid known-script mapping.");
                if (byGuid.ContainsKey(script.Guid) && !Paths.Equals(byGuid[script.Guid].Path, script.Path))
                    throw new InvalidOperationException("Ambiguous canonical script GUID.");
                byGuid[script.Guid] = script;
            }
            foreach (string meta in AssetScriptMetas(System.IO.Path.Combine(project, "Assets")))
            {
                KnownScript script; string guid = ReadGuid(meta);
                if (!byGuid.TryGetValue(guid, out script)) continue;
                string relative = meta.Substring(project.Length + 1).Replace(System.IO.Path.DirectorySeparatorChar, '/');
                string sourceRelative = relative.Substring(0, relative.Length - 5);
                if (Paths.Equals(sourceRelative, script.Path) || expected.ContainsKey(sourceRelative)) continue;
                string file = meta.Substring(0, meta.Length - 5);
                if (!File.Exists(file)) throw new InvalidOperationException("Duplicate script metadata has no source: " + relative);
                Retire(project, sourceRelative, script.KnownSha256, script.Guid, true, expected, operations);
                if (!operations.ContainsKey(relative)) operations.Add(relative, new Operation { Relative = relative, Target = meta, Before = HashFile(meta), Remove = true });
            }
            List<Operation> ordered = new List<Operation>(operations.Values);
            ordered.Sort(delegate(Operation a, Operation b) { return StringComparer.Ordinal.Compare(a.Relative, b.Relative); });
            PackageResult result = new PackageResult { Version = version, ProjectRoot = project, VerifyOnly = verifyOnly,
                Verified = expected.Count, Unchanged = source.Count, Receipt = new ReceiptEntry[ordered.Count] };
            for (int i = 0; i < ordered.Count; i++)
            {
                Operation operation = ordered[i];
                result.Receipt[i] = new ReceiptEntry { Path = operation.Relative, Existed = operation.Before != null, PreviousSha256 = operation.Before, InstalledSha256 = operation.After };
                if (operation.Remove) result.Removed++; else { result.Copied++; result.Unchanged--; }
            }
            if (verifyOnly || ordered.Count == 0) return result;
            string backup = System.IO.Path.Combine(project, "ProjectBackups", version + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            RejectReparse(backup); Directory.CreateDirectory(backup); result.BackupRoot = backup;
            foreach (Operation operation in ordered)
                if (operation.Before != null)
                {
                    string target = System.IO.Path.Combine(backup, "Files", operation.Relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)); File.Copy(operation.Target, target, false);
                    if (!Paths.Equals(HashFile(target), operation.Before)) throw new IOException("Backup verification failed: " + operation.Relative);
                }
            WriteReceipt(backup, result);
            string log = System.IO.Path.Combine(backup, "Install.log");
            File.WriteAllText(log, "CareProject " + version + "\r\nTarget: " + project + "\r\n", new UTF8Encoding(true));
            try
            {
                foreach (Operation operation in ordered)
                {
                    // Recheck immediately before mutation, so a concurrent edit is never silently overwritten.
                    if (operation.Before == null ? File.Exists(operation.Target) : !File.Exists(operation.Target) || !Paths.Equals(HashFile(operation.Target), operation.Before))
                        throw new IOException("Project changed during installation: " + operation.Relative);
                    if (operation.Remove) File.Delete(operation.Target); else AtomicCopy(operation.Source, operation.Target);
                    File.AppendAllText(log, (operation.Remove ? "QUARANTINE " : "INSTALL ") + operation.Relative + "\r\n");
                }
                foreach (PackageFile entry in expected.Values) VerifyFile(project, entry);
                File.AppendAllText(log, "PASS: source inventory installed. Open Unity and run Tools > CareProject > Repair Project.\r\n");
                return result;
            }
            catch (Exception error)
            {
                File.AppendAllText(log, "FAILED: " + error.Message + "\r\n");
                try { Restore(project, backup, result.Receipt); File.AppendAllText(log, "ROLLBACK PASS\r\n"); }
                catch (Exception rollback) { throw new IOException("Update failed; automatic rollback could not finish. Backup: " + backup + ". " + rollback.Message, error); }
                throw new IOException("Update failed and was rolled back. Backup: " + backup + ". " + error.Message, error);
            }
        }

        private static void AtomicCopy(string source, string target)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
            string temporary = target + ".intercom-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, temporary, false);
                if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static string Quote(string value)
        {
            if (value == null) return "null";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
        }

        private static void WriteReceipt(string backup, PackageResult result)
        {
            StringBuilder json = new StringBuilder("{\n  \"schema\": 1,\n  \"version\": ");
            json.Append(Quote(result.Version)).Append(",\n  \"projectRoot\": ");
            json.Append(Quote(result.ProjectRoot)).Append(",\n  \"entries\": [\n");
            for (int i = 0; i < result.Receipt.Length; i++)
            {
                ReceiptEntry e = result.Receipt[i];
                if (i > 0) json.Append(",\n");
                json.Append("    {\"path\": ").Append(Quote(e.Path)).Append(", \"existed\": ").Append(e.Existed ? "true" : "false")
                    .Append(", \"previousSha256\": ").Append(Quote(e.PreviousSha256)).Append(", \"installedSha256\": ").Append(Quote(e.InstalledSha256)).Append("}");
            }
            json.Append("\n  ]\n}\n");
            File.WriteAllText(System.IO.Path.Combine(backup, "Receipt.json"), json.ToString(), new UTF8Encoding(true));
        }

        public static void Restore(string projectRoot, string backupRoot, ReceiptEntry[] entries)
        {
            string project = Root(projectRoot), backup = Root(backupRoot); ValidateProject(project);
            if (!backup.StartsWith(System.IO.Path.Combine(project, "ProjectBackups") + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Backup must belong to this project's ProjectBackups folder.");
            if (entries == null) throw new InvalidOperationException("Restore receipt is missing.");
            HashSet<string> seen = new HashSet<string>(Paths);
            // Verify every backup and current destination before restoring any file.
            foreach (ReceiptEntry entry in entries)
            {
                Normalize(entry.Path); if (!seen.Add(entry.Path)) throw new InvalidOperationException("Duplicate restore path.");
                string target = At(project, entry.Path), current = File.Exists(target) ? HashFile(target) : null;
                // A planned creation may never have run when apply failed, or
                // may already have been removed by an earlier restore.
                if (!entry.Existed && current == null) continue;
                if (entry.Existed)
                {
                    if (!HashPattern.IsMatch(entry.PreviousSha256 ?? "")) throw new InvalidOperationException("Bad backup hash.");
                    string original = System.IO.Path.Combine(backup, "Files", entry.Path.Replace('/', System.IO.Path.DirectorySeparatorChar)); RejectReparse(original);
                    if (!File.Exists(original) || !Paths.Equals(HashFile(original), entry.PreviousSha256)) throw new InvalidOperationException("Backup is missing/changed: " + entry.Path);
                    if (Paths.Equals(current, entry.PreviousSha256)) continue;
                }
                if (!Paths.Equals(current, entry.InstalledSha256)) throw new InvalidOperationException("File changed after the update; preserved: " + entry.Path);
            }
            foreach (ReceiptEntry entry in entries)
            {
                string target = At(project, entry.Path);
                if (entry.Existed)
                {
                    if (File.Exists(target) && Paths.Equals(HashFile(target), entry.PreviousSha256)) continue;
                    AtomicCopy(System.IO.Path.Combine(backup, "Files", entry.Path.Replace('/', System.IO.Path.DirectorySeparatorChar)), target);
                }
                else if (File.Exists(target)) File.Delete(target);
            }
        }
    }
}
