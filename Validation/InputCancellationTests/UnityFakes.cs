// Narrow test doubles for executing the production pose driver's control flow.
// These do not validate Unity's real Animator implementation or engine APIs.
using System;
namespace UnityEngine {
 public class MonoBehaviour { public bool enabled=true; public object attachedComponent; public T GetComponent<T>() where T:class {return attachedComponent as T;} public T GetComponentInChildren<T>() where T:class {return null;} }
 public class RuntimeAnimatorController {}
 public class AnimatorOverrideController:RuntimeAnimatorController {}
 public class AnimationClip {public float length;}
 public class Animator {
  public RuntimeAnimatorController runtimeAnimatorController=new RuntimeAnimatorController();
  public bool isActiveAndEnabled=true; public float speed=1f;
  public static int StringToHash(string s){return s.GetHashCode();}
  public int NativeCalls;
  private void RequireController(){if(runtimeAnimatorController==null)throw new Exception("Animator called without controller");NativeCalls++;}
  public int GetLayerIndex(string s){RequireController();return 1;} public bool HasState(int a,int b){RequireController();return true;}
  public void Play(int a,int b,float c){RequireController();} public void SetLayerWeight(int a,float b){RequireController();}
  public void CrossFadeInFixedTime(int a,float b,int c,float d){RequireController();} public void CrossFade(int a,float b,int c,float d){RequireController();}
  public AnimatorStateInfo GetCurrentAnimatorStateInfo(int i){RequireController();return new AnimatorStateInfo{length=1f,normalizedTime=0f};}
 }
 public struct AnimatorStateInfo {public float length,normalizedTime;}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Repeat(float a,float b)=>a-(float)Math.Floor(a/b)*b;}
 public static class Time {public static float deltaTime;}
 public static class Debug {public static int ErrorCount;public static bool ThrowOnError=true;public static void LogError(string message,object context){ErrorCount++;if(ThrowOnError)throw new Exception(message);}}
 public class DisallowMultipleComponent:Attribute {}
 public class TooltipAttribute:Attribute {public TooltipAttribute(string s){}}
}
namespace UnityEngine.Events {public class UnityEvent {public void Invoke(){}}}
namespace IntercomMvp {
 public class IntercomSimpleShooter {public IntercomWeaponDefinition Weapon;}
 public class IntercomWeaponDefinition {public IntercomWeaponPoseProfile poseProfile;public float Interval=.3f;}
 public class IntercomPlayerInput {public uint CancellationVersion;public bool FireHeld,FirePressed;}
 public class IntercomWeaponPoseProfile {
  public UnityEngine.AnimatorOverrideController animatorOverride;
  public UnityEngine.AnimationClip raiseWeapons=new UnityEngine.AnimationClip{length=8f/24f};
  public UnityEngine.AnimationClip lowerWeapons=new UnityEngine.AnimationClip{length=12f/24f};
  public UnityEngine.AnimationClip shoot=new UnityEngine.AnimationClip{length=9f/24f};
  public float fireInterval=.4f,combatHold=2f,poseBlend=.06f;
 }
}
