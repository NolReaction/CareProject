using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    /// <summary>Creates the small mobile arena and keeps authored scene content on updates.</summary>
    internal static class IntercomMvpSceneBuilder
    {
        internal const string ScenePath = "Assets/IntercomMVP/Scenes/IntercomMVP.unity";
        private const string GroundMaterialPath = "Assets/IntercomMVP/Art/Ground/GroundMap.mat";
        private const float RobotHeight = 1.6f;
        // Verified from the user's Unity import: yaw 0 faces the -Z camera.
        private const float RobotYawDegrees = 0f;

        [MenuItem("Tools/Intercom MVP/Open Scene", false, 0)]
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

            CreateScene();
        }

        [MenuItem("Tools/Intercom MVP/Open Scene", true)]
        private static bool CanOpenScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling
                && !EditorApplication.isUpdating;
        }

        /// <summary>
        /// Used by Open Scene only when the target scene is missing. Never overwrites it.
        /// </summary>
        internal static void CreateScene()
        {
            if (File.Exists(ScenePath))
                return;

            IntercomRenderPipelineSetup.EnsurePipeline();
            int missingTextures = IntercomRobotAssets.EnsureAssets();
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(IntercomRobotAssets.ModelPath);
            if (modelAsset == null)
                throw new InvalidOperationException("Не найден IntercomRobot.fbx. Дождись окончания импорта и открой Tools > Intercom MVP > Open Scene.");

            AnimationClip readyPose = FindReadyPoseClip();
            if (readyPose == null)
                throw new InvalidOperationException("В FBX не найден клип Shoot с начальной стойкой. Проверь импорт анимации модели.");

            EnsureFolder("Assets/IntercomMVP/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground - Future Map";
            ground.transform.localScale = new Vector3(2.4f, 1f, 2.4f);
            ground.GetComponent<Renderer>().sharedMaterial = GetGroundMaterial();

            GameObject robot = new GameObject("Robot");
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, scene);
            visual.transform.SetParent(robot.transform, false);
            visual.name = "IntercomRobot";
            robot.transform.rotation = Quaternion.Euler(0f, RobotYawDegrees, 0f);

            // Sample a stable ready stance before measuring the model.
            // The movement installer connects the live Animator below.
            foreach (Animator animator in visual.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }
            foreach (Animation animation in visual.GetComponentsInChildren<Animation>(true))
            {
                animation.playAutomatically = false;
                animation.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animation);
            }

            readyPose.SampleAnimation(visual, 0f);
            Bounds robotBounds = GetPosedBounds(visual);
            if (robotBounds.size.y <= 0.0001f)
                throw new InvalidOperationException("У модели пустые границы. Проверь импорт FBX.");
            robot.transform.localScale = Vector3.one * (RobotHeight / robotBounds.size.y);
            robotBounds = GetPosedBounds(visual);
            robot.transform.position += new Vector3(-robotBounds.center.x, 0.015f - robotBounds.min.y, -robotBounds.center.z);

            // Direct sampling changes local bone transforms; recording their
            // overrides makes the grip survive scene save and reopen.
            foreach (Transform transform in visual.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);

            Vector3 focus = new Vector3(0f, 0.65f, 0f);
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.6f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.19f, 0.17f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = true;
            cameraObject.transform.rotation = Quaternion.Euler(35f, 10f, 0f);
            cameraObject.transform.position = focus - cameraObject.transform.forward * 12f;

            GameObject lightObject = new GameObject("Sun", typeof(Light));
            Light sun = lightObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.9f, 1f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.shadowBias = 0.03f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.70f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.49f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.31f, 0.27f);
            RenderSettings.fog = false;

            IntercomDistrictSetup.Setup(scene);
            IntercomMovementSetup.Setup(robot, camera);
            ConfigureMobilePresentation(robot, camera);
            focus = camera.transform.position + camera.transform.forward * camera.GetComponent<IntercomCameraFollow>().distance;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Unity не смог сохранить сцену " + ScenePath);

            List<EditorBuildSettingsScene> buildScenes = EditorBuildSettings.scenes.ToList();
            if (!buildScenes.Any(entry => entry.path == ScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = robot;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
                sceneView.LookAt(focus, cameraObject.transform.rotation, camera.orthographicSize, true, true);
            SceneView.RepaintAll();

            Debug.Log("Intercom MVP: создана тестовая площадка с наклонной камерой. Нажми Play: левый стик — движение, правый — прицеливание и стрельба. Для компьютера: WASD / стрелки и мышь. Тестовые объекты находятся в MVP_TestEnvironment.");
            if (missingTextures > 0)
                IntercomMvpSetupWindow.ShowWindow();
        }

        /// <summary>Connect camera follow and occlusion without rebuilding the scene or changing framing.</summary>
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

        private static AnimationClip FindReadyPoseClip()
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(IntercomRobotAssets.ModelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return clips.FirstOrDefault(clip => clip.name.IndexOf("Shoot", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? clips.FirstOrDefault(clip => clip.name.IndexOf("Pistols_Symmetric", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? clips.FirstOrDefault();
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

        private static Material GetGroundMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (existing != null)
                return existing;
            EnsureFolder("Assets/IntercomMVP/Art/Ground");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Не найден URP Lit. Дождись установки Universal Render Pipeline.");
            Material material = new Material(shader) { name = "GroundMap" };
            material.SetColor("_BaseColor", new Color(0.30f, 0.35f, 0.33f, 1f));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(material, GroundMaterialPath);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string parent = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string child = parent + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(child))
                    AssetDatabase.CreateFolder(parent, segments[i]);
                parent = child;
            }
        }
    }

}
