using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    /// <summary>Finish an overwrite-only ZIP upgrade without retaining the retired scene generators.</summary>
    [InitializeOnLoad]
    internal static class IntercomLegacyCleanup
    {
        private const string ManifestPath = "Assets/IntercomMVP/Editor/LegacyCleanup.json";
        private const string SessionKey = "Intercom.GameplayV3.LegacyCleanup";
        [Serializable] private sealed class Manifest { public string revision; public Entry[] files; }
        [Serializable] private sealed class Entry { public string path; public string sha256; }

        static IntercomLegacyCleanup()
        {
            if (!SessionState.GetBool(SessionKey, false)) EditorApplication.delayCall += FinishUpgrade;
        }

        private static void FinishUpgrade()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += FinishUpgrade;
                return;
            }
            SessionState.SetBool(SessionKey, true);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string manifestFile = Path.Combine(root, ManifestPath);
            if (!File.Exists(manifestFile)) return;
            int removed = 0, preserved = 0;
            try
            {
                Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestFile));
                if (manifest == null || manifest.revision != "gameplay-v3" || manifest.files == null) return;
                var expected = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Entry entry in manifest.files)
                    if (entry != null && !string.IsNullOrEmpty(entry.path)) expected[entry.path] = entry.sha256;
                foreach (Entry entry in manifest.files)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.path) || string.IsNullOrEmpty(entry.sha256)) continue;
                    // The manifest is a fixed list from the previous release. Never traverse outside the project.
                    if (Path.IsPathRooted(entry.path) || entry.path.Contains("..") || entry.path.Contains("\\")) continue;
                    string absolute = Path.GetFullPath(Path.Combine(root, entry.path));
                    if (!absolute.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(absolute)) continue;
                    if (HasLinkedAncestor(absolute, root)) continue;
                    string digest = Digest(absolute);
                    // An edited legacy file belongs to the user: leave it for manual review.
                    if (!string.Equals(digest, entry.sha256, StringComparison.OrdinalIgnoreCase)) { preserved++; continue; }
                    if (entry.path.StartsWith("Assets/", StringComparison.Ordinal))
                    {
                        if (entry.path.EndsWith(".meta", StringComparison.Ordinal)) continue;
                        // DeleteAsset also removes its .meta; preserve an independently edited importer.
                        if (File.Exists(absolute + ".meta") && (!expected.TryGetValue(entry.path + ".meta", out string metaHash)
                            || !string.Equals(Digest(absolute + ".meta"), metaHash, StringComparison.OrdinalIgnoreCase)))
                        { preserved++; continue; }
                        if (AssetDatabase.DeleteAsset(entry.path)) removed++;
                    }
                    else { File.Delete(absolute); removed++; }
                }
                if (removed > 0) Debug.Log("Intercom: обновление учебного двора убрало устаревшие файлы: " + removed + ".");
                if (preserved > 0) Debug.Log("Intercom: изменённые старые файлы сохранены для ручной проверки: " + preserved + ". Список: " + ManifestPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Intercom: очистка старых файлов не завершена: " + exception.Message);
            }
        }

        private static string Digest(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static bool HasLinkedAncestor(string path, string root)
        {
            for (string current = path; !string.Equals(current, root, StringComparison.Ordinal); current = Path.GetDirectoryName(current))
            {
                if (string.IsNullOrEmpty(current)) return true;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            return false;
        }
    }
}
