using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class Object{public static void Destroy(object o){}}
 public class MonoBehaviour{public bool isActiveAndEnabled=true;public Transform transform=new Transform();public object attached;public T GetComponent<T>()where T:class=>attached as T;public static void Destroy(object o){}}
 public class Transform{public Vector3 position,forward=Vector3.forward;public T GetComponent<T>()where T:class=>null;public Vector3 TransformPoint(Vector3 p)=>position+p;}
 public class CharacterController{public Vector3 center;}
 public class Camera{public static Camera main=new Camera();public Transform transform=new Transform();public Ray ScreenPointToRay(Vector2 p)=>new Ray(new Vector3(p.x/100,10,p.y/100),new Vector3(0,-1,1));}
 public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int x){}}public class DisallowMultipleComponent:Attribute{}
 public class TooltipAttribute:Attribute{public TooltipAttribute(string x){}}public class MinAttribute:Attribute{public MinAttribute(float v){}}public class HeaderAttribute:Attribute{public HeaderAttribute(string x){}}public class RangeAttribute:Attribute{public RangeAttribute(float x,float y){}}
 public static class Application{public static bool isMobilePlatform,isPlaying=true;}
 public static class Time{public static float timeScale=1,unscaledDeltaTime=.016f;}
 public enum ScreenOrientation{LandscapeLeft,Portrait}
 public static class Screen{public static int width=1920,height=1080;public static Rect safeArea=new Rect(0,0,1920,1080);public static ScreenOrientation orientation;}
 public struct Vector2{
  public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>new Vector2();public float sqrMagnitude=>x*x+y*y;
  public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
  public void Normalize(){float n=(float)Math.Sqrt(sqrMagnitude);if(n>0){x/=n;y/=n;}}
  public static Vector2 ClampMagnitude(Vector2 p,float n){if(p.sqrMagnitude>n*n){p.Normalize();p=p*n;}return p;}
 }
 public struct Vector3{
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 zero=>new Vector3();public static Vector3 up=>new Vector3(0,1,0);public static Vector3 forward=>new Vector3(0,0,1);
  public float sqrMagnitude=>x*x+y*y+z*z;public Vector3 normalized{get{var v=this;v.Normalize();return v;}}public void Normalize(){float n=(float)Math.Sqrt(sqrMagnitude);if(n>0){x/=n;y/=n;z/=n;}}
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3 operator*(Vector3 a,float n)=>new Vector3(a.x*n,a.y*n,a.z*n);
  public static Vector3 ProjectOnPlane(Vector3 a,Vector3 b)=>a-b*(Dot(a,b)/b.sqrMagnitude);public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;public static Vector3 Cross(Vector3 a,Vector3 b)=>new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
 }
 public struct Ray{public Vector3 origin,direction;public Ray(Vector3 a,Vector3 b){origin=a;direction=b;}public Vector3 GetPoint(float d)=>origin+direction*d;}
 public struct Plane{Vector3 normal,point;public Plane(Vector3 n,Vector3 p){normal=n;point=p;}public bool Raycast(Ray r,out float d){float divisor=Vector3.Dot(normal,r.direction);d=divisor==0?0:Vector3.Dot(normal,point-r.origin)/divisor;return divisor!=0&&d>=0;}}
 public struct Rect{
  public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
  public float xMin=>x;public float yMin=>y;public float xMax=>x+width;public float yMax=>y+height;
  public static bool operator==(Rect a,Rect b)=>a.x==b.x&&a.y==b.y&&a.width==b.width&&a.height==b.height;public static bool operator!=(Rect a,Rect b)=>!(a==b);public override bool Equals(object o)=>o is Rect r&&this==r;public override int GetHashCode()=>x.GetHashCode();
 }
 public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Sqrt(float a)=>(float)Math.Sqrt(a);public static float Clamp01(float a)=>Math.Max(0,Math.Min(1,a));public static int RoundToInt(float a)=>(int)Math.Round(a);public static int CeilToInt(float a)=>(int)Math.Ceiling(a);}
 public enum KeyCode{A,D,W,S,LeftArrow,RightArrow,UpArrow,DownArrow,Space,R,Backspace,Escape}
 public enum TouchPhase{Began,Moved,Stationary,Ended,Canceled}
 public struct Touch{public int fingerId;public Vector2 position;public TouchPhase phase;}
 public static class Input{
  public static bool multiTouchEnabled,MouseHeld,MouseDown,MouseUp;public static Vector2 mousePosition=new Vector2(600,400);public static Touch[] touches=Array.Empty<Touch>();public static HashSet<KeyCode> held=new HashSet<KeyCode>(),down=new HashSet<KeyCode>();
  public static int touchCount=>touches.Length;public static Touch GetTouch(int i)=>touches[i];public static bool GetMouseButton(int n)=>MouseHeld;public static bool GetMouseButtonDown(int n)=>MouseDown;public static bool GetMouseButtonUp(int n)=>MouseUp;public static bool GetKey(KeyCode k)=>held.Contains(k);public static bool GetKeyDown(KeyCode k)=>down.Contains(k);
  public static void Clear(){MouseHeld=MouseDown=MouseUp=false;held.Clear();down.Clear();touches=Array.Empty<Touch>();}
 }
 public class Texture{}public enum TextureFormat{RGBA32}public enum HideFlags{HideAndDontSave}public enum TextureWrapMode{Clamp}public enum FilterMode{Bilinear}
 public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1,1);}
 public struct Color32{public byte r,g,b,a;public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public class Texture2D:Texture{public string name="white";public int width,height;public Color32[] pixels;public HideFlags hideFlags;public TextureWrapMode wrapMode;public FilterMode filterMode;public static Texture2D whiteTexture=new Texture2D(1,1,TextureFormat.RGBA32,false);public Texture2D(int w,int h,TextureFormat f,bool m){width=w;height=h;pixels=new Color32[w*h];if(w==1&&h==1)pixels[0]=new Color32(255,255,255,255);}public void SetPixels32(Color32[] p){pixels=p;}public void Apply(bool a,bool b){}}
 public struct Matrix4x4{public static Matrix4x4 identity=>new Matrix4x4();}public enum EventType{Repaint}public class Event{public static Event current=new Event();public EventType type=EventType.Repaint;}
 public enum TextAnchor{MiddleCenter,MiddleLeft}public enum FontStyle{Normal,Bold}public class GUIStyleState{public Color textColor;}
 public class GUIStyle{public bool wordWrap,richText;public TextAnchor alignment;public FontStyle fontStyle;public int fontSize;public GUIStyleState normal=new GUIStyleState();public GUIStyle(){}public GUIStyle(GUIStyle other){alignment=other.alignment;fontStyle=other.fontStyle;wordWrap=other.wordWrap;richText=other.richText;}}
 public class GUISkin{public GUIStyle label=new GUIStyle();}
 public static class GUI{
  public static Color color;public static Matrix4x4 matrix;public static int depth;public static GUISkin skin=new GUISkin();public static List<string> labels=new List<string>();
  public static bool Record;public static List<object> commands=new List<object>();public static Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
  static object Bounds(Rect r)=>new[]{r.x,r.y,r.width,r.height};static float[] Rgba(Color c)=>new[]{c.r,c.g,c.b,c.a};
  public static void DrawTexture(Rect r,Texture t){if(!Record)return;var texture=(Texture2D)t;textures[texture.name]=texture;commands.Add(new{kind="texture",rect=Bounds(r),texture=texture.name,color=Rgba(color)});}
  public static void Label(Rect r,string s,GUIStyle style){labels.Add(s);if(Record)commands.Add(new{kind="text",rect=Bounds(r),text=s,fontSize=style.fontSize,bold=style.fontStyle==FontStyle.Bold,align=style.alignment.ToString(),wrap=style.wordWrap,color=Rgba(style.normal.textColor)});}
  public static void Save(string path){var images=new List<object>();foreach(var pair in textures){var t=pair.Value;if(pair.Key=="RobotPortrait"||pair.Key=="PistolPortrait")continue;var bytes=new byte[t.pixels.Length*4];for(int i=0;i<t.pixels.Length;i++){bytes[i*4]=t.pixels[i].r;bytes[i*4+1]=t.pixels[i].g;bytes[i*4+2]=t.pixels[i].b;bytes[i*4+3]=t.pixels[i].a;}images.Add(new{name=pair.Key,width=t.width,height=t.height,rgba=Convert.ToBase64String(bytes)});}System.IO.File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new{width=Screen.width,height=Screen.height,textures=images,commands=commands}));}
 }

}
namespace IntercomMvp {public class IntercomWeaponMagazine{public bool isActiveAndEnabled=true,IsReloading;public int CurrentAmmo=24,Capacity=24;public float ReloadProgress;}}

