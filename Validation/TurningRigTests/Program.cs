using System;
using System.Linq;
using System.Reflection;
using IntercomMvp;
using UnityEngine;
class Program {
 static int checks;static float maxOffset,maxDrift,maxTwist,maxVisibleTwist,maxSwingSpeed,minSwing=float.MaxValue,maxLandingGap;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Call(object o,string method,params object[] args){try{o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}catch(TargetInvocationException e){throw e.InnerException;}}
 static T Field<T>(object o,string field)=>(T)o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static void Set(object o,string field,object value)=>o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
 static float Yaw(Vector3 v)=>(float)(Math.Atan2(v.x,v.z)*180/Math.PI);
 sealed class Rig {
  public GameObject root;public IntercomGroundFeedback f;public IntercomPlayerMotor motor;public CharacterController controller;public Animator animator;public Transform[] bones;public Vector3[][] sole;public Vector3[] positions,scales;public int contacts;public bool badContact;private int previousStep;private Vector3 previousSwing;
  public Rig(){
   root=new GameObject("Robot");controller=root.AddComponent<CharacterController>();motor=root.AddComponent<IntercomPlayerMotor>();motor.modelForwardYaw=0;
   animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=new RuntimeAnimatorController();
   bones=RigGeometry.Names.Select(n=>new GameObject(n).transform).ToArray();
   int[] parents={-1,0,0,0,2,3,4,5};
   for(int i=0;i<bones.Length;i++){bones[i].parent=parents[i]<0?root.transform:bones[parents[i]];bones[i].position=RigGeometry.Positions[i];bones[i].rotation=RigGeometry.Rotations[i];root.children.Add(bones[i]);}
   f=root.AddComponent<IntercomGroundFeedback>();f.motor=motor;f.animator=animator;f.body=bones[0];f.torso=bones[1];f.leftCalf=bones[2];f.rightCalf=bones[3];f.leftFoot=bones[4];f.rightFoot=bones[5];f.leftToe=bones[6];f.rightToe=bones[7];
   f.leftSoleOffset=f.leftFoot.InverseTransformPoint(RigGeometry.LSole.OrderBy(v=>v.y).First());f.rightSoleOffset=f.rightFoot.InverseTransformPoint(RigGeometry.RSole.OrderBy(v=>v.y).First());
   f.leftHeelOffset=f.leftFoot.InverseTransformPoint(RigGeometry.LHeel);f.rightHeelOffset=f.rightFoot.InverseTransformPoint(RigGeometry.RHeel);f.leftToeOffset=f.leftToe.InverseTransformPoint(RigGeometry.LToe);f.rightToeOffset=f.rightToe.InverseTransformPoint(RigGeometry.RToe);
   f.soleOffsetsCalibrated=f.contactMarkersCalibrated=true;Set(f,"soleCalibrationVersion",1);Call(motor,"Awake");Call(f,"Awake");Call(f,"OnEnable");
   sole=new[]{RigGeometry.LSole.Select(f.leftFoot.InverseTransformPoint).ToArray(),RigGeometry.RSole.Select(f.rightFoot.InverseTransformPoint).ToArray()};positions=bones.Select(b=>b.localPosition).ToArray();scales=bones.Select(b=>b.localScale).ToArray();
   Physics.Floor=new GameObject("floor").AddComponent<Collider>();
   f.Footstep+=(c,p,s,l)=>{contacts++;if(!f.IsTurnFootstep||!motor.IsGrounded)badContact=true;};
   Tick(0,1f/60);
  }
  public void Tick(float target,float dt){
   Time.deltaTime=dt;Time.frameCount++;Call(f,"Update");
   Vector3 wanted=Quaternion.AngleAxis(target,Vector3.up)*Vector3.forward;
   if(f.TryConstrainStationaryHeading(wanted,0,out var limited))wanted=limited;
   root.transform.rotation=Quaternion.RotateTowards(root.transform.rotation,Quaternion.LookRotation(wanted),720*dt);
   Call(f,"LateUpdate");
  }
  public void Verify(){
   var state=Field<IntercomTurnStepState>(f,"turnSteps");
   for(int i=2;i<bones.Length;i++){Check(Vector3.Distance(bones[i].localPosition,positions[i])<1e-5f,"leg local translation changed");Check(bones[i].localScale.Equals(scales[i]),"bone scale changed");}
   float offset=Field<Vector3>(f,"turnBodyOffset").magnitude;maxOffset=Math.Max(maxOffset,offset);
   float twist=Math.Abs(IntercomTurnStepState.DeltaAngle(state.LowerYaw,Yaw(motor.FacingDirection)));maxTwist=Math.Max(maxTwist,twist);
   Check(twist<=f.maximumTurnTorsoYaw+.01f,"unbounded wrapper/lower yaw "+twist);
   float visible=Math.Abs(IntercomTurnStepState.DeltaAngle(state.LowerYaw,Yaw(f.torso.forward)));maxVisibleTwist=Math.Max(maxVisibleTwist,visible);Check(visible<=f.maximumTurnTorsoYaw+.01f,"unbounded final torso yaw "+visible);
   Check(offset<.16f,"Body offset must stay inside 16cm support envelope: "+offset+" yaw "+state.LowerYaw+" step "+state.StepId+" phase "+state.Phase);
   if(state.Active||state.LandedThisTick){
    var stance=state.SwingLeft?f.rightFoot:f.leftFoot;
    float drift=Vector3.Distance(stance.position,Field<Vector3>(f,"turnStanceAnkle"));maxDrift=Math.Max(maxDrift,drift);Check(drift<1e-5f,"planted ankle drift");
    var held=Field<Quaternion>(f,"turnStanceRotation");Check(Vector3.Angle(stance.rotation*Vector3.forward,held*Vector3.forward)<.04f && Vector3.Angle(stance.rotation*Vector3.up,held*Vector3.up)<.04f,"planted boot orientation drift");
    int side=state.SwingLeft?0:1;var swing=state.SwingLeft?f.leftFoot:f.rightFoot;
    if(previousStep==state.StepId){float swingSpeed=Vector3.Distance(swing.position,previousSwing)/Time.deltaTime;maxSwingSpeed=Math.Max(maxSwingSpeed,swingSpeed);Check(swingSpeed<3f*(.18f/f.turnStepSeconds),"swing azimuth must stay within the previous phase-space speed envelope");}previousStep=state.StepId;previousSwing=swing.position;
    float lowest=sole[side].Min(p=>swing.TransformPoint(p).y);minSwing=Math.Min(minSwing,lowest);Check(lowest>=-.0001f,"swing sole floor penetration: "+lowest+" step "+state.StepId+" phase "+state.Phase);
    if(state.LandedThisTick){maxLandingGap=Math.Max(maxLandingGap,Math.Abs(lowest));Check(Math.Abs(lowest)<.004f,"landing must reach floor");}
   }
   Check(!badContact,"airborne or unauthorised contact event");
  }
 }
 static float CompleteQuarterTurn(float duration,int fps){
  var r=new Rig();r.f.turnStepSeconds=duration;
  for(int i=0;i<fps*3;i++){
   r.Tick(90,1f/fps);r.Verify();var state=Field<IntercomTurnStepState>(r.f,"turnSteps");
   if(state.IsGatherStep&&!state.Active&&Math.Abs(IntercomTurnStepState.DeltaAngle(state.LowerYaw,90))<.1f)return (i+1f)/fps;
  }
  throw new Exception("quarter turn did not complete with both boots gathered");
 }
 static void Main(){
  foreach(int fps in new[]{30,60,120}){
   float oldDuration=CompleteQuarterTurn(.18f,fps),newDuration=CompleteQuarterTurn(.13f,fps);
   Check(newDuration<oldDuration*.82f,"V6 quarter turn including gather is at least 18 percent faster");
   Console.WriteLine("Quarter turn "+fps+" FPS: "+oldDuration.ToString("F3")+" -> "+newDuration.ToString("F3")+" seconds including final gather.");
   var r=new Rig();for(int i=0;i<fps*18;i++){float target=i<fps*5?180f:i<fps*10?i*210f/fps:i<fps*14?-i*180f/fps:0f;r.Tick(target,1f/fps);r.Verify();}
   Check(r.contacts>20,"turns must emit actual landing contacts");int landed=r.contacts;for(int i=0;i<fps*2;i++){r.Tick(0,1f/fps);r.Verify();}Check(r.contacts==landed,"settled idle emits no contacts");
   // Cancel half a step into movement, then immediately release. Finish the
   // fade rather than snapping back to the Animator baseline on the release.
   for(int i=0;i<5;i++)r.Tick(100,1f/60);
   Set(r.motor,"<WantsToMove>k__BackingField",true);r.Tick(100,1f/60);float first=Field<float>(r.f,"turnExitWeight");
   Set(r.motor,"<WantsToMove>k__BackingField",false);r.Tick(100,1f/60);Check(Field<bool>(r.f,"turnExiting")&&Field<float>(r.f,"turnExitWeight")<first,"short movement tap cancels through smooth exit");
   for(int i=0;i<20;i++)r.Tick(100,1f/60);r.controller.isGrounded=false;landed=r.contacts;for(int i=0;i<30;i++)r.Tick(200,1f/60);Check(r.contacts==landed,"airborne cancellation has no landing event");
   Call(r.f,"OnDisable");Check(!Field<bool>(r.f,"turnHasPose")&&!r.f.IsTurnFootstep,"disable clears turn pose and contact authorization");
  }
  var random=new Random(12017);var varied=new Rig();float heading=0;for(int i=0;i<18000;i++){if(i%11==0)heading=(float)random.NextDouble()*720-360;varied.Tick(heading,1f/60);varied.Verify();}
  Check(varied.contacts>1000,"long reversal stress completes over one thousand verified landings");
  var narrow=new Rig();narrow.f.maximumTurnTorsoYaw=15;narrow.f.turnStepDegrees=45;for(int i=0;i<1200;i++){narrow.Tick(i<600?150:-150,1f/60);narrow.Verify();}
  Console.WriteLine($"Turn rig: {checks} assertions; random verified landings {varied.contacts}; max Body offset {maxOffset:F5}m; stance drift {maxDrift:E2}m; wrapper/lower twist {maxTwist:F3}deg; final torso twist {maxVisibleTwist:F3}deg; max swing speed {maxSwingSpeed:F3}m/s; swing sole min {minSwing:F5}m; landing gap max {maxLandingGap:F5}m. Real production transform math + measured Idle geometry, not Unity skinning/physics.");
 }
}
