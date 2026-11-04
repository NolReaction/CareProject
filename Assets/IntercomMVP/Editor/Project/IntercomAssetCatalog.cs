using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    /// <summary>Editor-only asset identity. Known GUIDs survive folder changes; registry IO is cached.</summary>
    [InitializeOnLoad]
    internal static class IntercomAssetCatalog
    {
        internal const string RegistryPath = "Assets/IntercomMVP/Editor/Project/ProjectAssetsV13.json";
        private const string RegistryGuid = "dd5f7628d7a55161b30d1e388ce6233d";
        [Serializable] internal sealed class Registry { public string revision; public Entry[] files; }
        [Serializable] internal sealed class Entry
        {
            public string legacyPath, destination, guid;
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static bool loaded;
        private static string projectRoot;
        internal static int ManifestReadCount { get; private set; }

        static IntercomAssetCatalog() { EditorApplication.projectChanged += Invalidate; }

        internal static void Invalidate()
        {
            loaded = false;
            Entries.Clear();
        }

        private static void EnsureCache()
        {
            // A domain normally sees one project. Keeping its root in the cache
            // key also makes tests and multiple isolated editor sessions explicit.
            string root = Application.dataPath;
            if (loaded && string.Equals(projectRoot, root, StringComparison.Ordinal)) return;
            Invalidate(); projectRoot = root; loaded = true;
            string registry = AssetDatabase.GUIDToAssetPath(RegistryGuid);
            Register(Load(string.IsNullOrEmpty(registry) ? RegistryPath : registry, "architecture-v13"));
        }

        private static Registry Load(string assetPath, string revision)
        {
            string path = Absolute(assetPath);
            if (!File.Exists(path)) return null;
            try
            {
                ManifestReadCount++;
                Registry registry = JsonUtility.FromJson<Registry>(File.ReadAllText(path));
                return registry != null && registry.revision == revision ? registry : null;
            }
            catch (Exception error)
            {
                Debug.LogWarning("CareProject: не удалось прочитать каталог ресурсов: " + assetPath + ". " + error.Message);
                return null;
            }
        }

        private static void Register(Registry registry)
        {
            if (registry == null || registry.files == null) return;
            foreach (Entry entry in registry.files)
            {
                if (entry == null || !SafePath(entry.destination) || !ValidGuid(entry.guid)) continue;
                if (SafePath(entry.legacyPath)) Entries[entry.legacyPath] = entry;
                Entries[entry.destination] = entry;
            }
        }

        internal static string Resolve(string path)
        {
            if (!SafePath(path)) return null;
            EnsureCache();
            if (!Entries.TryGetValue(path, out Entry entry)) return path;
            return ResolveGuid(entry.guid, entry.destination);
        }

        internal static string ResolveGuid(string guid, string fallback)
        {
            if (ValidGuid(guid))
            {
                string existing = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(existing)) return existing;
            }
            return SafePath(fallback) ? fallback : null;
        }

        private static bool ValidGuid(string guid)
        {
            if (guid == null || guid.Length != 32) return false;
            for (int i = 0; i < guid.Length; i++)
                if (!((guid[i] >= '0' && guid[i] <= '9') || (guid[i] >= 'a' && guid[i] <= 'f') || (guid[i] >= 'A' && guid[i] <= 'F'))) return false;
            return true;
        }

        internal static void EnsureParentFolder(string assetPath)
        {
            if (!SafePath(assetPath)) throw new ArgumentException("Asset path must stay below Assets/IntercomMVP.", nameof(assetPath));
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
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
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/IntercomMVP/", StringComparison.Ordinal)
                || path.Contains("\\") || path.Contains(":") || Path.IsPathRooted(path)) return false;
            foreach (string part in path.Split('/'))
                if (part.Length == 0 || part == "." || part == "..") return false;
            return true;
        }

        internal static string Absolute(string path) { return Path.Combine(Application.dataPath, "..", path); }

    }
}