namespace IntercomMvp { public class IntercomHealth{public float Current=100,Maximum=100;} public class IntercomPlayerVitality{public IntercomHealth Health=new IntercomHealth();public bool IsDead;public float ShowHitRemaining;public int Retries;public bool Retry(){Retries++;IsDead=false;Health.Current=Health.Maximum;return true;}}}

namespace IntercomMvp {
 public enum IntercomMissionMode { Elimination, Rescue, FreePlay }
 public enum IntercomMissionPhase { Selection, Active, Victory, Defeat }
 public class IntercomMissionDirector {
  public bool isActiveAndEnabled=true;
  public IntercomMissionPhase Phase=IntercomMissionPhase.Selection;
  public IntercomMissionMode Mode;
  public bool BlocksGameplay=>Phase!=IntercomMissionPhase.Active;
  public int Kills=12,Goal=50,AliveEnemies=6,SpawnedEnemies=18,Wave=2;
  public bool IsAllyRecruited;
  public IntercomHealth AllyHealth;
  public string ObjectiveText="Задание",ResultText="Двор снова работает.",HudHint="";
  public int Starts,Retries,Selections;
  public bool StartMission(IntercomMissionMode mode){Starts++;Mode=mode;Phase=IntercomMissionPhase.Active;return true;}
  public bool RetryMission(){Retries++;Phase=IntercomMissionPhase.Active;return true;}
  public void ReturnToSelection(){Selections++;Phase=IntercomMissionPhase.Selection;}
 }
}

namespace IntercomMvp {
 public class IntercomPlayerMotor{public float moveSpeed=3.3f;}
 public class IntercomWeaponDefinition{public float Damage=15,Interval=.12f,ReloadSeconds=1.15f;public int MagazineCapacity=24;}
 public class IntercomFrontEndRoomCamera{public bool IsShown;public IntercomFrontEndSection Last;public int Shows,Hides;public void Show(IntercomFrontEndSection s){IsShown=true;Last=s;Shows++;}public void Hide(){IsShown=false;Hides++;}}
}
