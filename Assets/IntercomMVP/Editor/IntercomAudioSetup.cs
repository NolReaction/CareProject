using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    internal static class IntercomAudioSetup
    {
        private const string Folder = "Assets/IntercomMVP/Audio";
        private const string ProfilePath = Folder + "/PrototypeAudio.asset";

        [MenuItem("Tools/Intercom MVP/Setup Prototype Audio", false, 24)]
        private static void SetupActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject robot = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Robot");
            if (robot == null)
            {
                Debug.LogWarning("Intercom MVP: открой сцену IntercomMVP с объектом Robot.");
                return;
            }
            Setup(robot);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = robot;
            Debug.Log("Intercom MVP: звуки подключены. Сохрани сцену (Ctrl+S). Громкость и клипы: Audio/PrototypeAudio.asset.", robot);
        }

        [MenuItem("Tools/Intercom MVP/Setup Prototype Audio", true)]
        private static bool CanSetup() => !EditorApplication.isCompiling && !EditorApplication.isUpdating
            && !EditorApplication.isPlayingOrWillChangePlaymode;

        internal static void Setup(GameObject robot)
        {
            IntercomPlayerAudio audio = robot.GetComponent<IntercomPlayerAudio>();
            if (audio == null) audio = Undo.AddComponent<IntercomPlayerAudio>(robot);
            Undo.RecordObject(audio, "Connect Intercom Audio");
            audio.motor = robot.GetComponent<IntercomPlayerMotor>();
            audio.shooter = robot.GetComponent<IntercomSimpleShooter>();
            audio.animator = robot.GetComponentInChildren<Animator>(true);
            if (audio.profile == null) audio.profile = GetOrCreateProfile();
            EditorUtility.SetDirty(audio);
            if (PrefabUtility.IsPartOfPrefabInstance(audio))
                PrefabUtility.RecordPrefabInstancePropertyModifications(audio);
        }

        private static IntercomAudioProfile GetOrCreateProfile()
        {
            IntercomAudioProfile profile = AssetDatabase.LoadAssetAtPath<IntercomAudioProfile>(ProfilePath);
            if (profile != null) return profile; // Preserve replaced clips and the user's mix.
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/IntercomMVP", "Audio");
            profile = ScriptableObject.CreateInstance<IntercomAudioProfile>();
            profile.pistolShots = Enumerable.Range(1, 3).Select(index => Clip("Pistol_" + index.ToString("00"))).ToArray();
            profile.footsteps = Enumerable.Range(1, 4).Select(index => Clip("BootStep_" + index.ToString("00"))).ToArray();
            profile.ambience = Clip("Ambience_IndustrialYard");
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static AudioClip Clip(string name)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + name + ".wav");
            if (clip == null) Debug.LogWarning("Intercom MVP: не найден звуковой файл " + name + ".wav.");
            return clip;
        }
    }
}
