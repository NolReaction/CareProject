using System;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    internal sealed class IntercomMvpSetupWindow : EditorWindow
    {
        private Vector2 scroll;
        private string result;

        [MenuItem("Tools/Intercom MVP/Texture Setup")]
        internal static void ShowWindow()
        {
            var window = GetWindow<IntercomMvpSetupWindow>("Intercom MVP");
            window.minSize = new Vector2(420, 320);
            window.Show();
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorGUILayout.HelpBox("Останови Play и дождись завершения импорта, чтобы изменять материалы.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("Материалы робота", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Проверка текстур и восстановление материалов из RobotMaterials.json.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            if (GUILayout.Button("Открыть сцену", GUILayout.Height(30)))
                Run(() => IntercomMvpSceneBuilder.OpenOrCreateScene());
            EditorGUILayout.Space();
            if (GUILayout.Button("Восстановить материалы и привязки FBX", GUILayout.Height(30))) Run(() =>
            {
                int remaining = IntercomRobotAssets.EnsureAssets();
                result = remaining == 0 ? "Материалы и привязки восстановлены." : "Не найдено текстур: " + remaining;
            });
            try
            {
                var missing = IntercomRobotAssets.MissingTextureNames();
                if (missing.Count == 0)
                    EditorGUILayout.HelpBox("Все текстуры робота находятся внутри проекта.", MessageType.Info);
                else
                {
                    EditorGUILayout.HelpBox("Не хватает картинок корпуса и кистей: " + missing.Count +
                        ". Выбери папку с исходными изображениями: недостающие файлы скопируются в проект.", MessageType.Warning);
                    if (GUILayout.Button("Выбрать папку с текстурами (.fbm)", GUILayout.Height(30)))
                    {
                        string folder = EditorUtility.OpenFolderPanel("Папка с текстурами модели Tripo", "", "");
                        if (!string.IsNullOrEmpty(folder)) Run(() =>
                        {
                            int remaining = IntercomRobotAssets.EnsureAssets(folder);
                            result = remaining == 0 ? "Текстуры восстановлены. Сцену пересоздавать не нужно." : "Осталось найти картинок: " + remaining;
                        });
                    }
                    if (GUILayout.Button("Повторить поиск по исходным путям")) Run(() =>
                    {
                        int remaining = IntercomRobotAssets.EnsureAssets();
                        result = "Осталось найти картинок: " + remaining;
                    });
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    foreach (string name in missing) EditorGUILayout.SelectableLabel(name, GUILayout.Height(18));
                    EditorGUILayout.EndScrollView();
                }
            }
            catch (Exception exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception exception) { result = exception.Message; Debug.LogException(exception); }
            Repaint();
        }
    }
}
