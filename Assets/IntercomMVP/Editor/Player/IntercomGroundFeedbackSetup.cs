using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    internal static class IntercomGroundFeedbackSetup
    {
        internal const string MaterialPath = "Assets/IntercomMVP/Content/Characters/Robot/Effects/Materials/MVP_SoftContact.mat";
        internal static void Setup(GameObject robot)
        {
            IntercomGroundFeedback feedback = robot.GetComponent<IntercomGroundFeedback>();
            if (feedback == null) feedback = Undo.AddComponent<IntercomGroundFeedback>(robot);
            Undo.RecordObject(feedback, "Connect Intercom Ground Feedback");
            if (Mathf.Approximately(feedback.runLeanDegrees, 2.5f)) feedback.runLeanDegrees = 5.5f;
            if (Mathf.Approximately(feedback.turnStepSeconds, 0.18f)) feedback.turnStepSeconds = 0.13f;
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
                if (feedback.body == null) feedback.body = bones.FirstOrDefault(item => item.name == "Body");
                if (feedback.leftCalf == null) feedback.leftCalf = bones.FirstOrDefault(item => item.name == "L_Calf");
                if (feedback.rightCalf == null) feedback.rightCalf = bones.FirstOrDefault(item => item.name == "R_Calf");
                if (feedback.leftToe == null) feedback.leftToe = bones.FirstOrDefault(item => item.name == "L_Toe");
                if (feedback.rightToe == null) feedback.rightToe = bones.FirstOrDefault(item => item.name == "R_Toe");
                if (feedback.leftUpperArm == null) feedback.leftUpperArm = bones.FirstOrDefault(item => item.name == "L_Upperarm");
                if (feedback.rightUpperArm == null) feedback.rightUpperArm = bones.FirstOrDefault(item => item.name == "R_Upperarm");
                if (feedback.NeedsSoleCalibration)
                    feedback.CalibrateSoles();
            }
            EditorUtility.SetDirty(feedback);
            if (PrefabUtility.IsPartOfPrefabInstance(feedback))
                PrefabUtility.RecordPrefabInstancePropertyModifications(feedback);
        }
    }
}
