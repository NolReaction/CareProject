using System.IO;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    internal sealed class IntercomProjectHub : EditorWindow
    {
        [MenuItem("Tools/Intercom MVP/Project Guide and Balance")]
        private static void Open() { GetWindow<IntercomProjectHub>("CareProject"); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("CareProject — где что менять", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Начни с карточки оружия. Она объединяет урон, темп, звук, эффекты и ссылки на позы рук. Общая модель робота и движение ног находятся отдельно.", MessageType.Info);
            if (GUILayout.Button("Пистолеты: баланс, TTK, звук и эффекты")) Select("Assets/IntercomMVP/Game/Weapons/Pistols/DualPistols.asset");
            if (GUILayout.Button("Пистолеты: позы рук и анимация отдачи")) Select(IntercomAssetCatalog.Resolve("Assets/IntercomMVP/Animation/DualPistolsPose.asset"));
            if (GUILayout.Button("Общие шаги, попадания, музыка")) Select("Assets/IntercomMVP/Audio/PrototypeAudio.asset");
            if (GUILayout.Button("Модель робота и все клипы")) Select("Assets/IntercomMVP/Art/Robot/IntercomRobot.fbx");
            EditorGUILayout.Space();
            if (GUILayout.Button("Открыть инструкцию START_HERE.md")) OpenDocument("START_HERE.md");
            if (GUILayout.Button("Открыть архитектуру проекта")) OpenDocument("Docs/Architecture.md");
            if (GUILayout.Button("Проверить завершение раскладки папок")) IntercomAssetOrganization.FinishUpgrade();
        }

        private static void Select(string path)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) { Debug.LogWarning("Intercom: ресурс не найден: " + path); return; }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void OpenDocument(string relative)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
            if (File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
            else Debug.LogWarning("Intercom: документ не найден: " + relative);
        }
    }
}
