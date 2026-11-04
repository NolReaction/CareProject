using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace UnityEngine {
 public class Object {
  public bool destroyed; public string name; public HideFlags hideFlags;
  private static bool Null(Object o) => ReferenceEquals(o,null) || o.destroyed;
  public static bool operator ==(Object a,Object b) => Null(a) ? Null(b) : !Null(b) && ReferenceEquals(a,b);
  public static bool operator !=(Object a,Object b) => !(a==b);
  public override bool Equals(object o) => ReferenceEquals(this,o);
  public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
  public static void Destroy(Object o) { if(!ReferenceEquals(o,null))o.destroyed=true; }
 }
 public class Shader : Object {}
 public enum HideFlags { HideAndDontSave }
 public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} }
 public class Material : Object {
  public Shader shader; public int renderQueue;
  public readonly Dictionary<string,float> floats=new Dictionary<string,float>();
  public readonly Dictionary<string,Color> colors=new Dictionary<string,Color>();
  public readonly HashSet<string> keywords=new HashSet<string>();
  public Material(Shader shader){this.shader=shader;colors["_BaseColor"]=new Color(.2f,.4f,.6f,1);colors["_Color"]=colors["_BaseColor"];}
  public static int copies;
  public Material(Material original){copies++;shader=original.shader;name=original.name;CopyPropertiesFromMaterial(original);}
  public void CopyPropertiesFromMaterial(Material original){colors.Clear();floats.Clear();keywords.Clear();foreach(var p in original.colors)colors[p.Key]=p.Value;foreach(var p in original.floats)floats[p.Key]=p.Value;foreach(var k in original.keywords)keywords.Add(k);}
  public bool HasProperty(string key)=>colors.ContainsKey(key)||floats.ContainsKey(key);
  public Color GetColor(string key)=>colors[key]; public void SetColor(string key,Color c)=>colors[key]=c;
  public void SetFloat(string key,float f)=>floats[key]=f;
  public void EnableKeyword(string s)=>keywords.Add(s); public void DisableKeyword(string s)=>keywords.Remove(s);
  public void SetOverrideTag(string a,string b){}
 }
 public class Renderer : Object { public bool forceRenderingOff;public bool hasBlock;public int materialReads,materialWrites;private Material[] materials=new Material[0]; public Material[] sharedMaterials {get {materialReads++;return (Material[])materials.Clone();}set {materialWrites++;materials=(Material[])value.Clone();}}public bool HasPropertyBlock()=>hasBlock; }
 public static class Mathf { public static float Clamp01(float f)=>Math.Max(0,Math.Min(1,f));public static float Min(float a,float b)=>Math.Min(a,b); }
 public enum RuntimeInitializeLoadType { SubsystemRegistration }
 [AttributeUsage(AttributeTargets.Method)] public sealed class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
}
namespace UnityEngine.Rendering {public enum BlendMode {One=1,SrcAlpha=5,OneMinusSrcAlpha=10} public enum RenderQueue {Transparent=3000}}
