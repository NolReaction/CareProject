using System;
using System.Collections.Generic;
using System.Linq;
using N = System.Numerics;

namespace UnityEngine
{
    public class Object
    {
        internal static readonly List<Object> Registry = new List<Object>();
        public string name;
        public Object() { Registry.Add(this); }
        public int GetInstanceID() => GetHashCode();
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort) where T : Object => Registry.OfType<T>().ToArray();
    }
    public enum FindObjectsInactive { Include } public enum FindObjectsSortMode { None }
    public class GameObject : Object
    {
        internal readonly List<Component> components = new List<Component>();
        public readonly Transform transform;
        public bool activeInHierarchy = true; public int layer;
        public SceneManagement.Scene scene => new SceneManagement.Scene();
        public GameObject(string name) { this.name = name; transform = AddComponent<Transform>(); transform.name = name; }
        public T AddComponent<T>() where T : Component, new() { var c = new T { gameObject = this }; components.Add(c); return c; }
        public T GetComponent<T>() where T : Component => components.OfType<T>().FirstOrDefault();
    }
    public class Component : Object
    {
        public GameObject gameObject; public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>(bool inactive) where T : Component
        { for (Transform p = transform; p != null; p = p.parent) { var c = p.GetComponent<T>(); if (c != null) return c; } return null; }
        public T[] GetComponentsInChildren<T>(bool inactive) where T : Component
            => Registry.OfType<T>().Where(c => c.transform == transform || c.transform.IsChildOf(transform)).ToArray();
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }
    public class Transform : Component
    {
        public Transform parent; public Vector3 position, localScale = new Vector3(1, 1, 1);
        public N.Quaternion rotation = N.Quaternion.Identity;
        public Vector3 right => Rotate(new Vector3(1, 0, 0));
        public Vector3 up => Rotate(new Vector3(0, 1, 0));
        public Vector3 forward => Rotate(new Vector3(0, 0, 1));
        private Vector3 Rotate(Vector3 p) => Vector3.From(N.Vector3.Transform(p.Numerics, rotation));
        public Vector3 TransformPoint(Vector3 p) => position + Rotate(new Vector3(p.x * localScale.x, p.y * localScale.y, p.z * localScale.z));
        public Vector3 InverseTransformPoint(Vector3 p)
        { var v = Vector3.From(N.Vector3.Transform((p - position).Numerics, N.Quaternion.Inverse(rotation))); return new Vector3(v.x / localScale.x, v.y / localScale.y, v.z / localScale.z); }
        public bool IsChildOf(Transform target) { for (var p = parent; p != null; p = p.parent) if (p == target) return true; return false; }
    }
    public struct Vector3
    {
        public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        internal N.Vector3 Numerics => new N.Vector3(x, y, z);
        internal static Vector3 From(N.Vector3 n) => new Vector3(n.X, n.Y, n.Z);
        public static Vector3 up => new Vector3(0, 1, 0);
        public Vector3 normalized => From(N.Vector3.Normalize(Numerics));
        public static Vector3 operator +(Vector3 a, Vector3 b) => From(a.Numerics + b.Numerics);
        public static Vector3 operator -(Vector3 a, Vector3 b) => From(a.Numerics - b.Numerics);
        public static Vector3 operator *(Vector3 a, float b) => From(a.Numerics * b);
        public static float Dot(Vector3 a, Vector3 b) => N.Vector3.Dot(a.Numerics, b.Numerics);
        public static float Distance(Vector3 a, Vector3 b) => N.Vector3.Distance(a.Numerics, b.Numerics);
    }
    public struct Bounds
    {
        public Vector3 center, size; public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; }
        public Vector3 extents => size * .5f; public Vector3 min => center - extents; public Vector3 max => center + extents;
        public void Expand(float amount) { size += new Vector3(amount, amount, amount); }
        public void Encapsulate(Bounds b) { Vector3 lo = Vector3.From(N.Vector3.Min(min.Numerics, b.min.Numerics)), hi = Vector3.From(N.Vector3.Max(max.Numerics, b.max.Numerics)); center = (lo + hi) * .5f; size = hi - lo; }
        public bool IntersectRay(Ray ray, out float distance)
        {
            float near = 0, far = float.PositiveInfinity;
            var lo = min.Numerics; var hi = max.Numerics; var o = ray.origin.Numerics; var d = ray.direction.Numerics;
            for (int i = 0; i < 3; i++)
            {
                if (Math.Abs(d[i]) < 1e-9) { if (o[i] < lo[i] || o[i] > hi[i]) { distance = 0; return false; } continue; }
                float a = (lo[i] - o[i]) / d[i], b = (hi[i] - o[i]) / d[i];
                near = Math.Max(near, Math.Min(a, b)); far = Math.Min(far, Math.Max(a, b));
                if (near > far) { distance = 0; return false; }
            }
            distance = near; return true;
        }
    }
    public struct Ray { public Vector3 origin, direction; public Ray(Vector3 origin, Vector3 direction) { this.origin = origin; this.direction = direction.normalized; } public Vector3 GetPoint(float distance) => origin + direction * distance; }
    public class Material : Object { }
    public class Mesh : Object
    {
        internal Vector3[] sourceVertices; public int vertexReads;
        public Vector3[] vertices { get { vertexReads++; return sourceVertices; } }
        public int[] triangles; public bool isReadable = true;
    }
    public class MeshFilter : Component { public Mesh sharedMesh; }
    public class Renderer : Behaviour
    {
        public bool forceRenderingOff;
        public virtual Bounds bounds
        {
            get
            {
                Vector3[] points = GetComponent<MeshFilter>().sharedMesh.sourceVertices;
                var b = new Bounds(transform.TransformPoint(points[0]), new Vector3());
                for (int i = 1; i < points.Length; i++) b.Encapsulate(new Bounds(transform.TransformPoint(points[i]), new Vector3()));
                return b;
            }
        }
    }
    public class MeshRenderer : Renderer { }
    public class CharacterController : Behaviour
    {
        public Bounds bounds => new Bounds(transform.position + new Vector3(0, .84f, 0), new Vector3(.6f, 1.68f, .6f));
    }
    public class Camera : Behaviour
    {
        public static event Action<Camera> onPreCull;
        public int cullingMask = ~0;
        public bool orthographic = true; public float orthographicSize = 4.6f, fieldOfView = 60, aspect = 16f / 9f, nearClipPlane = .05f;
        public Ray ViewportPointToRay(Vector3 point)
        {
            float x = (point.x - .5f) * 2, y = (point.y - .5f) * 2;
            if (orthographic) return new Ray(transform.position + transform.right * (x * orthographicSize * aspect)
                + transform.up * (y * orthographicSize) + transform.forward * nearClipPlane, transform.forward);
            float tan = MathF.Tan(fieldOfView * .5f * MathF.PI / 180);
            Vector3 direction = (transform.forward + transform.right * (x * tan * aspect) + transform.up * (y * tan)).normalized;
            return new Ray(transform.position + direction * (nearClipPlane / Vector3.Dot(direction, transform.forward)), direction);
        }
    }
    public static class Mathf
    {
        public const float Deg2Rad = MathF.PI / 180;
        public static float Max(float a, float b) => Math.Max(a, b); public static float Abs(float a) => Math.Abs(a);
        public static float Clamp(float a, float min, float max) => Math.Clamp(a, min, max);
        public static int CeilToInt(float a) => (int)Math.Ceiling(a); public static float Tan(float a) => MathF.Tan(a);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
    public static class Application { public static bool isPlaying = true; }
    public static class Time { public static int frameCount; public static float unscaledTime, unscaledDeltaTime; }
    public struct LayerMask { public int value; public static implicit operator LayerMask(int v) => new LayerMask { value = v }; }
    [AttributeUsage(AttributeTargets.Class)] public class DefaultExecutionOrderAttribute : Attribute { public DefaultExecutionOrderAttribute(int v) { } }
    [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponentAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class RequireComponentAttribute : Attribute { public RequireComponentAttribute(Type t) { } }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public class MinAttribute : Attribute { public MinAttribute(float v) { } }
    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Method)] public class ContextMenuAttribute : Attribute { public ContextMenuAttribute(string v) { } }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public bool IsValid() => true; public bool isLoaded => true; }
    public enum LoadSceneMode { Single }
    public static class SceneManager { public static event Action<Scene, LoadSceneMode> sceneLoaded; public static event Action<Scene> sceneUnloaded; }
}
namespace UnityEngine.Rendering
{
    public struct ScriptableRenderContext { }
    public static class RenderPipelineManager { public static event Action<ScriptableRenderContext, UnityEngine.Camera> beginCameraRendering; }
}
namespace IntercomMvp
{
    // Lease/material behavior is separately exercised by OcclusionLeaseTests.
    internal static class IntercomOcclusionFade
    {
        internal static uint Epoch;
        internal static readonly Dictionary<UnityEngine.Renderer, float> Active = new Dictionary<UnityEngine.Renderer, float>();
        internal static void Set(UnityEngine.Renderer r, int owner, float alpha, UnityEngine.Material template, bool retainForCamera = false) { if (r != null) Active[r] = alpha; }
        internal static void Release(UnityEngine.Renderer r, int owner) { if (r != null) Active.Remove(r); }
        internal static void ReleaseUnused(int owner, HashSet<UnityEngine.Renderer> keep) { }
    }
}
