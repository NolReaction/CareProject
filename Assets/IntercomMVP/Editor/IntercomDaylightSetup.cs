using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    /// <summary>Explicit, edit-time installation. The saved scene needs no runtime lighting driver.</summary>
    internal static class IntercomDaylightSetup
    {
        internal const string ProfilePath = "Assets/IntercomMVP/Settings/TutorialDaylight.asset";
        internal const string ReflectionPath = "Assets/IntercomMVP/Settings/TutorialDaylightReflection.hdr";
        private const string PipelinePath = "Assets/IntercomMVP/Settings/IntercomPipeline.asset";
        private const string VolumeName = "Tutorial Daylight";

        [MenuItem("Tools/Intercom MVP/Apply Tutorial Daylight", false, 30)]
        internal static void ApplyToActiveScene()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .OrderByDescending(item => item.CompareTag("MainCamera")).FirstOrDefault();
                if (camera == null) throw new InvalidOperationException("В активной сцене не найдена игровая камера.");
                VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
                Cubemap reflection = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionPath);
                UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                if (profile == null || reflection == null || pipeline == null)
                    throw new InvalidOperationException("Не импортированы ресурсы TutorialDaylight в IntercomMVP/Settings. Дождись завершения импорта.");

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Apply tutorial daylight");
                Light sun = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true))
                    .Where(item => item.type == LightType.Directional)
                    .OrderByDescending(item => item == RenderSettings.sun).FirstOrDefault();
                if (sun == null)
                {
                    GameObject item = new GameObject("Sun", typeof(Light));
                    SceneManager.MoveGameObjectToScene(item, scene);
                    Undo.RegisterCreatedObjectUndo(item, "Create daylight sun");
                    sun = item.GetComponent<Light>();
                }
                Undo.RecordObjects(new UnityEngine.Object[] { sun, sun.transform, camera, Unsupported.GetRenderSettings() }, "Configure daylight");
                sun.type = LightType.Directional;
                sun.enabled = true;
                sun.color = new Color(1f, 0.96f, 0.88f);
                sun.useColorTemperature = false;
                sun.intensity = 1.65f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
                sun.shadowBias = 0.45f;
                sun.shadowNormalBias = 0.5f;
                sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                RenderSettings.sun = sun;
                RenderSettings.fog = false;
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.58f, 0.67f, 0.78f);
                RenderSettings.ambientEquatorColor = new Color(0.40f, 0.46f, 0.52f);
                RenderSettings.ambientGroundColor = new Color(0.29f, 0.28f, 0.25f);
                RenderSettings.ambientIntensity = 1f;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = reflection;
                RenderSettings.reflectionIntensity = 0.75f;
                RenderSettings.reflectionBounces = 1;
                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = pipeline;
                PlayerSettings.colorSpace = ColorSpace.Linear;

                // Keep the authored camera transform, lens and follow settings.
                camera.allowHDR = true;
                camera.allowMSAA = true;
                UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData == null) cameraData = Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
                Undo.RecordObject(cameraData, "Enable daylight grading");
                cameraData.renderPostProcessing = true;
                cameraData.renderShadows = true;
                cameraData.allowHDROutput = false;
                cameraData.volumeLayerMask |= 1;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.dithering = true;

                GameObject volumeObject = scene.GetRootGameObjects().FirstOrDefault(item => item.name == VolumeName);
                if (volumeObject == null)
                {
                    volumeObject = new GameObject(VolumeName);
                    SceneManager.MoveGameObjectToScene(volumeObject, scene);
                    Undo.RegisterCreatedObjectUndo(volumeObject, "Create daylight volume");
                }
                Volume volume = volumeObject.GetComponent<Volume>();
                if (volume == null) volume = Undo.AddComponent<Volume>(volumeObject);
                Undo.RecordObjects(new UnityEngine.Object[] { volume, volumeObject }, "Connect daylight profile");
                volumeObject.layer = 0;
                volume.isGlobal = true;
                volume.priority = 5f;
                volume.weight = 1f;
                volume.sharedProfile = profile;
                foreach (UnityEngine.Object item in new UnityEngine.Object[] { sun, sun.transform, camera, cameraData, volume })
                {
                    EditorUtility.SetDirty(item);
                    if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                }
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                SceneView.RepaintAll();
                Debug.Log("Intercom MVP: дневной свет, отражения и ACES подключены. Ракурс сохранён. Сохрани сцену Ctrl+S; для слабого телефона можно отключить Tutorial Contact AO в IntercomRenderer.");
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Tools/Intercom MVP/Apply Tutorial Daylight", true)]
        private static bool CanApply() => !EditorApplication.isCompiling && !EditorApplication.isUpdating
            && !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
