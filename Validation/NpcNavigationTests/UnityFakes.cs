// Narrow component/hierarchy/bounds stand-ins; not Unity or PhysX.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class GameObject
    {
        public bool activeInHierarchy = true;
        public readonly Transform transform;
        public readonly List<Component> components = new List<Component>();
        public GameObject() { transform = new Transform { gameObject = this }; components.Add(transform); }
        public T AddComponent<T>() where T : Component, new() { var value = new T { gameObject = this }; components.Add(value); return value; }
    }
    public class Component { public GameObject gameObject; }
    public class MonoBehaviour : Component { public bool enabled = true; public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy; }
    public class Transform : Component
    {
        public readonly List<Transform> children = new List<Transform>();
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        { var found = new List<T>(); Collect(this,found); return found.ToArray(); }
        static void Collect<T>(Transform root,List<T> found) where T : Component
        { foreach(var c in root.gameObject.components)if(c is T item)found.Add(item);foreach(var child in root.children)Collect(child,found); }
    }
    public class Collider : Component { public bool enabled = true, isTrigger; public Bounds bounds; }
    public class CharacterController : Collider { }
    public struct Vector2 { public float x,y; public Vector2(float x,float y) {this.x=x;this.y=y;} }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
        public override bool Equals(object o)=>o is Vector3 v&&v.x==x&&v.y==y&&v.z==z;
        public override int GetHashCode()=>HashCode.Combine(x,y,z);
    }
    public struct Bounds
    {
        public Vector3 min,max;
        public Bounds(Vector3 min,Vector3 max) {this.min=min;this.max=max;}
        public static bool operator ==(Bounds a,Bounds b)=>a.min.Equals(b.min)&&a.max.Equals(b.max);
        public static bool operator !=(Bounds a,Bounds b)=>!(a==b);
        public override bool Equals(object o)=>o is Bounds b&&this==b;
        public override int GetHashCode()=>HashCode.Combine(min,max);
    }
    public static class Time { public static float timeScale=1f,unscaledTime;public static int frameCount; }
    public static class Mathf
    {
        public static float Clamp(float n,float min,float max)=>Math.Clamp(n,min,max);
        public static int Clamp(int n,int min,int max)=>Math.Clamp(n,min,max);
        public static float Max(float a,float b)=>Math.Max(a,b);
    }
    public class DisallowMultipleComponent:Attribute { }
    public class DefaultExecutionOrder:Attribute { public DefaultExecutionOrder(int order) { } }
    public class MinAttribute:Attribute { public MinAttribute(float value) { } }
    public class RangeAttribute:Attribute { public RangeAttribute(float min,float max) { } }
    public class ContextMenu:Attribute { public ContextMenu(string text) { } }
}
