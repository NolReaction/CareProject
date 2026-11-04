using System;
namespace UnityEngine {
 public class MonoBehaviour{public bool isActiveAndEnabled=true;}public class Camera{public bool enabled;public Transform transform=new Transform();}
 public class DisallowMultipleComponent:Attribute{}public class MinAttribute:Attribute{public MinAttribute(float x){}}
 public class Transform{public Vector3 position;public Quaternion rotation=Quaternion.identity;public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}}
 public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>new Vector3(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,a.z+(b.z-a.z)*t);}
 public struct Quaternion{public float x,y,z,w;public Quaternion(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}public static Quaternion identity=>new Quaternion(0,0,0,1);public static Quaternion Slerp(Quaternion a,Quaternion b,float t)=>new Quaternion(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,a.z+(b.z-a.z)*t,a.w+(b.w-a.w)*t);}
 public static class Time{public static float unscaledDeltaTime=1f/60;}public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static float Exp(float a)=>(float)Math.Exp(a);}
}
