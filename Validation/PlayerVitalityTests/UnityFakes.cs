// Deliberately narrow stand-ins: no PhysX, Animator graph, GUI or Unity lifecycle execution.
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
    public class Object { }
    public class GameObject : Object
    {
        public readonly List<Component> Components = new List<Component>();
        public readonly Transform transform;
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public GameObject() { transform = new Transform { gameObject = this }; Components.Add(transform); }
        public T AddComponent<T>() where T : Component, new() { var component = new T { gameObject = this }; Components.Add(component); return component; }
        public T GetComponent<T>() where T : class => Components.OfType<T>().FirstOrDefault();
        public T GetComponentInParent<T>() where T : class { for(var p=transform; p!=null; p=p.parent) { var found=p.gameObject.GetComponent<T>(); if(found!=null)return found; }return null; }
        public void SetActive(bool active) { activeSelf=active; }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>() where T : class => gameObject.GetComponentInParent<T>();
        public T GetComponentInChildren<T>() where T : class => GetComponent<T>();
    }
    public class Behaviour : Component { public bool enabled=true; public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy; }
    public class MonoBehaviour : Behaviour { }
    public class Transform : Component
    {
        public Transform parent;
        public Vector3 position, localPosition, localScale=Vector3.one;
        public bool IsChildOf(Transform ancestor) { for(var p=this;p!=null;p=p.parent)if(p==ancestor)return true;return false; }
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>default;
        public static Vector3 one=>new Vector3(1,1,1);
        public static Vector3 forward=>new Vector3(0,0,1);
    }
    public class Animator : Behaviour
    {
        public object runtimeAnimatorController=new object();
        public float speed=1f;
        public readonly Dictionary<int,float> Values=new Dictionary<int,float>();
        public static int StringToHash(string name)=>name.GetHashCode();
        public void SetFloat(int key,float value) {Values[key]=value;}
        public void SetFloat(string key,float value) {SetFloat(StringToHash(key),value);}
        public float GetFloat(string key)=>Values.TryGetValue(StringToHash(key),out var v)?v:0;
    }
    public static class Application { public static bool isPlaying=true; }
    public static class Time { public static float time,unscaledTime,deltaTime=1f/60,timeScale=1f; }
    public static class Mathf
    {
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static int Max(int a,int b)=>Math.Max(a,b);
        public static float Min(float a,float b)=>Math.Min(a,b);
        public static float Clamp01(float value)=>Math.Clamp(value,0,1);
        public static float Clamp(float value,float a,float b)=>Math.Clamp(value,a,b);
    }
    public class DisallowMultipleComponent:Attribute { }
    public class MinAttribute:Attribute { public MinAttribute(float value) { } }
    public class TooltipAttribute:Attribute { public TooltipAttribute(string value) { } }
    public class HeaderAttribute:Attribute { public HeaderAttribute(string value) { } }
    public class RangeAttribute:Attribute { public RangeAttribute(float a,float b) { } }
    public class ContextMenu:Attribute { public ContextMenu(string label) { } }
    public class DefaultExecutionOrder:Attribute { public DefaultExecutionOrder(int order) { } }
    public class RequireComponent:Attribute { public RequireComponent(Type type) { } }
}
namespace UnityEngine.Events
{
    public class UnityEvent
    {
        public event Action Listeners;
        public void Invoke() {Listeners?.Invoke();}
        public void AddListener(Action listener) {Listeners+=listener;}
    }
}
namespace IntercomMvp
{
    public class IntercomPlayerInput:UnityEngine.MonoBehaviour
    {
        public bool ResetPressed, RetryPressed;
        public uint CancellationVersion;
        public void CancelUntilRelease() {CancellationVersion++;ResetPressed=RetryPressed=false;}
        public void ClearInput() {ResetPressed=RetryPressed=false;}
    }
    public class IntercomPlayerMotor:UnityEngine.MonoBehaviour
    {
        public uint RespawnVersion;
        public UnityEngine.Animator animator;
        public IntercomPlayerInput inputSource;
        public int ResetCalls;
        public void ResetToSpawn() {ResetCalls++;RespawnVersion++;inputSource?.CancelUntilRelease();}
    }
    public class IntercomSimpleShooter:UnityEngine.MonoBehaviour { }
    public class IntercomWeaponPose:UnityEngine.MonoBehaviour
    {
        public int Resets;
        public void ResetPosture() {Resets++;}
    }
}
