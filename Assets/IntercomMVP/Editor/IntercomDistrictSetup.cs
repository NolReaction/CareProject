using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    internal static class IntercomDistrictSetup
    {
        internal const string RootName = "MVP_TestEnvironment";
        private const string LayoutPath = "Assets/IntercomMVP/World/DistrictLayout.json";
        private const string MaterialFolder = "Assets/IntercomMVP/Art/PrototypeEnvironment/";
        [Serializable] private sealed class Layout { public int version; public float width, depth; public Group[] groups; public Target[] targets; }
        [Serializable] private sealed class Group { public string key, name, role; public float[] position, footprint; public bool hide; public Block[] blocks; }
        [Serializable] private sealed class Block { public string name, material, shape, footstepSurface; public float[] position, size; public float yaw; public bool collider, shadow; }
        [Serializable] private sealed class Target { public string key, name, material; public float[] position; }
        internal static void Setup(Scene scene)
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == RootName);
            if (root != null)
            {
                SetupSurfaceSounds(scene);
                return;
            }
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (source == null) throw new InvalidOperationException("Не найден World/DistrictLayout.json. Дождись импорта проекта.");
            Layout layout = JsonUtility.FromJson<Layout>(source.text);
            // Validate all material dependencies before creating a new district.
            foreach (string name in layout.groups.SelectMany(g => g.blocks).Select(b => b.material)
                .Concat(layout.targets.Select(t => t.material)).Concat(new[] { "Trim", "Door", "Backdrop" }).Distinct()) Material(name);
            root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create district root");
            foreach (Group spec in layout.groups)
            {
                GameObject group = Child(root.transform, spec.name, Vector(spec.position));
                Anchor(group, spec.key, spec.role, new Vector2(spec.footprint[0], spec.footprint[1]));
                group.AddComponent<IntercomOccluder>().hideWhenBlocking = spec.hide;
                foreach (Block block in spec.blocks)
                {
                    GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    part.name = block.name;
                    part.transform.SetParent(group.transform, false);
                    part.transform.localPosition = Vector(block.position);
                    part.transform.localRotation = Quaternion.Euler(0f, block.yaw, 0f);
                    part.transform.localScale = Vector(block.size);
                    UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                    if (block.collider)
                    {
                        GameObject solid = Child(group.transform, "Collision - " + block.name, Vector(block.position));
                        solid.transform.localRotation = part.transform.localRotation;
                        solid.transform.localScale = part.transform.localScale;
                        solid.AddComponent<BoxCollider>();
                    }
                    Renderer renderer = part.GetComponent<Renderer>();
                    renderer.sharedMaterial = Material(block.material);
                    renderer.shadowCastingMode = block.shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    Undo.RegisterCreatedObjectUndo(part, "Create district block");
                }
            }
            GameObject targets = Child(root.transform, "17 - AFK test targets", Vector3.zero);
            targets.AddComponent<IntercomOccluder>().hideWhenBlocking = false;
            foreach (Target spec in layout.targets) CreateTarget(targets.transform, spec);
            GameObject ground = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Ground - Future Map");
            if (ground != null)
            {
                Undo.RecordObject(ground.transform, "Expand district ground");
                ground.transform.localScale = new Vector3(layout.width / 10f, 1f, layout.depth / 10f);
                Renderer renderer = ground.GetComponent<Renderer>();
                if (renderer != null) { Undo.RecordObject(renderer, "Colour district ground"); renderer.sharedMaterial = Material("Backdrop"); }
            }
            Light sun = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<Light>())
                .FirstOrDefault(item => item.type == LightType.Directional);
            if (sun != null)
            {
                Undo.RecordObject(sun, "Tune district light");
                sun.color = new Color(1f, 0.96f, 0.86f);
                sun.intensity = 1.25f; sun.shadowStrength = 0.72f; sun.shadows = LightShadows.Soft;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.70f, 0.78f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.59f, 0.59f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.39f, 0.34f);
            IntercomDistrictMarker marker = Undo.AddComponent<IntercomDistrictMarker>(root);
            marker.layoutVersion = layout.version; marker.worldSize = new Vector2(layout.width, layout.depth);
            SetupSurfaceSounds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // Upgrade only named walkable patches once; retain existing buildings,
        // camera framing, object transforms and all later surface edits.
        internal static void SetupSurfaceSounds(Scene scene)
        {
            GameObject ground = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Ground - Future Map");
            if (ground != null && ground.GetComponent<IntercomFootstepSurface>() == null)
                Undo.AddComponent<IntercomFootstepSurface>(ground).kind = IntercomSurfaceKind.Asphalt;
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == RootName);
            IntercomDistrictMarker marker = root != null ? root.GetComponent<IntercomDistrictMarker>() : null;
            if (marker == null || marker.surfaceVersion >= 1) return;
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (source == null) throw new InvalidOperationException("Не найден World/DistrictLayout.json.");
            Layout layout = JsonUtility.FromJson<Layout>(source.text);
            foreach (Group groupSpec in layout.groups)
            {
                if (!groupSpec.blocks.Any(block => !string.IsNullOrEmpty(block.footstepSurface))) continue;
                Transform group = root.GetComponentsInChildren<IntercomWorldAnchor>(true)
                    .FirstOrDefault(anchor => anchor.anchorId == groupSpec.key)?.transform;
                if (group == null && groupSpec.key == "footstep_test")
                {
                    GameObject created = Child(root.transform, groupSpec.name, Vector(groupSpec.position));
                    Anchor(created, groupSpec.key, groupSpec.role, new Vector2(groupSpec.footprint[0], groupSpec.footprint[1]));
                    created.AddComponent<IntercomOccluder>().hideWhenBlocking = false;
                    group = created.transform;
                }
                if (group == null) continue;
                foreach (Block spec in groupSpec.blocks)
                {
                    if (string.IsNullOrEmpty(spec.footstepSurface)) continue;
                    if (!Enum.TryParse(spec.footstepSurface, out IntercomSurfaceKind kind))
                        throw new InvalidOperationException("Неизвестная поверхность: " + spec.footstepSurface);
                    Transform visual = group.Find(spec.name);
                    if (visual == null && groupSpec.key == "footstep_test")
                    {
                        GameObject created = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        created.name = spec.name; created.transform.SetParent(group, false);
                        created.transform.localPosition = Vector(spec.position);
                        created.transform.localScale = Vector(spec.size);
                        created.transform.localRotation = Quaternion.Euler(0f, spec.yaw, 0f);
                        UnityEngine.Object.DestroyImmediate(created.GetComponent<Collider>());
                        Renderer renderer = created.GetComponent<Renderer>();
                        renderer.sharedMaterial = Material(spec.material);
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        Undo.RegisterCreatedObjectUndo(created, "Create surface sound patch");
                        visual = created.transform;
                    }
                    if (visual == null) continue;
                    Transform solid = group.Find("Collision - " + spec.name);
                    Collider existingCollider = visual.GetComponent<Collider>();
                    if (solid == null && existingCollider != null && existingCollider.enabled && !existingCollider.isTrigger)
                        solid = visual;
                    if (solid == null)
                    {
                        solid = Child(group, "Collision - " + spec.name, visual.localPosition).transform;
                        solid.localRotation = visual.localRotation; solid.localScale = visual.localScale;
                        solid.gameObject.AddComponent<BoxCollider>();
                    }
                    if (solid.GetComponent<IntercomFootstepSurface>() == null)
                        Undo.AddComponent<IntercomFootstepSurface>(solid.gameObject).kind = kind;
                }
            }
            Undo.RecordObject(marker, "Upgrade surface sound tags");
            marker.surfaceVersion = 1; EditorUtility.SetDirty(marker);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void CreateTarget(Transform parent, Target spec)
        {
            GameObject root = Child(parent, spec.name, Vector(spec.position));
            Anchor(root, spec.key, "Target", new Vector2(0.7f, 0.7f));
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 0.8f; collider.radius = 0.35f; collider.height = 1.6f;
            GameObject visual = Child(root.transform, "Visual - replace capsule", Vector3.zero);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Capsule body"; body.transform.SetParent(visual.transform, false);
            body.transform.localPosition = Vector3.up * 0.8f; body.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            Renderer renderer = body.GetComponent<Renderer>(); renderer.sharedMaterial = Material(spec.material);
            Undo.RegisterCreatedObjectUndo(body, "Create AFK capsule");
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor"; visor.transform.SetParent(visual.transform, false);
            visor.transform.localPosition = new Vector3(0f, 1.05f, -0.327f); visor.transform.localScale = new Vector3(0.4f, 0.15f, 0.035f);
            UnityEngine.Object.DestroyImmediate(visor.GetComponent<Collider>()); visor.GetComponent<Renderer>().sharedMaterial = Material("Door");
            Undo.RegisterCreatedObjectUndo(visor, "Create AFK visor");
            IntercomTargetDummy target = root.AddComponent<IntercomTargetDummy>();
            target.visual = visual.transform; target.bodyRenderer = renderer; target.maxHealth = 5; target.resetSeconds = 2.5f;
        }

        private static GameObject Child(Transform parent, string name, Vector3 position)
        {
            GameObject item = new GameObject(name); item.transform.SetParent(parent, false); item.transform.localPosition = position;
            Undo.RegisterCreatedObjectUndo(item, "Create district group"); return item;
        }
        private static Vector3 Vector(float[] values) => new Vector3(values[0], values[1], values[2]);
        private static Material Material(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat");
            if (material == null) throw new InvalidOperationException("Не найден материал района: " + name); return material;
        }
        private static void Anchor(GameObject item, string key, string role, Vector2 size)
        {
            IntercomWorldAnchor anchor = item.AddComponent<IntercomWorldAnchor>();
            anchor.anchorId = key; anchor.role = (IntercomAnchorRole)Enum.Parse(typeof(IntercomAnchorRole), role); anchor.footprint = size;
        }
    }
}
