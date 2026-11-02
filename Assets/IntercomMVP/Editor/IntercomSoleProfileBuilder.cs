using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    /// <summary>Explicit Setup Robot authoring pass; never runs during import or gameplay.</summary>
    internal static class IntercomSoleProfileBuilder
    {
        internal const string AssetPath = "Assets/IntercomMVP/Art/Robot/IntercomSoleContact.asset";
        private const int RunPhases = 32;
        private const int PreferredPoints = 100;
        private const int MaximumPoints = 256;
        private static readonly string[] Runs = { "Run", "RunBackward", "RunLeft", "RunRight",
            "RunRelaxed", "RunBackwardRelaxed", "RunLeftRelaxed", "RunRightRelaxed" };

        private struct Pose
        {
            internal Transform transform;
            internal Vector3 position, scale;
            internal Quaternion rotation;
        }
        private sealed class Source
        {
            internal SkinnedMeshRenderer renderer;
            internal Mesh baked;
            internal Vector3[] world;
        }
        private sealed class Vertex
        {
            internal Source source;
            internal int index;
            internal Vector3 idleWorld;
            internal IntercomGroundFeedbackSoleProfile.Point point;
        }
        private sealed class Boot
        {
            internal readonly List<Vertex> vertices = new List<Vertex>();
            internal readonly HashSet<int> required = new HashSet<int>();
            internal readonly List<HashSet<int>> bands = new List<HashSet<int>>();
            internal Vector3 lateral, forward;
        }

        internal static void Rebuild(GameObject robot, Animator animator, AnimationClip[] clips)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode())
                throw new InvalidOperationException("Заверши Play/Animation Preview перед Setup Robot.");
            IntercomGroundFeedback feedback = robot.GetComponent<IntercomGroundFeedback>();
            if (feedback == null || animator == null || feedback.leftFoot == null || feedback.rightFoot == null
                || feedback.leftToe == null || feedback.rightToe == null || feedback.leftCalf == null || feedback.rightCalf == null)
                throw new InvalidOperationException("Для профиля подошв нужны Animator и обе цепочки Calf/Foot/Toe.");
            string fingerprint = SourceFingerprint();
            IntercomGroundFeedbackSoleProfile cached = AssetDatabase.LoadAssetAtPath<IntercomGroundFeedbackSoleProfile>(AssetPath);
            if (cached != null && cached.sourceGeometry == fingerprint && Valid(cached.left) && Valid(cached.right))
            {
                Connect(feedback, cached);
                return;
            }
            AnimationClip idle = Clip(clips, "Idle");
            AnimationClip[] runs = Runs.Select(name => Clip(clips, name)).ToArray();
            Pose[] original = Capture(robot.GetComponentsInChildren<Transform>(true));
            var sources = new Dictionary<SkinnedMeshRenderer, Source>();
            IntercomGroundFeedbackSoleProfile.Point[] left = null, right = null;
            Exception samplingFailure = null;
            try
            {
                idle.SampleAnimation(animator.gameObject, 0f);
                Pose[] idlePose = Capture(robot.GetComponentsInChildren<Transform>(true));
                Vector3 lateral = Vector3.ProjectOnPlane(feedback.leftFoot.position - feedback.rightFoot.position, Vector3.up).normalized;
                if (lateral.sqrMagnitude < 0.5f) throw new InvalidOperationException("Не определена поперечная ось ботинок в Idle.");
                SkinnedMeshRenderer[] renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Boot l = ReadBoot(renderers, feedback.leftFoot, feedback.leftToe, feedback.leftCalf, lateral, sources);
                Boot r = ReadBoot(renderers, feedback.rightFoot, feedback.rightToe, feedback.rightCalf, lateral, sources);
                SelectContour(l); SelectContour(r);
                foreach (AnimationClip run in runs)
                    for (int phase = 0; phase < RunPhases; phase++)
                    {
                        // Missing clip bindings must not inherit a preceding Run pose.
                        Restore(idlePose);
                        run.SampleAnimation(animator.gameObject, run.length * phase / RunPhases);
                        foreach (Source source in sources.Values) Bake(source);
                        SelectLowest(l); SelectLowest(r);
                    }
                left = Finish(l); right = Finish(r);
            }
            catch (Exception exception) { samplingFailure = exception; }
            finally
            {
                Restore(original);
                foreach (Source source in sources.Values)
                    if (source.baked != null) UnityEngine.Object.DestroyImmediate(source.baked);
            }
            if (samplingFailure != null)
            {
                // A previous model's stencil is unsafe for this replacement.
                // Keep its asset for rollback, but use measured heel/sole/toe markers.
                Connect(feedback, null);
                Debug.LogWarning("Intercom MVP: новый профиль подошв не построен: " + samplingFailure.Message
                    + " Старый asset сохранён, у робота включён контакт по трём маркерам. Проверь новую модель и повтори Setup Robot.", robot);
                return;
            }
            // Commit only after every source mesh and sample passed validation.
            IntercomGroundFeedbackSoleProfile profile = AssetDatabase.LoadAssetAtPath<IntercomGroundFeedbackSoleProfile>(AssetPath);
            if (profile == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(AssetPath) != null)
                    throw new InvalidOperationException("Путь профиля подошв занят другим asset: " + AssetPath);
                profile = ScriptableObject.CreateInstance<IntercomGroundFeedbackSoleProfile>();
                profile.name = "Intercom Sole Contact";
                AssetDatabase.CreateAsset(profile, AssetPath);
            }
            else Undo.RecordObject(profile, "Rebuild measured sole profile");
            profile.left = left; profile.right = right;
            profile.sourceGeometry = fingerprint;
            EditorUtility.SetDirty(profile);
            Connect(feedback, profile);
        }

        private static void Connect(IntercomGroundFeedback feedback, IntercomGroundFeedbackSoleProfile profile)
        {
            Undo.RecordObject(feedback, "Connect measured sole profile");
            feedback.soleProfile = profile;
            feedback.CalibrateSoles();
            EditorUtility.SetDirty(feedback);
            if (PrefabUtility.IsPartOfPrefabInstance(feedback)) PrefabUtility.RecordPrefabInstancePropertyModifications(feedback);
        }

        private static Boot ReadBoot(SkinnedMeshRenderer[] renderers, Transform foot, Transform toe, Transform calf,
            Vector3 lateral, Dictionary<SkinnedMeshRenderer, Source> sources)
        {
            float length = Vector3.Distance(calf.position, foot.position);
            Vector3 forward = Vector3.ProjectOnPlane(toe.position - foot.position, Vector3.up);
            forward = Vector3.ProjectOnPlane(forward, lateral).normalized;
            if (length < 0.01f || forward.sqrMagnitude < 0.5f)
                throw new InvalidOperationException("Неверная анатомическая система подошвы: " + foot.name);
            var boot = new Boot { lateral = lateral, forward = forward };
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Mesh mesh = renderer.sharedMesh;
                Transform[] bones = renderer.bones;
                int footIndex = Array.IndexOf(bones, foot), toeIndex = Array.IndexOf(bones, toe);
                if (mesh == null || (footIndex < 0 && toeIndex < 0)) continue;
                if (mesh.blendShapeCount != 0)
                    throw new InvalidOperationException("Профиль Foot/Toe не поддерживает blend shapes ботинка: " + renderer.name);
                // Setup is an edit-time menu operation: Unity permits these reads
                // even with Read/Write disabled. Never change the FBX importer flag.
                // https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh/isreadable
                Vector3[] vertices = mesh.vertices;
                var weights = mesh.GetAllBoneWeights();
                Matrix4x4[] bindposes = mesh.bindposes;
                var influences = mesh.GetBonesPerVertex();
                if (influences.Length != vertices.Length)
                    throw new InvalidOperationException("Нет полных skin weights у " + renderer.name);
                if (!sources.TryGetValue(renderer, out Source source))
                {
                    source = new Source { renderer = renderer, baked = new Mesh { name = "Temporary sole sampling" } };
                    sources.Add(renderer, source);
                    Bake(source);
                }
                int weightIndex = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (weightIndex + influences[i] > weights.Length)
                        throw new InvalidOperationException("Неполный массив skin weights у " + renderer.name);
                    float footWeight = 0f, toeWeight = 0f, otherWeight = 0f;
                    for (int influence = 0; influence < influences[i]; influence++)
                    {
                        BoneWeight1 weight = weights[weightIndex++];
                        if (!Finite(weight.weight) || weight.weight < 0f)
                            throw new InvalidOperationException("Некорректный skin weight у " + renderer.name);
                        if (weight.boneIndex == footIndex) footWeight += weight.weight;
                        else if (weight.boneIndex == toeIndex) toeWeight += weight.weight;
                        else otherWeight += weight.weight;
                    }
                    float total = footWeight + toeWeight;
                    if (total <= 0.00000001f) continue;
                    if (otherWeight > 0.00000001f || Mathf.Abs(total - 1f) > 0.00001f)
                        throw new InvalidOperationException("Вершины ботинка должны иметь только Foot/Toe веса с суммой 1: " + renderer.name);
                    if ((footIndex >= 0 && footIndex >= bindposes.Length) || (toeIndex >= 0 && toeIndex >= bindposes.Length))
                        throw new InvalidOperationException("Неполные bind poses у " + renderer.name);
                    Vector3 f = footIndex >= 0 ? foot.TransformPoint(bindposes[footIndex].MultiplyPoint3x4(vertices[i])) : Vector3.zero;
                    Vector3 t = toeIndex >= 0 ? toe.TransformPoint(bindposes[toeIndex].MultiplyPoint3x4(vertices[i])) : f;
                    if (footIndex < 0) f = t;
                    float blend = toeWeight / total;
                    Vector3 actual = source.world[i];
                    if (!Finite(f) || !Finite(t) || !Finite(actual)
                        || Vector3.Distance(f * (1f - blend) + t * blend, actual) > 0.0005f)
                        throw new InvalidOperationException("Skinning ботинка не соответствует Foot/Toe bind poses: " + renderer.name);
                    boot.vertices.Add(new Vertex { source = source, index = i, idleWorld = actual,
                        point = new IntercomGroundFeedbackSoleProfile.Point {
                            footPosition = Anatomical(f, foot.position, length, lateral, forward),
                            toePosition = Anatomical(t, foot.position, length, lateral, forward), toeWeight = blend } });
                }
            }
            if (boot.vertices.Count < 3) throw new InvalidOperationException("Не найдена геометрия подошвы " + foot.name);
            return boot;
        }

        // Shared with the supplied offline profile. An unchanged model retains
        // its denser 129-phase contour; only an explicit Setup after replacement rebuilds it.
        private static string SourceFingerprint()
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(Path.Combine(Application.dataPath, "..", IntercomRobotAssets.ModelPath)))
                return "Unity FBX SHA256:" + BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant()
                    + "; SoleContour:v2";
        }
        private static bool Valid(IntercomGroundFeedbackSoleProfile.Point[] points)
        {
            return points != null && points.Length >= 3 && points.Length <= MaximumPoints
                && points.All(point => Finite(point.footPosition) && Finite(point.toePosition)
                    && Finite(point.toeWeight) && point.toeWeight >= 0f && point.toeWeight <= 1f);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static Vector3 Anatomical(Vector3 point, Vector3 origin, float length, Vector3 lateral, Vector3 forward)
        {
            Vector3 delta = (point - origin) / length;
            return new Vector3(Vector3.Dot(delta, lateral), delta.y, Vector3.Dot(delta, forward));
        }
        private static void Bake(Source source)
        {
            source.renderer.BakeMesh(source.baked);
            Vector3[] vertices = source.baked.vertices;
            if (vertices.Length != source.renderer.sharedMesh.vertexCount)
                throw new InvalidOperationException("Изменилось число вершин при BakeMesh: " + source.renderer.name);
            for (int i = 0; i < vertices.Length; i++) vertices[i] = source.renderer.transform.TransformPoint(vertices[i]);
            source.world = vertices;
        }
        private static void SelectContour(Boot boot)
        {
            float lowest = boot.vertices.Min(vertex => vertex.idleWorld.y);
            int[] near = Enumerable.Range(0, boot.vertices.Count).Where(i => boot.vertices[i].idleWorld.y <= lowest + 0.045f).ToArray();
            boot.required.UnionWith(Extrema(boot, near));
            for (int band = 0; band < 5; band++)
            {
                int current = band;
                boot.bands.Add(Extrema(boot, near.Where(i => Band(boot.vertices[i].point.toeWeight) == current).ToArray()));
            }
            SelectLowest(boot);
        }
        private static int Band(float weight) => weight <= 0.00000001f ? 0 : weight < 0.25f ? 1
            : weight < 0.75f ? 2 : weight < 0.99999999f ? 3 : 4;
        private static HashSet<int> Extrema(Boot boot, int[] candidates)
        {
            var result = new HashSet<int>();
            if (candidates.Length == 0) return result;
            foreach (float slope in new[] { 0f, 0.2f, 0.55f, 1f })
                for (int angle = 0; angle < 24; angle++)
                {
                    float radians = angle * Mathf.PI * 2f / 24;
                    Vector3 direction = boot.lateral * Mathf.Cos(radians) + boot.forward * Mathf.Sin(radians);
                    float minimum = float.PositiveInfinity;
                    int selected = candidates[0];
                    foreach (int i in candidates)
                    {
                        Vector3 p = boot.vertices[i].idleWorld;
                        float value = p.y + slope * Vector3.Dot(direction, p);
                        if (value < minimum) { minimum = value; selected = i; }
                    }
                    result.Add(selected);
                }
            return result;
        }
        private static void SelectLowest(Boot boot)
        {
            int a = -1, b = -1, c = -1;
            float ya = float.PositiveInfinity, yb = ya, yc = ya;
            for (int i = 0; i < boot.vertices.Count; i++)
            {
                Vertex vertex = boot.vertices[i];
                float y = vertex.source.world[vertex.index].y;
                if (float.IsNaN(y) || float.IsInfinity(y)) throw new InvalidOperationException("Некорректная вершина при sample Run.");
                if (y < ya) { c = b; yc = yb; b = a; yb = ya; a = i; ya = y; }
                else if (y < yb) { c = b; yc = yb; b = i; yb = y; }
                else if (y < yc) { c = i; yc = y; }
            }
            if (a >= 0) boot.required.Add(a);
            if (b >= 0) boot.required.Add(b);
            if (c >= 0) boot.required.Add(c);
        }
        private static IntercomGroundFeedbackSoleProfile.Point[] Finish(Boot boot)
        {
            var chosen = new HashSet<int>(boot.required);
            var queues = boot.bands.Select(band => new Queue<int>(band.Except(chosen)
                .OrderBy(i => boot.vertices[i].idleWorld.y).ThenBy(i => i))).ToArray();
            while (chosen.Count < PreferredPoints && queues.Any(queue => queue.Count > 0))
                foreach (Queue<int> queue in queues)
                {
                    while (queue.Count > 0 && chosen.Contains(queue.Peek())) queue.Dequeue();
                    if (queue.Count > 0 && chosen.Count < PreferredPoints) chosen.Add(queue.Dequeue());
                }
            if (chosen.Count > MaximumPoints)
                throw new InvalidOperationException("Контур подошвы слишком сложен для мобильного профиля: " + chosen.Count
                    + ". Упрости геометрию контакта; точные минимумы не были отброшены.");
            return chosen.OrderBy(i => i).Select(i => boot.vertices[i].point).ToArray();
        }
        private static AnimationClip Clip(AnimationClip[] clips, string name)
        {
            AnimationClip result = clips.FirstOrDefault(clip => clip.name.Equals(name, StringComparison.OrdinalIgnoreCase)
                || clip.name.EndsWith("|" + name, StringComparison.OrdinalIgnoreCase));
            if (result == null) throw new InvalidOperationException("Нет клипа для профиля подошв: " + name);
            return result;
        }
        private static Pose[] Capture(Transform[] transforms) => transforms.Select(item => new Pose {
            transform = item, position = item.localPosition, rotation = item.localRotation, scale = item.localScale }).ToArray();
        private static void Restore(Pose[] poses)
        {
            foreach (Pose pose in poses)
            {
                if (pose.transform == null) continue;
                pose.transform.localPosition = pose.position;
                pose.transform.localRotation = pose.rotation;
                pose.transform.localScale = pose.scale;
            }
        }
    }
}
