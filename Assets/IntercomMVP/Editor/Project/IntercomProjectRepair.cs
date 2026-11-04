using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#endif

namespace IntercomMvp.Editor
{
    /// <summary>Read-only disk diagnostics also used by the standalone repair regressions.</summary>
    internal static class IntercomProjectRepairAudit
    {
        internal sealed class Report
        {
            internal readonly List<string> Errors = new List<string>();
            internal readonly List<string> Warnings = new List<string>();
            internal readonly List<string> Facts = new List<string>();
        }

        internal static Report Inspect(string projectRoot, string expectedVersion, string scenePath)
        {
            var report = new Report();
            string root = Path.GetFullPath(projectRoot);
            report.Facts.Add("Корень открытого проекта: " + root);
            foreach (string directory in new[] { "Assets", "Packages", "ProjectSettings" })
                if (!Directory.Exists(Path.Combine(root, directory))) report.Errors.Add("Нет папки " + directory + ": выбран не корень Unity-проекта.");
            CheckSource(root, "IntercomFrontEnd.cs", "public const string BuildVersion = \"" + expectedVersion + "\"", report,
                "Исходники меню не соответствуют " + expectedVersion + ". Переимпорт старого кода не установит обновление.");
            CheckSource(root, "IntercomWorkshopHud.cs", "HeartRed", report,
                "На диске старый HUD. Распакуй актуальное обновление в корень открытого проекта.");
            CheckSource(root, "IntercomPlayerInput.cs", "PassiveHudContains", report,
                "На диске старый ввод. Меню и ввод должны обновляться одним пакетом.");
            string absoluteScene = Path.Combine(root, scenePath);
            if (!File.Exists(absoluteScene)) report.Errors.Add("Нет готовой игровой сцены: " + scenePath + ". Восстанови её из полного ZIP; старая площадка больше не генерируется.");
            else
            {
                string scene = File.ReadAllText(absoluteScene);
                Match frontend = Regex.Match(scene, @"^  m_EditorClassIdentifier: Assembly-CSharp::IntercomMvp.IntercomFrontEnd\r?\n([\s\S]*?)(?=^--- |\z)", RegexOptions.Multiline);
                if (!frontend.Success) report.Warnings.Add("В сцене отсутствует компонент главного меню. Восстановление добавит его только при наличии готовых объектов игры и комнат.");
                else foreach (string key in new[] { "mission", "input", "roomCamera", "homeDoodle", "rescueDoodle" })
                {
                    Match value = Regex.Match(frontend.Groups[1].Value, @"^  " + key + @": \{fileID: (-?\d+)", RegexOptions.Multiline);
                    if (!value.Success || value.Groups[1].Value == "0") report.Warnings.Add("Не назначено поле меню: " + key + ".");
                }
                if (!scene.Contains("  m_Name: MVP_MenuHub\n") && !scene.Contains("  m_Name: MVP_MenuHub\r\n"))
                    report.Errors.Add("В сцене нет готовых комнат MVP_MenuHub. Нужна актуальная сцена из пакета; восстановление связей не заменяет геометрию.");
                report.Facts.Add("Сцена на диске: " + scenePath);
            }
            foreach (string nested in FindNestedPackages(root))
                report.Warnings.Add("Обновление или другой Unity-проект лежит вложенной папкой: " + nested + ". Unity не берёт оттуда новые исходники автоматически.");
            foreach (string duplicate in FindExtraImportedSources(root))
                report.Warnings.Add("В Assets найдена лишняя копия исходника: " + duplicate + ". Она может конфликтовать с актуальным кодом. Проверка ничего не удаляет; используй установщик или перенеси копию за пределы Assets.");
            if (report.Errors.Count == 0) report.Facts.Add("Дисковая структура и исходники " + expectedVersion + " согласованы.");
            return report;
        }

        private static void CheckSource(string root, string filename, string marker, Report report, string error)
        {
            string runtime = Path.Combine(root, "Assets/IntercomMVP/Runtime");
            string[] candidates = Directory.Exists(runtime) ? Directory.GetFiles(runtime, filename, SearchOption.AllDirectories) : Array.Empty<string>();
            if (candidates.Length != 1) { report.Errors.Add("Ожидался один " + filename + ", найдено: " + candidates.Length + "."); return; }
            if (!File.ReadAllText(candidates[0]).Contains(marker)) report.Errors.Add(error);
        }

