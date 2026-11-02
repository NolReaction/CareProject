using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    internal static class IntercomAudioSetup
    {
        private const string Folder = "Assets/IntercomMVP/Audio";
        private const string ProfilePath = Folder + "/PrototypeAudio.asset";

        internal static void Setup(GameObject robot)
        {
            IntercomPlayerAudio audio = robot.GetComponent<IntercomPlayerAudio>();
            if (audio == null) audio = Undo.AddComponent<IntercomPlayerAudio>(robot);
            Undo.RecordObject(audio, "Connect Intercom Audio");
            audio.motor = robot.GetComponent<IntercomPlayerMotor>();
            audio.shooter = robot.GetComponent<IntercomSimpleShooter>();
            audio.animator = robot.GetComponentInChildren<Animator>(true);
            audio.groundFeedback = robot.GetComponent<IntercomGroundFeedback>();
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
            profile.concreteSteps = Enumerable.Range(1, 3).Select(index => Clip("BootStep_Concrete_" + index.ToString("00"))).ToArray();
            profile.metalSteps = Enumerable.Range(1, 3).Select(index => Clip("BootStep_Metal_" + index.ToString("00"))).ToArray();
            profile.gravelSteps = Enumerable.Range(1, 3).Select(index => Clip("BootStep_Gravel_" + index.ToString("00"))).ToArray();
            profile.woodSteps = Enumerable.Range(1, 3).Select(index => Clip("BootStep_Wood_" + index.ToString("00"))).ToArray();
            profile.soilSteps = Enumerable.Range(1, 3).Select(index => Clip("BootStep_Soil_" + index.ToString("00"))).ToArray();
            profile.impactHits = Enumerable.Range(1, 2).Select(index => Clip("Impact_" + index.ToString("00"))).ToArray();
            profile.metalImpacts = Enumerable.Range(1, 3).Select(index => Clip("Impact_Metal_" + index.ToString("00"))).ToArray();
            profile.woodImpacts = Enumerable.Range(1, 3).Select(index => Clip("Impact_Wood_" + index.ToString("00"))).ToArray();
            profile.concreteImpacts = Enumerable.Range(1, 3).Select(index => Clip("Impact_Concrete_" + index.ToString("00"))).ToArray();
            profile.soilImpacts = Enumerable.Range(1, 3).Select(index => Clip("Impact_Soil_" + index.ToString("00"))).ToArray();
            profile.ambience = Clip("Ambience_IndustrialYard");
            profile.musicLoop = Clip("Music_IndustrialGroove", ".ogg");
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static AudioClip Clip(string name, string extension = ".wav")
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + name + extension);
            if (clip == null) Debug.LogWarning("Intercom MVP: не найден звуковой файл " + name + extension + ".");
            return clip;
        }
    }
}
