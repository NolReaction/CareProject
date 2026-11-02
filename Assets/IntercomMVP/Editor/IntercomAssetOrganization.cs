using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    /// <summary>
    /// The ZIP contains relocated originals plus importable old-path placeholders with distinct GUIDs.
    /// This makes first and repeated overwrite-only installs free from duplicate GUIDs.
    /// Remove only exact unreferenced placeholders, after the original GUID is available.
    /// </summary>
    [InitializeOnLoad]
    internal static class IntercomAssetOrganization
    {
        private const string SessionKey = "Intercom.FoundationV4.AssetOrganization";
        static IntercomAssetOrganization()
        {
            if (!SessionState.GetBool(SessionKey, false)) EditorApplication.delayCall += FinishUpgrade;
        }

        [MenuItem("Tools/Intercom MVP/Finish Folder Upgrade")]
        internal static void FinishUpgrade()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += FinishUpgrade;
                return;
            }
            SessionState.SetBool(SessionKey, true);
            int removed = 0, moved = 0, preserved = 0;
            try
            {
                IntercomAssetCatalog.Manifest manifest = IntercomAssetCatalog.ReadManifest();
                if (manifest == null || manifest.files == null) return;
                foreach (IntercomAssetCatalog.Entry entry in manifest.files)
                {
                    if (entry == null || !IntercomAssetCatalog.SafePath(entry.legacyPath)
                        || !IntercomAssetCatalog.SafePath(entry.destination)) continue;
                    string oldFile = IntercomAssetCatalog.Absolute(entry.legacyPath);
                    if (!File.Exists(oldFile) || !File.Exists(oldFile + ".meta")) continue;
                    if (HasLinkedAncestor(oldFile)) { preserved++; continue; }
                    string oldGuid = AssetDatabase.AssetPathToGUID(entry.legacyPath);
                    string originalPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                    // Partial upgrades from an unorganized checkout can still move the original safely.
                    if (oldGuid == entry.guid && originalPath == entry.legacyPath)
                    {
                        if (File.Exists(IntercomAssetCatalog.Absolute(entry.destination))) { preserved++; continue; }
                        IntercomAssetCatalog.EnsureParentFolder(entry.destination);
                        string error = AssetDatabase.MoveAsset(entry.legacyPath, entry.destination);
                        if (string.IsNullOrEmpty(error)) moved++;
                        else { preserved++; Debug.LogWarning("Intercom: " + error); }
                        continue;
                    }
                    bool originalPresent = !string.IsNullOrEmpty(originalPath) && originalPath != entry.legacyPath
                        && File.Exists(IntercomAssetCatalog.Absolute(originalPath));
                    if (!originalPresent || oldGuid != entry.tombstoneGuid) { preserved++; continue; }
                    if (!string.Equals(IntercomAssetCatalog.Digest(oldFile), entry.tombstoneSha256, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(IntercomAssetCatalog.Digest(oldFile + ".meta"), entry.tombstoneMetaSha256, StringComparison.OrdinalIgnoreCase))
                    { preserved++; continue; }
                    // A user can deliberately reuse a placeholder before running this command: keep it then.
                    if (IsReferenced(entry.legacyPath)) { preserved++; continue; }
                    if (AssetDatabase.DeleteAsset(entry.legacyPath)) removed++;
                }
                if (removed + moved > 0)
                    Debug.Log("Intercom: ресурсы пистолетов собраны в Game/Weapons/Pistols. Убрано временных копий: " + removed + ", перемещено: " + moved + ".");
                if (preserved > 0)
                    Debug.LogWarning("Intercom: сохранены изменённые или используемые старые ресурсы: " + preserved
                        + ". Ничего не перезаписано. Проверка: " + IntercomAssetCatalog.ManifestPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Intercom: организация папок не завершена: " + exception.Message);
            }
        }

        private static bool IsReferenced(string assetPath)
        {
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path == assetPath || AssetDatabase.IsValidFolder(path)) continue;
                foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                    if (dependency == assetPath) return true;
            }
            return false;
        }

        private static bool HasLinkedAncestor(string absolute)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            for (string current = Path.GetFullPath(absolute); !string.Equals(current, root, StringComparison.Ordinal); current = Path.GetDirectoryName(current))
            {
                if (string.IsNullOrEmpty(current)) return true;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            return false;
        }
    }
}
