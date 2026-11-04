using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    [Serializable]
    internal sealed class RobotTextureReference
    {
        public string filename;
        public string assetPath;
        public string originalSourcePath;
        public bool available;
    }

    [Serializable]
    internal sealed class RobotMaterialReference
    {
        public string sourceName;
        public float[] baseColorRGBA;
        public bool baseColorUsesTextureDirectly;
        public float[] baseColorMultiplierRGBA;
        public float metallic;
        public float roughness = 0.7f;
        public RobotTextureReference baseColorTexture;
        public RobotTextureReference normalTexture;
        public float normalStrength = 1f;
        public float[] emissionRGBA;
        public float emissionStrength;
    }

    [Serializable]
    internal sealed class RobotMaterialManifest
    {
        public RobotMaterialReference[] materials;
    }

    internal static class IntercomRobotAssets
    {
        internal const string RootPath = "Assets/IntercomMVP";
        internal const string RobotPath = RootPath + "/Content/Characters/Robot";
        internal const string ModelPath = RobotPath + "/Model/IntercomRobot.fbx";
        internal const string ManifestPath = RobotPath + "/RobotMaterials.json";
        private const string MaterialsPath = RobotPath + "/Materials";

        internal static RobotMaterialManifest LoadManifest()
        {
            if (!File.Exists(ManifestPath))
                throw new FileNotFoundException("Не найден список материалов робота.", ManifestPath);
            var manifest = JsonUtility.FromJson<RobotMaterialManifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.materials == null || manifest.materials.Length == 0)
                throw new InvalidDataException("Список материалов робота пуст.");
            return manifest;
        }

        internal static int EnsureAssets(string searchFolder = null)
        {
            var manifest = LoadManifest();
            var references = TextureReferences(manifest).ToArray();
            var folderIndex = BuildFolderIndex(searchFolder);
            bool copied = false;
            foreach (var reference in references)
            {
                string destination = TexturePath(reference);
                if (File.Exists(destination)) continue;
                string source = FindOriginal(reference, folderIndex);
                if (source == null) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, false);
                copied = true;
            }
            Directory.CreateDirectory(MaterialsPath);
            if (copied || !AssetDatabase.IsValidFolder(MaterialsPath))
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var material in manifest.materials)
            {
                ConfigureTexture(material.baseColorTexture, false);
                ConfigureTexture(material.normalTexture, true);
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit ещё не загрузился. Дождись импорта пакетов и повтори создание сцены.");
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new FileNotFoundException("Unity не импортировал FBX робота.", ModelPath);
            // First-time FBX import may run before these editor scripts have
            // compiled. Apply the settings explicitly as well as in the hook.
            bool remapChanged = IntercomRobotImporter.Configure(importer);
            var externalMap = importer.GetExternalObjectMap();
            foreach (var entry in manifest.materials)
            {
                string materialPath = MaterialsPath + "/" + SafeFileName(entry.sourceName) + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader) { name = entry.sourceName };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                else material.shader = shader;
                var albedo = LoadTexture(entry.baseColorTexture);
                var normal = LoadTexture(entry.normalTexture);
                Color color = ReadColor(entry.baseColorRGBA, Color.white);
                if (albedo != null)
                    color = ReadColor(entry.baseColorMultiplierRGBA,
                        entry.baseColorUsesTextureDirectly ? Color.white : color);
                else if (entry.baseColorTexture != null)
                    color = new Color(0.40f, 0.51f, 0.47f, 1f);
                material.SetColor("_BaseColor", color);
                material.SetTexture("_BaseMap", albedo);
                material.SetFloat("_Metallic", Mathf.Clamp01(entry.metallic));
                material.SetFloat("_Smoothness", Mathf.Clamp01(1f - entry.roughness));
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", entry.normalStrength);
                if (normal != null) material.EnableKeyword("_NORMALMAP");
                else material.DisableKeyword("_NORMALMAP");
                var emission = ReadColor(entry.emissionRGBA, Color.black) * entry.emissionStrength;
                material.SetColor("_EmissionColor", emission);
                if (emission.maxColorComponent > 0f) material.EnableKeyword("_EMISSION");
                else material.DisableKeyword("_EMISSION");
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.sourceName);
                if (!externalMap.TryGetValue(id, out var oldMaterial) || oldMaterial != material)
                {
                    importer.AddRemap(id, material);
                    remapChanged = true;
                }
            }
            AssetDatabase.SaveAssets();
            if (remapChanged) importer.SaveAndReimport();
            return MissingTextureNames().Count;
        }

        internal static List<string> MissingTextureNames()
        {
            return TextureReferences(LoadManifest())
                .Where(reference => !File.Exists(TexturePath(reference)))
                .Select(reference => reference.filename)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();
        }

        private static IEnumerable<RobotTextureReference> TextureReferences(RobotMaterialManifest manifest)
        {
            return manifest.materials.SelectMany(material => new[] { material.baseColorTexture, material.normalTexture })
                .Where(reference => reference != null && !string.IsNullOrEmpty(reference.filename))
                .GroupBy(reference => TexturePath(reference), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());
        }

        private static Dictionary<string, string> BuildFolderIndex(string folder)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return result;
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                try
                {
                    foreach (string path in Directory.EnumerateFiles(current))
                    {
                        string extension = Path.GetExtension(path).ToLowerInvariant();
                        if (extension != ".jpg" && extension != ".jpeg" && extension != ".png") continue;
                        string name = Path.GetFileName(path);
                        if (!result.ContainsKey(name)) result.Add(name, path);
                    }
                    foreach (string child in Directory.EnumerateDirectories(current))
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            return result;
        }

        private static string FindOriginal(RobotTextureReference reference, Dictionary<string, string> folderIndex)
        {
            if (!string.IsNullOrEmpty(reference.originalSourcePath) && File.Exists(reference.originalSourcePath))
                return reference.originalSourcePath;
            return folderIndex.TryGetValue(reference.filename, out string path) ? path : null;
        }

        private static string TexturePath(RobotTextureReference reference)
        {
            string path = string.IsNullOrEmpty(reference.assetPath)
                ? RobotPath + "/Textures/" + Path.GetFileName(reference.filename)
                : reference.assetPath.Replace('\\', '/');
            if (!path.StartsWith(RobotPath + "/Textures/", StringComparison.Ordinal) || path.Contains(".."))
                throw new InvalidDataException("Некорректный путь текстуры: " + path);
            return path;
        }

        private static void ConfigureTexture(RobotTextureReference reference, bool normal)
        {
            if (reference == null || string.IsNullOrEmpty(reference.filename)) return;
            string path = TexturePath(reference);
            if (!File.Exists(path)) return;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool changed = importer.textureType != type || importer.sRGBTexture == normal ||
                !importer.mipmapEnabled || importer.maxTextureSize != 2048 ||
                (normal && (importer.convertToNormalmap || importer.flipGreenChannel));
            importer.textureType = type;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            if (normal) { importer.convertToNormalmap = false; importer.flipGreenChannel = false; }
            if (changed) importer.SaveAndReimport();
        }

        private static Texture2D LoadTexture(RobotTextureReference reference)
        {
            return reference == null || string.IsNullOrEmpty(reference.filename) ? null :
                AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(reference));
        }

        private static Color ReadColor(float[] rgba, Color fallback)
        {
            return rgba != null && rgba.Length >= 3
                ? new Color(rgba[0], rgba[1], rgba[2], rgba.Length > 3 ? rgba[3] : 1f) : fallback;
        }

        private static string SafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) || c == '/' || c == '\\' || c == ':' ? '_' : c).ToArray());
        }
    }

    internal sealed class IntercomRobotImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!string.Equals(assetPath, IntercomRobotAssets.ModelPath, StringComparison.Ordinal)) return;
            Configure((ModelImporter)assetImporter);
        }

        internal static bool Configure(ModelImporter importer)
        {
            bool changed = importer.animationType != ModelImporterAnimationType.Generic ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                !importer.importAnimation || importer.animationCompression != ModelImporterAnimationCompression.Off ||
                importer.importNormals != ModelImporterNormals.Import ||
                importer.importTangents != ModelImporterTangents.CalculateMikk ||
                importer.optimizeGameObjects || importer.importCameras || importer.importLights || importer.importVisibility ||
                importer.addCollider || !Mathf.Approximately(importer.globalScale, 1f) ||
                !importer.useFileUnits || !importer.bakeAxisConversion || !importer.preserveHierarchy ||
                importer.isReadable || importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.optimizeGameObjects = false;
            importer.importCameras = false;
            importer.importLights = false;
            // Blender can export a hidden bone as FBX Visibility=0. This must
            // not hide rigid weapon renderers below that bone in Unity.
            importer.importVisibility = false;
            importer.addCollider = false;
            importer.globalScale = 1f;
            importer.useFileUnits = true;
            importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;
            // Editor code can read vertices outside Play Mode even with the
            // runtime CPU copy disabled. Scene setup never runs in Play Mode.
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            return changed;
        }
    }
}
