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
            EditorGUILayout.LabelField("Робот и тестовая площадка", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("В сцене только поле, робот, камера и свет.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            if (GUILayout.Button("Открыть сцену", GUILayout.Height(30)))
                Run(() => IntercomMvpSceneBuilder.OpenOrCreateScene());
            EditorGUILayout.Space();
            try
            {
                var missing = IntercomRobotAssets.MissingTextureNames();
                if (missing.Count == 0)
                    EditorGUILayout.HelpBox("Все текстуры робота находятся внутри проекта.", MessageType.Info);
                else
                {
                    EditorGUILayout.HelpBox("Не хватает картинок корпуса и кистей: " + missing.Count +
                        ". В присланном архиве их не было. Выбери папку .fbm из исходной модели Tripo: файлы скопируются в проект. Пока эти части показаны однотонным материалом.", MessageType.Warning);
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
