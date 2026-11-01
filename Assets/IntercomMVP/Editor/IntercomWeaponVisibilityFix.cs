using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    // This file also works on its own in the first version of the MVP project.
    internal sealed class IntercomWeaponVisibilityImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (assetPath == IntercomWeaponVisibilityFix.ModelPath)
                ((ModelImporter)assetImporter).importVisibility = false;
        }
    }

    [InitializeOnLoad]
    internal static class IntercomWeaponVisibilityFix
    {
        internal const string ModelPath = "Assets/IntercomMVP/Art/Robot/IntercomRobot.fbx";
        private const string ProjectMarker = "ProjectSettings/IntercomMvpProject.json";
        private const string AppliedMarker = "ProjectSettings/IntercomWeaponVisibilityFix-v1.json";
        private static bool queued;

        static IntercomWeaponVisibilityFix()
        {
            if (!File.Exists(ProjectMarker)) return;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;
            QueueAutomaticFix();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) { QueueAutomaticFix(); }
        private static void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode) { QueueAutomaticFix(); }

        private static void QueueAutomaticFix()
        {
            if (queued || File.Exists(AppliedMarker)) return;
            queued = true;
            EditorApplication.update += WaitForImports;
        }

        private static void WaitForImports()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= WaitForImports;
            queued = false;
            if (File.Exists(AppliedMarker) || !File.Exists(ModelPath)) return;
            try { ApplyFix(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Tools/Intercom MVP/Fix Weapon Visibility", false, 10)]
        internal static void ApplyFix()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new FileNotFoundException("Не найдена модель IntercomRobot.fbx.", ModelPath);
            if (importer.importVisibility)
            {
                importer.importVisibility = false;
                // Reuse the existing .meta, GUID, and all material remaps.
                importer.SaveAndReimport();
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            GameObject robot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Robot");
            if (robot == null) return;
            Transform[] transforms = robot.GetComponentsInChildren<Transform>(true);
            Transform[] weaponRoots = transforms.Where(transform =>
                transform.name == "Pistol_L" || transform.name == "Pistol_R").ToArray();
            Renderer[] weapons = weaponRoots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                .Distinct().ToArray();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Restore Intercom pistols and front view");
            bool changed = false;
            Vector3 angles = robot.transform.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(angles.y, 0f)) > 0.01f)
            {
                Undo.RecordObject(robot.transform, "Face the robot toward the camera");
                angles.y = 0f;
                robot.transform.eulerAngles = angles;
                changed = true;
            }
            var visited = new HashSet<GameObject>();
            foreach (Renderer renderer in weapons)
            {
                for (Transform current = renderer.transform; current != null && current != robot.transform; current = current.parent)
                {
                    GameObject gameObject = current.gameObject;
                    if (!visited.Add(gameObject) || gameObject.activeSelf) continue;
                    Undo.RecordObject(gameObject, "Activate the weapon hierarchy");
                    gameObject.SetActive(true);
                    RecordPrefab(gameObject);
                    changed = true;
                }
                if (!renderer.enabled)
                {
                    Undo.RecordObject(renderer, "Show pistol mesh");
                    renderer.enabled = true;
                    RecordPrefab(renderer);
                    changed = true;
                }
            }
            Undo.CollapseUndoOperations(undoGroup);
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            if (weaponRoots.Length == 2 && weapons.Length == 8 &&
                weapons.All(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy))
            {
                File.WriteAllText(AppliedMarker, "{\"version\":1,\"weaponRenderers\":8,\"importVisibility\":false}");
                Debug.Log("Intercom MVP: восстановлена видимость двух пистолетов (8 частей), робот развёрнут к камере. Сохрани сцену, если появилась звёздочка рядом с её названием.");
            }
            else
            {
                Debug.LogWarning("Intercom MVP: настройка видимости исправлена. В текущей сцене найдено частей оружия: " + weapons.Length +
                    ". Раскрой Pistol_L и Pistol_R в Hierarchy для проверки.");
            }
        }

        private static void RecordPrefab(UnityEngine.Object target)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