        internal static string[] FindNestedPackages(string projectRoot)
        {
            var result = new List<string>();
            var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Assets", "Library", "Packages", "ProjectSettings", "Source", "Validation", "Tools", "BlenderTools", "Docs", "ProjectBackups", "Logs", "Temp", "UserSettings", ".git" };
            foreach (string child in Directory.GetDirectories(projectRoot))
            {
                if (ignored.Contains(Path.GetFileName(child)) || IsLink(child)) continue;
                InspectNested(child, projectRoot, 0, result);
            }
            return result.ToArray();
        }

        private static void InspectNested(string directory, string root, int depth, List<string> result)
        {
            if (Directory.Exists(Path.Combine(directory, "Assets/IntercomMVP"))
                || (Directory.Exists(Path.Combine(directory,"Assets")) && Directory.Exists(Path.Combine(directory,"ProjectSettings"))))
            { result.Add(directory.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar)); return; }
            if (depth >= 2) return;
            foreach (string child in Directory.GetDirectories(directory))
                if (!IsLink(child)) InspectNested(child, root, depth + 1, result);
        }
        private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        internal static string[] FindExtraImportedSources(string projectRoot)
        {
            string assets=Path.Combine(projectRoot,"Assets"), runtime=Path.Combine(assets,"IntercomMVP/Runtime"), editor=Path.Combine(assets,"IntercomMVP/Editor");
            if(!Directory.Exists(assets))return Array.Empty<string>();
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var guids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string folder in new[]{runtime,editor})
                if(Directory.Exists(folder))foreach(string source in Directory.GetFiles(folder,"*Intercom*.cs",SearchOption.AllDirectories))
                {
                    names.Add(Path.GetFileName(source));string meta=source+".meta";
                    if(File.Exists(meta)){Match guid=Regex.Match(File.ReadAllText(meta),@"^guid: ([0-9a-f]{32})$",RegexOptions.Multiline);if(guid.Success)guids.Add(guid.Groups[1].Value);}
                }
            var result=new List<string>();
            string runtimePrefix=runtime+Path.DirectorySeparatorChar,editorPrefix=editor+Path.DirectorySeparatorChar;
            foreach(string source in Directory.GetFiles(assets,"*Intercom*.cs",SearchOption.AllDirectories))
            {
                if(source.StartsWith(runtimePrefix,StringComparison.OrdinalIgnoreCase) || source.StartsWith(editorPrefix,StringComparison.OrdinalIgnoreCase))continue;
                if(!names.Contains(Path.GetFileName(source)))continue;
                string label=source.Substring(projectRoot.TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar);
                string meta=source+".meta";
                if(File.Exists(meta)){Match guid=Regex.Match(File.ReadAllText(meta),@"^guid: ([0-9a-f]{32})$",RegexOptions.Multiline);if(guid.Success && guids.Contains(guid.Groups[1].Value))label+=" (совпадает GUID)";}
                result.Add(label);
            }
            return result.ToArray();
        }
    }

