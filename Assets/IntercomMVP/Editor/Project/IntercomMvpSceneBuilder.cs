using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    /// <summary>Opens the authored game scene and shares explicit camera/bounds authoring helpers.</summary>
    internal static class IntercomMvpSceneBuilder
    {
        internal const string ScenePath = "Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity";

        [MenuItem("Tools/CareProject/Open Scene", false, 0)]
        public static void OpenOrCreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == ScenePath)
                return;

            // The menu can also be used after copying the assets into another
            // project. Preserve any unsaved work in that project.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return;
            }

            Debug.LogError("CareProject: отсутствует готовая учебная сцена. Восстанови полный пакет проекта; устаревшая площадка больше не генерируется поверх текущих данных.");
            IntercomProjectRepair.Open();
        }

        [MenuItem("Tools/CareProject/Open Scene", true)]
        private static bool CanOpenScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling
                && !EditorApplication.isUpdating;
        }

        internal static void ConfigureMobilePresentation(GameObject robot, Camera camera)
        {
            if (robot == null || camera == null)
                throw new InvalidOperationException("Для мобильной сцены нужны Robot и игровая Camera.");
            if (robot.scene != camera.gameObject.scene)
                throw new InvalidOperationException("Robot и игровая Camera должны находиться в одной сцене.");
            GameObject ground = robot.scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Ground - Future Map");
            Renderer groundRenderer = ground != null ? ground.GetComponent<Renderer>() : null;
            Bounds groundBounds = groundRenderer != null
                ? groundRenderer.bounds : new Bounds(Vector3.zero, new Vector3(24f, 0f, 24f));
            IntercomCameraFollow follow = camera.GetComponent<IntercomCameraFollow>();
            bool captureCamera = follow == null || follow.target != robot.transform;
            if (follow == null) follow = Undo.AddComponent<IntercomCameraFollow>(camera.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { follow, camera, camera.transform }, "Configure Intercom mobile camera");
            follow.target = robot.transform;
            follow.groundHeight = groundBounds.max.y;
            follow.groundMinimum = new Vector2(groundBounds.min.x, groundBounds.min.z);
            follow.groundMaximum = new Vector2(groundBounds.max.x, groundBounds.max.z);
            // Adopt hand-placed cameras without applying a default framing preset.
            if (captureCamera) follow.CaptureFromCamera(camera);
            else follow.CaptureManualCameraChanges();
            follow.enabled = true;
            follow.SnapToTarget();
            EditorUtility.SetDirty(follow);
            EditorUtility.SetDirty(camera);
            foreach (UnityEngine.Object item in new UnityEngine.Object[] { follow, camera, camera.transform })
                if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            IntercomOcclusionSetup.Setup(robot, camera);
            EditorSceneManager.MarkSceneDirty(robot.scene);
        }

        internal static Bounds GetPosedBounds(GameObject root)
        {
            Bounds bounds = new Bounds();
            bool hasVertex = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                Mesh mesh = null;
                bool temporary = false;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh();
                    skinned.BakeMesh(mesh, false);
                    temporary = true;
                }
                else if (renderer is MeshRenderer)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null)
                        mesh = filter.sharedMesh;
                }

                if (mesh == null)
                    continue;
                foreach (Vector3 localVertex in mesh.vertices)
                {
                    Vector3 worldVertex = renderer.transform.TransformPoint(localVertex);
                    if (!hasVertex)
                    {
                        bounds = new Bounds(worldVertex, Vector3.zero);
                        hasVertex = true;
                    }
                    else
                        bounds.Encapsulate(worldVertex);
                }
                if (temporary)
                    UnityEngine.Object.DestroyImmediate(mesh);
            }
            if (!hasVertex)
                throw new InvalidOperationException("В FBX не найдены видимые вершины модели.");
            return bounds;
        }

    }
}
