using System;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    internal static class IntercomCombatSetup
    {
        private const string Folder = "Assets/IntercomMVP/Art/Materials";
        internal static void Setup(GameObject robot)
        {
            IntercomSimpleShooter shooter = robot.GetComponent<IntercomSimpleShooter>();
            if (shooter == null) return;
            Undo.RecordObject(shooter, "Tune Cartoon Projectiles");
            if (shooter.weapon == null) shooter.weapon = AssetDatabase.LoadAssetAtPath<IntercomWeaponDefinition>(
                "Assets/IntercomMVP/Game/Weapons/Pistols/DualPistols.asset");
            if (Mathf.Approximately(shooter.projectileSpeed, 16f)) shooter.projectileSpeed = 36f;
            if (Mathf.Approximately(shooter.projectileRadius, 0.025f)) shooter.projectileRadius = 0.035f;
            if (Mathf.Approximately(shooter.tracerLength, 0.18f) || Mathf.Approximately(shooter.tracerLength, 0.42f))
                shooter.tracerLength = 0.68f;
            if (shooter.tracerWidth < 0.03f) shooter.tracerWidth = 0.15f;
            Material flash = Material("MVP_Flash", new Color(1f, 0.96f, 0.64f));
            Material casing = Material("MVP_Casing", new Color(0.98f, 0.56f, 0.12f));
            Material impact = Material("MVP_Impact", new Color(1f, 0.69f, 0.23f));
            Material wood = Material("MVP_WoodImpact", new Color(0.55f, 0.30f, 0.11f));
            Material stone = Material("MVP_StoneImpact", new Color(0.62f, 0.60f, 0.52f));
            Material soil = Material("MVP_SoilImpact", new Color(0.35f, 0.27f, 0.16f));
            if (shooter.projectileCoreMaterial == null) shooter.projectileCoreMaterial = flash;
            EditorUtility.SetDirty(shooter);
            IntercomCombatEffects effects = robot.GetComponent<IntercomCombatEffects>();
            if (effects == null) effects = Undo.AddComponent<IntercomCombatEffects>(robot);
            Undo.RecordObject(effects, "Connect Cartoon Combat Effects");
            effects.shooter = shooter;
            if (effects.flashMaterial == null) effects.flashMaterial = flash;
            if (effects.casingMaterial == null) effects.casingMaterial = casing;
            if (effects.impactMaterial == null) effects.impactMaterial = impact;
            if (effects.woodImpactMaterial == null) effects.woodImpactMaterial = wood;
            if (effects.stoneImpactMaterial == null) effects.stoneImpactMaterial = stone;
            if (effects.soilImpactMaterial == null) effects.soilImpactMaterial = soil;
            EditorUtility.SetDirty(effects);
            if (PrefabUtility.IsPartOfPrefabInstance(shooter)) PrefabUtility.RecordPrefabInstancePropertyModifications(shooter);
            if (PrefabUtility.IsPartOfPrefabInstance(effects)) PrefabUtility.RecordPrefabInstancePropertyModifications(effects);
        }

        private static Material Material(string name, Color color)
        {
            string path = IntercomAssetCatalog.Resolve(Folder + "/" + name + ".mat");
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
            IntercomAssetCatalog.EnsureParentFolder(path);
            AssetDatabase.CreateAsset(result, path);
            return result;
        }
    }
}
