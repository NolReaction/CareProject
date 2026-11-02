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
        private const string MeshFolder = MaterialFolder + "Modules/";
        [Serializable] private sealed class Layout { public int version, surfaceVersion; public float width, depth; public Group[] groups; public Target[] targets; }
        [Serializable] private sealed class Group { public string key, name, role; public float[] position, footprint; public bool hide; public Block[] blocks; }
        [Serializable] private sealed class Block { public string name, material, mesh, shape, footstepSurface; public string[] materials; public float[] position, size, collisionCenter, collisionSize; public float yaw; public bool collider, shadow; public bool visual = true; }
        [Serializable] private sealed class Target { public string key, name, material, mesh, footstepSurface; public float[] position; }
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
            foreach (string name in layout.groups.SelectMany(g => g.blocks).Select(b => b.mesh).Where(n => !string.IsNullOrEmpty(n)).Distinct()) Mesh(name);
            if (layout.targets.Any(t => !string.IsNullOrEmpty(t.mesh))) { Mesh("TrainingStand"); Mesh("TrainingTarget"); }
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
                    GameObject part = string.IsNullOrEmpty(block.mesh)
                        ? GameObject.CreatePrimitive(PrimitiveType.Cube) : new GameObject(block.name);
                    part.name = block.name;
                    part.transform.SetParent(group.transform, false);
                    part.transform.localPosition = Vector(block.position);
                    part.transform.localRotation = Quaternion.Euler(0f, block.yaw, 0f);
                    part.transform.localScale = Vector(block.size);
                    Collider primitiveCollider = part.GetComponent<Collider>();
                    if (primitiveCollider != null) UnityEngine.Object.DestroyImmediate(primitiveCollider);
                    if (!string.IsNullOrEmpty(block.mesh))
                    {
                        part.AddComponent<MeshFilter>().sharedMesh = Mesh(block.mesh);
                        part.AddComponent<MeshRenderer>();
                    }
                    if (block.collider)
                    {
                        GameObject solid = Child(group.transform, "Collision - " + block.name, Vector(block.position));
                        solid.transform.localRotation = part.transform.localRotation;
                        solid.transform.localScale = part.transform.localScale;
                        BoxCollider collider = solid.AddComponent<BoxCollider>();
                        if (block.collisionCenter != null && block.collisionCenter.Length == 3) collider.center = Vector(block.collisionCenter);
                        if (block.collisionSize != null && block.collisionSize.Length == 3) collider.size = Vector(block.collisionSize);
                        ConfigureSurface(solid, block.footstepSurface);
                    }
                    Renderer renderer = part.GetComponent<Renderer>();
                    if (!block.visual)
                    {
                        if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
                        MeshFilter filter = part.GetComponent<MeshFilter>();
                        if (filter != null) UnityEngine.Object.DestroyImmediate(filter);
                    }
                    else
                    {
                        renderer.sharedMaterials = block.materials != null && block.materials.Length > 0
                            ? block.materials.Select(Material).ToArray() : new[] { Material(block.material) };
                        renderer.shadowCastingMode = block.shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    }
                    Undo.RegisterCreatedObjectUndo(part, "Create district module");
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
                if (renderer != null) { Undo.RecordObject(renderer, "Colour district ground"); renderer.sharedMaterial = Material("Asphalt"); }
            }
            IntercomDistrictMarker marker = Undo.AddComponent<IntercomDistrictMarker>(root);
            marker.layoutVersion = layout.version; marker.surfaceVersion = layout.surfaceVersion; marker.worldSize = new Vector2(layout.width, layout.depth);
            SetupSurfaceSounds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // Add explicit collision materials once; retain existing buildings,
        // camera framing, object transforms and all later surface edits.
        internal static void SetupSurfaceSounds(Scene scene)
        {
            GameObject ground = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Ground - Future Map");
            if (ground != null && ground.GetComponent<IntercomFootstepSurface>() == null)
                Undo.AddComponent<IntercomFootstepSurface>(ground).kind = IntercomSurfaceKind.Asphalt;
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == RootName);
            IntercomDistrictMarker marker = root != null ? root.GetComponent<IntercomDistrictMarker>() : null;
            if (marker == null || marker.surfaceVersion >= 2) return;
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
                        BoxCollider collider = solid.gameObject.AddComponent<BoxCollider>();
                        if (spec.collisionCenter != null && spec.collisionCenter.Length == 3) collider.center = Vector(spec.collisionCenter);
                        if (spec.collisionSize != null && spec.collisionSize.Length == 3) collider.size = Vector(spec.collisionSize);
                    }
                    if (solid.GetComponent<IntercomFootstepSurface>() == null)
                        Undo.AddComponent<IntercomFootstepSurface>(solid.gameObject).kind = kind;
                }
            }
            Transform targets = root.transform.Find("12 - Training targets")
                ?? root.transform.Find("17 - AFK test targets");
            if (targets != null && layout.targets != null)
                foreach (Target spec in layout.targets)
                {
                    Transform target = targets.Find(spec.name);
                    if (target != null) ConfigureSurface(target.gameObject, spec.footstepSurface);
                }
            Undo.RecordObject(marker, "Upgrade surface sound tags");
            marker.surfaceVersion = 2; EditorUtility.SetDirty(marker);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void CreateTarget(Transform parent, Target spec)
        {
            GameObject root = Child(parent, spec.name, Vector(spec.position));
            Anchor(root, spec.key, "Target", new Vector2(0.7f, 0.7f));
            ConfigureSurface(root, spec.footstepSurface);
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 0.8f; collider.radius = 0.35f; collider.height = 1.6f;
            GameObject visual = Child(root.transform, "Visual - replace capsule", Vector3.zero);
            Renderer renderer;
            if (!string.IsNullOrEmpty(spec.mesh))
            {
                UnityEngine.Object.DestroyImmediate(collider);
                BoxCollider hitbox = root.AddComponent<BoxCollider>();
                hitbox.center = new Vector3(0f, 0.85f, 0f); hitbox.size = new Vector3(0.95f, 1.7f, 0.45f);
                GameObject stand = Child(visual.transform, "Target stand", Vector3.zero);
                stand.AddComponent<MeshFilter>().sharedMesh = Mesh("TrainingStand");
                stand.AddComponent<MeshRenderer>().sharedMaterials = new[] { Material("Roof"), Material("Trim") };
                GameObject face = Child(visual.transform, "Target face", Vector3.zero);
                face.AddComponent<MeshFilter>().sharedMesh = Mesh(spec.mesh);
                renderer = face.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { Material(spec.material), Material("Paint") };
            }
            else
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Capsule body"; body.transform.SetParent(visual.transform, false);
                body.transform.localPosition = Vector3.up * 0.8f; body.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
                UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
                renderer = body.GetComponent<Renderer>(); renderer.sharedMaterial = Material(spec.material);
                Undo.RegisterCreatedObjectUndo(body, "Create training capsule");
            }
            IntercomTargetDummy target = root.AddComponent<IntercomTargetDummy>();
            target.visual = visual.transform; target.bodyRenderer = renderer; target.maxHealth = 5; target.resetSeconds = 2.5f;
        }

        private static void ConfigureSurface(GameObject owner, string surface)
        {
            if (string.IsNullOrEmpty(surface) || owner.GetComponent<IntercomFootstepSurface>() != null) return;
            if (!Enum.TryParse(surface, out IntercomSurfaceKind kind) || !Enum.IsDefined(typeof(IntercomSurfaceKind), kind))
                throw new InvalidOperationException("Неизвестная поверхность: " + surface);
            Undo.AddComponent<IntercomFootstepSurface>(owner).kind = kind;
        }

        private static GameObject Child(Transform parent, string name, Vector3 position)
        {
            GameObject item = new GameObject(name); item.transform.SetParent(parent, false); item.transform.localPosition = position;
            Undo.RegisterCreatedObjectUndo(item, "Create district group"); return item;
        }
        private static Vector3 Vector(float[] values) => new Vector3(values[0], values[1], values[2]);
        private static Mesh Mesh(string name)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + name + ".mesh");
            if (mesh == null) throw new InvalidOperationException("Не найден модуль района: " + name);
            return mesh;
        }
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
