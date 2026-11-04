// Narrow deterministic stand-ins: compiles real controller/factory; does not simulate Unity or PhysX.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using N=System.Numerics;
namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static readonly List<Object> Destroyed = new List<Object>();
        public static readonly List<bool> DestroyedColliderWasEnabled = new List<bool>();
        public static void Destroy(Object item)
        {
            if(item is Collider c) DestroyedColliderWasEnabled.Add(c.enabled);
            if(item is GameObject go) {go.SetActive(false);foreach(var child in go.transform.children.ToArray())Destroy(child.gameObject);foreach(var component in go.components.ToArray()) Lifecycle(component,"OnDestroy");}
            if(item is Component componentItem)componentItem.gameObject.components.Remove(componentItem);
            Destroyed.Add(item);
        }
        public static int InstantiateCalls;
        public static GameObject Instantiate(GameObject original,Transform parent,bool worldPositionStays)
        {
            InstantiateCalls++;
            var copy=new GameObject(original.name+" clone");copy.SetActive(false);
            copy.transform.SetParent(parent,worldPositionStays);
            copy.transform.localPosition=original.transform.localPosition;copy.transform.localRotation=original.transform.localRotation;copy.transform.localScale=original.transform.localScale;
            foreach(var c in original.components)
            {
                if(c is Animator a){var n=copy.AddComponent<Animator>();n.runtimeAnimatorController=a.runtimeAnimatorController;n.isInitialized=a.isInitialized;n.applyRootMotion=a.applyRootMotion;n.speed=a.speed;}
                else if(c is Renderer r){var n=copy.AddComponent<MeshRenderer>();n.sharedMaterial=r.sharedMaterial;var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);n.SetPropertyBlock(block);}
                else if(c is Collider collider){var n=copy.AddComponent<Collider>();n.enabled=collider.enabled;}
            }
            foreach(var child in original.transform.children)Instantiate(child.gameObject,copy.transform,false);
            copy.SetActive(original.activeSelf);return copy;
        }
        internal static void Lifecycle(object item,string name)
        {
            var method=item.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance);
            if(method!=null)try{method.Invoke(item,null);}catch(TargetInvocationException e){throw e.InnerException;}
        }
    }
    public class ScriptableObject:Object { }
    public class GameObject:Object
    {
        public static readonly List<GameObject> Created=new List<GameObject>();
        public static Action<Component> BeforeAwake;
        public readonly List<Component> components=new List<Component>();
        public readonly Transform transform;
        public bool activeSelf {get;private set;}=true;
        public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
        public GameObject(string name="") {this.name=name;transform=new Transform{gameObject=this};components.Add(transform);Created.Add(this);}
        public T AddComponent<T>() where T:Component,new()
        {var c=new T{gameObject=this};components.Add(c);if(activeInHierarchy)Activate(c);return c;}
        public T GetComponent<T>() =>components.OfType<T>().FirstOrDefault();
        public T GetComponentInChildren<T>(bool includeInactive=false)
        {
            if(!includeInactive&&!activeInHierarchy)return default;
            var own=GetComponent<T>();if(own is not null)return own;
            foreach(var child in transform.children){var found=child.gameObject.GetComponentInChildren<T>(includeInactive);if(found is not null)return found;}
            return default;
        }

        public T GetComponentInParent<T>()=>transform.GetComponentInParent<T>();
        public void SetActive(bool value)
        {
            if(activeSelf==value)return;
            bool was=activeInHierarchy;activeSelf=value;
            if(was==activeInHierarchy)return;
            NotifyTree(activeInHierarchy);
        }
        private void NotifyTree(bool now)
        {
            foreach(var c in components.ToArray())
            {if(now)Activate(c);else if(c.awakened&&(c is not Behaviour b||b.enabled))Lifecycle(c,"OnDisable");}
            foreach(var t in transform.children)if(t.gameObject.activeSelf)t.gameObject.NotifyTree(now);
        }
        private static void Activate(Component c)
        {
            if(!c.awakened){c.awakened=true;BeforeAwake?.Invoke(c);Lifecycle(c,"Awake");}
            if(c is not Behaviour b||b.enabled)Lifecycle(c,"OnEnable");
        }
        public static GameObject CreatePrimitive(PrimitiveType kind)
        {
            var o=new GameObject(kind.ToString());o.AddComponent<MeshFilter>();o.AddComponent<MeshRenderer>();o.AddComponent<Collider>();return o;
        }
    }
    public class Component:Object
    {
        public GameObject gameObject;internal bool awakened;
        public Transform transform=>gameObject.transform;
        public T GetComponent<T>()=>gameObject.GetComponent<T>();
        public T GetComponentInParent<T>()
        {for(Transform t=transform;t!=null;t=t.parent){var c=t.GetComponent<T>();if(c is not null)return c;}return default;}
    }
    public class Behaviour:Component
    {
        private bool on=true;
        public bool enabled {get=>on;set{if(on==value)return;on=value;if(awakened&&gameObject.activeInHierarchy)Lifecycle(this,value?"OnEnable":"OnDisable");}}
        public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;
    }
    public class MonoBehaviour:Behaviour { }
    public enum AnimatorCullingMode {AlwaysAnimate}
    public class RuntimeAnimatorController:Object {}
    public class Animator:Behaviour
    {
        public RuntimeAnimatorController runtimeAnimatorController;
        public bool applyRootMotion,isInitialized=true;public float speed=1f;
        public AnimatorCullingMode cullingMode;public int LastState,PlayCalls,UpdateCalls;public float LastTime;
        public readonly List<(int state,float time)> Samples=new List<(int,float)>();
        public static int StringToHash(string s)=>s.GetHashCode();
        public readonly HashSet<int> MissingStates=new HashSet<int>();
        public Action<Animator> OnSample;
        public bool HasState(int layer,int hash)=>!MissingStates.Contains(hash);
        public void Play(int state,int layer,float time){LastState=state;LastTime=time;PlayCalls++;Samples.Add((state,time));}
        public void Update(float delta){if(!isActiveAndEnabled||!isInitialized)throw new Exception("Uninitialized/inactive Animator evaluation");UpdateCalls++;OnSample?.Invoke(this);}
    }

    public class Transform:Component
    {
        public Transform parent; public readonly List<Transform> children=new List<Transform>();
        public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation=Quaternion.identity;
        public Vector3 position {get=>parent==null?localPosition:parent.position+parent.rotation*localPosition;set=>localPosition=parent==null?value:Quaternion.Inverse(parent.rotation)*(value-parent.position);}
        public Quaternion rotation {get=>parent==null?localRotation:parent.rotation*localRotation;set=>localRotation=parent==null?value:Quaternion.Inverse(parent.rotation)*value;}
        public Vector3 forward=>rotation*Vector3.forward;
        public void SetParent(Transform p,bool worldPositionStays=true){var old=position;parent?.children.Remove(this);parent=p;p?.children.Add(this);if(worldPositionStays)position=old;}
        public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}
        public bool IsChildOf(Transform ancestor){for(var t=this;t!=null;t=t.parent)if(t==ancestor)return true;return false;}
    }
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        internal N.Vector3 Value=>new N.Vector3(x,y,z);internal static Vector3 From(N.Vector3 v)=>new Vector3(v.X,v.Y,v.Z);
        public static Vector3 zero=>default;public static Vector3 one=>new Vector3(1,1,1);public static Vector3 up=>new Vector3(0,1,0);public static Vector3 forward=>new Vector3(0,0,1);
        public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);public Vector3 normalized=>magnitude>.000001f?this/magnitude:zero;
        public static Vector3 operator -(Vector3 a)=>From(-a.Value);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>From(a.Value+b.Value);public static Vector3 operator -(Vector3 a,Vector3 b)=>From(a.Value-b.Value);
        public static Vector3 operator *(Vector3 a,float b)=>From(a.Value*b);public static Vector3 operator /(Vector3 a,float b)=>From(a.Value/b);
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Math.Clamp(t,0,1);
        public static Vector3 ClampMagnitude(Vector3 a,float b)=>a.magnitude>b?a.normalized*b:a;
        public static Vector3 MoveTowards(Vector3 a,Vector3 b,float d){var delta=b-a;return delta.magnitude<=d?b:a+delta.normalized*d;}
    }
    public struct Quaternion
    {
        private N.Quaternion value;private static Quaternion From(N.Quaternion value)=>new Quaternion{value=value};
        public static Quaternion identity=>From(N.Quaternion.Identity);
        public static Quaternion operator *(Quaternion a,Quaternion b)=>From(a.value*b.value);
        public static Vector3 operator *(Quaternion a,Vector3 b)=>Vector3.From(N.Vector3.Transform(b.Value,a.value));
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t)=>From(N.Quaternion.Slerp(a.value,b.value,Math.Clamp(t,0,1)));
        public static Quaternion Inverse(Quaternion q)=>From(N.Quaternion.Inverse(q.value));
        public static Quaternion Euler(float x,float y,float z)=>From(N.Quaternion.CreateFromYawPitchRoll(y*Mathf.Deg2Rad,x*Mathf.Deg2Rad,z*Mathf.Deg2Rad));
        public static Quaternion LookRotation(Vector3 forward,Vector3 up)=>Euler(0,(float)Math.Atan2(forward.x,forward.z)/Mathf.Deg2Rad,0);
        public static Quaternion RotateTowards(Quaternion a,Quaternion b,float degrees)
        {float dot=Math.Clamp(Math.Abs(N.Quaternion.Dot(a.value,b.value)),0,1);float angle=2*(float)Math.Acos(dot)/Mathf.Deg2Rad;return angle<.00001f?b:From(N.Quaternion.Slerp(a.value,b.value,Math.Min(1,degrees/angle)));}
    }
    public class Collider:Behaviour {public bool isTrigger;}
    public enum CollisionFlags {None=0,Sides=1,Above=2,Below=4}
    public class CharacterController:Collider
    {
        public float height,radius,skinWidth,stepOffset,slopeLimit,minMoveDistance;public Vector3 center;public bool isGrounded=true;
        public Vector3 LastMove;public int MoveCalls;public bool BlockHorizontal;
        public CollisionFlags Move(Vector3 delta)
        {if(!enabled || !gameObject.activeInHierarchy)throw new InvalidOperationException("CharacterController.Move called on inactive controller");LastMove=delta;MoveCalls++;var pos=transform.position+delta;if(BlockHorizontal){pos.x=transform.position.x;pos.z=transform.position.z;}if(pos.y<=0){pos.y=0;isGrounded=true;}else isGrounded=false;transform.position=pos;return isGrounded?CollisionFlags.Below:CollisionFlags.None;}
    }
    public struct RaycastHit {public Collider collider;}
    public enum QueryTriggerInteraction {Ignore}
    public static class Physics
    {
        public static Func<Vector3,Vector3,float,Collider[]> Hits;
        public static int RayCalls;
        public static int RaycastNonAlloc(Vector3 origin,Vector3 direction,RaycastHit[] buffer,float length,int mask,QueryTriggerInteraction triggers)
        {RayCalls++;var hits=Hits?.Invoke(origin,direction,length)??Array.Empty<Collider>();int count=Math.Min(hits.Length,buffer.Length);for(int i=0;i<count;i++)buffer[i]=new RaycastHit{collider=hits[i]};return count;}
    }
    public class Material:Object {public static int Created;public Material(){Created++;}public bool HasProperty(int id)=>false;public Color GetColor(int id)=>new Color(1,1,1,1);}
    public struct Color
    {
        public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color white=>new Color(1,1,1,1);public static Color Lerp(Color a,Color b,float t){t=Math.Clamp(t,0,1);return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
    }
    public class MaterialPropertyBlock
    {
        public readonly Dictionary<int,Color> Colors=new Dictionary<int,Color>();public bool isEmpty=>Colors.Count==0;public bool HasColor(int id)=>Colors.ContainsKey(id);public Color GetColor(int id)=>Colors.TryGetValue(id,out var c)?c:default;public void SetColor(int id,Color color){Colors[id]=color;}
    }
    public class Renderer:Component
    {
        public int PropertyReads, PropertyWrites;
        public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;
        private readonly Dictionary<int,Color> colors=new Dictionary<int,Color>();
        public void GetPropertyBlock(MaterialPropertyBlock p){PropertyReads++;p.Colors.Clear();foreach(var x in colors)p.Colors[x.Key]=x.Value;}
        public void SetPropertyBlock(MaterialPropertyBlock p){PropertyWrites++;colors.Clear();if(p!=null)foreach(var x in p.Colors)colors[x.Key]=x.Value;}
    }
    public class MeshRenderer:Renderer{}
    public class SkinnedMeshRenderer:Renderer {public Transform[] bones;}
    public class MeshFilter:Component {public Mesh sharedMesh;}
    public class Mesh:Object {public Vector3[] vertices;public int[] triangles;public void RecalculateNormals(){}public void RecalculateBounds(){}}
    public enum PrimitiveType {Capsule,Cube,Cylinder}
    public static class Shader {public static int PropertyToID(string s)=>s.GetHashCode();}
    public class Camera:Behaviour {public static Camera main;public Vector3 WorldToScreenPoint(Vector3 p)=>new Vector3(p.x+100,p.y+100,1);}
    public class Texture2D:Object {public static readonly Texture2D whiteTexture=new Texture2D();}
    public struct Rect {public float x,y,width,height;public float xMax=>x+width;public float yMax=>y+height;public Rect(float x,float y,float width,float height){this.x=x;this.y=y;this.width=width;this.height=height;}}
    public enum TextAnchor{MiddleCenter}public enum FontStyle{Bold}public class GUIStyleState{public Color textColor;}public class GUIStyle{public TextAnchor alignment;public FontStyle fontStyle;public int fontSize;public GUIStyleState normal=new GUIStyleState();public GUIStyle(){}public GUIStyle(GUIStyle other){}} public class GUISkin{public GUIStyle label=new GUIStyle();}public static class GUI {public static GUISkin skin=new GUISkin();public static Color color;public static int depth;public static void DrawTexture(Rect r,Texture2D t){}public static void Label(Rect r,string text,GUIStyle style){}}
    public enum EventType {Repaint}
    public class Event {public static Event current=new Event();public EventType type;}
    public static class Screen {public static int width=1280,height=720;public static Rect safeArea=new Rect(0,0,1280,720);}
    public static class Time {public static float timeScale=1,deltaTime=.01f,time;}
    public static class Application {public static bool isFocused=true,isPlaying=true;}
    public static class Debug {public static void LogWarning(string message,Object context){}}
    public static class Mathf
    {
        public const float PI=(float)Math.PI,Deg2Rad=(float)(Math.PI/180);
        public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);
        public static int Clamp(int a,int min,int max)=>Math.Clamp(a,min,max);public static int Min(int a,int b)=>Math.Min(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static int RoundToInt(float a)=>(int)Math.Round(a);public static int CeilToInt(float a)=>(int)Math.Ceiling(a);public static float Abs(float a)=>Math.Abs(a);public static float Sqrt(float a)=>(float)Math.Sqrt(a);
        public static float Clamp(float a,float min,float max)=>Math.Clamp(a,min,max);public static float Clamp01(float a)=>Math.Clamp(a,0,1);
        public static float Sin(float a)=>(float)Math.Sin(a);public static float Cos(float a)=>(float)Math.Cos(a);
    }
    public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int order){}}
    public class DisallowMultipleComponent:Attribute{}
    public class RequireComponent:Attribute{public RequireComponent(Type a,Type b){}}
    public class TooltipAttribute:Attribute{public TooltipAttribute(string x){}}public class TextAreaAttribute:Attribute{}public class ContextMenu:Attribute{public ContextMenu(string label){}}
    public class MinAttribute:Attribute{public MinAttribute(float min){}}
    public class RangeAttribute:Attribute{public RangeAttribute(float min,float max){}}
    public class HeaderAttribute:Attribute{public HeaderAttribute(string label){}}
    public class CreateAssetMenuAttribute:Attribute{public string menuName,fileName;}
}
namespace UnityEngine.Rendering {public enum ShadowCastingMode{Off}}
namespace IntercomMvp
{
    using UnityEngine;
    public class IntercomPlayerMotor:MonoBehaviour {public uint RespawnVersion;}
    public enum IntercomSurfaceKind{Asphalt,Concrete,Metal}
    public enum IntercomImpactKind{Surface,Robot}
    public class IntercomFootstepSurface:MonoBehaviour {public IntercomSurfaceKind kind;public IntercomImpactKind impactKind;public float impactVolume,impactPitch;}
    public enum IntercomNavResult{Success,Unreachable,Deferred}
    public class IntercomNpcNavigation:MonoBehaviour
    {
        public uint Version;public bool IsReady=true;public void RebuildObstacleCache(){IsReady=true;}public IntercomNavResult NextResult=IntercomNavResult.Success;
        public Func<Vector3,Vector3,bool> CheckSegment;public Func<Vector3,Vector3,Vector3[]> Path;public int PathRequests,SegmentRequests;
        public IntercomNavResult TryFindPath(Vector3 start,Vector3 goal,List<Vector3> output)
        {PathRequests++;if(NextResult==IntercomNavResult.Success){output.Clear();output.AddRange(Path?.Invoke(start,goal)??new[]{goal});}else if(NextResult==IntercomNavResult.Unreachable)output.Clear();return NextResult;}
        public bool SegmentClear(Vector3 start,Vector3 end){SegmentRequests++;return CheckSegment?.Invoke(start,end)??true;}
        public bool TryProject(Vector3 point,out Vector3 projected){projected=point;return true;}
    }
}

