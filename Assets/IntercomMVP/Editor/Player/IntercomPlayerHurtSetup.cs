using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    /// <summary>Connect the existing hero without replacing authored reaction settings.</summary>
    internal static class IntercomPlayerHurtSetup
    {
        internal static void Setup(GameObject robot)
        {
            if (robot == null) return;
            IntercomHealth health = robot.GetComponent<IntercomHealth>();
            if (health == null) return;
            IntercomPlayerHurtFeedback hurt = robot.GetComponent<IntercomPlayerHurtFeedback>();
            if (hurt == null) hurt = Undo.AddComponent<IntercomPlayerHurtFeedback>(robot);
            Undo.RecordObject(hurt, "Connect player damage reaction");
            if (hurt.health == null) hurt.health = health;
            if (hurt.motor == null) hurt.motor = robot.GetComponent<IntercomPlayerMotor>();
            if (hurt.vitality == null) hurt.vitality = robot.GetComponent<IntercomPlayerVitality>();
            if (hurt.hurtClips == null || hurt.hurtClips.Length == 0)
                hurt.hurtClips = new[] {
                    AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IntercomMVP/Content/Characters/Robot/Audio/ChassisHurt_01.wav"),
                    AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IntercomMVP/Content/Characters/Robot/Audio/ChassisHurt_02.wav")
                };
            EditorUtility.SetDirty(hurt);
            if (PrefabUtility.IsPartOfPrefabInstance(hurt))
                PrefabUtility.RecordPrefabInstancePropertyModifications(hurt);
        }
    }
}