#if UNITY_EDITOR
    /// <summary>Explicit, missing-reference-only recovery. Never rebuilds the district or resets tuning.</summary>
    internal sealed class IntercomProjectRepair : EditorWindow
    {
        private Vector2 scroll;
        private IntercomProjectRepairAudit.Report report;
        private string outcome = "";
        private const string FrontendRoot = "Assets/IntercomMVP/Content/Frontend/";

        [MenuItem("Tools/CareProject/Проверить и восстановить проект", false, 1)]
        internal static void Open()
        {
            var window = GetWindow<IntercomProjectRepair>("CareProject — проверка");
            window.minSize = new Vector2(480f, 430f);
            window.Scan(); window.Show();
        }

        private void OnEnable() { Scan(); }
        private void Scan()
        {
            try
            {
                report = IntercomProjectRepairAudit.Inspect(ProjectRoot(), IntercomFrontEnd.BuildVersion, IntercomProjectPaths.TutorialScene.Resolve());
                foreach (var key in IntercomProjectPaths.All)
                    if (AssetDatabase.LoadMainAssetAtPath(key.Resolve()) == null) report.Errors.Add("Не импортирован ресурс: " + key.Label + ".");
            }
            catch (Exception error) { report = new IntercomProjectRepairAudit.Report(); report.Errors.Add(error.Message); }
            Repaint();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("CareProject · " + IntercomFrontEnd.BuildVersion, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Это версия скомпилированного кода открытого проекта. Если игра выглядит старой, сравни эту метку с версией ZIP и проверь путь к проекту ниже.", MessageType.Info);
            if (report != null)
            {
                foreach (string line in report.Facts) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
                foreach (string line in report.Errors) EditorGUILayout.HelpBox(line, MessageType.Error);
                foreach (string line in report.Warnings) EditorGUILayout.HelpBox(line, MessageType.Warning);
            }
            bool busy = EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode;
            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("1. Проверить файлы и версию")) Scan();
                if (GUILayout.Button("2. Обновить импорт интерфейса и ударника")) ReimportPresentation();
                if (GUILayout.Button("3. Открыть готовую игровую сцену")) IntercomMvpSceneBuilder.OpenOrCreateScene();
                if (GUILayout.Button("4. Восстановить отсутствующие связи (с резервной копией)")) Repair();
            }
            if (busy) EditorGUILayout.HelpBox("Останови Play Mode и дождись окончания импорта/компиляции.", MessageType.Info);
            EditorGUILayout.HelpBox("Восстановление сохраняет расположение предметов, настройки оружия, анимации, выбранные ресурсы и уже назначенные ссылки. Оно не пересоздаёт карту. Если отсутствует сама сцена, используй полный ZIP или установщик из пакета.", MessageType.None);
            if (!string.IsNullOrEmpty(outcome)) EditorGUILayout.HelpBox(outcome, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void ReimportPresentation()
        {
            if (!CanEdit()) return;
            foreach (string path in new[] { FrontendRoot.TrimEnd('/'), "Assets/IntercomMVP/Runtime/Frontend", "Assets/IntercomMVP/Runtime/Presentation/UI", "Assets/IntercomMVP/Runtime/Player/Input", "Assets/IntercomMVP/Content/Enemies/Udarnik/Model", "Assets/IntercomMVP/Content/Enemies/Udarnik/Textures" })
                if (AssetDatabase.IsValidFolder(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            outcome = "Запрошен импорт интерфейса. Дождись завершения компиляции и проверь первую красную ошибку Console, если она появится.";
            Scan();
        }

        private void Repair()
        {
            if (!CanEdit()) return;
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Сначала сохрани открытые сцены (Ctrl+S). Несохранённые изменения не будут перезаписаны восстановлением.");
                string path = IntercomProjectPaths.TutorialScene.Resolve();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) throw new FileNotFoundException("Готовая сцена не найдена. Восстанови полный пакет проекта.");
                Scene scene = SceneManager.GetActiveScene();
                if (scene.path != path) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                GameObject hero = Find(scene, "Robot"), missions = Find(scene, "MVP_NpcSandbox"), hub = Find(scene, "MVP_MenuHub");
                Camera gameCamera = Find(scene, "Main Camera").GetComponent<Camera>();
                Camera menuCamera = Find(scene, "Menu Room Camera").GetComponent<Camera>();
                var input = Required<IntercomPlayerInput>(hero); var health = Required<IntercomHealth>(hero);
                var motor = Required<IntercomPlayerMotor>(hero); var vitality = Required<IntercomPlayerVitality>(hero);
                var mission = Required<IntercomMissionDirector>(missions);
                if (gameCamera == null || menuCamera == null) throw new InvalidOperationException("В готовой сцене отсутствуют компоненты камер. Восстанови сцену из пакета.");
                // Resolve all authored anchors before the first mutation.
                var anchors = new[] { Find(scene,"Menu Camera Anchor - Home").transform, Find(scene,"Menu Camera Anchor - Play").transform,
                    Find(scene,"Menu Camera Anchor - Robot").transform, Find(scene,"Menu Camera Anchor - Armory").transform };
                // Resolve required defaults before the first mutation. Existing custom assignments win.
                var existingFront = missions.GetComponent<IntercomFrontEnd>();
                var weapon = existingFront != null && existingFront.weapon != null ? existingFront.weapon : Load<IntercomWeaponDefinition>("efc4ccf9a1224f078dadf974b01d842e", "Assets/IntercomMVP/Content/Weapons/Pistols/Settings/DualPistols.asset");
                var heroArt = existingFront != null && existingFront.heroPortrait != null ? existingFront.heroPortrait : Load<Texture2D>("a81669f6c57052ff8f192b3e2cd784fe",FrontendRoot+"Textures/RobotPortrait.png");
                var weaponArt = existingFront != null && existingFront.weaponPortrait != null ? existingFront.weaponPortrait : Load<Texture2D>("db2cb70e07935f7580a0ce12cc835761",FrontendRoot+"Textures/PistolPortrait.png");
                var homeArt = existingFront != null && existingFront.homeDoodle != null ? existingFront.homeDoodle : Load<Texture2D>("0ff01427557a5c358daea6a697ce39cb",FrontendRoot+"Artwork/HomeDoodle.png");
                var rescueArt = existingFront != null && existingFront.rescueDoodle != null ? existingFront.rescueDoodle : Load<Texture2D>("fcf3447c8a535d2385cfb0b8086b8e6d",FrontendRoot+"Artwork/RescueDoodle.png");
                var existingFx = mission.deathEffects != null ? mission.deathEffects : missions.GetComponent<IntercomRobotDeathEffects>();
                var gearMat = existingFx != null && existingFx.gearMaterial != null ? existingFx.gearMaterial : Load<Material>("49bea61217ce5da5817878518d6dee57", "Assets/IntercomMVP/Content/Enemies/Udarnik/Materials/UdarnikScrap.mat");
                var inkMat = existingFx != null && existingFx.outlineMaterial != null ? existingFx.outlineMaterial : Load<Material>("e92760ecb48556b5a79ea1ad20cf9bcc", "Assets/IntercomMVP/Content/Enemies/Udarnik/Materials/UdarnikScrapInk.mat");
                var popMat = existingFx != null && existingFx.flashMaterial != null ? existingFx.flashMaterial : Load<Material>("6bd6116b63c5557aa6e52c330e362bc4", "Assets/IntercomMVP/Content/Enemies/Udarnik/Materials/UdarnikPop.mat");
                string robotOutcome;
                try { IntercomNpcSetup.EnsureRobotVisualAssets(false); robotOutcome = " Подготовка ударника проверена."; }
                catch (Exception error)
                {
                    robotOutcome = " Модель ударника требует проверки: " + error.Message;
                    Debug.LogWarning("CareProject: " + robotOutcome);
                }
                var edit = new RepairEdit(scene, path);
                var front = edit.Component<IntercomFrontEnd>(missions);
                var rooms = edit.Component<IntercomFrontEndRoomCamera>(hub);
                var effects = existingFx != null ? existingFx : edit.Component<IntercomRobotDeathEffects>(missions);
                edit.Fill(ref mission.deathEffects, effects, mission, "Задание → шестерёнки и запчасти");
                edit.Fill(ref effects.definition, mission.enemyDefinition, effects, "Запчасти → профиль ударника");
                edit.Fill(ref effects.gearMaterial, gearMat, effects, "Материал шестерёнок");
                edit.Fill(ref effects.outlineMaterial, inkMat, effects, "Контуры запчастей");
                edit.Fill(ref effects.flashMaterial, popMat, effects, "Материал хлопка");
                edit.Fill(ref front.mission, mission, front, "Меню → задание");
                edit.Fill(ref front.input, input, front, "Меню → ввод");
                edit.Fill(ref front.playerHealth, health, front, "Меню → здоровье");
                edit.Fill(ref front.playerMotor, motor, front, "Меню → движение");
                edit.Fill(ref front.roomCamera, rooms, front, "Меню → комнаты");
                edit.Fill(ref front.weapon, weapon, front, "Меню → оружие");
                edit.Fill(ref front.heroPortrait, heroArt, front, "Портрет героя");
                edit.Fill(ref front.weaponPortrait, weaponArt, front, "Портрет оружия");
                edit.Fill(ref front.homeDoodle, homeArt, front, "Зарисовка отдыха");
                edit.Fill(ref front.rescueDoodle, rescueArt, front, "Зарисовка спасения");
                edit.Fill(ref rooms.roomCamera, menuCamera, rooms, "Камера комнат");
                edit.Fill(ref rooms.gameplayCamera, gameCamera, rooms, "Игровая камера");
                edit.Fill(ref rooms.homeAnchor, anchors[0], rooms, "Ракурс главной");
                edit.Fill(ref rooms.playAnchor, anchors[1], rooms, "Ракурс заданий");
                edit.Fill(ref rooms.robotAnchor, anchors[2], rooms, "Ракурс мастерской");
                edit.Fill(ref rooms.armoryAnchor, anchors[3], rooms, "Ракурс оружейной");
                edit.Fill(ref input.mission, mission, input, "Ввод → задание");
                edit.Fill(ref input.vitality, vitality, input, "Ввод → состояние героя");
                edit.Fill(ref input.target, hero.transform, input, "Цель ввода");
                edit.Fill(ref input.movementCamera, gameCamera, input, "Камера ввода");
                edit.Fill(ref input.weaponHudPortrait, front.weaponPortrait, input, "Значок пистолетов в HUD");
                edit.Fill(ref mission.playerHealth, health, mission, "Задание → здоровье");
                edit.Fill(ref mission.playerMotor, motor, mission, "Задание → движение");
                edit.Fill(ref mission.playerVitality, vitality, mission, "Задание → состояние героя");
                if (edit.Changes.Count == 0) outcome = "Связи сцены уже назначены. Подготовка визуала ударника проверена; сцена не изменена.";
                else
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Сцена не сохранилась. Резервная копия: " + edit.Backup);
                    Undo.CollapseUndoOperations(edit.UndoGroup);
                    outcome = "Восстановлено связей/компонентов: " + edit.Changes.Count + ". Резервная копия: " + edit.Backup;
                }
                outcome += robotOutcome;
                Scan();
            }
            catch (Exception error) { outcome = error.Message; Debug.LogWarning("CareProject: " + outcome); Repaint(); }
        }

        private static bool CanEdit() => !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode;
        private static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        private static GameObject Find(Scene scene,string name)
        {
            GameObject[] found=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).Where(t=>t.name==name).Select(t=>t.gameObject).ToArray();
            if(found.Length!=1) throw new InvalidOperationException("Ожидался один объект «"+name+"», найдено: "+found.Length+". Восстанови готовую сцену из пакета или проверь имена вручную.");
            return found[0];
        }
        private static T Required<T>(GameObject owner) where T:Component
        {T value=owner.GetComponent<T>();if(value==null)throw new InvalidOperationException("Нет обязательного компонента "+typeof(T).Name+" у "+owner.name+". Игровая логика автоматически не пересоздаётся.");return value;}
        private static T Load<T>(string guid,string fallback) where T:UnityEngine.Object
        {string path=IntercomAssetCatalog.ResolveGuid(guid,fallback);T value=AssetDatabase.LoadAssetAtPath<T>(path);if(value==null)throw new FileNotFoundException("Ресурс не импортирован: "+path);return value;}

        private sealed class RepairEdit
        {
            private readonly Scene scene; private readonly string scenePath;
            internal readonly List<string> Changes=new List<string>();
            internal string Backup { get; private set; }
            internal int UndoGroup { get; private set; }
            internal RepairEdit(Scene value,string path){scene=value;scenePath=path;}
            private void BeforeChange()
            {
                if(Backup!=null)return;
                string folder=Path.Combine(ProjectRoot(),"ProjectBackups",DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8));
                Directory.CreateDirectory(folder);Backup=Path.Combine(folder,Path.GetFileName(scenePath));
                File.Copy(Path.Combine(ProjectRoot(),scenePath),Backup,false);
                string meta=Path.Combine(ProjectRoot(),scenePath+".meta");if(File.Exists(meta))File.Copy(meta,Backup+".meta",false);
                Undo.IncrementCurrentGroup();UndoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("CareProject: restore missing links");
            }
            internal T Component<T>(GameObject owner) where T:Component
            {T value=owner.GetComponent<T>();if(value!=null)return value;BeforeChange();value=Undo.AddComponent<T>(owner);Changes.Add("Добавлен "+typeof(T).Name);return value;}
            internal void Fill<T>(ref T slot,T value,UnityEngine.Object owner,string label) where T:UnityEngine.Object
            {
                if(slot!=null || value==null)return;
                BeforeChange();Undo.RecordObject(owner,"CareProject: "+label);slot=value;EditorUtility.SetDirty(owner);
                if(PrefabUtility.IsPartOfPrefabInstance(owner))PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
                Changes.Add(label);
            }
        }
    }
#endif
}
