using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    /// <summary>Explicit sandbox installation. Repeated invocation preserves existing authored tuning.</summary>
    internal static class IntercomNpcSetup
    {
        internal const string ArenaName = "MVP_NpcSandbox";
        internal const string DefinitionPath = "Assets/IntercomMVP/Game/Enemies/Udarnik/UdarnikCapsule.asset";
        private static readonly Vector3[] SpawnPositions =
            { new Vector3(-6f, 0f, -3f), new Vector3(3.5f, 0f, 1f), new Vector3(1f, 0f, 9f) };

        [MenuItem("Tools/Intercom MVP/NPC/Install Capsule Sandbox", false, 120)]
        private static void InstallCurrent()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != IntercomMvpSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open the saved IntercomMVP scene outside Play Mode before installing NPCs.");
            Install(scene);
        }

        internal static void Install(Scene scene)
        {
            GameObject robot = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Robot");
            GameObject environment = scene.GetRootGameObjects().FirstOrDefault(item => item.name == IntercomDistrictSetup.RootName);
            if (robot == null || environment == null)
                throw new InvalidOperationException("NPC sandbox requires the existing Robot and tutorial environment.");
            IntercomPlayerMotor motor = robot.GetComponent<IntercomPlayerMotor>();
            IntercomPlayerInput input = robot.GetComponent<IntercomPlayerInput>();
            IntercomSimpleShooter shooter = robot.GetComponent<IntercomSimpleShooter>();
            IntercomWeaponPose pose = robot.GetComponent<IntercomWeaponPose>();
            if (motor == null || input == null || shooter == null || pose == null)
                throw new InvalidOperationException("Complete the existing player setup before installing NPCs.");
            IntercomNpcDefinition definition = EnsureDefinition();

            IntercomHealth health = robot.GetComponent<IntercomHealth>();
            if (health == null)
            {
                health = Undo.AddComponent<IntercomHealth>(robot);
                health.maximumHealth = 100f;
                health.team = IntercomTeam.Player;
                health.postHitInvulnerability = .6f;
                EditorUtility.SetDirty(health);
            }
            IntercomPlayerVitality vitality = robot.GetComponent<IntercomPlayerVitality>();
            if (vitality == null)
            {
                vitality = Undo.AddComponent<IntercomPlayerVitality>(robot);
                vitality.health = health; vitality.motor = motor; vitality.input = input;
                vitality.shooter = shooter; vitality.weaponPose = pose;
                EditorUtility.SetDirty(vitality);
            }
            if (input.vitality == null)
            {
                Undo.RecordObject(input, "Connect player vitality");
                input.vitality = vitality; EditorUtility.SetDirty(input);
            }

            GameObject arena = scene.GetRootGameObjects().FirstOrDefault(item => item.name == ArenaName);
            if (arena == null)
            {
                arena = new GameObject(ArenaName);
                SceneManager.MoveGameObjectToScene(arena, scene);
                Undo.RegisterCreatedObjectUndo(arena, "Create NPC sandbox");
            }
            IntercomNpcNavigation navigation = arena.GetComponent<IntercomNpcNavigation>();
            if (navigation == null)
            {
                navigation = Undo.AddComponent<IntercomNpcNavigation>(arena);
                navigation.obstacleRoot = environment.transform;
                EditorUtility.SetDirty(navigation);
            }
            if (arena.GetComponent<IntercomNpcArena>() == null)
            {
                var markers = new Transform[SpawnPositions.Length];
                for (int i = 0; i < markers.Length; i++)
                {
                    string name = "NPC Spawn " + (i + 1).ToString("00");
                    markers[i] = arena.transform.Find(name);
                    if (markers[i] != null) continue;
                    GameObject point = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(point, "Create NPC spawn marker");
                    point.transform.SetParent(arena.transform, false);
                    point.transform.localPosition = SpawnPositions[i];
                    markers[i] = point.transform;
                }
                IntercomNpcArena bootstrap = Undo.AddComponent<IntercomNpcArena>(arena);
                bootstrap.definition = definition; bootstrap.navigation = navigation;
                bootstrap.playerHealth = health; bootstrap.playerMotor = motor;
                bootstrap.spawnPoints = markers; bootstrap.maximumActors = 3;
                EditorUtility.SetDirty(bootstrap);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static IntercomNpcDefinition EnsureDefinition()
        {
            IntercomNpcDefinition existing = AssetDatabase.LoadAssetAtPath<IntercomNpcDefinition>(DefinitionPath);
            if (existing != null) return existing;
            IntercomAssetCatalog.EnsureParentFolder(DefinitionPath);
            IntercomNpcDefinition definition = ScriptableObject.CreateInstance<IntercomNpcDefinition>();
            definition.name = "UdarnikCapsule";
            definition.bodyMaterial = EnsureMaterial("UdarnikBody", new Color(.72f, .68f, .55f), false);
            definition.detailMaterial = EnsureMaterial("UdarnikDetail", new Color(.12f, .19f, .20f), false);
            definition.telegraphMaterial = EnsureMaterial("UdarnikTelegraph", new Color(.95f, .67f, .22f, .38f), true);
            AssetDatabase.CreateAsset(definition, DefinitionPath);
            return definition;
        }

        private static Material EnsureMaterial(string name, Color color, bool transparent)
        {
            string path = "Assets/IntercomMVP/Game/Enemies/Udarnik/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find(transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("NPC material requires the existing URP shaders.");
            material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f); material.SetFloat("_Cull", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
