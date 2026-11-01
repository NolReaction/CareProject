using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    internal static class IntercomCombatSetup
    {
        private const string Folder = "Assets/IntercomMVP/Art/Materials";
        [MenuItem("Tools/Intercom MVP/Setup Combat Feedback", false, 25)]
        private static void SetupActiveScene()
        {
            GameObject robot = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(item => item.name == "Robot");
            if (robot == null) { Debug.LogWarning("Intercom MVP: открой сцену с Robot."); return; }
            Setup(robot);
            EditorSceneManager.MarkSceneDirty(robot.scene); AssetDatabase.SaveAssets(); Selection.activeGameObject = robot;
        }
        [MenuItem("Tools/Intercom MVP/Setup Combat Feedback", true)]
        private static bool CanSetup() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        internal static void Setup(GameObject robot)
        {
            IntercomSimpleShooter shooter = robot.GetComponent<IntercomSimpleShooter>();
            if (shooter == null) return;
            Undo.RecordObject(shooter, "Tune Cartoon Projectiles");
            if (Mathf.Approximately(shooter.projectileSpeed, 16f)) shooter.projectileSpeed = 36f;
            if (Mathf.Approximately(shooter.projectileRadius, 0.025f)) shooter.projectileRadius = 0.035f;
            if (Mathf.Approximately(shooter.tracerLength, 0.18f)) shooter.tracerLength = 0.42f;
            Material flash = Material("MVP_Flash", new Color(1f, 0.96f, 0.64f));
            Material casing = Material("MVP_Casing", new Color(0.98f, 0.56f, 0.12f));
            Material impact = Material("MVP_Impact", new Color(1f, 0.69f, 0.23f));
            if (shooter.projectileCoreMaterial == null) shooter.projectileCoreMaterial = flash;
            EditorUtility.SetDirty(shooter);
            IntercomCombatEffects effects = robot.GetComponent<IntercomCombatEffects>();
            if (effects == null) effects = Undo.AddComponent<IntercomCombatEffects>(robot);
            Undo.RecordObject(effects, "Connect Cartoon Combat Effects");
            effects.shooter = shooter;
            if (effects.flashMaterial == null) effects.flashMaterial = flash;
            if (effects.casingMaterial == null) effects.casingMaterial = casing;
            if (effects.impactMaterial == null) effects.impactMaterial = impact;
            EditorUtility.SetDirty(effects);
            if (PrefabUtility.IsPartOfPrefabInstance(shooter)) PrefabUtility.RecordPrefabInstancePropertyModifications(shooter);
            if (PrefabUtility.IsPartOfPrefabInstance(effects)) PrefabUtility.RecordPrefabInstancePropertyModifications(effects);
        }

        private static Material Material(string name, Color color)
        {
            string path = Folder + "/" + name + ".mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result != null) return result;
            Shader shader = name == "MVP_Casing" ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("Не найден Unlit shader для эффектов.");
            result = new Material(shader) { name = name, enableInstancing = true };
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            if (result.HasProperty("_Color")) result.SetColor("_Color", color);
            if (name == "MVP_Casing")
            {
                if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", 0.4f);
                if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", 0.32f);
            }
            AssetDatabase.CreateAsset(result, path);
            return result;
        }
    }
}
