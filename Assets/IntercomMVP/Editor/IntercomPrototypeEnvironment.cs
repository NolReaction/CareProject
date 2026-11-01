using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    /// <summary>Disposable greybox set. Generated once; user scene edits are kept on subsequent setup.</summary>
    internal static class IntercomPrototypeEnvironment
    {
        internal const string RootName = "MVP_TestEnvironment";
        private const string MaterialFolder = "Assets/IntercomMVP/Art/PrototypeEnvironment";
        [Serializable] private sealed class Layout { public MaterialSpec[] materials; public Block[] blocks; }
        [Serializable] private sealed class MaterialSpec { public string name; public float[] color; }
        [Serializable] private sealed class Block
        {
            public string cluster, name, material;
            public float[] position, size;
            public bool collider, shadow;
            public float yaw;
        }
        private const string LayoutJson = @"{""materials"":[{""name"":""Backdrop"",""color"":[0.24,0.28,0.29,1]},{""name"":""Asphalt"",""color"":[0.2,0.24,0.26,1]},{""name"":""Paint"",""color"":[0.84,0.7,0.38,1]},{""name"":""Concrete"",""color"":[0.47,0.52,0.51,1]},{""name"":""BlueGrey"",""color"":[0.36,0.48,0.54,1]},{""name"":""Roof"",""color"":[0.24,0.32,0.36,1]},{""name"":""Door"",""color"":[0.14,0.19,0.21,1]},{""name"":""Orange"",""color"":[0.66,0.39,0.25,1]},{""name"":""Crate"",""color"":[0.37,0.46,0.39,1]}],""blocks"":[{""cluster"":""Ground surround"",""name"":""Backdrop"",""position"":[0,-0.045,0],""size"":[64,0.04,64],""material"":""Backdrop"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Ground markings"",""name"":""Service lane"",""position"":[0,0.00075,0],""size"":[4.2,0.0005,22],""material"":""Asphalt"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Ground markings"",""name"":""Cross lane"",""position"":[0,0.00175,0],""size"":[21,0.0005,3.2],""material"":""Asphalt"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Ground markings"",""name"":""Lane dash 1"",""position"":[0,0.00275,-8],""size"":[0.08,0.0005,1.2],""material"":""Paint"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Ground markings"",""name"":""Lane dash 2"",""position"":[0,0.00275,-4],""size"":[0.08,0.0005,1.2],""material"":""Paint"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Ground markings"",""name"":""Lane dash 3"",""position"":[0,0.00275,0],""size"":[0.08,0.0005,1.2],""material"":""Paint"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Ground markings"",""name"":""Lane dash 4"",""position"":[0,0.00275,4],""size"":[0.08,0.0005,1.2],""material"":""Paint"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Ground markings"",""name"":""Lane dash 5"",""position"":[0,0.00275,8],""size"":[0.08,0.0005,1.2],""material"":""Paint"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Ground markings"",""name"":""Parking pad"",""position"":[6,0.00075,-4],""size"":[7,0.0005,3.8],""material"":""Asphalt"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Ground markings"",""name"":""Parking line 1"",""position"":[4,0.00225,-4],""size"":[0.07,0.0005,2.8],""material"":""Paint"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Ground markings"",""name"":""Parking line 2"",""position"":[6,0.00225,-4],""size"":[0.07,0.0005,2.8],""material"":""Paint"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Ground markings"",""name"":""Parking line 3"",""position"":[8,0.00225,-4],""size"":[0.07,0.0005,2.8],""material"":""Paint"",""collider"":false,""yaw"":0,""shadow"":false},{""cluster"":""Placeholder building A"",""name"":""Main volume"",""position"":[-8,1.2,7],""size"":[4,2.4,3.2],""material"":""BlueGrey"",""collider"":true,""yaw"":0,""shadow"":true},{""cluster"":""Placeholder building A"",""name"":""Roof cap"",""position"":[-8,2.465,7],""size"":[4.22,0.13,3.4200000000000004],""material"":""Roof"",""collider"":false,""yaw"":0,""shadow"":true},{""cluster"":""Placeholder building A"",""name"":""Door inset"",""position"":[-8,0.54,5.3740000000000006],""size"":[0.75,1.08,0.035],""material"":""Door"",""collider"":false,""yaw"":0,""shadow"":true},{""cluster"":""Placeholder building B"",""name"":""Main volume"",""position"":[7.7,0.85,8],""size"":[4.5,1.7,2.8],""material"":""Concrete"",""collider"":true,""yaw"":0,""shadow"":true},{""cluster"":""Placeholder building B"",""name"":""Roof cap"",""position"":[7.7,1.765,8],""size"":[4.72,0.13,3.02],""material"":""Roof"",""collider"":false,""yaw"":0,""shadow"":true},{""cluster"":""Placeholder building B"",""name"":""Door inset"",""position"":[7.7,0.54,6.574],""size"":[0.75,1.08,0.035],""material"":""Door"",""collider"":false,""yaw"":0,""shadow"":true},{""cluster"":""Placeholder utility block"",""name"":""Main volume"",""position"":[-9,0.675,-0.8],""size"":[2.8,1.35,3],""material"":""BlueGrey"",""collider"":true,""yaw"":0,""shadow"":true},{""cluster"":""Placeholder utility block"",""name"":""Roof cap"",""position"":[-9,1.415,-0.8],""size"":[3.02,0.13,3.22],""material"":""Roof"",""collider"":false,""yaw"":0,""shadow"":true},{""cluster"":""Placeholder utility block"",""name"":""Door inset"",""position"":[-9,0.54,-2.3259999999999996],""size"":[0.75,1.08,0.035],""material"":""Door"",""collider"":false,""yaw"":0,""shadow"":true},{""cluster"":""Orange crate"",""name"":""Crate"",""position"":[-4.5,0.48,-3.5],""size"":[0.95,0.96,0.95],""material"":""Orange"",""collider"":true,""yaw"":0,""shadow"":true},{""cluster"":""Orange crate"",""name"":""Top brace"",""position"":[-4.5,0.975,-3.5],""size"":[1,0.035,0.13],""material"":""Roof"",""collider"":false,""yaw"":0,""shadow"":true},{""cluster"":""Green crate"",""name"":""Crate"",""position"":[5,0.36,2.5],""size"":[0.8,0.72,0.8],""material"":""Crate"",""collider"":true,""yaw"":12,""shadow"":true},{""cluster"":""Green crate"",""name"":""Top brace"",""position"":[5,0.735,2.5],""size"":[0.85,0.035,0.12],""material"":""Roof"",""collider"":false,""yaw"":12,""shadow"":true},{""cluster"":""Barrier A"",""name"":""Concrete barrier"",""position"":[3.2,0.375,-6],""size"":[2.2,0.75,0.45],""material"":""Concrete"",""collider"":true,""yaw"":0,""shadow"":true},{""cluster"":""Barrier A"",""name"":""Painted cap"",""position"":[3.2,0.77,-6],""size"":[2.2,0.04,0.47],""material"":""Paint"",""collider"":false,""yaw"":0,""shadow"":true},{""cluster"":""Barrier B"",""name"":""Concrete barrier"",""position"":[-4,0.375,3.3],""size"":[2.2,0.75,0.45],""material"":""Concrete"",""collider"":true,""yaw"":10,""shadow"":true},{""cluster"":""Barrier B"",""name"":""Painted cap"",""position"":[-4,0.77,3.3],""size"":[2.2,0.04,0.47],""material"":""Paint"",""collider"":false,""yaw"":10,""shadow"":true},{""cluster"":""Arena boundary"",""name"":""North low wall"",""position"":[0,0.225,11.75],""size"":[23.75,0.45,0.25],""material"":""Concrete"",""collider"":true,""yaw"":0,""shadow"":true},{""cluster"":""Arena boundary"",""name"":""South low wall"",""position"":[0,0.225,-11.75],""size"":[23.75,0.45,0.25],""material"":""Concrete"",""collider"":true,""yaw"":0,""shadow"":true},{""cluster"":""Arena boundary"",""name"":""West low wall"",""position"":[-11.75,0.225,0],""size"":[0.25,0.45,23.75],""material"":""Concrete"",""collider"":true,""yaw"":0,""shadow"":true},{""cluster"":""Arena boundary"",""name"":""East low wall"",""position"":[11.75,0.225,0],""size"":[0.25,0.45,23.75],""material"":""Concrete"",""collider"":true,""yaw"":0,""shadow"":true}]}";

        internal static void Ensure(Scene scene, Bounds groundBounds, Transform ground)
        {
            if (scene.GetRootGameObjects().Any(item => item.name == RootName)) return;
            Layout layout = JsonUtility.FromJson<Layout>(LayoutJson);
            EnsureFolder(MaterialFolder);
            Dictionary<string, Material> materials = layout.materials.ToDictionary(item => item.name, GetMaterial);
            float scaleX = Mathf.Max(0.1f, groundBounds.size.x / 24f);
            float scaleZ = Mathf.Max(0.1f, groundBounds.size.z / 24f);
            Vector3 origin = new Vector3(groundBounds.center.x, groundBounds.max.y, groundBounds.center.z);
            Physics.SyncTransforms();
            Collider[] existing = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<Collider>(true))
                .Where(item => item.enabled && item.gameObject.activeInHierarchy && !item.isTrigger
                    && (ground == null || (item.transform != ground && !item.transform.IsChildOf(ground)))) .ToArray();
            // Do not spawn a solid block inside the existing player or authored colliders.
            HashSet<string> skip = new HashSet<string>();
            foreach (Block block in layout.blocks.Where(item => item.collider))
            {
                Vector3 position = Position(block, origin, scaleX, scaleZ);
                Vector3 size = Size(block, scaleX, scaleZ);
                float a = block.yaw * Mathf.Deg2Rad;
                Vector3 aabb = new Vector3(Mathf.Abs(Mathf.Cos(a)) * size.x + Mathf.Abs(Mathf.Sin(a)) * size.z,
                    size.y, Mathf.Abs(Mathf.Sin(a)) * size.x + Mathf.Abs(Mathf.Cos(a)) * size.z);
                Bounds candidate = new Bounds(position, aabb);
                candidate.Expand(0.12f);
                if (existing.Any(collider => collider.bounds.Intersects(candidate))) skip.Add(block.cluster);
            }
            GameObject root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create MVP test environment");
            Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
            foreach (Block block in layout.blocks)
            {
                if (skip.Contains(block.cluster)) continue;
                if (!groups.TryGetValue(block.cluster, out Transform group))
                {
                    GameObject item = new GameObject(block.cluster);
                    item.transform.SetParent(root.transform, false);
                    Undo.RegisterCreatedObjectUndo(item, "Create MVP environment group");
                    group = item.transform; groups.Add(block.cluster, group);
                }
                GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = block.name;
                part.transform.SetParent(group, false);
                part.transform.position = Position(block, origin, scaleX, scaleZ);
                part.transform.rotation = Quaternion.Euler(0f, block.yaw, 0f);
                part.transform.localScale = Size(block, scaleX, scaleZ);
                if (!block.collider) UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                Renderer renderer = part.GetComponent<Renderer>();
                renderer.sharedMaterial = materials[block.material];
                renderer.shadowCastingMode = block.shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = block.material != "Backdrop";
                GameObjectUtility.SetStaticEditorFlags(part, StaticEditorFlags.BatchingStatic);
                Undo.RegisterCreatedObjectUndo(part, "Create MVP environment part");
            }
            if (skip.Count > 0)
                Debug.Log("Intercom MVP: пропущены тестовые объекты, пересекающиеся с существующими коллайдерами: " + string.Join(", ", skip));
        }

        private static Vector3 Position(Block block, Vector3 origin, float sx, float sz)
        {
            return origin + new Vector3(block.position[0] * sx, block.position[1], block.position[2] * sz);
        }
        private static Vector3 Size(Block block, float sx, float sz)
        {
            return new Vector3(block.size[0] * sx, block.size[1], block.size[2] * sz);
        }
        private static Material GetMaterial(MaterialSpec spec)
        {
            string path = MaterialFolder + "/" + spec.name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Дождись импорта Universal Render Pipeline, затем повтори Setup Mobile Prototype.");
            Material material = new Material(shader) { name = "MVP " + spec.name };
            material.SetColor("_BaseColor", new Color(spec.color[0], spec.color[1], spec.color[2], spec.color[3]));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }
    }
}
