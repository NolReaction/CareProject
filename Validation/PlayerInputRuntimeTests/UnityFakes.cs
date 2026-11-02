using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class MonoBehaviour{public Transform transform=new Transform();public object attached;public T GetComponent<T>()where T:class=>attached as T;public static void Destroy(object o){}}
 public class Transform{public Vector3 position,forward=Vector3.forward;public T GetComponent<T>()where T:class=>null;public Vector3 TransformPoint(Vector3 p)=>position+p;}
 public class CharacterController{public Vector3 center;}
 public class Camera{public static Camera main=new Camera();public Transform transform=new Transform();public Ray ScreenPointToRay(Vector2 p)=>new Ray(new Vector3(p.x/100,10,p.y/100),new Vector3(0,-1,1));}
 public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int x){}}public class DisallowMultipleComponent:Attribute{}
 public class HeaderAttribute:Attribute{public HeaderAttribute(string x){}}public class RangeAttribute:Attribute{public RangeAttribute(float x,float y){}}
 public static class Application{public static bool isMobilePlatform;}
 public static class Time{public static float timeScale=1;}
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
 public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Sqrt(float a)=>(float)Math.Sqrt(a);public static float Clamp01(float a)=>Math.Max(0,Math.Min(1,a));public static int RoundToInt(float a)=>(int)Math.Round(a);}
 public enum KeyCode{A,D,W,S,LeftArrow,RightArrow,UpArrow,DownArrow,Space,R,Backspace}
 public enum TouchPhase{Began,Moved,Stationary,Ended,Canceled}
 public struct Touch{public int fingerId;public Vector2 position;public TouchPhase phase;}
 public static class Input{
  public static bool multiTouchEnabled,MouseHeld,MouseDown,MouseUp;public static Vector2 mousePosition=new Vector2(600,400);public static Touch[] touches=Array.Empty<Touch>();public static HashSet<KeyCode> held=new HashSet<KeyCode>(),down=new HashSet<KeyCode>();
  public static int touchCount=>touches.Length;public static Touch GetTouch(int i)=>touches[i];public static bool GetMouseButton(int n)=>MouseHeld;public static bool GetMouseButtonDown(int n)=>MouseDown;public static bool GetMouseButtonUp(int n)=>MouseUp;public static bool GetKey(KeyCode k)=>held.Contains(k);public static bool GetKeyDown(KeyCode k)=>down.Contains(k);
  public static void Clear(){MouseHeld=MouseDown=MouseUp=false;held.Clear();down.Clear();touches=Array.Empty<Touch>();}
 }
 public class Texture{}public enum TextureFormat{RGBA32}public enum HideFlags{HideAndDontSave}public enum TextureWrapMode{Clamp}public enum FilterMode{Bilinear}
 public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1,1);}
 public struct Color32{public Color32(byte r,byte g,byte b,byte a){}}
 public class Texture2D:Texture{public string name;public HideFlags hideFlags;public TextureWrapMode wrapMode;public FilterMode filterMode;public static Texture2D whiteTexture=new Texture2D(1,1,TextureFormat.RGBA32,false);public Texture2D(int w,int h,TextureFormat f,bool m){}public void SetPixels32(Color32[] p){}public void Apply(bool a,bool b){}}
 public struct Matrix4x4{public static Matrix4x4 identity=>new Matrix4x4();}public enum EventType{Repaint}public class Event{public static Event current=new Event();public EventType type=EventType.Repaint;}
 public enum TextAnchor{MiddleCenter}public enum FontStyle{Bold}public class GUIStyleState{public Color textColor;}
 public class GUIStyle{public TextAnchor alignment;public FontStyle fontStyle;public int fontSize;public GUIStyleState normal=new GUIStyleState();public GUIStyle(){}public GUIStyle(GUIStyle other){}}
 public class GUISkin{public GUIStyle label=new GUIStyle();}
 public static class GUI{public static Color color;public static Matrix4x4 matrix;public static int depth;public static GUISkin skin=new GUISkin();public static List<string> labels=new List<string>();public static void DrawTexture(Rect r,Texture t){}public static void Label(Rect r,string s,GUIStyle style){labels.Add(s);}}
}
namespace IntercomMvp {public class IntercomWeaponMagazine{public bool isActiveAndEnabled=true,IsReloading;public int CurrentAmmo=24,Capacity=24;public float ReloadProgress;}}
