using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    internal static class IntercomGroundFeedbackSetup
    {
        internal const string MaterialPath = "Assets/IntercomMVP/Art/Materials/MVP_SoftContact.mat";
        internal static void Setup(GameObject robot)
        {
            IntercomGroundFeedback feedback = robot.GetComponent<IntercomGroundFeedback>();
            if (feedback == null) feedback = Undo.AddComponent<IntercomGroundFeedback>(robot);
            Undo.RecordObject(feedback, "Connect Intercom Ground Feedback");
            feedback.motor = robot.GetComponent<IntercomPlayerMotor>();
            feedback.audioSource = robot.GetComponent<IntercomPlayerAudio>();
            feedback.shooter = robot.GetComponent<IntercomSimpleShooter>();
            feedback.animator = robot.GetComponentInChildren<Animator>(true);
            if (feedback.contactMaterial == null)
                feedback.contactMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (feedback.animator != null)
            {
                Transform[] bones = feedback.animator.GetComponentsInChildren<Transform>(true);
                if (feedback.leftFoot == null) feedback.leftFoot = bones.FirstOrDefault(item => item.name == "L_Foot");
                if (feedback.rightFoot == null) feedback.rightFoot = bones.FirstOrDefault(item => item.name == "R_Foot");
                if (feedback.torso == null) feedback.torso = bones.FirstOrDefault(item => item.name == "Torso");
                if (!feedback.soleOffsetsCalibrated)
                {
                    // Measure Idle without committing its animated pose to the scene.
                    AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(IntercomRobotAssets.ModelPath)
                        .OfType<AnimationClip>().FirstOrDefault(item => item.name == "Idle");
                    Vector3[] positions = bones.Select(item => item.localPosition).ToArray();
                    Quaternion[] rotations = bones.Select(item => item.localRotation).ToArray();
                    Vector3[] scales = bones.Select(item => item.localScale).ToArray();
                    try
                    {
                        if (idle != null) idle.SampleAnimation(feedback.animator.gameObject, 0f);
                        feedback.CalibrateSoles();
                    }
                    finally
                    {
                        for (int i = 0; i < bones.Length; i++)
                        {
                            bones[i].localPosition = positions[i];
                            bones[i].localRotation = rotations[i];
                            bones[i].localScale = scales[i];
                        }
                    }
                }
            }
            EditorUtility.SetDirty(feedback);
            if (PrefabUtility.IsPartOfPrefabInstance(feedback))
                PrefabUtility.RecordPrefabInstancePropertyModifications(feedback);
        }
    }
}
