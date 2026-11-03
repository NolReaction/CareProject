// Narrow deterministic stand-ins. These tests do not claim Unity physics/render execution.
using System;
using System.Collections.Generic;
using System.Linq;
using N = System.Numerics;
namespace UnityEngine
{
    public class Object { }
    public class GameObject : Object
    {
        public readonly List<Component> Components = new List<Component>();
        public readonly Transform transform;
        public bool activeSelf { get; private set; } = true;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public GameObject() { transform = new Transform { gameObject = this }; Components.Add(transform); }
        public T AddComponent<T>() where T : Component, new() { var component = new T { gameObject = this }; Components.Add(component); return component; }
        public void SetActive(bool value) { activeSelf = value; }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T[] GetComponents<T>() where T : Component => gameObject.Components.OfType<T>().ToArray();
    }
    public class Behaviour : Component { public bool enabled = true; public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy; }
    public class MonoBehaviour : Behaviour { }
    public class Collider : Component { public bool enabled = true; }
    public class Transform : Component
    {
        public Transform parent;
        public Vector3 localPosition, localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        public Quaternion rotation => parent == null ? localRotation : parent.rotation * localRotation;
        public Vector3 InverseTransformDirection(Vector3 direction) => Quaternion.Inverse(rotation) * direction;
        public bool IsChildOf(Transform ancestor) {for(var current=this;current!=null;current=current.parent)if(current==ancestor)return true;return false;}
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        internal N.Vector3 Numeric => new N.Vector3(x,y,z);
        internal static Vector3 From(N.Vector3 v) => new Vector3(v.X,v.Y,v.Z);
        public static Vector3 one => new Vector3(1,1,1);
        public static Vector3 up => new Vector3(0,1,0);
        public static Vector3 right => new Vector3(1,0,0);
        public static Vector3 forward => new Vector3(0,0,1);
        public static Vector3 zero => default;
        public float sqrMagnitude => x*x+y*y+z*z;
        public Vector3 normalized => sqrMagnitude > 1e-12f ? From(N.Vector3.Normalize(Numeric)) : zero;
        public static Vector3 operator +(Vector3 a, Vector3 b) => From(a.Numeric+b.Numeric);
        public static Vector3 operator -(Vector3 a, Vector3 b) => From(a.Numeric-b.Numeric);
        public static Vector3 operator *(Vector3 a, float n) => From(a.Numeric*n);
        public static Vector3 Scale(Vector3 a, Vector3 b) => From(a.Numeric*b.Numeric);
        public static Vector3 Cross(Vector3 a, Vector3 b) => From(N.Vector3.Cross(a.Numeric,b.Numeric));
        public static Vector3 ProjectOnPlane(Vector3 a, Vector3 normal) => From(a.Numeric-normal.Numeric*N.Vector3.Dot(a.Numeric,normal.Numeric));
    }
    public struct Quaternion
    {
        public float x,y,z,w;
        internal N.Quaternion Numeric => new N.Quaternion(x,y,z,w);
        internal static Quaternion From(N.Quaternion q) => new Quaternion {x=q.X,y=q.Y,z=q.Z,w=q.W};
        public static Quaternion identity => From(N.Quaternion.Identity);
        public static Quaternion Inverse(Quaternion q) => From(N.Quaternion.Inverse(q.Numeric));
        public static Quaternion operator *(Quaternion a, Quaternion b) => From(a.Numeric*b.Numeric);
        public static Vector3 operator *(Quaternion q, Vector3 v) => Vector3.From(N.Vector3.Transform(v.Numeric,q.Numeric));
        public static Quaternion AngleAxis(float degrees, Vector3 axis) => From(N.Quaternion.CreateFromAxisAngle(axis.normalized.Numeric,degrees*(float)Math.PI/180f));
        public static Quaternion Euler(float x,float y,float z) => From(N.Quaternion.CreateFromYawPitchRoll(y*(float)Math.PI/180f,x*(float)Math.PI/180f,z*(float)Math.PI/180f));
    }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a) {this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color Lerp(Color a, Color b, float t) {t=Math.Clamp(t,0,1);return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
        public static Color operator *(Color c,float n) => new Color(c.r*n,c.g*n,c.b*n,c.a*n);
    }
    public class Material : Object
    {
        public static int Created;
        public readonly Dictionary<int,Color> Colors = new Dictionary<int,Color>();
        public Material() { Created++; }
        public bool HasProperty(int id) => Colors.ContainsKey(id);
        public Color GetColor(int id) => Colors.TryGetValue(id,out var color) ? color : default;
    }
    public class MaterialPropertyBlock
    {
        public static int Created;
        public readonly Dictionary<int,Color> Colors = new Dictionary<int,Color>();
        public MaterialPropertyBlock() { Created++; }
        public void SetColor(int id,Color color) { Colors[id]=color; }
        public void CopyFrom(Dictionary<int,Color> source) {Colors.Clear();foreach(var pair in source)Colors.Add(pair.Key,pair.Value);}
    }
    public class Renderer : Component
    {
        public Material sharedMaterial;
        public readonly Dictionary<int,Color> Colors = new Dictionary<int,Color>();
        public void GetPropertyBlock(MaterialPropertyBlock block) {block.CopyFrom(Colors);}
        public void SetPropertyBlock(MaterialPropertyBlock block) {Colors.Clear();foreach(var pair in block.Colors)Colors.Add(pair.Key,pair.Value);}
    }
    public static class Shader {public static int PropertyToID(string name) => name.GetHashCode();}
    public static class Time {public static float timeScale=1,deltaTime=1f/60;}
    public static class Mathf
    {
        public const float PI=(float)Math.PI;
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static float Abs(float a)=>Math.Abs(a);
        public static float Clamp(float a,float min,float max)=>Math.Clamp(a,min,max);
        public static float Clamp01(float a)=>Math.Clamp(a,0,1);
        public static float Sin(float a)=>(float)Math.Sin(a);
        public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
        public static float MoveTowards(float a,float b,float delta)=>a<b?Math.Min(a+delta,b):Math.Max(a-delta,b);
    }
    public class DisallowMultipleComponent : Attribute { }
    public class TooltipAttribute : Attribute {public TooltipAttribute(string value) {}}
    public class MinAttribute : Attribute {public MinAttribute(float value) {}}
    public class RangeAttribute : Attribute {public RangeAttribute(float min,float max) {}}
    public class ContextMenu : Attribute {public ContextMenu(string label) {}}
}
namespace IntercomMvp
{
    public class IntercomPlayerMotor : UnityEngine.MonoBehaviour {public uint RespawnVersion;}
}
