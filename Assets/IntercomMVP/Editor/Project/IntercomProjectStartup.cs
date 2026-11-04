using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    /// <summary>First opening of the standalone project: restore its authored scene only over a pristine default scene.</summary>
    [InitializeOnLoad]
    internal static class IntercomProjectStartup
    {
        private const string Marker = "ProjectSettings/IntercomMvpProject.json";
        private const string Menu = "Tools/CareProject/Start Here";
        private const double TimeoutSeconds = 180.0;
        private static bool scheduled;
        private static double deadline;
        private static string preferenceKey;

        static IntercomProjectStartup() { TrySchedule(); }

        internal static void TrySchedule()
        {
            if (scheduled || Application.isBatchMode || !File.Exists(Marker)) return;
            preferenceKey = ProjectPreferenceKey(Application.dataPath);
            if (EditorPrefs.GetBool(preferenceKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            scheduled = true;
            EditorApplication.update += Poll;
        }

        private static void Stop()
        {
            EditorApplication.update -= Poll;
            scheduled = false;
        }

        private static void Complete()
        {
            Stop();
            EditorPrefs.SetBool(preferenceKey, true);
        }

        private static void Poll()
        {
            // A user starting Play or removing the project marker owns the next action.
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Marker))
            { Stop(); return; }
            if (EditorApplication.timeSinceStartup >= deadline)
            {
                Stop();
                Debug.LogWarning("CareProject: первый импорт ещё не завершён. Когда Unity закончит импорт, открой Tools → CareProject → Open Scene.");
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (SceneManager.sceneCount == 0) return;
                if (SceneManager.sceneCount != 1) { Complete(); return; }
                Scene active = SceneManager.GetActiveScene();
                if (!IsPristineDefaultScene(active)) { Complete(); return; }
                string scenePath = IntercomProjectPaths.TutorialScene.Resolve();
                if (string.IsNullOrEmpty(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) return;
                Scene opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!opened.IsValid() || opened.path != scenePath)
                    throw new InvalidOperationException("Учебная сцена не открылась.");
                Complete();
                EditorApplication.ExecuteMenuItem(Menu);
            }
            catch (Exception error)
            {
                Stop();
                Debug.LogWarning("CareProject: открой учебную сцену через Tools → CareProject → Open Scene. " + error.Message);
            }
        }

        internal static bool IsPristineDefaultScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.isDirty || !string.IsNullOrEmpty(scene.path)) return false;
            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Length == 0) return true;
            if (roots.Length != 2) return false;
            bool camera = false, light = false;
            foreach (GameObject root in roots)
            {
                if (root.transform.childCount != 0) return false;
                bool defaultCamera = root.name == "Main Camera" && root.GetComponent<Camera>() != null;
                Light sun = root.GetComponent<Light>();
                bool defaultLight = root.name == "Directional Light" && sun != null && sun.type == LightType.Directional;
                if ((!defaultCamera && !defaultLight) || (defaultCamera && camera) || (defaultLight && light)) return false;
                foreach (Component component in root.GetComponents<Component>())
                {
                    if (component == null) return false; // A missing script still belongs to the user.
                    if (component is Transform || component is Camera || component is Light || component is AudioListener) continue;
                    string type = component.GetType().FullName;
                    if (defaultCamera && type == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData") continue;
                    if (defaultLight && type == "UnityEngine.Rendering.Universal.UniversalAdditionalLightData") continue;
                    return false;
                }
                camera |= defaultCamera;
                light |= defaultLight;
            }
            return camera && light;
        }

        internal static string ProjectPreferenceKey(string assetsPath)
        {
            string path = Path.GetFullPath(Path.Combine(assetsPath, "..")).Replace('\\', '/').TrimEnd('/');
            if (Path.DirectorySeparatorChar == '\\') path = path.ToUpperInvariant();
            using (SHA256 sha = SHA256.Create())
                return "CareProject.Startup.V14." + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(path))).Replace("-", "");
        }
    }
}
