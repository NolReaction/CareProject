using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    internal static class IntercomMovementSetup
    {
        private const string AnimationFolder = "Assets/IntercomMVP/Content/Characters/Robot/Animation";
        // A new asset leaves the previous controller and any user edits intact.
        private const string LegacyControllerPath = AnimationFolder + "/IntercomCombat.controller";
        private const string ControllerPath = AnimationFolder + "/IntercomDirectional.controller";
        private static string ProfilePath => IntercomProjectPaths.PistolPose.Resolve();
        private const string GeneratedMaskName = "Intercom Upper Body";
        private static readonly string[] ClipNames = { "Idle", "Run", "RunBackward", "RunLeft", "RunRight",
            "IdleRelaxed", "RunRelaxed", "RunBackwardRelaxed", "RunLeftRelaxed", "RunRightRelaxed",
            "RaiseWeapons", "LowerWeapons", "Shoot" };

        [MenuItem("Tools/CareProject/Setup Robot", false, 20)]
        private static void SetupActiveScene()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                GameObject robot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Robot");
                if (robot == null)
                    throw new InvalidOperationException("В активной сцене не найден корневой объект Robot. Открой Tools > Intercom MVP > Open Scene.");
                Setup(robot, FindCamera(scene));
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = robot;
                Debug.Log("Intercom MVP: движение, контакт ботинок, оружие и звук подключены. Сохрани сцену (Ctrl+S), нажми Play и кликни Game. WASD / стрелки — движение; ЛКМ / пробел — прицеливание и стрельба; R — старт. На телефоне левый стик — движение, правый — направление и огонь.", robot);
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Tools/CareProject/Setup Robot", true)]
        private static bool CanSetup()
        {
            return !EditorApplication.isCompiling && !EditorApplication.isUpdating
                && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        internal static void Setup(GameObject robot, Camera camera)
        {
            ModelImporter importer = AssetImporter.GetAtPath(IntercomRobotAssets.ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Не найден импортированный IntercomRobot.fbx.");
            bool changed = ConfigureClips(importer);
            if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }
            if (importer.importVisibility) { importer.importVisibility = false; changed = true; }
            if (changed) importer.SaveAndReimport();

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(IntercomRobotAssets.ModelPath)
                .OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)).ToArray();
            AnimationClip idle = FindClip(clips, "Idle");
            AnimationClip run = FindClip(clips, "Run");
            AnimationClip shoot = FindClip(clips, "Shoot");
            AnimationClip idleRelaxed = FindClip(clips, "IdleRelaxed");
            AnimationClip runRelaxed = FindClip(clips, "RunRelaxed");
            AnimationClip raise = FindClip(clips, "RaiseWeapons");
            AnimationClip lower = FindClip(clips, "LowerWeapons");
            string[] missing = ClipNames.Where(name => FindClip(clips, name) == null).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("В FBX не найдены клипы: " + string.Join(", ", missing)
                    + ". Скопируй обновлённый IntercomRobot.fbx вместе со скриптами и дождись импорта.");

            // Bind at the actual imported prefab root; adding an Animator to
            // the Robot wrapper would shift every animation binding by a level.
            Animator animator = robot.GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(item => AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject)) == IntercomRobotAssets.ModelPath);
            if (animator == null) animator = robot.GetComponentInChildren<Animator>(true);
            if (animator == null)
                throw new InvalidOperationException("Не найден Animator на корне импортированной модели. У FBX должен быть Rig: Generic, Avatar Definition: Create From This Model.");
            if (robot.GetComponent<Rigidbody>() != null)
                throw new InvalidOperationException("На Robot уже есть Rigidbody. Эта заготовка использует CharacterController. Сохрани свою настройку физики и выбери один способ движения перед установкой.");

            AnimatorController controller = GetOrCreateController(animator, clips, idle, run, idleRelaxed, runRelaxed, raise, lower, shoot);
            IntercomWeaponPoseProfile defaultProfile = GetOrCreateProfile(raise, lower, shoot);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Intercom Movement");

            Undo.RecordObject(animator, "Connect Intercom Animator");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (PrefabUtility.IsPartOfPrefabInstance(animator))
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

            CharacterController capsule = robot.GetComponent<CharacterController>();
            bool createdCapsule = capsule == null;
            if (capsule == null)
            {
                capsule = Undo.AddComponent<CharacterController>(robot);
                FitCapsule(capsule, animator.gameObject);
            }
            // The older scene kept a capsule fitted to an earlier model. Its
            // bottom sat 4 cm below the current soles. Migrate only that exact
            // shipped fit; keep an intentionally edited capsule unchanged.
            bool legacyGroundFit = Mathf.Abs(capsule.center.y - 0.7993455f) < 0.0001f
                && Mathf.Abs(capsule.height - 1.6800449f) < 0.0001f;
            if (createdCapsule || legacyGroundFit) AlignCapsuleToIdleFeet(capsule, animator, idle);
            IntercomPlayerMotor motor = robot.GetComponent<IntercomPlayerMotor>();
            if (motor == null) motor = Undo.AddComponent<IntercomPlayerMotor>(robot);
            Undo.RecordObject(motor, "Connect Intercom Movement");
            motor.animator = animator;
            motor.movementCamera = camera;
            motor.enabled = true;
            // Migrate only the old shipped reference value; keep a user's tuning.
            if (Mathf.Approximately(motor.moveSpeed, 0.8f) || Mathf.Approximately(motor.moveSpeed, 1.8f)
                || Mathf.Approximately(motor.moveSpeed, 2.1f))
                motor.moveSpeed = 3.3f;
            if (motor.strafeSpeedRatio <= 0f) motor.strafeSpeedRatio = 0.75f;
            // New components use the authored .75 lateral ratio; retain user tuning.
            if (Mathf.Approximately(motor.acceleration, 18f) || Mathf.Approximately(motor.acceleration, 8f)) motor.acceleration = 22f;
            if (Mathf.Approximately(motor.deceleration, 24f) || Mathf.Approximately(motor.deceleration, 12f)) motor.deceleration = 30f;
            if (Mathf.Approximately(motor.animationDamping, 0.08f)) motor.animationDamping = 0.05f;
            if (Mathf.Approximately(motor.runReferenceSpeed, 0.8f) || Mathf.Approximately(motor.runReferenceSpeed, 1.0f)
                || Mathf.Approximately(motor.runReferenceSpeed, 1.2f))
                motor.runReferenceSpeed = 1.3f; // Measured V12 stance travel, paired with the new run clips.
            EditorUtility.SetDirty(motor);
            if (PrefabUtility.IsPartOfPrefabInstance(motor))
                PrefabUtility.RecordPrefabInstancePropertyModifications(motor);

            IntercomWeaponPose weaponPose = robot.GetComponent<IntercomWeaponPose>();
            if (weaponPose == null) weaponPose = Undo.AddComponent<IntercomWeaponPose>(robot);
            Undo.RecordObject(weaponPose, "Connect Intercom Weapon Poses");
            weaponPose.animator = animator;
            if (weaponPose.profile == null) weaponPose.profile = defaultProfile;
            weaponPose.profile = CompatibleDirectionalProfile(weaponPose.profile, controller);
            weaponPose.enabled = true;
            EditorUtility.SetDirty(weaponPose);
            if (PrefabUtility.IsPartOfPrefabInstance(weaponPose))
                PrefabUtility.RecordPrefabInstancePropertyModifications(weaponPose);

            IntercomPlayerInput input = robot.GetComponent<IntercomPlayerInput>();
            if (input == null) input = Undo.AddComponent<IntercomPlayerInput>(robot);
            Undo.RecordObject(input, "Connect Intercom Twin Sticks");
            input.target = robot.transform;
            input.movementCamera = camera;
            input.enabled = true;
            motor.inputSource = input;
            motor.weaponPose = weaponPose;
            weaponPose.inputSource = input;
            EditorUtility.SetDirty(input);
            EditorUtility.SetDirty(motor);
            EditorUtility.SetDirty(weaponPose);
            if (PrefabUtility.IsPartOfPrefabInstance(input))
                PrefabUtility.RecordPrefabInstancePropertyModifications(input);
            if (PrefabUtility.IsPartOfPrefabInstance(motor))
                PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
            if (PrefabUtility.IsPartOfPrefabInstance(weaponPose))
                PrefabUtility.RecordPrefabInstancePropertyModifications(weaponPose);
            IntercomMuzzleSetup.Setup(robot, animator, weaponPose);
            IntercomMobileSetup.EnsureRuntimeSettings(robot);
            IntercomCombatSetup.Setup(robot);
            IntercomGroundFeedbackSetup.Setup(robot);
            IntercomSoleProfileBuilder.Rebuild(robot, animator, clips);
            IntercomAudioSetup.Setup(robot);
            IntercomCharacterAnimationSetup.Setup(robot, animator);
            IntercomDistrictSetup.SetupSurfaceSounds(robot.scene);

            if (camera != null)
            {
                IntercomCameraFollow follow = camera.GetComponent<IntercomCameraFollow>();
                bool captureCamera = follow == null || follow.target != robot.transform;
                if (follow == null) follow = Undo.AddComponent<IntercomCameraFollow>(camera.gameObject);
                Undo.RecordObject(follow, "Connect Intercom Camera");
                follow.target = robot.transform;
                // New/rebound drivers adopt the actual camera; existing authored follow
                // settings remain intact unless the camera was moved by hand.
                if (captureCamera) follow.CaptureFromCamera(camera);
                else follow.CaptureManualCameraChanges();
                EditorUtility.SetDirty(follow);
                if (PrefabUtility.IsPartOfPrefabInstance(follow))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(follow);
            }
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
        }

        // Called by OnPreprocessAnimation and explicitly by the installer,
        // including for existing .meta files imported before these scripts.
        internal static bool ConfigureClips(ModelImporter importer)
        {
            // A Motion-node override bypasses the clip Bake Into Pose settings.
            // Keep the authored Body bob/lean as pose curves, not extracted motion.
            bool changed = !string.IsNullOrEmpty(importer.motionNodeName);
            importer.motionNodeName = string.Empty;
            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults == null || defaults.Length == 0) return changed;
            ModelImporterClipAnimation[] current = importer.clipAnimations ?? Array.Empty<ModelImporterClipAnimation>();
            List<ModelImporterClipAnimation> result = current.Where(clip => KnownName(clip.name) == null).ToList();
            foreach (string name in ClipNames)
            {
                ModelImporterClipAnimation source = defaults.FirstOrDefault(clip => KnownName(clip.name) == name || KnownName(clip.takeName) == name);
                if (source == null) continue;
                ModelImporterClipAnimation clip = current.FirstOrDefault(item => KnownName(item.name) == name) ?? source;
                bool loop = name == "Idle" || name == "IdleRelaxed" || name.StartsWith("Run", StringComparison.Ordinal);
                if (!current.Contains(clip) || clip.name != name || clip.takeName != source.takeName
                    || !Mathf.Approximately(clip.firstFrame, source.firstFrame) || !Mathf.Approximately(clip.lastFrame, source.lastFrame)
                    || clip.loopTime != loop || clip.loopPose || !clip.lockRootHeightY || !clip.lockRootPositionXZ || !clip.lockRootRotation
                    || !clip.keepOriginalOrientation || !clip.keepOriginalPositionXZ || !clip.keepOriginalPositionY)
                    changed = true;
                clip.name = name;
                clip.takeName = source.takeName;
                clip.firstFrame = source.firstFrame;
                clip.lastFrame = source.lastFrame;
                clip.loopTime = loop;
                clip.loopPose = false; // The authored endpoints already match exactly.
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalPositionY = true;
                result.Add(clip);
            }
            if (result.Count != current.Length) changed = true;
            if (changed) importer.clipAnimations = result.ToArray();
            return changed;
        }

        private static string KnownName(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (string name in ClipNames)
                if (text.Equals(name, StringComparison.OrdinalIgnoreCase) || text.EndsWith("|" + name, StringComparison.OrdinalIgnoreCase))
                    return name;
            return null;
        }

        private static AnimationClip FindClip(IEnumerable<AnimationClip> clips, string name)
        {
            return clips.FirstOrDefault(clip => KnownName(clip.name) == name);
        }

        private static AnimatorController GetOrCreateController(Animator animator, AnimationClip[] clips, AnimationClip idle, AnimationClip run,
            AnimationClip idleRelaxed, AnimationClip runRelaxed, AnimationClip raise, AnimationClip lower, AnimationClip shoot)
        {
            // Validate the new hierarchy before touching a controller or mask.
            Transform[] hierarchy = animator.GetComponentsInChildren<Transform>(true);
            Transform[] torsos = hierarchy.Where(item => item.name == "Torso").ToArray();
            Transform[] armRoots = hierarchy.Where(item => item.name == "R_Upperarm" || item.name == "L_Upperarm").ToArray();
            if (torsos.Length != 1 || torsos[0].parent == null || torsos[0].parent.name != "Body"
                || armRoots.Count(item => item.name == "R_Upperarm") != 1
                || armRoots.Count(item => item.name == "L_Upperarm") != 1
                || armRoots.Any(item => !item.IsChildOf(torsos[0])))
                throw new InvalidOperationException("Нужен обновлённый FBX: Torso должен быть дочерним Body, а обе руки — дочерними Torso. Замени IntercomRobot.fbx новой версией и дождись импорта.");
            Transform torso = torsos[0];
            string[] legNames = { "R_Calf", "L_Calf", "R_Foot", "L_Foot", "R_Toe", "L_Toe" };
            if (hierarchy.Any(item => legNames.Contains(item.name) && item.IsChildOf(torso)))
                throw new InvalidOperationException("Кости ног и носков должны оставаться вне ветки Torso, иначе отдача будет двигать ноги. Проверь иерархию нового FBX.");
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
            {
                ValidateDirectionalController(existing);
                UpdateUpperBodyMask(existing, animator.transform, hierarchy, torso);
                return existing;
            }
            IntercomAssetCatalog.EnsureParentFolder(ControllerPath);

            // Copy the previous generated controller before upgrading motions.
            // Preserve its state settings, transitions and the original asset.
            AnimatorController legacy = AssetDatabase.LoadAssetAtPath<AnimatorController>(LegacyControllerPath);
            if (legacy != null)
            {
                ValidateLegacyLocomotion(legacy);
                if (!AssetDatabase.CopyAsset(LegacyControllerPath, ControllerPath))
                    throw new InvalidOperationException("Не удалось создать копию IntercomDirectional.controller.");
                existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                EnsureFloatParameter(existing, "MoveX", 0f);
                EnsureFloatParameter(existing, "MoveY", 1f);
                UpgradeLocomotion(existing.layers[0].stateMachine, existing, "Locomotion", clips, false);
                AnimatorStateMachine arms = existing.layers.First(layer => layer.name == "Arms").stateMachine;
                UpgradeLocomotion(arms, existing, "Relaxed", clips, true);
                UpgradeLocomotion(arms, existing, "Combat", clips, false);
                UpdateUpperBodyMask(existing, animator.transform, hierarchy, torso);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            EnsureFloatParameter(controller, "MoveX", 0f);
            EnsureFloatParameter(controller, "MoveY", 1f);
            controller.AddParameter(new AnimatorControllerParameter { name = "RunRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            AnimatorStateMachine bodyMachine = controller.layers[0].stateMachine;
            bodyMachine.defaultState = AddLocomotion(bodyMachine, controller, "Locomotion", idle, CreateDirectionalRun(controller, clips, false));

            AnimatorStateMachine armsMachine = new AnimatorStateMachine { name = "Arms" };
            AssetDatabase.AddObjectToAsset(armsMachine, controller);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Arms", defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override, stateMachine = armsMachine
            });
            UpdateUpperBodyMask(controller, animator.transform, hierarchy, torso);
            armsMachine.defaultState = AddLocomotion(armsMachine, controller, "Relaxed", idleRelaxed, CreateDirectionalRun(controller, clips, true));
            AddLocomotion(armsMachine, controller, "Combat", idle, CreateDirectionalRun(controller, clips, false));
            AddGesture(armsMachine, "RaiseWeapons", raise);
            AddGesture(armsMachine, "LowerWeapons", lower);
            AddGesture(armsMachine, "Shoot", shoot);
            // IntercomWeaponPose crossfades this layer. No AnyState transitions
            // compete with queued shots or reset the independent leg cycle.
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void UpdateUpperBodyMask(AnimatorController controller, Transform animatorRoot,
            Transform[] hierarchy, Transform torso)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = Array.FindIndex(layers, layer => layer.name == "Arms");
            if (layerIndex < 0)
                throw new InvalidOperationException("В контроллере нет слоя Arms. Верни имя слоя Arms: установщик сохраняет твои состояния и не пересоздаёт контроллер.");

            AvatarMask assigned = layers[layerIndex].avatarMask;
            // Only mutate our own embedded mask. An assigned custom mask asset
            // remains intact; switch this layer to a generated mask instead.
            AvatarMask mask = IsGeneratedMask(assigned, controller) ? assigned
                : AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller))
                    .OfType<AvatarMask>().FirstOrDefault(item => IsGeneratedMask(item, controller));
            if (mask == null)
            {
                mask = new AvatarMask { name = GeneratedMaskName };
                AssetDatabase.AddObjectToAsset(mask, controller);
            }
            bool changed = mask.name != GeneratedMaskName || mask.transformCount != hierarchy.Length;
            if (!changed)
            {
                for (int i = 0; i < hierarchy.Length; i++)
                {
                    string path = AnimationUtility.CalculateTransformPath(hierarchy[i], animatorRoot);
                    bool active = hierarchy[i] == torso || hierarchy[i].IsChildOf(torso);
                    if (mask.GetTransformPath(i) != path || mask.GetTransformActive(i) != active)
                    { changed = true; break; }
                }
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                if (mask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i)) changed = true;
            if (changed)
            {
                // Generic rigs use transform paths. Torso supplies recoil while
                // Body, both legs and toes keep the independent base gait.
                mask.name = GeneratedMaskName;
                mask.transformCount = hierarchy.Length;
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                    mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                for (int i = 0; i < hierarchy.Length; i++)
                {
                    mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(hierarchy[i], animatorRoot));
                    mask.SetTransformActive(i, hierarchy[i] == torso || hierarchy[i].IsChildOf(torso));
                }
                EditorUtility.SetDirty(mask);
            }
            if (assigned != mask)
            {
                layers[layerIndex].avatarMask = mask;
                controller.layers = layers;
                EditorUtility.SetDirty(controller);
            }
        }

        private static bool IsGeneratedMask(AvatarMask mask, AnimatorController controller)
        {
            return mask != null && AssetDatabase.GetAssetPath(mask) == AssetDatabase.GetAssetPath(controller)
                && (mask.name == GeneratedMaskName || mask.name == "Intercom Arms Only");
        }

        private static AnimatorState AddLocomotion(AnimatorStateMachine machine, AnimatorController controller,
            string name, AnimationClip idle, Motion run)
        {
            BlendTree blend = new BlendTree { name = name + " Idle Run", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(blend, controller);
            blend.AddChild(idle, 0f);
            blend.AddChild(run, 1f);
            AnimatorState state = machine.AddState(name);
            state.motion = blend;
            state.speedParameter = "RunRate";
            state.speedParameterActive = true;
            return state;
        }

        private static BlendTree CreateDirectionalRun(AnimatorController controller, AnimationClip[] clips, bool relaxed)
        {
            string suffix = relaxed ? "Relaxed" : string.Empty;
            BlendTree run = new BlendTree
            {
                name = relaxed ? "Intercom Relaxed Directional Run" : "Intercom Combat Directional Run",
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "MoveX", blendParameterY = "MoveY", useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(run, controller);
            run.AddChild(FindClip(clips, "Run" + suffix), new Vector2(0f, 1f));
            run.AddChild(FindClip(clips, "RunBackward" + suffix), new Vector2(0f, -1f));
            run.AddChild(FindClip(clips, "RunLeft" + suffix), new Vector2(-1f, 0f));
            run.AddChild(FindClip(clips, "RunRight" + suffix), new Vector2(1f, 0f));
            return run;
        }

        private static void EnsureFloatParameter(AnimatorController controller, string name, float initial)
        {
            AnimatorControllerParameter parameter = controller.parameters.FirstOrDefault(item => item.name == name);
            if (parameter != null)
            {
                if (parameter.type != AnimatorControllerParameterType.Float)
                    throw new InvalidOperationException("Параметр Animator " + name + " должен иметь тип Float.");
                return;
            }
            controller.AddParameter(new AnimatorControllerParameter
                { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = initial });
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            return machine.states.Select(item => item.state).FirstOrDefault(state => state.name == name);
        }

        private static BlendTree LegacyLocomotion(AnimatorStateMachine machine, string stateName, string expectedRun)
        {
            AnimatorState state = FindState(machine, stateName);
            BlendTree blend = state != null ? state.motion as BlendTree : null;
            if (blend == null || blend.name != stateName + " Idle Run"
                || blend.blendType != BlendTreeType.Simple1D || blend.blendParameter != "Speed"
                || blend.children.Length != 2 || !(blend.children[1].motion is AnimationClip clip)
                || KnownName(clip.name) != expectedRun)
                throw new InvalidOperationException("Состояние " + stateName
                    + " прежнего контроллера изменено вручную. Исходный контроллер сохранён. "
                    + "Для автоматической миграции верни его сгенерированное дерево Idle Run; "
                    + "либо сначала сделай копию своего контроллера и перенеси направления вручную.");
            return blend;
        }

        private static void ValidateLegacyLocomotion(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length == 0) throw new InvalidOperationException("В прежнем контроллере нет Base Layer.");
            AnimatorControllerLayer arms = layers.FirstOrDefault(layer => layer.name == "Arms");
            if (arms == null) throw new InvalidOperationException("В прежнем контроллере нет слоя Arms.");
            LegacyLocomotion(layers[0].stateMachine, "Locomotion", "Run");
            LegacyLocomotion(arms.stateMachine, "Relaxed", "RunRelaxed");
            LegacyLocomotion(arms.stateMachine, "Combat", "Run");
            foreach (string name in new[] { "MoveX", "MoveY" })
            {
                AnimatorControllerParameter parameter = controller.parameters.FirstOrDefault(item => item.name == name);
                if (parameter != null && parameter.type != AnimatorControllerParameterType.Float)
                    throw new InvalidOperationException("Параметр Animator " + name + " уже существует и должен иметь тип Float.");
            }
        }

        private static void UpgradeLocomotion(AnimatorStateMachine machine, AnimatorController controller,
            string stateName, AnimationClip[] clips, bool relaxed)
        {
            BlendTree outer = LegacyLocomotion(machine, stateName, relaxed ? "RunRelaxed" : "Run");
            ChildMotion[] children = outer.children;
            children[1].motion = CreateDirectionalRun(controller, clips, relaxed);
            outer.children = children;
            EditorUtility.SetDirty(outer);
        }

        private static void ValidateDirectionalController(AnimatorController controller)
        {
            // Repeat setup preserves authored motions. Validate the parameter
            // contract rather than replacing the user's edited blend trees.
            foreach (string name in new[] { "Speed", "RunRate", "MoveX", "MoveY" })
            {
                AnimatorControllerParameter parameter = controller.parameters.FirstOrDefault(item => item.name == name);
                if (parameter == null || parameter.type != AnimatorControllerParameterType.Float)
                    throw new InvalidOperationException("В IntercomDirectional.controller нужен Float-параметр " + name + ".");
            }
        }

        private static void AddGesture(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = clip;
            state.writeDefaultValues = true;
        }

        private static IntercomWeaponPoseProfile CompatibleDirectionalProfile(IntercomWeaponPoseProfile source,
            AnimatorController controller)
        {
            AnimatorOverrideController originalOverride = source.animatorOverride;
            if (originalOverride == null || originalOverride.runtimeAnimatorController == controller) return source;
            if (AssetDatabase.GetAssetPath(originalOverride.runtimeAnimatorController) != LegacyControllerPath)
                throw new InvalidOperationException("В профиле оружия назначен Override Controller с другой основой. "
                    + "Сохрани его копию и назначь IntercomDirectional.controller как основу. "
                    + "Исходный профиль установщик не изменяет.");

            // An old profile can otherwise silently restore the old controller
            // during WeaponPose.Start. Rebase copies, keeping the user's assets.
            string sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
            if (string.IsNullOrEmpty(sourceGuid))
                throw new InvalidOperationException("Перед миграцией сохрани профиль оружия как asset в проекте.");
            const string folder = AnimationFolder + "/DirectionalProfiles";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(AnimationFolder, "DirectionalProfiles");
            string profilePath = folder + "/" + sourceGuid + ".asset";
            IntercomWeaponPoseProfile cached = AssetDatabase.LoadAssetAtPath<IntercomWeaponPoseProfile>(profilePath);
            if (cached != null)
            {
                if (cached.animatorOverride != null && cached.animatorOverride.runtimeAnimatorController == controller)
                    return cached;
                throw new InvalidOperationException("Ранее созданная направленная копия профиля изменена. "
                    + "Проверь основу её Override Controller: нужен IntercomDirectional.controller.");
            }

            string overridePath = folder + "/" + sourceGuid + ".overrideController";
            AnimatorOverrideController rebased = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (rebased == null)
            {
                rebased = new AnimatorOverrideController(controller) { name = originalOverride.name + " Directional" };
                var previous = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                var replacements = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                originalOverride.GetOverrides(previous);
                rebased.GetOverrides(replacements);
                for (int i = 0; i < replacements.Count; i++)
                {
                    AnimationClip clip = replacements[i].Key;
                    KeyValuePair<AnimationClip, AnimationClip> match = previous.FirstOrDefault(pair => pair.Key == clip);
                    if (match.Key != null) replacements[i] = new KeyValuePair<AnimationClip, AnimationClip>(clip, match.Value);
                }
                rebased.ApplyOverrides(replacements);
                AssetDatabase.CreateAsset(rebased, overridePath);
            }
            else if (rebased.runtimeAnimatorController != controller)
                throw new InvalidOperationException("Основа направленной копии Override Controller изменена. Проверь " + overridePath);
            IntercomWeaponPoseProfile copy = ScriptableObject.CreateInstance<IntercomWeaponPoseProfile>();
            EditorUtility.CopySerialized(source, copy);
            copy.name = source.name + " Directional";
            copy.animatorOverride = rebased;
            AssetDatabase.CreateAsset(copy, profilePath);
            return copy;
        }

        private static IntercomWeaponPoseProfile GetOrCreateProfile(AnimationClip raise, AnimationClip lower, AnimationClip shoot)
        {
            IntercomWeaponPoseProfile profile = AssetDatabase.LoadAssetAtPath<IntercomWeaponPoseProfile>(ProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<IntercomWeaponPoseProfile>();
            profile.raiseWeapons = raise;
            profile.lowerWeapons = lower;
            profile.shoot = shoot;
            IntercomAssetCatalog.EnsureParentFolder(ProfilePath);
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static void FitCapsule(CharacterController capsule, GameObject visual)
        {
            Bounds bounds = IntercomMvpSceneBuilder.GetPosedBounds(visual);
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(capsule.transform.lossyScale.y));
            float scaleXZ = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.x), Mathf.Abs(capsule.transform.lossyScale.z)));
            float worldHeight = Mathf.Max(0.5f, bounds.size.y);
            capsule.height = worldHeight / scaleY;
            // A body capsule, not an arm-to-arm capsule: guns do not block doors.
            capsule.radius = worldHeight * 0.18f / scaleXZ;
            // The wrapper was centred on the full silhouette, including the
            // forward-pointing pistols. Its pivot is not the torso centre.
            Transform bodyMesh = visual.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "tripo_part_0_fixed");
            Vector3 bodyCenter;
            if (bodyMesh != null)
                bodyCenter = IntercomMvpSceneBuilder.GetPosedBounds(bodyMesh.gameObject).center;
            else
            {
                Transform bodyBone = visual.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(item => item.name == "Body");
                bodyCenter = bodyBone != null ? bodyBone.position : bounds.center;
            }
            capsule.center = capsule.transform.InverseTransformPoint(new Vector3(bodyCenter.x, bounds.center.y, bodyCenter.z));
            capsule.skinWidth = Mathf.Max(0.01f, capsule.radius * 0.1f);
            capsule.stepOffset = Mathf.Min(0.15f / scaleY, capsule.height * 0.2f);
            capsule.slopeLimit = 50f;
            capsule.minMoveDistance = 0f;
        }

        [MenuItem("Tools/CareProject/Fit Capsule To Idle Feet", false, 23)]
        private static void FitActiveCapsuleToIdleFeet()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                GameObject robot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Robot");
                CharacterController capsule = robot != null ? robot.GetComponent<CharacterController>() : null;
                Animator animator = robot != null ? robot.GetComponentInChildren<Animator>(true) : null;
                AnimationClip idle = FindClip(AssetDatabase.LoadAllAssetsAtPath(IntercomRobotAssets.ModelPath)
                    .OfType<AnimationClip>().ToArray(), "Idle");
                if (capsule == null || animator == null || idle == null)
                    throw new InvalidOperationException("Открой сцену IntercomMVP с Robot, CharacterController и клипом Idle.");
                AlignCapsuleToIdleFeet(capsule, animator, idle);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = robot;
                Debug.Log("Intercom MVP: нижняя точка капсулы совмещена с подошвами Idle. Размер капсулы и поза сохранены. Ctrl+S, затем проверь контакт стоп с землёй в Play Mode.", robot);
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Tools/CareProject/Fit Capsule To Idle Feet", true)]
        private static bool CanFitActiveCapsule() { return CanSetup(); }

        private static void AlignCapsuleToIdleFeet(CharacterController capsule, Animator animator, AnimationClip idle)
        {
            // Sample only to measure. Restore every local transform afterwards
            // so an editor-selected pose or prefab bone override is not baked.
            Transform[] transforms = animator.GetComponentsInChildren<Transform>(true);
            Vector3[] positions = transforms.Select(item => item.localPosition).ToArray();
            Quaternion[] rotations = transforms.Select(item => item.localRotation).ToArray();
            Vector3[] scales = transforms.Select(item => item.localScale).ToArray();
            Bounds bounds;
            try
            {
                idle.SampleAnimation(animator.gameObject, 0f);
                bounds = IntercomMvpSceneBuilder.GetPosedBounds(animator.gameObject);
            }
            finally
            {
                for (int i = 0; i < transforms.Length; i++)
                {
                    transforms[i].localPosition = positions[i];
                    transforms[i].localRotation = rotations[i];
                    transforms[i].localScale = scales[i];
                }
            }
            if (bounds.size.y <= 0.0001f)
                throw new InvalidOperationException("Не удалось измерить подошвы: у модели пустые границы.");
            Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            float soleY = capsule.transform.InverseTransformPoint(bottom).y;
            Vector3 center = capsule.center;
            center.y = soleY + capsule.height * 0.5f;
            Undo.RecordObject(capsule, "Align Intercom Capsule With Idle Feet");
            capsule.center = center;
            EditorUtility.SetDirty(capsule);
            if (PrefabUtility.IsPartOfPrefabInstance(capsule))
                PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);
        }

        private static Camera FindCamera(Scene scene)
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .OrderByDescending(camera => camera.CompareTag("MainCamera")).FirstOrDefault();
        }
    }

    internal sealed class IntercomLocomotionImporter : AssetPostprocessor
    {
        private void OnPreprocessAnimation()
        {
            if (string.Equals(assetPath, IntercomRobotAssets.ModelPath, StringComparison.Ordinal))
                IntercomMovementSetup.ConfigureClips((ModelImporter)assetImporter);
        }
    }
}
