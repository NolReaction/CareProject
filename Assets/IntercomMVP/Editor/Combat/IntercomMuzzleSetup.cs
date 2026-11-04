using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    /// <summary>Creates editable muzzle markers from the imported pistol geometry, not guessed hand offsets.</summary>
    internal static class IntercomMuzzleSetup
    {
        private const string LegacyMaterialRoot = "Assets/IntercomMVP/Art/Materials";
        private static string MaterialPath => IntercomAssetCatalog.Resolve(LegacyMaterialRoot + "/MVP_Projectile.mat");

        private readonly struct MuzzlePose
        {
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public MuzzlePose(Vector3 position, Quaternion rotation)
            { this.position = position; this.rotation = rotation; }
        }

        internal static void Setup(GameObject robot, Animator animator, IntercomWeaponPose pose)
        {
            if (robot == null || animator == null || pose == null)
                throw new ArgumentNullException("Robot, Animator and Weapon Pose are required for muzzle setup.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Настраивай дульные точки вне Play Mode.");

            Transform rightPistol = FindUnique(animator.transform, "Pistol_R");
            Transform leftPistol = FindUnique(animator.transform, "Pistol_L");
            Transform right = rightPistol.Find("IntercomMuzzle_R");
            Transform left = leftPistol.Find("IntercomMuzzle_L");
            // Existing markers may have been hand-tuned. Never overwrite them on repeated setup.
            if (right == null || left == null)
            {
                Dictionary<string, MuzzlePose> reference = MeasureReferenceMuzzles();
                if (right == null) right = CreateMarker(rightPistol, "IntercomMuzzle_R", reference["R"]);
                if (left == null) left = CreateMarker(leftPistol, "IntercomMuzzle_L", reference["L"]);
            }

            IntercomSimpleShooter shooter = robot.GetComponent<IntercomSimpleShooter>();
            if (shooter == null) shooter = Undo.AddComponent<IntercomSimpleShooter>(robot);
            Undo.RecordObject(shooter, "Connect Intercom Muzzles");
            shooter.Initialize(pose, right, left, robot.transform);
            if (shooter.projectileMaterial == null) shooter.projectileMaterial = EnsureProjectileMaterial();
            shooter.enabled = true;
            EditorUtility.SetDirty(shooter);
            if (PrefabUtility.IsPartOfPrefabInstance(shooter))
                PrefabUtility.RecordPrefabInstancePropertyModifications(shooter);
        }

        private static Transform CreateMarker(Transform pistol, string name, MuzzlePose localPose)
        {
            GameObject marker = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(marker, "Create Intercom Muzzle");
            Undo.SetTransformParent(marker.transform, pistol, "Parent Intercom Muzzle");
            marker.transform.localPosition = localPose.position;
            marker.transform.localRotation = localPose.rotation;
            marker.transform.localScale = Vector3.one;
            return marker.transform;
        }

        private static Dictionary<string, MuzzlePose> MeasureReferenceMuzzles()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(IntercomRobotAssets.ModelPath);
            if (model == null) throw new InvalidOperationException("Не найден IntercomRobot.fbx для настройки дульных точек.");
            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(IntercomRobotAssets.ModelPath)
                .OfType<AnimationClip>().FirstOrDefault(clip => clip.name == "Idle"
                    || clip.name.EndsWith("|Idle", StringComparison.Ordinal));
            if (idle == null) throw new InvalidOperationException("Для настройки дульных точек нужен клип Idle в FBX.");

            // Sample only a temporary prefab in an isolated preview scene. The user's scene,
            // animated pose, prefab overrides and active Animation window are left unchanged.
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject reference = null;
            try
            {
                reference = PrefabUtility.InstantiatePrefab(model, previewScene) as GameObject;
                if (reference == null) throw new InvalidOperationException("Не удалось создать временную копию модели.");
                reference.hideFlags = HideFlags.HideAndDontSave;
                idle.SampleAnimation(reference, 0f);
                Vector3 expectedForward = -reference.transform.forward; // This FBX's authored facing axis.
                Vector3 referenceUp = reference.transform.up;
                var result = new Dictionary<string, MuzzlePose>();
                foreach (string side in new[] { "R", "L" })
                {
                    Transform pistol = FindUnique(reference.transform, "Pistol_" + side);
                    Vector3 point, direction;
                    MeasureFrontCap(pistol, expectedForward, out point, out direction);
                    Vector3 localDirection = pistol.InverseTransformDirection(direction).normalized;
                    Vector3 localUp = pistol.InverseTransformDirection(referenceUp).normalized;
                    if (Mathf.Abs(Vector3.Dot(localDirection, localUp)) > 0.98f)
                        localUp = Vector3.up;
                    result.Add(side, new MuzzlePose(pistol.InverseTransformPoint(point),
                        Quaternion.LookRotation(localDirection, localUp)));
                }
                return result;
            }
            finally
            {
                if (reference != null) UnityEngine.Object.DestroyImmediate(reference);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void MeasureFrontCap(Transform pistol, Vector3 expectedForward,
            out Vector3 point, out Vector3 direction)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (MeshFilter filter in pistol.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null) AppendMesh(filter.sharedMesh, filter.transform, vertices, triangles);
            }
            foreach (SkinnedMeshRenderer renderer in pistol.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh baked = new Mesh();
                try
                {
                    renderer.BakeMesh(baked, false);
                    AppendMesh(baked, renderer.transform, vertices, triangles);
                }
                finally { UnityEngine.Object.DestroyImmediate(baked); }
            }
            if (vertices.Count == 0 || triangles.Count == 0)
                throw new InvalidOperationException("У " + pistol.name + " не найдена геометрия пистолета.");

            expectedForward.Normalize();
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (Vector3 vertex in vertices)
            {
                float projection = Vector3.Dot(vertex, expectedForward);
                min = Mathf.Min(min, projection); max = Mathf.Max(max, projection);
            }
            float depth = max - min;
            if (depth < 0.0001f) throw new InvalidOperationException("Не удалось определить длину " + pistol.name + ".");
            // The projecting barrel rim occupies the first 2% on this model. A wider slab
            // also includes the lower receiver and incorrectly pulls the marker below the bore.
            float threshold = max - depth * 0.02f;
            Vector3 weightedCenter = Vector3.zero, weightedNormal = Vector3.zero;
            float totalArea = 0f;
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                if (Mathf.Min(Vector3.Dot(a, expectedForward),
                    Mathf.Min(Vector3.Dot(b, expectedForward), Vector3.Dot(c, expectedForward))) < threshold) continue;
                Vector3 cross = Vector3.Cross(b - a, c - a);
                float twiceArea = cross.magnitude;
                if (twiceArea < 1e-10f) continue;
                Vector3 normal = cross / twiceArea;
                float facing = Vector3.Dot(normal, expectedForward);
                if (Mathf.Abs(facing) < 0.75f) continue;
                if (facing < 0f) normal = -normal; // Robust to mirrored mesh winding.
                weightedCenter += ((a + b + c) / 3f) * twiceArea;
                weightedNormal += normal * twiceArea;
                totalArea += twiceArea;
            }
            if (totalArea <= 1e-10f || weightedNormal.sqrMagnitude <= 1e-12f)
                throw new InvalidOperationException("Не найден передний срез ствола " + pistol.name
                    + ". Для новой модели оружия добавь IntercomMuzzle_R/L вручную: синяя ось Z должна смотреть из ствола.");
            point = weightedCenter / totalArea;
            direction = weightedNormal.normalized;
            float front = float.NegativeInfinity;
            foreach (Vector3 vertex in vertices) front = Mathf.Max(front, Vector3.Dot(vertex, direction));
            // Put the marker just beyond the actual front surface, never in the grip's bounds centre.
            point += direction * (front - Vector3.Dot(point, direction) + depth * 0.006f);
        }

        private static void AppendMesh(Mesh mesh, Transform transform, List<Vector3> vertices, List<int> triangles)
        {
            int offset = vertices.Count;
            foreach (Vector3 vertex in mesh.vertices) vertices.Add(transform.TransformPoint(vertex));
            foreach (int index in mesh.triangles) triangles.Add(index + offset);
        }

        private static Transform FindUnique(Transform root, string name)
        {
            Transform[] matches = root.GetComponentsInChildren<Transform>(true).Where(item => item.name == name).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("В модели должна быть ровно одна точка " + name + ".");
            return matches[0];
        }

        private static Material EnsureProjectileMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("Не загружен шейдер для пуль. Дождись импорта URP и повтори Setup.");
            Material material = new Material(shader) { name = "MVP Projectile", enableInstancing = true };
            Color color = new Color(1f, 0.76f, 0.12f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            IntercomAssetCatalog.EnsureParentFolder(MaterialPath);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