namespace UnityEngine.Events {public class UnityEvent {public void Invoke(){}}}
namespace IntercomMvp {
 public class IntercomPlayerInput:UnityEngine.MonoBehaviour {public IntercomMissionDirector mission;public IntercomPlayerVitality vitality;public int cancellations;public void CancelUntilRelease(){cancellations++;}}
 public class IntercomPlayerVitality:UnityEngine.MonoBehaviour {public IntercomHealth health;public IntercomPlayerMotor motor;public IntercomPlayerInput input;public bool MissionBlocked;object owner;public bool CanSetMissionBlocked(object next)=>owner==null||ReferenceEquals(owner,next);public bool SetMissionBlocked(object next,bool blocked){if(!CanSetMissionBlocked(next))return false;owner=blocked?next:null;MissionBlocked=blocked;return true;}public bool Retry(){motor.RespawnVersion++;health.ResetHealth();return true;}}
 public class IntercomRescueAlly:UnityEngine.MonoBehaviour {public bool Recruited,WaitingForPlayer,Suspended;public IntercomHealth Health;public static IntercomRescueAlly Create(IntercomNpcNavigation n,IntercomHealth h,IntercomNpcDefinition d,IntercomMissionDefinition m,UnityEngine.Vector3 p)=>throw new System.NotImplementedException();public void ResetForMission(IntercomMissionDefinition d,UnityEngine.Vector3 p){}}
}

namespace IntercomMvp {
 public class IntercomRobotDeathEffects:UnityEngine.MonoBehaviour {
  public IntercomNpcDefinition definition;public int EmitCalls,ClearCalls;public bool Ready=true;
  public bool Emit(UnityEngine.Vector3 p,UnityEngine.Quaternion q,uint seed){EmitCalls++;return Ready;}
  public void Clear(){ClearCalls++;}
 }
}
