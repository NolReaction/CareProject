using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class Object { public static void Destroy(Object o) {} }
 public class ScriptableObject : Object {}
 public class Component : Object { public GameObject gameObject; public Transform transform => gameObject.transform; public T GetComponent<T>() where T:class => gameObject.GetComponent<T>(); public T GetComponentInChildren<T>() where T:class => GetComponent<T>(); }
 public class Behaviour : Component { public bool isActiveAndEnabled=true; }
 public class MonoBehaviour : Behaviour {}
 public class GameObject : Object {
  public static int Created; public Transform transform=new Transform(); private Dictionary<Type,Component> components=new Dictionary<Type,Component>();
  public GameObject(string name="") { Created++; }
  public T AddComponent<T>() where T:Component,new(){var c=new T {gameObject=this};components[typeof(T)]=c;return c;}
  public T GetComponent<T>() where T:class { return components.TryGetValue(typeof(T),out var c)?c as T:null; }
 }
 public class Transform {public Vector3 position; public void SetParent(Transform t,bool world){} }
 public struct Vector3 {public float x,y,z;}
 public class AudioClip : Object {}
 public class AudioSource : Behaviour {
  public AudioClip clip;public bool loop,playOnAwake,ignoreListenerPause;public float volume,pitch,spatialBlend,dopplerLevel;public int priority;
  public bool isPlaying; public bool paused;public int playCalls,stopCalls,pauseCalls,unpauseCalls;
  public void Play(){if(clip!=null){isPlaying=true;paused=false;playCalls++;}}
  public void Stop(){isPlaying=false;paused=false;stopCalls++;}
  public void Pause(){if(isPlaying){isPlaying=false;paused=true;pauseCalls++;}}
  public void UnPause(){if(paused){isPlaying=true;paused=false;unpauseCalls++;}}
 }
 public static class AudioListener {public static bool pause;}
 public static class Time {public static float timeScale=1f,unscaledDeltaTime=1f/60;}
 public class RuntimeAnimatorController : Object {}
 public class Animator : Behaviour {public float speed=1;public RuntimeAnimatorController runtimeAnimatorController=new RuntimeAnimatorController();public bool transition;public AnimatorStateInfo state;public bool IsInTransition(int layer)=>transition;public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer)=>state;}
 public struct AnimatorStateInfo {public float normalizedTime;public int fullPathHash;}
 public static class Mathf {public static float Clamp01(float x)=>Math.Clamp(x,0f,1f);public static float Clamp(float x,float a,float b)=>Math.Clamp(x,a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float MoveTowards(float a,float b,float d)=>a<b?Math.Min(a+d,b):Math.Max(a-d,b);}
 public static class Random {static System.Random r=new System.Random(4);public static int Range(int a,int b)=>r.Next(a,b);public static float Range(float a,float b)=>a+(float)r.NextDouble()*(b-a);}
 public class DisallowMultipleComponent:Attribute{}
 public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int x){}}
 public class CreateAssetMenuAttribute:Attribute{public string menuName,fileName;}
 public class HeaderAttribute:Attribute{public HeaderAttribute(string x){}}
 public class TooltipAttribute:Attribute{public TooltipAttribute(string x){}}
 public class RangeAttribute:Attribute{public RangeAttribute(float a,float b){}}
 public class MinAttribute:Attribute{public MinAttribute(float a){}}
}
namespace IntercomMvp {
 public class IntercomPlayerMotor : UnityEngine.Behaviour {public float ActualPlanarSpeed;public bool IsGrounded=true;}
 public class IntercomSimpleShooter : UnityEngine.Behaviour {public event Action<UnityEngine.Vector3,int> VolleyFired;public void Emit(int count=2){VolleyFired?.Invoke(new UnityEngine.Vector3(),count);}}
}
