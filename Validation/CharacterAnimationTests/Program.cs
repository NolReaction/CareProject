using System;
using System.Linq;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

static class Program
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Near(float a, float b, float epsilon, string message) => Check(Math.Abs(a-b)<=epsilon,message+": "+a+" / "+b);
    static void Call(object value, string name, params object[] args) => value.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(value,args);
    static bool Same(Quaternion a, Quaternion b) => Math.Abs(Quaternion.Dot(a,b))>.99999f;
    static Transform Bone(string name,Transform parent,Vector3 position)
    { var o=new GameObject(name);o.transform.parent=parent;o.transform.localPosition=position;return o.transform; }
    sealed class Rig
    {
        internal GameObject root;
        internal IntercomCharacterAnimation animation;
        internal IntercomPlayerMotor motor;
        internal IntercomWeaponPose pose;
        internal IntercomWeaponMagazine magazine;
        internal IntercomPlayerInput input;
        internal IntercomSimpleShooter shooter;
        internal Animator animator;
        internal Transform body,left,right,leftHand,rightHand;
        internal Transform[] bones;
        internal Vector3[] positions,scales;
        internal Rig()
        {
            root=new GameObject("Robot");motor=root.AddComponent<IntercomPlayerMotor>();pose=root.AddComponent<IntercomWeaponPose>();
            magazine=root.AddComponent<IntercomWeaponMagazine>();input=root.AddComponent<IntercomPlayerInput>();shooter=root.AddComponent<IntercomSimpleShooter>();
            var model=new GameObject("Model");model.transform.parent=root.transform;
            animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=new RuntimeAnimatorController();
            body=Bone("Torso",model.transform,new Vector3(0,.85f,0));
            left=Bone("L_Upperarm",body,new Vector3(.3f,0,0));right=Bone("R_Upperarm",body,new Vector3(-.3f,0,0));
            leftHand=Bone("L_Hand",left,new Vector3(0,-.05f,.4f));rightHand=Bone("R_Hand",right,new Vector3(0,-.05f,.4f));
            bones=new[]{body,left,right,leftHand,rightHand};positions=bones.Select(x=>x.localPosition).ToArray();scales=bones.Select(x=>x.localScale).ToArray();
            model.children.AddRange(bones.Cast<object>());root.children.Add(animator);
            animation=root.AddComponent<IntercomCharacterAnimation>();Call(animation,"Awake");Call(animation,"OnEnable");
            Physics.WallHits=Array.Empty<RaycastHit>();Time.deltaTime=1f/60;Time.frameCount=0;
        }
        internal void Step(float dt=1f/60)
        {Time.deltaTime=dt;Time.frameCount++;Call(animation,"Update");Call(animation,"LateUpdate");}
        internal void Invariants()
        {
            for(int i=0;i<bones.Length;i++)
            {
                Near((bones[i].localPosition-positions[i]).magnitude,0,1e-7f,"gesture never translates bone");
                Near((bones[i].localScale-scales[i]).magnitude,0,1e-7f,"gesture never stretches bone");
            }
            Check(Same(body.localRotation,Quaternion.identity),"torso is owned by GroundFeedback, not gestures");
        }
        internal void AtWall()
        {var wall=new GameObject("Wall").AddComponent<Collider>();Physics.WallHits=new[]{new RaycastHit{collider=wall,normal=new Vector3(0,0,-1),distance=.2f}};}
    }
    static void CheckAlternatingRecoil()
    {
        foreach(int fps in new[]{30,60,120})
        {
            var rig=new Rig();rig.pose.Posture=IntercomWeaponPosture.Combat;
            Check(rig.shooter.ListenerCount==1,"one confirmed-barrel listener after enable");
            rig.shooter.Fire(false);rig.Step(1f/fps);rig.Invariants();
            Check(rig.animation.LeftRecoil>.3f&&rig.animation.RightRecoil==0,"left shot kicks only left shoulder");
            Check(!Same(rig.left.localRotation,Quaternion.identity)&&Same(rig.right.localRotation,Quaternion.identity),"left confirmed shot moves actual left arm only");
            Check(!Same(rig.leftHand.localRotation,Quaternion.identity)&&Same(rig.rightHand.localRotation,Quaternion.identity),"left confirmed shot moves actual left wrist only");
            for(int i=1;i<Math.Round(fps*.12f);i++)rig.Step(1f/fps);
            float leftBefore=rig.animation.LeftRecoil;
            rig.shooter.Fire(true);rig.Step(1f/fps);rig.Invariants();
            Check(rig.animation.RightRecoil>.3f&&rig.animation.LeftRecoil<leftBefore,"right kick does not retrigger left envelope");
            for(int shot=0;shot<120;shot++)
            {
                rig.shooter.Fire((shot&1)!=0);
                for(int i=0;i<Math.Max(1,(int)Math.Round(fps*.12f));i++){rig.Step(1f/fps);rig.Invariants();}
            }
            for(int i=0;i<fps;i++)rig.Step(1f/fps);
            Check(Same(rig.left.localRotation,Quaternion.identity)&&Same(rig.rightHand.localRotation,Quaternion.identity),"continuous fire returns to exact combat pose without accumulated rotations");
            rig.shooter.Fire(false);rig.Step();rig.magazine.IsReloading=true;rig.Step();
            Check(rig.animation.LeftRecoil==0&&rig.animation.RightRecoil==0,"reload owns shoulders and clears shot pose");
            rig.magazine.IsReloading=false;rig.shooter.Fire(true);rig.Step();Call(rig.animation,"OnApplicationFocus",false);
            Check(rig.animation.RightRecoil==0&&Same(rig.right.localRotation,Quaternion.identity),"focus loss clears and restores recoil");
            Call(rig.animation,"OnApplicationFocus",true);Call(rig.animation,"OnDisable");
            Check(rig.shooter.ListenerCount==0,"disable unsubscribes confirmed shot listener");
            Call(rig.animation,"OnEnable");Call(rig.animation,"OnEnable");
            Check(rig.shooter.ListenerCount==1,"repeated enable never duplicates subscription");
            rig.shooter.AlternatingFire=false;rig.shooter.Fire(false);rig.Step();
            Check(rig.animation.LeftRecoil==0,"legacy dual-volley retains its original clip without extra single-hand recoil");
            var old=rig.shooter;rig.shooter=rig.root.AddComponent<IntercomSimpleShooter>();rig.animation.shooter=rig.shooter;rig.Step();
            Check(old.ListenerCount==0&&rig.shooter.ListenerCount==1,"equipment source replacement detaches old events");
            old.Fire(false);rig.Step();Check(rig.animation.LeftRecoil==0,"old weapon cannot animate replacement equipment");
        }
        Near(IntercomCharacterAnimationMath.ShotKick(0,.16f),0,0,"confirmed shot begins without instant transform jump");
        Near(IntercomCharacterAnimationMath.ShotKick(.014f,.16f),1,.00001f,"short attack reaches one bounded kick");
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})Near(IntercomCharacterAnimationMath.ShotKick(bad,.16f),0,0,"invalid shot time neutral");
    }
    static void CheckShoulderDrive()
    {
        foreach(int fps in new[]{30,60,120})
        {
            var rig=new Rig();rig.pose.Posture=IntercomWeaponPosture.Relaxed;rig.motor.ActualPlanarSpeed=3.3f;
            float leftXMin=99,leftXMax=-99,tipZMin=99,tipZMax=-99;
            for(int i=0;i<fps*2;i++)
            {
                rig.animator.state=new AnimatorStateInfo{normalizedTime=i/(float)fps*3.75f};rig.Step(1f/fps);rig.Invariants();
                if(i<fps)continue;
                leftXMin=Math.Min(leftXMin,rig.left.forward.x);leftXMax=Math.Max(leftXMax,rig.left.forward.x);
                tipZMin=Math.Min(tipZMin,rig.leftHand.position.z);tipZMax=Math.Max(tipZMax,rig.leftHand.position.z);
            }
            Check(leftXMax-leftXMin>.12f,"reduced additive shoulder drive remains readable above newly authored run");
            Console.WriteLine("fps="+fps+" additive yawrange="+(leftXMax-leftXMin)+" tipZspan="+(tipZMax-tipZMin));Check(tipZMax-tipZMin>.004f,"small runtime follow remains after authored source run");
            rig.motor.ActualPlanarSpeed=0;
            for(int i=0;i<fps;i++)rig.Step(1f/fps);
            Check(Same(rig.left.localRotation,Quaternion.identity)&&Same(rig.leftHand.localRotation,Quaternion.identity),"athletic shoulder drive settles fully at stop");
        }
    }
    static void Main()
    {
        CheckAlternatingRecoil();CheckShoulderDrive();
        Near(IntercomCharacterAnimationMath.ReloadWeight(0),0,0,"reload starts in authored pose");
        Near(IntercomCharacterAnimationMath.ReloadWeight(1),0,0,"reload finishes in authored pose before firing resumes");
        Near(IntercomCharacterAnimationMath.ReloadLatch(0),0,0,"latch starts zero");
        Near(IntercomCharacterAnimationMath.ReloadLatch(1),0,0,"latch ends zero");
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {Near(IntercomCharacterAnimationMath.ReloadWeight(bad),0,0,"invalid progress stays neutral");Near(IntercomCharacterAnimationMath.WallWeight(bad,.3f,.8f),0,0,"invalid wall distance stays neutral");}
        for(int i=0;i<=1000;i++)
        {
            float p=i/1000f,w=IntercomCharacterAnimationMath.ReloadWeight(p),l=IntercomCharacterAnimationMath.ReloadLatch(p);
            Check(w>=0&&w<=1&&l>=0&&l<=1,"bounded reload envelopes");
        }
        foreach(int fps in new[]{30,60,120})
        {
            var rig=new Rig();rig.pose.Posture=IntercomWeaponPosture.Combat;rig.magazine.IsReloading=true;
            for(int i=0;i<fps*2;i++)
            {
                rig.magazine.ReloadProgress=Math.Min(1,i/(fps*1.15f));rig.Step(1f/fps);rig.Invariants();
            }
            Check(Same(rig.left.localRotation,Quaternion.identity)&&Same(rig.leftHand.localRotation,Quaternion.identity),"reload return fully neutral");
            rig.magazine.IsReloading=false;rig.Step(1f/fps);Check(Same(rig.rightHand.localRotation,Quaternion.identity),"first postreload firing pose is clean");
        }
        {
            var rig=new Rig();rig.pose.Posture=IntercomWeaponPosture.Combat;rig.magazine.IsReloading=true;rig.magazine.ReloadProgress=.3f;rig.Step();
            var stable=rig.left.localRotation;var hand=rig.leftHand.localRotation;
            // No Animator refresh at all: resembles a culled visual hierarchy.
            for(int i=0;i<300;i++){rig.Step();Check(Same(stable,rig.left.localRotation)&&Same(hand,rig.leftHand.localRotation),"culled Animator cannot accumulate arm rotations");rig.Invariants();}
            var external=Quaternion.AngleAxis(.02f,Vector3.up);
            rig.left.localRotation=external;Call(rig.animation,"Update");
            Check(Same(rig.left.localRotation,external),"another component's restored arm pose must survive");
            Check(Same(rig.leftHand.localRotation,Quaternion.identity),"own hand snapshot is restored independently");
            rig.Step();rig.animator.enabled=false;rig.Step();Check(Same(rig.left.localRotation,external),"disabled Animator releases owned offset");
        }
        {
            var rig=new Rig();rig.AtWall();for(int i=0;i<12;i++)rig.Step();Near(rig.animation.WallCompression,1,.0001f,"wall folds relaxed arms");
            rig.pose.Posture=IntercomWeaponPosture.Raising;for(int i=0;i<7;i++)rig.Step();Near(rig.animation.WallCompression,0,0,"arms release well before .33s raise completes");
            rig.pose.Posture=IntercomWeaponPosture.Combat;rig.input.FireHeld=true;rig.Step();
            Check(Same(rig.left.localRotation,Quaternion.identity)&&Same(rig.leftHand.localRotation,Quaternion.identity),"active combat aim and muzzle pose untouched");
            rig.pose.Posture=IntercomWeaponPosture.Relaxed;rig.input.FireHeld=false;for(int i=0;i<12;i++)rig.Step();
            Call(rig.animation,"OnApplicationFocus",false);Near(rig.animation.WallCompression,0,0,"focus loss clears wall posture");
            Check(Same(rig.left.localRotation,Quaternion.identity),"focus loss restores arms");
        }
        {
            var rig=new Rig();var player=rig.root.AddComponent<Collider>();var ground=new GameObject("Ground").AddComponent<Collider>();
            Physics.WallHits=new[]{new RaycastHit{collider=player,normal=Vector3.forward,distance=.01f},new RaycastHit{collider=ground,normal=Vector3.up,distance=.01f}};
            for(int i=0;i<30;i++)rig.Step();Near(rig.animation.WallCompression,0,0,"player and floor excluded from wall probe");
            Physics.WallHits=Enumerable.Repeat(new RaycastHit{collider=ground,normal=Vector3.forward,distance=.01f},24).ToArray();rig.Step();Near(rig.animation.WallCompression,0,0,"saturated query stays safe");
        }
        {
            var rig=new Rig();for(int i=0;i<270;i++)rig.Step();Check(!Same(rig.leftHand.localRotation,Quaternion.identity),"quiet idle has one slow wrist inspection");
            rig.motor.ActualPlanarSpeed=3.3f;rig.Step();Check(Same(rig.leftHand.localRotation,Quaternion.identity),"inspection never adds running jitter");
            rig.motor.ActualPlanarSpeed=0;rig.AtWall();for(int i=0;i<12;i++)rig.Step();Call(rig.animation,"OnDisable");
            Check(Same(rig.left.localRotation,Quaternion.identity)&&Same(rig.leftHand.localRotation,Quaternion.identity),"disable leaves no owned bone offset");
        }
        {
            var rig=new Rig();
            var leftMuzzle=Bone("IntercomMuzzle_L",rig.leftHand,new Vector3(0,0,.2f));
            var rightMuzzle=Bone("IntercomMuzzle_R",rig.rightHand,new Vector3(0,0,.2f));
            leftMuzzle.localRotation=Quaternion.AngleAxis(-14f,Vector3.up)*Quaternion.AngleAxis(-3f,new Vector3(1,0,0));
            rightMuzzle.localRotation=Quaternion.AngleAxis(14f,Vector3.up)*Quaternion.AngleAxis(-3f,new Vector3(1,0,0));
            rig.animation.leftMuzzle=leftMuzzle;rig.animation.rightMuzzle=rightMuzzle;
            Vector3 common=IntercomProjectileDirection.Parallel(rightMuzzle.forward,leftMuzzle.forward,Vector3.forward,true);
            float originalPitch=leftMuzzle.forward.y;
            rig.pose.Posture=IntercomWeaponPosture.Raising;
            for(int i=0;i<11;i++)rig.Step();
            Near(Vector3.Angle(Vector3.ProjectOnPlane(leftMuzzle.forward,Vector3.up),common),0,.03f,"left visible barrel matches shared trajectory before raise finishes");
            Near(Vector3.Angle(Vector3.ProjectOnPlane(rightMuzzle.forward,Vector3.up),common),0,.03f,"right visible barrel matches shared trajectory before raise finishes");
            Near(leftMuzzle.forward.y,originalPitch,1e-6f,"yaw correction preserves authored barrel pitch/recoil");
            rig.Invariants();
            rig.pose.Posture=IntercomWeaponPosture.Combat;
            for(int i=0;i<120;i++)
            {
                rig.Step();Near(Vector3.Angle(Vector3.ProjectOnPlane(leftMuzzle.forward,Vector3.up),Vector3.ProjectOnPlane(rightMuzzle.forward,Vector3.up)),0,.03f,"visible barrels stay parallel without cumulative wrist offset");
            }
            rig.magazine.IsReloading=true;rig.magazine.ReloadProgress=1f;rig.Step();
            Near(Vector3.Angle(Vector3.ProjectOnPlane(leftMuzzle.forward,Vector3.up),Vector3.ProjectOnPlane(rightMuzzle.forward,Vector3.up)),0,.03f,"reload completion is already aligned for next shot");
            rig.pose.Posture=IntercomWeaponPosture.Relaxed;rig.magazine.IsReloading=false;
            for(int i=0;i<12;i++)rig.Step();Check(Same(rig.leftHand.localRotation,Quaternion.identity),"lowering releases alignment to source animation");
            rig.pose.Posture=IntercomWeaponPosture.Combat;rig.animation.maximumAimYawCorrection=5;rig.Step();
            float residual=Vector3.Angle(Vector3.ProjectOnPlane(leftMuzzle.forward,Vector3.up),common);
            Check(residual>8.9f&&residual<9.1f,"custom excessive toe-in is bounded by correction limit");
        }
        foreach(int fps in new[]{30,60,120})
        {
            var rig=new Rig();rig.animation.runningShoulderDrive=rig.animation.runningShoulderRoll=rig.animation.runningReachDegrees=0;rig.pose.Posture=IntercomWeaponPosture.Relaxed;rig.motor.ActualPlanarSpeed=3.3f;
            float minLeft=99,maxLeft=-99,minRight=99,maxRight=-99;
            for(int i=0;i<fps*2;i++)
            {
                float phase=i/(float)fps*3.75f;rig.animator.state=new AnimatorStateInfo{normalizedTime=phase};
                rig.Step(1f/fps);rig.Invariants();
                float leftAngle=Vector3.SignedAngle(Vector3.forward,rig.left.forward,new Vector3(1,0,0));
                float rightAngle=Vector3.SignedAngle(Vector3.forward,rig.right.forward,new Vector3(1,0,0));
                if(i>fps){minLeft=Math.Min(minLeft,leftAngle);maxLeft=Math.Max(maxLeft,leftAngle);minRight=Math.Min(minRight,rightAngle);maxRight=Math.Max(maxRight,rightAngle);Near(leftAngle+rightAngle,-12,.02f,"small authored-run additive carry remains symmetric around6degrees");}
            }
            Check(maxLeft-minLeft>8&&maxRight-minRight>8,"runtime arm follow is retained above authored24degree swing");
            Check(maxLeft<0&&maxRight<0,"even backwards arm swing stays lifted relative to authored lowered pose");
            rig.input.FireHeld=true;rig.pose.Posture=IntercomWeaponPosture.Raising;rig.Step(1f/fps);
            Check(!Same(rig.left.localRotation,Quaternion.identity),"weapon raise fades the running carry instead of snapping arms down on its first frame");
            for(int i=0;i<fps/4+1;i++)rig.Step(1f/fps);
            Check(Same(rig.left.localRotation,Quaternion.identity)&&Same(rig.right.localRotation,Quaternion.identity),"running carry clears before the .33-second raise can fire");
            rig.pose.Posture=IntercomWeaponPosture.Combat;rig.Step();
            Check(Same(rig.left.localRotation,Quaternion.identity)&&Same(rig.right.localRotation,Quaternion.identity),"first shot owns exact source arms with no running residual");
            rig.input.FireHeld=false;rig.pose.Posture=IntercomWeaponPosture.Relaxed;rig.motor.ActualPlanarSpeed=0;
            for(int i=0;i<fps;i++)rig.Step(1f/fps);
            Check(Same(rig.left.localRotation,Quaternion.identity)&&Same(rig.rightHand.localRotation,Quaternion.identity),"stop fully returns shoulders and wrist to authored idle");
            rig.motor.ActualPlanarSpeed=3.3f;for(int i=0;i<fps;i++)rig.Step(1f/fps);Call(rig.animation,"OnDisable");
            Check(Same(rig.left.localRotation,Quaternion.identity)&&Same(rig.right.localRotation,Quaternion.identity),"disable restores added running shoulders");
        }
        Console.WriteLine("Character gestures: "+checks+" assertions passed; production component/math, narrow Unity stand-ins. No Unity playback/render claim.");
    }
}
