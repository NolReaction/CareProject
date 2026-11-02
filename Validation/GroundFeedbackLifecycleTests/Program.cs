using System;
using System.Linq;
using System.Reflection;
using IntercomMvp;
using UnityEngine;
class Program {
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Call(object o,string method,params object[] args){try{o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}catch(TargetInvocationException e){throw e.InnerException;}}
 static T Field<T>(object o,string field)=>(T)o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 sealed class Rig {
  public IntercomGroundFeedback feedback;public IntercomPlayerMotor motor;public Animator animator;public Transform[] bones;public AnimationClip idle;public SkinnedMeshRenderer left,right;
  public Rig(bool controller=true,bool legacyCalibration=false){
   var root=new GameObject("Robot");motor=root.AddComponent<IntercomPlayerMotor>();motor.enabled=false;
   animator=root.AddComponent<Animator>();bones=new[]{"Body","Torso","L_Calf","R_Calf","L_Foot","R_Foot","L_Toe","R_Toe","L_Upperarm","R_Upperarm"}.Select(n=>new GameObject(n).transform).ToArray();
   foreach(var b in bones){root.children.Add(b);b.localPosition=new Vector3(.1f,.2f,.3f);b.localRotation=Quaternion.Euler(4,5,6);b.localScale=new Vector3(1.1f,1.2f,1.3f);}
   bones[4].parent=bones[2];bones[5].parent=bones[3];bones[6].parent=bones[4];bones[7].parent=bones[5];
   left=MakeBoot("tripo_part_3_fixed");right=MakeBoot("tripo_part_2_fixed");root.children.Add(left);root.children.Add(right);
   idle=new AnimationClip{name="Idle",sample=obj=>{foreach(var b in bones){b.localPosition=Vector3.zero;b.localRotation=Quaternion.identity;b.localScale=Vector3.one;}}};
   animator.runtimeAnimatorController=controller?Controller(idle):null;
   feedback=root.AddComponent<IntercomGroundFeedback>();feedback.motor=motor;feedback.animator=animator;feedback.soleOffsetsCalibrated=feedback.contactMarkersCalibrated=legacyCalibration;
   Call(feedback,"Awake");Call(feedback,"OnEnable");
  }
  static SkinnedMeshRenderer MakeBoot(string name){var r=new GameObject(name).AddComponent<SkinnedMeshRenderer>();r.name=name;r.vertices=new[]{new Vector3(-.1f,0,-.2f),new Vector3(.1f,0,-.2f),new Vector3(-.1f,0,.2f),new Vector3(.1f,0,.2f),new Vector3(0,.1f,0)};return r;}
  public static RuntimeAnimatorController Controller(params AnimationClip[] clips)=>new RuntimeAnimatorController{animationClips=clips};
  public (Vector3,Quaternion,Vector3)[] Snapshot()=>bones.Select(b=>(b.localPosition,b.localRotation,b.localScale)).ToArray();
  public void Restored((Vector3,Quaternion,Vector3)[] pose){for(int i=0;i<bones.Length;i++){Check(bones[i].localPosition.Equals(pose[i].Item1),"position restored");Check(bones[i].localRotation.Equals(pose[i].Item2),"rotation restored");Check(bones[i].localScale.Equals(pose[i].Item3),"scale restored");}}
 }
 static void Main(){
  var r=new Rig(true,true);Check(!r.feedback.NeedsSoleCalibration&&r.feedback.ProvidesFootContacts,"Awake resolves rig and calibrates both boots");Check(r.idle.samples==1,"one-shot initial calibration");var pose=r.Snapshot();r.feedback.CalibrateSoles();r.Restored(pose);Check(Field<int>(r.feedback,"soleCalibrationVersion")==1,"successful current geometry version");
  r.right.fail=true;bool threw=false;try{r.feedback.CalibrateSoles();}catch{threw=true;}r.Restored(pose);Check(threw&&r.feedback.NeedsSoleCalibration&&!r.feedback.ProvidesFootContacts,"failed right BakeMesh does not certify partial markers");Check(Field<int>(r.feedback,"soleCalibrationVersion")==0,"failed measurement clears version");r.right.fail=false;
  r.idle.sample=obj=>{r.bones[0].localPosition=new Vector3(20,30,40);throw new Exception("sample failed");};threw=false;try{r.feedback.CalibrateSoles();}catch{threw=true;}r.Restored(pose);Check(threw&&r.feedback.NeedsSoleCalibration,"sampling failure restores pose");
  r=new Rig(false);Check(r.feedback.NeedsSoleCalibration&&!r.feedback.ProvidesFootContacts,"no controller starts unavailable");r.animator.runtimeAnimatorController=Rig.Controller(r.idle);pose=r.Snapshot();Call(r.feedback,"LateUpdate");r.Restored(pose);Check(!r.feedback.NeedsSoleCalibration&&r.idle.samples==1,"controller assigned after Awake recovers once");Call(r.feedback,"LateUpdate");Check(r.idle.samples==1,"no per-frame mesh bake after success");
  r=new Rig(false);r.animator.runtimeAnimatorController=Rig.Controller(new AnimationClip{name="Run"});Call(r.feedback,"LateUpdate");Check(Field<bool>(r.feedback,"calibrationAttempted")&&r.feedback.NeedsSoleCalibration,"missing Idle attempted but not certified");var attempted=Field<RuntimeAnimatorController>(r.feedback,"calibrationController");for(int i=0;i<5;i++)Call(r.feedback,"LateUpdate");Check(Field<RuntimeAnimatorController>(r.feedback,"calibrationController")==attempted&&r.left.bakes==0,"missing Idle does not bake on every frame");r.animator.runtimeAnimatorController=Rig.Controller(r.idle);Call(r.feedback,"LateUpdate");Check(!r.feedback.NeedsSoleCalibration&&r.idle.samples==1,"replacement controller repairs unavailable calibration");
  r=new Rig();r.right.fail=true;try{r.feedback.CalibrateSoles();}catch{}r.right.fail=false;Call(r.feedback,"OnDisable");Call(r.feedback,"OnEnable");Call(r.feedback,"LateUpdate");Check(!r.feedback.NeedsSoleCalibration,"reenable retries repaired geometry with the same controller");
  r=new Rig();r.animator.runtimeAnimatorController=null;Call(r.feedback,"LateUpdate");Check(!r.feedback.ProvidesFootContacts,"controller removal advertises no contact provider");r.animator.runtimeAnimatorController=Rig.Controller(r.idle);Call(r.feedback,"LateUpdate");Check(r.feedback.ProvidesFootContacts&&r.idle.samples==1,"same geometry reuses valid calibration after teardown");
  r=new Rig();pose=r.Snapshot();Call(r.feedback,"SavePose");foreach(var b in r.bones)b.localPosition=new Vector3(7,8,9);Call(r.feedback,"OnDisable");for(int i=0;i<r.bones.Length;i++){if(i==6||i==7)continue;Check(r.bones[i].localPosition.Equals(pose[i].Item1),"disable restores bones mutated by feedback");}Check(Field<int>(r.feedback,"savedPoseCount")==0,"disable empties pose cache");
  Console.WriteLine("GroundFeedback lifecycle: "+checks+" assertions passed. Real GroundFeedback + Motor + GroundMath compile together against narrow engine stand-ins. No Unity rendering/physics/skin deformation claim.");
 }
}
