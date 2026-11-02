using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    /// <summary>Stable GUID lookup across folder changes. Existing user locations take priority.</summary>
    internal static class IntercomAssetCatalog
    {
        internal const string ManifestPath = "Assets/IntercomMVP/Editor/AssetOrganizationV4.json";
        [Serializable] internal sealed class Manifest { public string revision; public Entry[] files; }
        [Serializable] internal sealed class Entry
        {
            public string legacyPath, destination, guid, tombstoneGuid;
            public string tombstoneSha256, tombstoneMetaSha256;
        }

        internal static Manifest ReadManifest()
        {
            string path = Absolute(ManifestPath);
            if (!File.Exists(path)) return null;
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            return manifest != null && manifest.revision == "foundation-v4" ? manifest : null;
        }

        internal static string Resolve(string legacyPath)
        {
            Manifest manifest = ReadManifest();
            if (manifest == null || manifest.files == null) return legacyPath;
            foreach (Entry entry in manifest.files)
            {
                if (entry == null || entry.legacyPath != legacyPath || !SafePath(entry.destination)) continue;
                string existing = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (!string.IsNullOrEmpty(existing)) return existing;
                return entry.destination;
            }
            return legacyPath;
        }

        internal static void EnsureParentFolder(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            EnsureFolder(folder);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Asset folder must be below Assets.");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        internal static bool SafePath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/IntercomMVP/", StringComparison.Ordinal)
                && !path.Contains("..") && !path.Contains("\\") && !Path.IsPathRooted(path);
        }

        internal static string Absolute(string path) { return Path.Combine(Application.dataPath, "..", path); }

        internal static string Digest(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
