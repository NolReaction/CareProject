using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    /// <summary>Explicit sandbox installation. Repeated invocation preserves existing authored tuning.</summary>
    internal static class IntercomNpcSetup
    {
        internal const string ArenaName = "MVP_NpcSandbox";
        internal const string DefinitionPath = "Assets/IntercomMVP/Content/Enemies/Udarnik/Settings/UdarnikCapsule.asset";
        internal const string RobotModelPath = "Assets/IntercomMVP/Content/Enemies/Udarnik/Model/Udarnik.fbx";
        internal const string RobotControllerPath = "Assets/IntercomMVP/Content/Enemies/Udarnik/Animation/Udarnik.controller";
        internal const string RobotPrefabPath = "Assets/IntercomMVP/Content/Enemies/Udarnik/Prefabs/UdarnikVisual.prefab";
        internal const string RobotBreakupModelPath = "Assets/IntercomMVP/Content/Enemies/Udarnik/Model/UdarnikBreakup.fbx";
        internal const string RobotBreakupPrefabPath = "Assets/IntercomMVP/Content/Enemies/Udarnik/Prefabs/UdarnikBreakup.prefab";
        internal const string RobotMaterialPath = "Assets/IntercomMVP/Content/Enemies/Udarnik/Materials/UdarnikAtlas.mat";
        internal const string RobotAtlasPath = "Assets/IntercomMVP/Content/Enemies/Udarnik/Textures/UdarnikAtlas.png";
        private const string RobotRevisionLabel = "CareProject_Udarnik_V19";
        private static readonly string[] LegacyRobotClipNames = { "Idle", "Walk", "Windup", "Strike", "Recover", "Hurt", "Death", "Emerge" };
        private static readonly string[] RobotClipNames = LegacyRobotClipNames.Concat(new[]{"EmergeGround","EmergeHatch"}).ToArray();
        private static string RobotPreparationSessionKey => "CareProject.UdarnikPrepared.V19."+Application.dataPath;
        [InitializeOnLoadMethod]
        private static void QueueImportedRobotPreparation() { EditorApplication.delayCall+=PrepareImportedRobotWhenReady; }
        private static void PrepareImportedRobotWhenReady()
        {
            if(SessionState.GetBool(RobotPreparationSessionKey,false))return;
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorApplication.delayCall+=PrepareImportedRobotWhenReady;return; }
            string project=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,".."));
            if(!System.IO.File.Exists(System.IO.Path.Combine(project,RobotModelPath)))return;
            IntercomNpcDefinition definition=AssetDatabase.LoadAssetAtPath<IntercomNpcDefinition>(DefinitionPath);
            bool breakupPending=System.IO.File.Exists(System.IO.Path.Combine(project,RobotBreakupModelPath))
                && AssetDatabase.LoadAssetAtPath<GameObject>(RobotBreakupModelPath)==null;
            if(definition==null || AssetDatabase.LoadAssetAtPath<GameObject>(RobotModelPath)==null || breakupPending)
            {
                string pendingKey=RobotPreparationSessionKey+".ImportWait";
                int waited=SessionState.GetInt(pendingKey,0);SessionState.SetInt(pendingKey,waited+1);
                if(waited<3) { EditorApplication.delayCall+=PrepareImportedRobotWhenReady;return; }
                SessionState.SetBool(RobotPreparationSessionKey,true);
                Debug.LogWarning("CareProject: дождитесь импорта FBX ударника, затем откройте Tools → CareProject → Проверить и восстановить проект.");
                return;
            }
            SessionState.SetBool(RobotPreparationSessionKey,true);
            if(definition.visualPrefab!=null && AssetDatabase.GetAssetPath(definition.visualPrefab)!=RobotPrefabPath)return;
            try { EnsureRobotVisualAssets(false); }
            catch(Exception error)
            {
                Debug.LogWarning("CareProject: автоматическая подготовка ударника не завершена. "+error.Message+
                    " После импорта откройте Tools → CareProject → Проверить и восстановить проект. Ваши сцены и назначенные модели сохранены.");
            }
        }
        private static readonly Vector3[] SpawnPositions =
            { new Vector3(-6f, 0f, -3f), new Vector3(3.5f, 0f, 1f), new Vector3(1f, 0f, 9f) };

        [MenuItem("Tools/CareProject/Advanced/Authoring/NPC/Install Capsule Sandbox", false, 120)]
        private static void InstallCurrent()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != IntercomMvpSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open the saved IntercomMVP scene outside Play Mode before installing NPCs.");
            Install(scene);
        }

        [MenuItem("Tools/CareProject/Advanced/Authoring/NPC/Setup Ground and Hatch Points", false, 121)]
        private static void SetupAuthoredPoints()
        {
            Scene scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!=IntercomMvpSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open the saved tutorial scene outside Play Mode.");
            GameObject arena=scene.GetRootGameObjects().FirstOrDefault(item=>item.name==ArenaName);
            IntercomMissionDirector director=arena!=null ? arena.GetComponent<IntercomMissionDirector>() : null;
            if(director==null) throw new InvalidOperationException("The tutorial scene needs its existing Mission Director.");
            string[] names={"Spawn Hatch - Central Yard","Spawn Hatch - Workshop Lane","Ground Arrival - North Yard"};
            Vector3[] positions={new Vector3(4f,0f,-2f),new Vector3(-8f,0f,-5f),new Vector3(1f,0f,6f)};
            var points=new IntercomEnemySpawnPoint[names.Length];
            bool changed=false;
            for(int index=0;index<names.Length;index++)
            {
                Transform marker=arena.transform.Find(names[index]);
                if(marker==null)
                {
                    var point=new GameObject(names[index]);
                    Undo.RegisterCreatedObjectUndo(point,"Create authored robot arrival");
                    point.transform.SetParent(arena.transform,false);point.transform.localPosition=positions[index];
                    marker=point.transform;changed=true;
                }
                points[index]=marker.GetComponent<IntercomEnemySpawnPoint>();
                if(points[index]!=null) continue; // Preserve a designer's marker tuning and visual references.
                points[index]=Undo.AddComponent<IntercomEnemySpawnPoint>(marker.gameObject);
                points[index].kind=index<2 ? IntercomEnemySpawnKind.Hatch : IntercomEnemySpawnKind.Ground;
                EditorUtility.SetDirty(points[index]);changed=true;
            }
            if(director.authoredSpawnPoints==null || director.authoredSpawnPoints.Length==0)
            {
                Undo.RecordObject(director,"Connect authored robot arrivals");
                director.authoredSpawnPoints=points;EditorUtility.SetDirty(director);changed=true;
            }
            if(changed) EditorSceneManager.MarkSceneDirty(scene);
        }

        internal static void Install(Scene scene)
        {
            GameObject robot = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Robot");
            GameObject environment = scene.GetRootGameObjects().FirstOrDefault(item => item.name == IntercomDistrictSetup.RootName);
            if (robot == null || environment == null)
                throw new InvalidOperationException("NPC sandbox requires the existing Robot and tutorial environment.");
            IntercomPlayerMotor motor = robot.GetComponent<IntercomPlayerMotor>();
            IntercomPlayerInput input = robot.GetComponent<IntercomPlayerInput>();
            IntercomSimpleShooter shooter = robot.GetComponent<IntercomSimpleShooter>();
            IntercomWeaponPose pose = robot.GetComponent<IntercomWeaponPose>();
            if (motor == null || input == null || shooter == null || pose == null)
                throw new InvalidOperationException("Complete the existing player setup before installing NPCs.");
            IntercomNpcDefinition definition = EnsureDefinition();

            IntercomHealth health = robot.GetComponent<IntercomHealth>();
            if (health == null)
            {
                health = Undo.AddComponent<IntercomHealth>(robot);
                health.maximumHealth = 100f;
                health.team = IntercomTeam.Player;
                health.postHitInvulnerability = .6f;
                EditorUtility.SetDirty(health);
            }
            IntercomPlayerVitality vitality = robot.GetComponent<IntercomPlayerVitality>();
            if (vitality == null)
            {
                vitality = Undo.AddComponent<IntercomPlayerVitality>(robot);
                vitality.health = health; vitality.motor = motor; vitality.input = input;
                vitality.shooter = shooter; vitality.weaponPose = pose;
                EditorUtility.SetDirty(vitality);
            }
            if (input.vitality == null)
            {
                Undo.RecordObject(input, "Connect player vitality");
                input.vitality = vitality; EditorUtility.SetDirty(input);
            }
            IntercomPlayerHurtSetup.Setup(robot);

            GameObject arena = scene.GetRootGameObjects().FirstOrDefault(item => item.name == ArenaName);
            if (arena == null)
            {
                arena = new GameObject(ArenaName);
                SceneManager.MoveGameObjectToScene(arena, scene);
                Undo.RegisterCreatedObjectUndo(arena, "Create NPC sandbox");
            }
            IntercomNpcNavigation navigation = arena.GetComponent<IntercomNpcNavigation>();
            if (navigation == null)
            {
                navigation = Undo.AddComponent<IntercomNpcNavigation>(arena);
                navigation.obstacleRoot = environment.transform;
                EditorUtility.SetDirty(navigation);
            }
            if (arena.GetComponent<IntercomNpcArena>() == null)
            {
                var markers = new Transform[SpawnPositions.Length];
                for (int i = 0; i < markers.Length; i++)
                {
                    string name = "NPC Spawn " + (i + 1).ToString("00");
                    markers[i] = arena.transform.Find(name);
                    if (markers[i] != null) continue;
                    GameObject point = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(point, "Create NPC spawn marker");
                    point.transform.SetParent(arena.transform, false);
                    point.transform.localPosition = SpawnPositions[i];
                    markers[i] = point.transform;
                }
                IntercomNpcArena bootstrap = Undo.AddComponent<IntercomNpcArena>(arena);
                bootstrap.definition = definition; bootstrap.navigation = navigation;
                bootstrap.playerHealth = health; bootstrap.playerMotor = motor;
                bootstrap.spawnPoints = markers; bootstrap.maximumActors = 3;
                EditorUtility.SetDirty(bootstrap);
            }
            IntercomPerformanceTools.Setup(robot, navigation);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static IntercomNpcDefinition EnsureDefinition()
        {
            IntercomNpcDefinition existing = AssetDatabase.LoadAssetAtPath<IntercomNpcDefinition>(DefinitionPath);
            if (existing != null) return existing;
            IntercomAssetCatalog.EnsureParentFolder(DefinitionPath);
            IntercomNpcDefinition definition = ScriptableObject.CreateInstance<IntercomNpcDefinition>();
            definition.name = "UdarnikCapsule";
            definition.bodyMaterial = EnsureMaterial("UdarnikBody", new Color(.72f, .68f, .55f), false);
            definition.detailMaterial = EnsureMaterial("UdarnikDetail", new Color(.12f, .19f, .20f), false);
            definition.telegraphMaterial = EnsureMaterial("UdarnikTelegraph", new Color(.95f, .67f, .22f, .38f), true);
            AssetDatabase.CreateAsset(definition, DefinitionPath);
            return definition;
        }

        /// <summary>Native authoring after the FBX importer has run. No guessed model sub-asset IDs or scene rebuild.</summary>
        internal static void EnsureRobotVisualAssets(bool replace=false)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Дождитесь импорта и компиляции вне Play Mode перед подготовкой ударника.");
            IntercomNpcDefinition definition=EnsureDefinition();
            if(!replace && definition.visualPrefab!=null && AssetDatabase.GetAssetPath(definition.visualPrefab)!=RobotPrefabPath)
                return; // A developer explicitly assigned a different robot. Do not replace their work.
            var importer=AssetImporter.GetAtPath(RobotModelPath) as ModelImporter;
            if(importer!=null && (!importer.importAnimation || importer.animationType!=ModelImporterAnimationType.Generic
                || importer.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel))
            {
                importer.importAnimation=true;importer.animationType=ModelImporterAnimationType.Generic;
                importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();
            }
            GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(RobotModelPath);
            if(model==null) throw new InvalidOperationException("Не импортирован ударник: "+RobotModelPath);
            // Imported Generic FBXs need no AnimatorController yet. Read native clip subassets directly.
            AnimationClip[] clips=AssetDatabase.LoadAllAssetsAtPath(RobotModelPath).OfType<AnimationClip>().ToArray();
            var byName=new System.Collections.Generic.Dictionary<string,AnimationClip>(StringComparer.Ordinal);
            foreach(AnimationClip clip in clips)
            {
                if(clip==null || clip.name.StartsWith("__preview__",StringComparison.Ordinal)) continue;
                string name=clip.name;
                // FBX takes may be prefixed with the armature name. The actual AnimationClip remains referenced.
                int separator=name.LastIndexOf('|');if(separator>=0) name=name.Substring(separator+1);
                if(RobotClipNames.Contains(name)) byName[name]=clip;
            }
            string[] missing=RobotClipNames.Where(name=>!byName.ContainsKey(name)).ToArray();
            if(missing.Length>0) throw new InvalidOperationException("В FBX ударника не найдены клипы: "+string.Join(", ",missing));
            Material material=AssetDatabase.LoadAssetAtPath<Material>(RobotMaterialPath);
            if(material==null)
            {
                Texture2D atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(RobotAtlasPath);
                Shader shader=Shader.Find("Universal Render Pipeline/Lit");
                if(atlas==null || shader==null) throw new InvalidOperationException("Ударнику нужны импортированный UdarnikAtlas.png и URP/Lit.");
                IntercomAssetCatalog.EnsureParentFolder(RobotMaterialPath);
                material=new Material(shader){name="UdarnikAtlas",enableInstancing=true};
                material.SetTexture("_BaseMap",atlas);material.SetColor("_BaseColor",Color.white);
                material.SetFloat("_Metallic",0f);material.SetFloat("_Smoothness",.22f);
                AssetDatabase.CreateAsset(material,RobotMaterialPath);
            }
            var controller=AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(RobotControllerPath);
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabPath);
            bool newController=controller==null;
            bool generatedPrefab=prefab==null || IsGeneratedVisualPrefab(prefab,controller);
            bool legacyController=!newController && IsGeneratedRobotController(controller,LegacyRobotClipNames);
            bool currentController=!newController && IsGeneratedRobotController(controller,RobotClipNames);
            if(!replace && (!generatedPrefab || (!newController && !legacyController && !currentController)))
                throw new InvalidOperationException("Udarnik.controller или UdarnikVisual.prefab изменён вручную. Он сохранён без изменений; назначьте собственные клипы самостоятельно или используйте отдельный prefab.");
            bool refreshController=!newController && (replace || legacyController ||
                controller.layers[0].stateMachine.states.Any(child=>child.state.motion!=byName[child.state.name]));
            if(refreshController || (replace && prefab!=null))
                BackupRobotAssets(RobotControllerPath,RobotPrefabPath,DefinitionPath);
            if(newController)
            {
                IntercomAssetCatalog.EnsureParentFolder(RobotControllerPath);
                controller=UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(RobotControllerPath);
                AddRobotStates(controller,byName);
            }
            else if(refreshController)
            {
                // Keep the controller GUID and prefab reference. The imported model variant inherits the repaired mesh.
                Undo.RecordObject(controller,"Upgrade generated Udarnik states to V19");
                var machine=controller.layers[0].stateMachine;
                Undo.RecordObject(machine,"Upgrade generated Udarnik state machine");
                if(replace)
                    foreach(var child in machine.states.Where(child=>RobotClipNames.Contains(child.state.name)).ToArray())
                        machine.RemoveState(child.state);
                AddRobotStates(controller,byName);
            }
            MarkRobotRevision(controller);
            if(prefab==null || replace)
            {
                IntercomAssetCatalog.EnsureParentFolder(RobotPrefabPath);
                Scene preview=EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(model,preview);
                    instance.name="UdarnikVisual";
                    instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);instance.transform.localScale=Vector3.one;
                    // Animator stays on the imported model root: adding a wrapper here breaks FBX bone curve paths.
                    Animator animator=instance.GetComponent<Animator>();
                    if(animator==null) animator=instance.AddComponent<Animator>();
                    animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                    animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    foreach(Collider collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                    foreach(Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                        if(renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen=false;
                    }
                    prefab=PrefabUtility.SaveAsPrefabAsset(instance,RobotPrefabPath);
                    if(prefab==null) throw new InvalidOperationException("Unity не удалось сохранить prefab ударника.");
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            MarkRobotRevision(prefab);
            GameObject breakup=EnsureRobotBreakupPrefab(material,replace);
            bool connectVisual=definition.visualPrefab==null;
            bool connectBreakup=definition.breakupPrefab==null && breakup!=null;
            if(connectVisual || connectBreakup)
            {
                BackupRobotAssets(DefinitionPath);
                Undo.RecordObject(definition,"Connect authored Udarnik presentation");
                if(connectVisual) definition.visualPrefab=prefab;
                if(connectBreakup) definition.breakupPrefab=breakup;
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
        }

        // V18 generated exactly this simple eight-state graph. Unknown parameters, transitions,
        // behaviours, blend trees, renamed states or a foreign motion are treated as authored work.
        private static bool IsGeneratedRobotController(UnityEditor.Animations.AnimatorController controller,string[] expected)
        {
            if(controller==null || controller.layers.Length!=1 || controller.parameters.Length!=0) return false;
            var layer=controller.layers[0];var machine=layer.stateMachine;
            if(layer.name!="Base Layer" || layer.avatarMask!=null || layer.syncedLayerIndex!=-1 || machine==null
                || machine.stateMachines.Length!=0 || machine.behaviours.Length!=0
                || machine.anyStateTransitions.Length!=0 || machine.entryTransitions.Length!=0
                || machine.states.Length!=expected.Length || machine.defaultState==null || machine.defaultState.name!="Idle") return false;
            foreach(var child in machine.states)
            {
                var state=child.state;
                if(state==null || !expected.Contains(state.name) || machine.states.Count(item=>item.state.name==state.name)!=1
                    || state.transitions.Length!=0 || state.behaviours.Length!=0 || !state.writeDefaultValues
                    || state.speed!=1f || state.cycleOffset!=0f || state.mirror || state.iKOnFeet || !string.IsNullOrEmpty(state.tag)
                    || state.speedParameterActive || state.mirrorParameterActive || state.cycleOffsetParameterActive || state.timeParameterActive)
                    return false;
                if(state.motion==null) continue; // A reimport may invalidate a native subasset; reconnect only this known graph.
                if(!(state.motion is AnimationClip) || AssetDatabase.GetAssetPath(state.motion)!=RobotModelPath
                    || ClipShortName(state.motion.name)!=state.name) return false;
            }
            return true;
        }

        private static string ClipShortName(string name)
        { int separator=name.LastIndexOf('|');return separator>=0?name.Substring(separator+1):name; }

        private static bool IsGeneratedVisualPrefab(GameObject prefab,UnityEditor.Animations.AnimatorController controller)
        {
            if(prefab==null) return true;
            if(controller==null) return false;
            GameObject source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab);
            Animator animator=prefab.GetComponent<Animator>();
            return source!=null && AssetDatabase.GetAssetPath(source)==RobotModelPath
                && animator!=null && animator.runtimeAnimatorController==controller
                && prefab.GetComponentsInChildren<Animator>(true).Length==1
                && prefab.GetComponentsInChildren<MonoBehaviour>(true).Length==0
                && prefab.GetComponentsInChildren<Collider>(true).Length==0;
        }

        private static void MarkRobotRevision(UnityEngine.Object asset)
        {
            if(asset==null) return;
            string[] labels=AssetDatabase.GetLabels(asset);
            if(!labels.Contains(RobotRevisionLabel)) AssetDatabase.SetLabels(asset,labels.Concat(new[]{RobotRevisionLabel}).ToArray());
        }

        private static void BackupRobotAssets(params string[] assets)
        {
            string project=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,".."));
            string backup=System.IO.Path.Combine(project,"ProjectBackups",DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_udarnik_V19_"+Guid.NewGuid().ToString("N").Substring(0,8));
            System.IO.Directory.CreateDirectory(backup);
            foreach(string asset in assets.Distinct())
                foreach(string suffix in new[]{"",".meta"})
                {
                    string original=System.IO.Path.Combine(project,asset+suffix);
                    if(System.IO.File.Exists(original))System.IO.File.Copy(original,System.IO.Path.Combine(backup,System.IO.Path.GetFileName(asset)+suffix),false);
                }
        }

        private static GameObject EnsureRobotBreakupPrefab(Material material,bool replace)
        {
            GameObject existing=AssetDatabase.LoadAssetAtPath<GameObject>(RobotBreakupPrefabPath);
            if(existing!=null && !replace) return existing;
            GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(RobotBreakupModelPath);
            if(model==null) return null; // Optional presentation: the death pool always has a shared generic gear fallback.
            if(existing!=null) BackupRobotAssets(RobotBreakupPrefabPath,DefinitionPath);
            IntercomAssetCatalog.EnsureParentFolder(RobotBreakupPrefabPath);
            Scene preview=EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject imported=(GameObject)PrefabUtility.InstantiatePrefab(model,preview);
                MeshFilter[] filters=imported.GetComponentsInChildren<MeshFilter>(true);
                if(filters.Length==0 || filters.Length>6 || imported.GetComponentInChildren<SkinnedMeshRenderer>(true)!=null)
                    throw new InvalidOperationException("UdarnikBreakup.fbx должен содержать от одной до шести статических деталей без скелета.");
                GameObject root=new GameObject("UdarnikBreakup");SceneManager.MoveGameObjectToScene(root,preview);
                foreach(MeshFilter filter in filters)
                {
                    if(filter.sharedMesh==null) throw new InvalidOperationException("У детали UdarnikBreakup отсутствует mesh.");
                    // Flatten imported transforms without changing each chunk's centered, authored pivot.
                    Matrix4x4 relative=imported.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    GameObject chunk=new GameObject(filter.gameObject.name);chunk.transform.SetParent(root.transform,false);
                    chunk.transform.localPosition=relative.GetColumn(3);chunk.transform.localRotation=relative.rotation;
                    chunk.transform.localScale=relative.lossyScale;
                    chunk.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                    var renderer=chunk.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                }
                GameObject prefab=PrefabUtility.SaveAsPrefabAsset(root,RobotBreakupPrefabPath);
                if(prefab==null) throw new InvalidOperationException("Unity не удалось сохранить детали ударника.");
                MarkRobotRevision(prefab);return prefab;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void AddRobotStates(UnityEditor.Animations.AnimatorController controller,
            System.Collections.Generic.Dictionary<string,AnimationClip> clips)
        {
            var machine=controller.layers[0].stateMachine;
            for(int i=0;i<RobotClipNames.Length;i++)
            {
                string name=RobotClipNames[i];
                var state=machine.states.FirstOrDefault(child=>child.state.name==name).state;
                if(state==null) state=machine.AddState(name,new Vector3(220f+(i%2)*230f,70f+(i/2)*80f));
                else Undo.RecordObject(state,"Reconnect generated Udarnik clip");
                state.motion=clips[name];state.writeDefaultValues=true;
                if(name=="Idle") machine.defaultState=state;
            }
            EditorUtility.SetDirty(controller);
        }

        private static Material EnsureMaterial(string name, Color color, bool transparent)
        {
            string path = "Assets/IntercomMVP/Content/Enemies/Udarnik/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find(transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("NPC material requires the existing URP shaders.");
            material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f); material.SetFloat("_Cull", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            IntercomAssetCatalog.EnsureParentFolder(path);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
