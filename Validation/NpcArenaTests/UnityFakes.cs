using System;
using System.Collections.Generic;

// Deliberately narrow lifecycle harness, not a Unity scene/physics simulation.
namespace UnityEngine
{
    public sealed class DisallowMultipleComponent : Attribute { }
    public sealed class RangeAttribute : Attribute { public RangeAttribute(int min, int max) { } }
    public sealed class GameObject
    {
        public bool activeSelf = true, destroyed;
        public readonly Transform transform = new Transform();
        public void SetActive(bool value) { activeSelf = value; }
    }
    public class MonoBehaviour
    {
        public readonly GameObject gameObject = new GameObject();
        public Transform transform => gameObject.transform;
        public static void Destroy(GameObject target) { target.destroyed = true; }
    }
    public sealed class Transform
    {
        public Vector3 position;
        public Transform parent;
        public void SetParent(Transform value, bool worldPositionStays) { parent = value; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public static class Mathf
    {
        public static int Min(int a, int b) => Math.Min(a, b);
        public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
    }
    public static class Debug
    {
        public static readonly List<string> Warnings = new List<string>();
        public static void LogWarning(string value, object context) { Warnings.Add(value); }
    }
}

namespace IntercomMvp
{
    using UnityEngine;
    public sealed class IntercomNpcDefinition { }
    public sealed class IntercomHealth { }
    public sealed class IntercomPlayerMotor { public uint RespawnVersion; }
    public sealed class IntercomNpcNavigation
    {
        public bool IsReady;
        public int rebuildCount;
        public void RebuildObstacleCache() { IsReady = true; rebuildCount++; }
        public bool TryProject(Vector3 source, out Vector3 result)
        {
            result = new Vector3(source.x + .1f, 0f, source.z + .1f);
            return source.x >= 0;
        }
    }
    public sealed class IntercomNpcController : MonoBehaviour
    {
        public static readonly List<IntercomNpcController> Created = new List<IntercomNpcController>();
        public IntercomNpcDefinition definition;
        public IntercomNpcNavigation navigation;
        public IntercomHealth targetHealth;
        public IntercomPlayerMotor resetMotor;
        public int ordinal;
        public static IntercomNpcController Create(IntercomNpcDefinition definition,
            IntercomNpcNavigation navigation, IntercomHealth targetHealth,
            IntercomPlayerMotor resetMotor, Vector3 spawn, int ordinal)
        {
            var actor = new IntercomNpcController { definition = definition, navigation = navigation,
                targetHealth = targetHealth, resetMotor = resetMotor, ordinal = ordinal };
            actor.transform.position = spawn;
            Created.Add(actor);
            return actor;
        }
    }
}
