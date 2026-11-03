using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    internal static class IntercomCharacterAnimationSetup
    {
        [MenuItem("Tools/Intercom MVP/Setup Character Gestures", false, 26)]
        private static void SetupActiveScene()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                GameObject robot = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Robot");
                if (robot == null) throw new InvalidOperationException("В активной сцене не найден Robot.");
                Setup(robot, robot.GetComponentInChildren<Animator>(true));
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = robot;
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Tools/Intercom MVP/Setup Character Gestures", true)]
        private static bool CanSetup() => !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        internal static void Setup(GameObject robot, Animator animator)
        {
            if (robot == null || animator == null)
                throw new ArgumentNullException("Robot and Animator are required for character gestures.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Настрой жесты вне Play Mode.");
            IntercomCharacterAnimation gestures = robot.GetComponent<IntercomCharacterAnimation>();
            if (gestures == null) gestures = Undo.AddComponent<IntercomCharacterAnimation>(robot);
            Undo.RecordObject(gestures, "Connect Intercom Character Gestures");
            gestures.animator = animator;
            gestures.motor = robot.GetComponent<IntercomPlayerMotor>();
            gestures.weaponPose = robot.GetComponent<IntercomWeaponPose>();
            gestures.magazine = robot.GetComponent<IntercomWeaponMagazine>();
            gestures.inputSource = robot.GetComponent<IntercomPlayerInput>();
            gestures.ResolveReferences();
            gestures.enabled = true;
            EditorUtility.SetDirty(gestures);
            IntercomPlayerHurtSetup.Setup(robot);
            if (PrefabUtility.IsPartOfPrefabInstance(gestures))
                PrefabUtility.RecordPrefabInstancePropertyModifications(gestures);
        }
    }
}
