using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class GameObject
    {
        public readonly Transform transform;
        private readonly Dictionary<Type, Component> components = new Dictionary<Type, Component>();
        public GameObject() { transform = new Transform { gameObject = this }; }
        public T AddComponent<T>() where T : Component, new() { var component = new T { gameObject = this }; components[typeof(T)] = component; return component; }
        public T GetComponent<T>() where T : class { components.TryGetValue(typeof(T), out var c); return c as T; }
    }
    public class Component { public GameObject gameObject; public Transform transform => gameObject.transform; }
    public class MonoBehaviour : Component { public bool isActiveAndEnabled = true; }
    public class Transform { public GameObject gameObject; public Transform parent; public T GetComponent<T>() where T : class => gameObject.GetComponent<T>(); }
    public class Collider : Component { }
    public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c) { x=a;y=b;z=c; } }
    public static class Mathf { public static float Clamp(float value,float lo,float hi) => Math.Clamp(value,lo,hi); }
    public class DisallowMultipleComponent : Attribute { }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string label) {} }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string label) {} }
    public class RangeAttribute : Attribute { public RangeAttribute(float min,float max) {} }
}
