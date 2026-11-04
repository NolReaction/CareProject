using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    /// <summary>Explicit mobile setup and local test-build settings. Never builds or switches platform automatically.</summary>
    [InitializeOnLoad]
    internal static class IntercomMobileSetup
    {
        private const string ProjectMarker = "ProjectSettings/IntercomMvpProject.json";
        private const string LandscapeMarker = "ProjectSettings/IntercomMobileDefaults.json";
        private const string DefaultIdentifier = "com.intercommvp.prototype";

        static IntercomMobileSetup()
        {
            // Only the supplied standalone project opts into first-open defaults.
            // Importing these Assets into another project requires the menu below.
            if (File.Exists(ProjectMarker) && !File.Exists(LandscapeMarker))
                EditorApplication.update += ApplyFirstOpenDefaults;
        }

        private static void ApplyFirstOpenDefaults()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= ApplyFirstOpenDefaults;
            if (!File.Exists(ProjectMarker) || File.Exists(LandscapeMarker)) return;
            try
            {
                ApplyLandscapeSettings();
                AssetDatabase.SaveAssets();
                File.WriteAllText(LandscapeMarker,
                    "{\n  \"version\": 1,\n  \"orientation\": \"landscape-left-and-right\"\n}\n");
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Tools/CareProject/Setup Mobile Prototype", false, 22)]
        private static void SetupMobilePrototype()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                GameObject robot = FindRobot(scene);
                Camera camera = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .OrderByDescending(item => item.CompareTag("MainCamera")).FirstOrDefault();
                if (camera == null)
                    throw new InvalidOperationException("В активной сцене не найдена игровая Camera. Сначала открой Tools > Intercom MVP > Open Scene.");

                IntercomMovementSetup.Setup(robot, camera);
                IntercomMvpSceneBuilder.ConfigureMobilePresentation(robot, camera);
                EnsureRuntimeSettings(robot);
                ApplyLandscapeSettings();
                RegisterTestScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                Selection.activeGameObject = robot;
                Debug.Log("Intercom MVP: мобильный прототип настроен. Сохрани сцену (Ctrl+S). Два стика: слева движение, справа направление и огонь. Для телефона см. MOBILE_TESTING.md; сборка ещё не запускалась.", robot);
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Tools/CareProject/Setup Mobile Prototype", true)]
        [MenuItem("Tools/CareProject/Configure Android Test Settings", true)]
        [MenuItem("Tools/CareProject/Configure iPhone Test Settings", true)]
        private static bool CanSetup()
        {
            return !EditorApplication.isCompiling && !EditorApplication.isUpdating
                && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        internal static void ApplyLandscapeSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            // Respect the game's two landscape directions even when the phone
            // has portrait rotation lock enabled. This is an Android setting.
            PlayerSettings.Android.autoRotationBehavior = AndroidAutoRotationBehavior.Sensor;
        }

        internal static void EnsureRuntimeSettings(GameObject robot)
        {
            IntercomMobileRuntimeSettings settings = robot.GetComponent<IntercomMobileRuntimeSettings>();
            if (settings == null) settings = Undo.AddComponent<IntercomMobileRuntimeSettings>(robot);
            if (!settings.enabled)
            {
                Undo.RecordObject(settings, "Enable Intercom Mobile Settings");
                settings.enabled = true;
            }
            EditorUtility.SetDirty(settings);
            if (PrefabUtility.IsPartOfPrefabInstance(settings))
                PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
        }

        [MenuItem("Tools/CareProject/Configure Android Test Settings", false, 40)]
        private static void ConfigureAndroidTestSettings()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                FindRobot(scene);
                ApplyLandscapeSettings();
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                AndroidSdkVersions supportedMinimum = PlayerSettings.Android.minSupportedSdkVersion;
                if ((int)PlayerSettings.Android.minSdkVersion < (int)supportedMinimum)
                    PlayerSettings.Android.minSdkVersion = supportedMinimum;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
                PlayerSettings.Android.optimizedFramePacing = true;
                PlayerSettings.Android.startInFullscreen = true;
                PlayerSettings.Android.renderOutsideSafeArea = true;
                PlayerSettings.Android.minifyDebug = false;
                SetIdentifierOnlyIfDefault(NamedBuildTarget.Android);
                // Keep any existing signing credentials untouched. A new project
                // uses Unity's development/debug signing for a local test APK.
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                ConfigureDevelopmentBuild();
                RegisterTestScene(scene);
                AssetDatabase.SaveAssets();
                OpenBuildProfiles();
                Debug.Log("Intercom MVP: подготовлены Android APK / Development Build / IL2CPP ARM64. Сохрани сцену. В File > Build Profiles выбери Android, Switch Platform, подключённый Run Device и Build And Run. Если Android недоступен, установи через Unity Hub Android Build Support + SDK/NDK + OpenJDK. Платформа автоматически не переключалась; APK ещё не создан.");
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Tools/CareProject/Configure iPhone Test Settings", false, 41)]
        private static void ConfigureIPhoneTestSettings()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                FindRobot(scene);
                ApplyLandscapeSettings();
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
                SetIdentifierOnlyIfDefault(NamedBuildTarget.iOS);
                ConfigureDevelopmentBuild();
                RegisterTestScene(scene);
                AssetDatabase.SaveAssets();
                OpenBuildProfiles();
                Debug.Log("Intercom MVP: настройки теста iPhone подготовлены. На Mac выбери iOS в Build Profiles и собери Xcode-проект. Команду подписи выбери в Xcode; личную команду и Apple Account скрипт не меняет. Платформа автоматически не переключалась; iOS-приложение ещё не собрано.");
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void ConfigureDevelopmentBuild()
        {
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
        }

        private static void SetIdentifierOnlyIfDefault(NamedBuildTarget target)
        {
            string current = PlayerSettings.GetApplicationIdentifier(target);
            if (string.IsNullOrWhiteSpace(current)
                || current.StartsWith("com.DefaultCompany.", StringComparison.OrdinalIgnoreCase)
                || current.Equals("com.Company.ProductName", StringComparison.OrdinalIgnoreCase))
                PlayerSettings.SetApplicationIdentifier(target, DefaultIdentifier);
        }

        private static GameObject FindRobot(Scene scene)
        {
            GameObject robot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Robot");
            if (robot == null)
                throw new InvalidOperationException("В активной сцене нет корневого Robot. Открой сцену MVP, затем запусти настройку повторно.");
            return robot;
        }

        private static void RegisterTestScene(Scene scene)
        {
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("Сцена ещё не сохранена. Сохрани её в Assets и повтори настройку телефона, чтобы добавить её в Scene List.");
                return;
            }
            // Start the requested prototype scene on the device. Preserve the
            // relative order and enabled state of all other build entries.
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(entry => entry.path != scene.path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(scene.path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void OpenBuildProfiles()
        {
            if (!EditorApplication.ExecuteMenuItem("File/Build Profiles"))
                Debug.Log("Открой File > Build Profiles для выбора платформы и Build And Run.");
        }
    }
}
