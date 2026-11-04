using System;
using System.Reflection;
using UnityEngine;
using IntercomMvp;

static class Program
{
    static int checks;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Call(object o,string method,params object[] values){try{o.GetType().GetMethod(method,Flags).Invoke(o,values);}catch(TargetInvocationException e){throw e.InnerException;}}
    static void Set(object o,string key,object value)=>o.GetType().GetField(key,Flags).SetValue(o,value);
    static void Property(object o,string key,object value)=>Set(o,"<"+key+">k__BackingField",value);
    static float Yaw(Vector3 value)=>(float)(Math.Atan2(value.x,value.z)*180/Math.PI);
    static float Difference(float a,float b)=>Math.Abs(IntercomTurnStepState.DeltaAngle(a,b));
    sealed class Rig
    {
        public GameObject root=new GameObject("waist actor");
        public IntercomGroundFeedback feedback;
        public IntercomPlayerMotor motor;
        public Animator animator;
        public Transform body,torso,leftCalf,rightCalf,leftFoot,rightFoot;
        public Rig()
        {
            root.AddComponent<CharacterController>();
            motor=root.AddComponent<IntercomPlayerMotor>();motor.modelForwardYaw=0;
            animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=new RuntimeAnimatorController();motor.animator=animator;
            feedback=root.AddComponent<IntercomGroundFeedback>();feedback.motor=motor;feedback.animator=animator;
            Transform Bone(string name,Transform parent,Vector3 local){var t=new GameObject(name).transform;t.parent=parent;t.localPosition=local;return t;}
            body=Bone("Body",root.transform,new Vector3(0,.75f,0));torso=Bone("Torso",body,Vector3.up*.1f);
            leftCalf=Bone("L_Calf",body,new Vector3(.2f,-.55f,0));rightCalf=Bone("R_Calf",body,new Vector3(-.2f,-.55f,0));
            leftFoot=Bone("L_Foot",leftCalf,Vector3.down*.2f);rightFoot=Bone("R_Foot",rightCalf,Vector3.down*.2f);
            feedback.body=body;feedback.torso=torso;feedback.leftCalf=leftCalf;feedback.rightCalf=rightCalf;feedback.leftFoot=leftFoot;feedback.rightFoot=rightFoot;
            feedback.proceduralGaitWeight=0;feedback.soleOffsetsCalibrated=feedback.contactMarkersCalibrated=true;Set(feedback,"soleCalibrationVersion",1);
            Call(feedback,"BindRig");Call(feedback,"CalibrateSoleClouds");
            Call(motor,"Awake");Set(motor,"aiming",true);Set(motor,"hasAim",true);
            Property(motor,"ActualPlanarVelocity",Vector3.forward*3);Property(motor,"ActualPlanarSpeed",3f);Property(motor,"WantsToMove",true);
        }
        public float Step(float aim,float dt)
        {
            Call(feedback,"RestorePose");root.transform.rotation=Quaternion.AngleAxis(aim,Vector3.up);
            Set(motor,"lastAimDirection",Quaternion.AngleAxis(aim,Vector3.up)*Vector3.forward);
            var footPosition=leftFoot.localPosition;var link=Vector3.Distance(leftCalf.position,leftFoot.position);
            Call(feedback,"SavePose");Call(feedback,"ApplyTurnFootwork",dt,1f);Call(feedback,"ApplyMovingWaist",true,false,dt);Call(feedback,"ApplyTorso",0f,true,dt,true);
            Check(Difference(Yaw(torso.TransformDirection(Vector3.forward)),aim)<.04f,"upper aim remains exact while pelvis turns independently: aim="+aim+" torso="+Yaw(torso.TransformDirection(Vector3.forward))+" lower="+Yaw(body.TransformDirection(Vector3.forward)));
            float lower=Yaw(body.TransformDirection(Vector3.forward));
            Check(Difference(lower,aim)<=42.01f,"moving waist stays inside mechanical cone");
            Check(Difference(lower,Yaw(motor.LocomotionFacingDirection))<.04f,"motor animation projection consumes lower heading");
            Check(Vector3.Distance(leftFoot.localPosition,footPosition)<1e-7f&&Math.Abs(Vector3.Distance(leftCalf.position,leftFoot.position)-link)<1e-6f,"waist does not stretch or translate a rigid leg");
            return lower;
        }
    }
    static void Main()
    {
        foreach(int fps in new[]{30,60,120})
        {
            var standing=new Rig();
            Property(standing.motor,"ActualPlanarVelocity",Vector3.zero);Property(standing.motor,"ActualPlanarSpeed",0f);Property(standing.motor,"WantsToMove",false);
            Call(standing.feedback,"ApplyTurnFootwork",1f/fps,0f);
            standing.root.transform.rotation=Quaternion.AngleAxis(20,Vector3.up);
            Call(standing.feedback,"ApplyTurnFootwork",1f/fps,0f);
            Check(!standing.feedback.IsTurnStepping,"20degree stationary aim is absorbed by waist before feet step");
            standing.root.transform.rotation=Quaternion.AngleAxis(33,Vector3.up);
            Call(standing.feedback,"ApplyTurnFootwork",1f/fps,0f);
            Check(standing.feedback.IsTurnStepping,"larger stationary aim starts an actual planted step");
            float dt=1f/fps;var rig=new Rig();rig.Step(0,dt);float small=rig.Step(20,dt);
            Check(Math.Abs(small)<.03f,"small aim turn rotates chest before legs");
            float final=0;for(int i=0;i<fps;i++)final=rig.Step(90,dt);
            Check(Math.Abs(final-48)<.03f,"large aim turn lets pelvis catch only to42degree limit");
            for(int loop=0;loop<3;loop++)for(int heading=0;heading<360;heading+=5)rig.Step(heading,dt);
            Call(rig.feedback,"RestorePose");Property(rig.motor,"ActualPlanarSpeed",0f);Property(rig.motor,"WantsToMove",false);
            float before=(float)typeof(IntercomGroundFeedback).GetField("runningLowerYaw",Flags).GetValue(rig.feedback);
            for(int i=0;i<5;i++)
            {
                Call(rig.feedback,"RestorePose");Call(rig.feedback,"SavePose");
                Call(rig.feedback,"ApplyTurnFootwork",dt,.6f-i*.1f);
                Call(rig.feedback,"ApplyMovingWaist",false,false,dt);
                Check(Difference(before,Yaw(rig.body.TransformDirection(Vector3.forward)))<.03f,"decelerating Speed blend retains old leg heading before Idle can plant: before="+before+" after="+Yaw(rig.body.TransformDirection(Vector3.forward)));
            }
            Call(rig.feedback,"RestorePose");
            Call(rig.feedback,"SavePose");Call(rig.feedback,"ApplyTurnFootwork",dt,0f);
            float after=Yaw(rig.body.TransformDirection(Vector3.forward));
            Check(Difference(before,after)<3,"stopping keeps lower heading rather than snapping boots to upper aim");
            Call(rig.feedback,"OnApplicationFocus",false);
            Check(!rig.feedback.TryGetLocomotionFacing(out _),"focus reset invalidates independent moving heading");
            Call(rig.feedback,"OnApplicationFocus",true);Property(rig.motor,"ActualPlanarSpeed",3f);Property(rig.motor,"WantsToMove",true);rig.Step(30,dt);
            rig.feedback.independentMovingWaist=false;
            Check(!rig.feedback.TryGetLocomotionFacing(out _),"authored optout returns locomotion to normal wrapper");
        }
        foreach(int fps in new[]{30,60,120})
        {
            var rig=new Rig();float dt=1f/fps;float phase=0;
            for(int i=0;i<fps*4;i++)
            {
                Time.deltaTime=dt;Time.frameCount++;Call(rig.feedback,"Update");
                float aim=i<fps?20f:i<fps*2?90f:i<fps*3?-90f:175f;
                rig.root.transform.rotation=Quaternion.AngleAxis(aim,Vector3.up);
                phase+=dt*1.5f;rig.animator.state=new AnimatorStateInfo{fullPathHash=17,normalizedTime=phase};
                Property(rig.motor,"ActualPlanarVelocity",Vector3.forward*3);Property(rig.motor,"ActualPlanarSpeed",3f);Property(rig.motor,"WantsToMove",true);
                Call(rig.feedback,"LateUpdate");
                Check(Difference(Yaw(rig.torso.TransformDirection(Vector3.forward)),aim)<.04f,"full Ground LateUpdate preserves upper aim while running");
                Check(Difference(Yaw(rig.body.TransformDirection(Vector3.forward)),aim)<=42.01f,"full Ground owner bounds waist through reversals");
            }
            float lastLower=Yaw(rig.body.TransformDirection(Vector3.forward));
            for(int i=0;i<fps/2;i++)
            {
                Time.deltaTime=dt;Time.frameCount++;Call(rig.feedback,"Update");
                Property(rig.motor,"ActualPlanarVelocity",Vector3.zero);Property(rig.motor,"ActualPlanarSpeed",0f);Property(rig.motor,"WantsToMove",false);
                rig.animator.SetFloat(Animator.StringToHash("Speed"),i<5?.6f-i*.1f:0f);
                phase+=dt;rig.animator.state=new AnimatorStateInfo{fullPathHash=17,normalizedTime=phase};Call(rig.feedback,"LateUpdate");
                float lower=Yaw(rig.body.TransformDirection(Vector3.forward));
                Check(Difference(lastLower,lower)<18,"full running-to-idle handoff has no42degree boot snap");lastLower=lower;
            }
            for(int i=0;i<fps/2;i++)
            {
                Time.deltaTime=dt;Time.frameCount++;Call(rig.feedback,"Update");
                Property(rig.motor,"ActualPlanarVelocity",Vector3.forward*3);Property(rig.motor,"ActualPlanarSpeed",3f);Property(rig.motor,"WantsToMove",true);rig.animator.SetFloat(Animator.StringToHash("Speed"),1);
                phase+=dt*1.5f;rig.animator.state=new AnimatorStateInfo{fullPathHash=17,normalizedTime=phase};Call(rig.feedback,"LateUpdate");
                float lower=Yaw(rig.body.TransformDirection(Vector3.forward));
                Check(Difference(lastLower,lower)<18,"stationary-to-running blend adopts lower heading without unwinding waist twice");lastLower=lower;
            }
            Call(rig.feedback,"OnDisable");
            Check(Quaternion.Dot(rig.body.localRotation,Quaternion.identity)>.99999f,"full owner restores lower pose on disable");
            Check(Quaternion.Dot(rig.torso.localRotation,Quaternion.identity)>.99999f,"full owner restores upper pose on disable");
        }
        Console.WriteLine($"Running waist: {checks} production Motor/Ground assertions passed at30/60/120fps with narrow transform/engine stand-ins. No Unity animation playback claim.");
    }
}
