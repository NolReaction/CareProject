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
 sealed class MotorRig {
  public IntercomPlayerMotor motor;public IntercomPlayerInput input;public Animator animator;public IntercomGroundFeedback feedback;
  public MotorRig(bool arcade=false){
   var root=new GameObject("MovingRobot");root.transform.rotation=Quaternion.Euler(0,180,0);
   var controller=root.AddComponent<CharacterController>();controller.center=new Vector3(.07f,.75f,.12f);
   input=root.AddComponent<IntercomPlayerInput>();input.AimActive=true;input.Aim=Vector2.up;
   animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=Rig.Controller(new AnimationClip{name="Idle"});
   motor=root.AddComponent<IntercomPlayerMotor>();motor.inputSource=input;motor.animator=animator;
   if(!arcade){motor.moveSpeed=2.1f;motor.acceleration=8f;motor.deceleration=12f;}
   else {
    feedback=root.AddComponent<IntercomGroundFeedback>();feedback.body=root.transform;feedback.animator=animator;
    feedback.leftCalf=new GameObject("calf L").transform;feedback.rightCalf=new GameObject("calf R").transform;
    feedback.leftFoot=new GameObject("foot L").transform;feedback.rightFoot=new GameObject("foot R").transform;
    feedback.leftFoot.parent=feedback.leftCalf;feedback.rightFoot.parent=feedback.rightCalf;
    feedback.leftFoot.localPosition=feedback.rightFoot.localPosition=Vector3.down*.15f;
    Call(feedback,"CalibrateSoleClouds");
    // This narrow Motor test supplies an already calibrated rig. The measured
    // running tests execute the real calibration and skeletal support solve.
    feedback.soleOffsetsCalibrated=feedback.contactMarkersCalibrated=true;
    typeof(IntercomGroundFeedback).GetField("soleCalibrationVersion",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(feedback,1);
   }
   Call(motor,"Awake");
  }
  public void Step(float dt=1f/60){
   // The stand-in integrates a requested displacement without collisions. Keep
   // its grounded height fixed so long traces do not invoke the falling reset.
   var p=motor.transform.position;motor.transform.position=new Vector3(p.x,0,p.z);
   Time.deltaTime=dt;Time.frameCount++;Call(motor,"Update");
   float rate=animator.GetFloat("RunRate");
   Check(float.IsFinite(rate)&&rate<=motor.moveSpeed/(motor.runReferenceSpeed*motor.RuntimeStrideScale)+.00001f,"actual Motor caps cadence during direction transitions");
   Vector3 facing=motor.FacingDirection,right=Vector3.Cross(Vector3.up,facing),velocity=motor.ActualPlanarVelocity;
   float x=Vector3.Dot(velocity,right)/(motor.moveSpeed*motor.strafeSpeedRatio),y=Vector3.Dot(velocity,facing)/motor.moveSpeed;
   Check(x*x+y*y<=1.0002f,"actual Motor travel remains inside the final-facing speed ellipse");
   float bx=animator.GetFloat("MoveX"),by=animator.GetFloat("MoveY");
   Check(Math.Abs(bx*bx+by*by-1f)<.0001f,"smoothed authored direction stays normalized");
   Check(Vector3.Dot(motor.AimWorldDirection,new Vector3(input.Aim.x,0,input.Aim.y).normalized)>.9999f,"travel never replaces held independent aim");
  }
  public void Settle(Vector2 travel,int frames=120){input.Move=travel;for(int i=0;i<frames;i++)Step();}
  public Vector3 BlendTravel(){Vector3 facing=motor.FacingDirection;return (Vector3.Cross(Vector3.up,facing)*(animator.GetFloat("MoveX")*.75f)+facing*animator.GetFloat("MoveY")).normalized;}
 }
 static void CheckArcadeMotor(){
  foreach(int fps in new[]{30,60,120}){
   var r=new MotorRig(true);float dt=1f/fps;int steps=0;r.input.Move=Vector2.up;
   Check(Math.Abs(r.motor.moveSpeed-3.3f)<1e-6f,"V5 default is always running without Shift");
   Check(Math.Abs(r.motor.RuntimeStrideScale-1.32f)<1e-6f,"ready rigid rig enables measured longer stroke");
   while(r.motor.ActualPlanarSpeed<3.2999f&&steps<fps){r.Step(dt);steps++;}
   Check(steps*dt<=.15f+dt+.0001f,"V5 reaches full forward speed in 150 ms plus one frame");
   Check(Math.Abs(r.animator.GetFloat("RunRate")-2.5f)<.0001f,"V5 full-speed cadence accounts for longer stroke");
   r.input.Move=Vector2.zero;steps=0;Vector3 begin=r.motor.transform.position;
   while(r.motor.ActualPlanarSpeed>.0001f&&steps<fps){r.Step(dt);steps++;}
   Check(steps*dt<=.11f+dt+.0001f,"V5 brakes within 110 ms plus one frame");
   Check(Vector3.ProjectOnPlane(r.motor.transform.position-begin,Vector3.up).magnitude<.19f,"releasing the stick does not coast a long distance");
   r.input.Move=Vector2.up;for(int i=0;i<fps;i++)r.Step(dt);
   r.input.Move=new Vector2(0,-1);steps=0;
   while(r.motor.ActualPlanarVelocity.z> -3.2999f&&steps<fps){r.Step(dt);steps++;}
   Check(steps*dt<=.3f+dt+.0001f,"V5 can reverse full-speed travel within 300 ms plus one frame");
   r.feedback.enabled=false;r.Step(dt);
   Check(r.motor.RuntimeStrideScale==1f,"disabled solver cannot retain a longer cadence reference without the stroke");
   Check(Math.Abs(r.animator.GetFloat("RunRate")-3.3f)<.0002f,"disabled solver returns to the authored stride cadence");
  }
 }
 static void CheckMotorEllipse(){
  var directions=new[]{Vector2.up,Vector2.right,new Vector2(.70710678f,.70710678f),new Vector2(0,-1),new Vector2(-.70710678f,-.70710678f)};
  var speeds=new[]{2.1f,1.575f,1.7819091f,2.1f,1.7819091f};
  for(int i=0;i<directions.Length;i++){
   var r=new MotorRig();r.Settle(directions[i]);
   var expected=new Vector3(directions[i].x,0,directions[i].y).normalized;
   Check(Math.Abs(r.motor.ActualPlanarSpeed-speeds[i])<.0002f,"steady directional top speed matches authored stride ellipse");
   Check(Vector3.Dot(r.motor.ActualPlanarVelocity.normalized,expected)>.99999f,"ellipse scales speed without changing requested world heading");
   Check(Vector3.Dot(r.BlendTravel(),expected)>.99999f,"anisotropic animation blend converges to physical travel heading");
   Check(Math.Abs(r.animator.GetFloat("RunRate")-2.1f)<.0002f,"all full-speed directions use the same maximum cadence");
  }
  var transition=new MotorRig();transition.Settle(Vector2.up);transition.input.Aim=Vector2.right;transition.Step();
  IntercomLocomotionDirection.TryProject(transition.motor.ActualPlanarVelocity.x,transition.motor.ActualPlanarVelocity.z,
   transition.motor.FacingDirection.x,transition.motor.FacingDirection.z,out float physicalX,out float physicalY);
  IntercomLocomotionCadence.AnimationDirection(physicalX,physicalY,.75f,out float targetX,out float targetY);
  Check(Math.Abs(transition.animator.GetFloat("MoveX"))<Math.Abs(targetX)-.01f,"animation direction damps an aim turn instead of snapping");
  for(int i=0;i<120;i++)transition.Step();
  Check(Vector3.Dot(transition.BlendTravel(),Vector3.forward)>.99999f,"smoothed blend catches up after aim rotates independently");
  Check(Math.Abs(transition.motor.ActualPlanarSpeed-1.575f)<.0002f,"aim turn changes permitted stride speed without steering travel");
  var sequence=new[]{new Vector2(0,-1),Vector2.up,Vector2.right,new Vector2(-1,0),new Vector2(.70710678f,.70710678f)};
  for(int cycle=0;cycle<3;cycle++)for(int i=0;i<sequence.Length;i++){
   transition.input.Aim=sequence[(i+cycle)%sequence.Length];
   transition.Settle(sequence[(i+1)%sequence.Length],24);
  }
  transition.input.Aim=Vector2.up;transition.Settle(Vector2.zero,60);
  Check(transition.motor.ActualPlanarSpeed<.0001f&&transition.motor.IsAiming,"releasing movement stops travel while held aim remains active");
 }
 static void Main(){
  var r=new Rig(true,true);Check(!r.feedback.NeedsSoleCalibration&&r.feedback.ProvidesFootContacts,"Awake resolves rig and calibrates both boots");Check(r.idle.samples==1,"one-shot initial calibration");var pose=r.Snapshot();r.feedback.CalibrateSoles();r.Restored(pose);Check(Field<int>(r.feedback,"soleCalibrationVersion")==1,"successful current geometry version");
  Call(r.motor,"Awake");Check(Math.Abs(r.motor.RuntimeStrideScale-1.32f)<1e-6f,"valid calibrated feedback enables V5 stride reference");
  r.right.fail=true;bool threw=false;try{r.feedback.CalibrateSoles();}catch{threw=true;}r.Restored(pose);Check(threw&&r.feedback.NeedsSoleCalibration&&!r.feedback.ProvidesFootContacts,"failed right BakeMesh does not certify partial markers");Check(Field<int>(r.feedback,"soleCalibrationVersion")==0,"failed measurement clears version");Check(r.motor.RuntimeStrideScale==1f,"failed recalibration cannot slow cadence while support solve is unavailable");r.right.fail=false;
  r.idle.sample=obj=>{r.bones[0].localPosition=new Vector3(20,30,40);throw new Exception("sample failed");};threw=false;try{r.feedback.CalibrateSoles();}catch{threw=true;}r.Restored(pose);Check(threw&&r.feedback.NeedsSoleCalibration,"sampling failure restores pose");
  r=new Rig(false);Check(r.feedback.NeedsSoleCalibration&&!r.feedback.ProvidesFootContacts,"no controller starts unavailable");r.animator.runtimeAnimatorController=Rig.Controller(r.idle);pose=r.Snapshot();Call(r.feedback,"LateUpdate");r.Restored(pose);Check(!r.feedback.NeedsSoleCalibration&&r.idle.samples==1,"controller assigned after Awake recovers once");Call(r.feedback,"LateUpdate");Check(r.idle.samples==1,"no per-frame mesh bake after success");
  r=new Rig(false);r.animator.runtimeAnimatorController=Rig.Controller(new AnimationClip{name="Run"});Call(r.feedback,"LateUpdate");Check(Field<bool>(r.feedback,"calibrationAttempted")&&r.feedback.NeedsSoleCalibration,"missing Idle attempted but not certified");var attempted=Field<RuntimeAnimatorController>(r.feedback,"calibrationController");for(int i=0;i<5;i++)Call(r.feedback,"LateUpdate");Check(Field<RuntimeAnimatorController>(r.feedback,"calibrationController")==attempted&&r.left.bakes==0,"missing Idle does not bake on every frame");r.animator.runtimeAnimatorController=Rig.Controller(r.idle);Call(r.feedback,"LateUpdate");Check(!r.feedback.NeedsSoleCalibration&&r.idle.samples==1,"replacement controller repairs unavailable calibration");
  r=new Rig();r.right.fail=true;try{r.feedback.CalibrateSoles();}catch{}r.right.fail=false;Call(r.feedback,"OnDisable");Call(r.feedback,"OnEnable");Call(r.feedback,"LateUpdate");Check(!r.feedback.NeedsSoleCalibration,"reenable retries repaired geometry with the same controller");
  r=new Rig();r.animator.runtimeAnimatorController=null;Call(r.feedback,"LateUpdate");Check(!r.feedback.ProvidesFootContacts,"controller removal advertises no contact provider");r.animator.runtimeAnimatorController=Rig.Controller(r.idle);Call(r.feedback,"LateUpdate");Check(r.feedback.ProvidesFootContacts&&r.idle.samples==1,"same geometry reuses valid calibration after teardown");
  r=new Rig();pose=r.Snapshot();Call(r.feedback,"SavePose");foreach(var b in r.bones)b.localPosition=new Vector3(7,8,9);Call(r.feedback,"OnDisable");for(int i=0;i<r.bones.Length;i++){if(i==6||i==7)continue;Check(r.bones[i].localPosition.Equals(pose[i].Item1),"disable restores bones mutated by feedback");}Check(Field<int>(r.feedback,"savedPoseCount")==0,"disable empties pose cache");
  r=new Rig();var measured=new IntercomGroundFeedbackSoleProfile{left=new[]{new IntercomGroundFeedbackSoleProfile.Point()},right=new[]{new IntercomGroundFeedbackSoleProfile.Point()}};r.feedback.soleProfile=measured;pose=r.Snapshot();Call(r.feedback,"LateUpdate");r.Restored(pose);Check(r.idle.samples==2&&Field<IntercomGroundFeedbackSoleProfile>(r.feedback,"calibratedSoleProfile")==measured,"late assigned contour is calibrated once in Idle and restores the pose");for(int i=0;i<5;i++)Call(r.feedback,"LateUpdate");Check(r.idle.samples==2,"contour does not trigger per-frame mesh baking");r.feedback.soleProfile=null;Call(r.feedback,"LateUpdate");Check(r.idle.samples==3&&r.feedback.ProvidesFootContacts,"removing contour restores the legacy contact provider once");
  CheckMotorEllipse();CheckArcadeMotor();
  Console.WriteLine("GroundFeedback lifecycle and Motor ellipse: "+checks+" assertions passed. Real GroundFeedback + Motor + GroundMath compile together against narrow engine stand-ins. No Unity rendering/physics/skin deformation claim.");
 }
}
