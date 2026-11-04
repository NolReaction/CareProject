using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    internal static class IntercomPerformanceTools
    {
        internal static void Setup(GameObject robot, IntercomNpcNavigation navigation)
        {
            if (robot == null) return;
            var capture = robot.GetComponent<IntercomPerformanceCapture>();
            if (capture == null) capture = Undo.AddComponent<IntercomPerformanceCapture>(robot);
            Undo.RecordObject(capture, "Connect frame diagnostics");
            if (capture.navigation == null) capture.navigation = navigation;
            if (capture.mission == null && navigation != null) capture.mission = navigation.GetComponent<IntercomMissionDirector>();
            if (capture.effects == null) capture.effects = robot.GetComponent<IntercomCombatEffects>();
            var motor = robot.GetComponent<IntercomPlayerMotor>();
            if (capture.occlusion == null && motor != null && motor.movementCamera != null)
                capture.occlusion = motor.movementCamera.GetComponent<IntercomCameraOcclusion>();
            EditorUtility.SetDirty(capture);
        }
        [MenuItem("Tools/CareProject/Development/Performance/Open Last Capture")]
        private static void OpenCapture()
        {
            string path = IntercomPerformanceCapture.CapturePath;
            if (System.IO.File.Exists(path)) EditorUtility.RevealInFinder(path);
            else EditorUtility.DisplayDialog("CareProject", "Нажми Play, воспроизведи задержку и останови игру. Запись сохранится автоматически.", "OK");
        }
    }
}
