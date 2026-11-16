using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class Object { public bool destroyed; public static void Destroy(Object value) { if(value!=null)value.destroyed=true; } }
    public class Component:Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T:class => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour:Component { public bool isActiveAndEnabled=true; }
    public class ScriptableObject:Object { }
    public class GameObject:Object
    {
        public static int Created;
        public string name; public bool activeSelf=true; public Transform transform;
        private readonly Dictionary<Type,Component> components=new Dictionary<Type,Component>();
        public GameObject(string label="") {name=label;Created++;transform=new Transform{gameObject=this};components[typeof(Transform)]=transform;}
        public T AddComponent<T>() where T:Component,new() {var c=new T{gameObject=this};components[typeof(T)]=c;return c;}
        public T GetComponent<T>() where T:class => components.TryGetValue(typeof(T),out Component value)?value as T:null;
        public void SetActive(bool value) { activeSelf=value; }
    }
    public class Transform:Component
    {
        public Transform parent; public Vector3 localPosition,localScale=Vector3.one;
        public Vector3 lossyScale => parent==null?localScale:new Vector3(parent.lossyScale.x*localScale.x,parent.lossyScale.y*localScale.y,parent.lossyScale.z*localScale.z);
        public void SetParent(Transform value,bool stay) {parent=value;}
    }
    public class Mesh:Object
    {
        public static int Created; public string name;public Vector3[] vertices;public int[] triangles;
        public Mesh(){Created++;} public void RecalculateNormals(){} public void RecalculateBounds(){}
    }
    public class Material:Object{} public class AudioClip:Object{}
    public class MeshFilter:Component {public Mesh sharedMesh;}
    public class MeshRenderer:Component {public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;}
    public struct Vector3
    {
        public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}
        public static Vector3 one => new Vector3(1,1,1);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
    }
    public static class Mathf { public static float Max(float a,float b)=>Math.Max(a,b);public static float Abs(float v)=>Math.Abs(v);}
    public static class Time {public static float unscaledTime,deltaTime,timeScale=1f;}
    public static class Application {public static bool isFocused=true;}
    public static class PlayerPrefs
    {
        public static readonly Dictionary<string,string> Values=new Dictionary<string,string>();
        public static int Saves,Sets;public static bool FailSave,FailRead;
        public static string GetString(string key,string fallback="") {if(FailRead)throw new Exception("read failed");return Values.TryGetValue(key,out string value)?value:fallback;}
        public static bool HasKey(string key)=>Values.ContainsKey(key);
        public static void DeleteKey(string key)=>Values.Remove(key);
        public static void SetString(string key,string value){Sets++;Values[key]=value;}
        public static void Save(){if(FailSave)throw new Exception("save failed");Saves++;}
        public static void Reset(){Values.Clear();Saves=Sets=0;FailSave=FailRead=false;}
    }
    public class DisallowMultipleComponent:Attribute{}
    public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int v){}}
    public class HeaderAttribute:Attribute{public HeaderAttribute(string v){}}
    public class TooltipAttribute:Attribute{public TooltipAttribute(string v){}}
    public class MinAttribute:Attribute{public MinAttribute(float v){}}
    public class RangeAttribute:Attribute{public RangeAttribute(float a,float b){}}
    public class TextAreaAttribute:Attribute{}
    public class CreateAssetMenuAttribute:Attribute{public string menuName,fileName;}
}
namespace UnityEngine.Rendering { public enum ShadowCastingMode { Off,On } }
namespace IntercomMvp
{
    public class IntercomWeaponPoseProfile{}
    public class IntercomSimpleShooter:UnityEngine.MonoBehaviour
    {public IntercomWeaponDefinition weapon;public IntercomWeaponDefinition Weapon=>weapon;public UnityEngine.Transform muzzleLeft,muzzleRight;}
    public class IntercomPlayerMotor:UnityEngine.MonoBehaviour{public uint RespawnVersion;}
    public class IntercomPlayerInput:UnityEngine.MonoBehaviour{public bool ReloadPressed;}
    public class IntercomMissionDirector:UnityEngine.MonoBehaviour
    {
        public IntercomPlayerMotor playerMotor;public bool HasRunSummary;public IntercomMissionRunSummary LastRunSummary;
        public uint ResultRevision,RunRevision;public IntercomMissionPhase Phase=IntercomMissionPhase.Selection;
    }
}
