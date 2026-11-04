using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IntercomMvp.Editor
{
    /// <summary>Developer navigation only. Opening this window never rebuilds or changes the game scene.</summary>
    internal sealed class IntercomProjectHub : EditorWindow
    {
        private Vector2 scroll;

        [MenuItem("Tools/CareProject/Start Here", false, 0)]
        private static void Open()
        {
            var window = GetWindow<IntercomProjectHub>("CareProject — Start Here");
            window.minSize = new Vector2(420f, 480f);
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("CareProject — Start Here", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Скомпилированная версия: " + IntercomFrontEnd.BuildVersion, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Ресурсы сгруппированы по системам. Кнопки ниже находят важные карточки по GUID: их можно перемещать, сохраняя .meta. Настройки оружия, модель героя и режимы игры редактируются отдельно.", MessageType.Info);

            Section("Начало работы");
            if (GUILayout.Button("Проверить / восстановить проект")) IntercomProjectRepair.Open();
            if (GUILayout.Button("Открыть учебную сцену")) OpenScene();
            if (GUILayout.Button("Открыть START_HERE.md")) OpenDocument(IntercomProjectPaths.StartGuide);
            if (GUILayout.Button("Архитектура и карта папок")) OpenDocument(IntercomProjectPaths.ArchitectureGuide);
            if (GUILayout.Button("Руководство разработчика")) OpenDocument(IntercomProjectPaths.DeveloperGuide);

            Section("Герой");
            AssetButton(IntercomProjectPaths.RobotModel);
            AssetButton(IntercomProjectPaths.RobotAnimator);
            if (GUILayout.Button("Исходник Blender")) OpenDocument(IntercomProjectPaths.RobotSource);
            if (GUILayout.Button("Как редактировать и экспортировать анимации")) OpenDocument(IntercomProjectPaths.AnimationGuide);

            Section("Оружие и звук");
            AssetButton(IntercomProjectPaths.Pistols);
            AssetButton(IntercomProjectPaths.PistolPose);
            AssetButton(IntercomProjectPaths.Audio);

            Section("Враги и задания");
            AssetButton(IntercomProjectPaths.Enemy);
            AssetButton(IntercomProjectPaths.Elimination);
            AssetButton(IntercomProjectPaths.Rescue);

            Section("Учебный двор и отрисовка");
            AssetButton(IntercomProjectPaths.YardLayout);
            AssetButton(IntercomProjectPaths.Lighting);
            AssetButton(IntercomProjectPaths.RenderPipeline);

            Section("Мобильная разработка");
            if (GUILayout.Button("Настроить управление и ориентацию")) EditorApplication.ExecuteMenuItem("Tools/CareProject/Mobile/01 Configure Controls");
            if (GUILayout.Button("Настройки тестовой сборки Android")) EditorApplication.ExecuteMenuItem("Tools/CareProject/Mobile/02 Android Test Settings");
            if (GUILayout.Button("Проверка на устройстве")) OpenDocument("MOBILE_TESTING.md");
            EditorGUILayout.HelpBox("Верхнее меню Mobile содержит только инструменты телефона. Изменение модели, материалов и карты находится в Advanced → Authoring; обычное обновление проекта не требует этих команд.", MessageType.None);

            Section("Проверка структуры");
            if (GUILayout.Button("Проверить ссылки разделов")) ValidateEntryPoints();
            if (GUILayout.Button("Открыть папку автономных проверок")) OpenDocument(IntercomProjectPaths.ValidationFolder);
            EditorGUILayout.HelpBox("START_HERE объясняет обычные правки. Architecture описывает границы систем и порядок обновления кадра. Validation содержит проверки кода; полноценный запуск Unity выполняется отдельно.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private static void Section(string title)
        { EditorGUILayout.Space(); EditorGUILayout.LabelField(title, EditorStyles.boldLabel); }

        private static void AssetButton(IntercomProjectPaths.AssetKey key)
        { if (GUILayout.Button(key.Label)) Select(key.Resolve()); }

        private static void Select(string path)
        {
            Object asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) { Debug.LogWarning("CareProject: ресурс не найден: " + path); return; }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void OpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorUtility.DisplayDialog("CareProject", "Сначала останови Play Mode.", "OK"); return; }
            string path = IntercomProjectPaths.TutorialScene.Resolve();
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) == null)
            { Debug.LogWarning("CareProject: учебная сцена не найдена."); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }

        private static void ValidateEntryPoints()
        {
            IntercomAssetCatalog.Invalidate();
            var missing = new StringBuilder();
            int available = 0;
            foreach (var key in IntercomProjectPaths.All)
            {
                string path = key.Resolve();
                if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadMainAssetAtPath(path) != null) available++;
                else missing.AppendLine(key.Label + " — " + key.CanonicalPath);
            }
            string message = available + " / " + IntercomProjectPaths.All.Length + " основных ресурсов доступны.";
            if (missing.Length > 0) message += "\n\nНе найдены:\n" + missing;
            EditorUtility.DisplayDialog("CareProject — ссылки разделов", message, "OK");
        }

        private static void OpenDocument(string relative)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
            if (File.Exists(path) || Directory.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
            else Debug.LogWarning("CareProject: файл не найден: " + relative);
        }
    }
}
