using System;
namespace UnityEngine {
 public class Object{} public class ScriptableObject:Object{} public class GameObject:Object{} public class Material:Object{} public class Mesh:Object{} public class AudioClip:Object{}
 public class MonoBehaviour {public bool isActiveAndEnabled=true;}
 public class DisallowMultipleComponent:Attribute{} public class ContextMenu:Attribute{public ContextMenu(string x){}}
 public class CreateAssetMenuAttribute:Attribute{public string menuName,fileName;}
 public class HeaderAttribute:Attribute{public HeaderAttribute(string x){}} public class TooltipAttribute:Attribute{public TooltipAttribute(string x){}}
 public class TextAreaAttribute:Attribute{} public class MinAttribute:Attribute{public MinAttribute(float x){}} public class RangeAttribute:Attribute{public RangeAttribute(float x,float y){}}
 public struct Vector3 {
 public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
 public static Vector3 up=>new Vector3(0,1,0);public static Vector3 forward=>new Vector3(0,0,1);
 public float sqrMagnitude=>x*x+y*y+z*z;public Vector3 normalized=>this/(float)Math.Sqrt(sqrMagnitude);
 public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator/(Vector3 a,float n)=>new Vector3(a.x/n,a.y/n,a.z/n);
 public static Vector3 ProjectOnPlane(Vector3 a,Vector3 n){float d=(a.x*n.x+a.y*n.y+a.z*n.z)/n.sqrMagnitude;return new Vector3(a.x-n.x*d,a.y-n.y*d,a.z-n.z*d);}
 }
}
namespace UnityEngine.Events {public class UnityEvent{public int Invocations;public void Invoke(){Invocations++;}}}
namespace IntercomMvp {public class IntercomWeaponPoseProfile{}}
