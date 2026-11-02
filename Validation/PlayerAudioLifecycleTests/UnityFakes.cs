using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class Object { public static void Destroy(Object o) {} }
 public class ScriptableObject : Object {}
 public class Component : Object { public GameObject gameObject; public Transform transform => gameObject.transform; public T GetComponent<T>() where T:class => gameObject.GetComponent<T>(); public T GetComponentInChildren<T>() where T:class => GetComponent<T>(); public T GetComponentInParent<T>() where T:class {for(var t=transform;t!=null;t=t.parent){var c=t.owner.GetComponent<T>();if(c!=null)return c;}return null;} }
 public class Behaviour : Component { public bool isActiveAndEnabled=true; }
 public class MonoBehaviour : Behaviour {}
 public class GameObject : Object {
  public static int Created; public Transform transform=new Transform(); private Dictionary<Type,Component> components=new Dictionary<Type,Component>();
  public GameObject(string name="") { Created++;transform.owner=this; }
  public T AddComponent<T>() where T:Component,new(){var c=new T {gameObject=this};components[typeof(T)]=c;return c;}
  public T GetComponent<T>() where T:class { return components.TryGetValue(typeof(T),out var c)?c as T:null; }
 }
 public class Transform {public Vector3 position; public GameObject owner; public T GetComponent<T>() where T:class=>owner.GetComponent<T>(); public Transform parent; public void SetParent(Transform t,bool world){parent=t;} public bool IsChildOf(Transform root){for(var t=this;t!=null;t=t.parent)if(t==root)return true;return false;} }
 public struct Vector3 {public float x,y,z; public static Vector3 up=>new Vector3{y=1};public static Vector3 down=>new Vector3{y=-1}; public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3{x=a.x+b.x,y=a.y+b.y,z=a.z+b.z};public static Vector3 operator*(Vector3 a,float s)=>new Vector3{x=a.x*s,y=a.y*s,z=a.z*s}; public static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));}
 public class Collider:Component {public bool isTrigger;}
 public struct RaycastHit {public Collider collider;public Vector3 point,normal;public float distance;}
 public enum QueryTriggerInteraction {Ignore}
 public static class Physics {public static RaycastHit[] Hits=Array.Empty<RaycastHit>();public static int RaycastNonAlloc(Vector3 origin,Vector3 direction,RaycastHit[] results,float distance,int mask,QueryTriggerInteraction query){int count=Math.Min(Hits.Length,results.Length);Array.Copy(Hits,results,count);return count;} }
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
 public static class Time {public static float timeScale=1f,unscaledDeltaTime=1f/60,unscaledTime;}
 public class RuntimeAnimatorController : Object {}
 public class Animator : Behaviour {public float speed=1;public RuntimeAnimatorController runtimeAnimatorController=new RuntimeAnimatorController();public bool transition;public AnimatorStateInfo state;public bool IsInTransition(int layer)=>transition;public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer)=>state;}
 public struct AnimatorStateInfo {public float normalizedTime;public int fullPathHash;}
 public static class Mathf {public static int Clamp(int x,int a,int b)=>Math.Clamp(x,a,b);public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);public static float Clamp01(float x)=>Math.Clamp(x,0f,1f);public static float Clamp(float x,float a,float b)=>Math.Clamp(x,a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float MoveTowards(float a,float b,float d)=>a<b?Math.Min(a+d,b):Math.Max(a-d,b);}
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
 public class IntercomGroundFeedback:UnityEngine.Behaviour {public bool ContactsAvailable=true;public bool IsTurnFootstep;public bool ProvidesFootContacts=>isActiveAndEnabled&&ContactsAvailable;public event Action<UnityEngine.Collider,UnityEngine.Vector3,float,bool> Footstep;public void Emit(UnityEngine.Collider c,float strength=1f){Footstep?.Invoke(c,new UnityEngine.Vector3{x=2,y=.01f,z=3},strength,true);}public void EmitTurn(UnityEngine.Collider c){IsTurnFootstep=true;try{Emit(c,.42f);}finally{IsTurnFootstep=false;}}}
 public class IntercomPlayerMotor : UnityEngine.Behaviour {public float ActualPlanarSpeed;public bool IsGrounded=true;}
 public class IntercomWeaponDefinition {public UnityEngine.AudioClip[] shotClips;public float shotVolume,shotPitchVariation;public UnityEngine.AudioClip reloadStartClip,reloadFinishClip;public float reloadVolume=.36f;}
 public class IntercomWeaponMagazine:UnityEngine.Behaviour {public event Action ReloadStarted,ReloadCompleted;public void StartReload()=>ReloadStarted?.Invoke();public void FinishReload()=>ReloadCompleted?.Invoke();}
 public class IntercomSimpleShooter : UnityEngine.Behaviour {
  public IntercomWeaponDefinition Weapon {get;set;}
  public event Action<UnityEngine.Vector3,int> VolleyFired;
  public event Action<UnityEngine.Vector3,UnityEngine.Vector3,bool> ProjectileHit;
  public event Action<IntercomProjectileImpact> DetailedProjectileHit;
  public void Hit(bool target=false,float distance=0){HitSurface(null,target,distance);}
  public void HitSurface(UnityEngine.Collider collider,bool target=false,float distance=0){var point=new UnityEngine.Vector3{x=distance};var normal=new UnityEngine.Vector3{y=1};DetailedProjectileHit?.Invoke(IntercomProjectileImpact.Resolve(collider,point,normal,target));ProjectileHit?.Invoke(point,normal,target);}
  public void Emit(int count=2){VolleyFired?.Invoke(new UnityEngine.Vector3(),count);}
 }
}
