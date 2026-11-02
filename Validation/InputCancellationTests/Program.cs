using System;
using System.Reflection;
using IntercomMvp;
using UnityEngine;
internal static class Program {
 static int cases, assertions;
 sealed class Harness {
  public IntercomPlayerInput Input=new IntercomPlayerInput{CancellationVersion=1};
  public IntercomWeaponPose Pose;public int Shots,Cancellations;
  public readonly IntercomTwinStickState Sticks=new IntercomTwinStickState();
  public Harness(bool start=true) {
   Sticks.Configure(100,100,900,100,70,.12f,.2f);
   Pose=new IntercomWeaponPose{animator=new Animator(),profile=new IntercomWeaponPoseProfile(),inputSource=Input,previewInput=false};
   Pose.ShotReady+=()=>Shots++;Pose.FireCancelled+=()=>Cancellations++;
   Call(Pose,"Awake");if(start)Call(Pose,"Start");
  }
  public void Frame(float dt=.01f){Time.deltaTime=dt;Call(Pose,"Update");Input.FirePressed=false;}
  public void Advance(int frames=60){for(int i=0;i<frames;i++)Frame();}
  public void StartTap(){Sticks.BeginFrame();Sticks.PointerDown(7,970,100);Sticks.EndFrame();Input.FireHeld=Sticks.Right.Active;Input.FirePressed=true;Frame();}
  public void Release(){Sticks.BeginFrame();Sticks.PointerUp(7);Check(!Sticks.EndFrame(),"Normal release cannot be cancellation");Input.FireHeld=false;}
  public void Cancel(){Sticks.Reset();unchecked{Input.CancellationVersion++;}Input.FireHeld=Input.FirePressed=false;}
 }
 static void Main(){
  Case("normal short release survives raise and fires exactly once",()=>{var h=new Harness();h.StartTap();h.Release();h.Advance();Check(h.Shots==1,"Tap lost");Check(h.Cancellations==0,"Normal release reset pose");});
  Case("timeScale-zero frame cancels before deltaTime early exit",()=>{var h=new Harness();h.StartTap();h.Release();h.Cancel();h.Frame(0f);Check(h.Pose.Posture==IntercomWeaponPosture.Relaxed,"Paused frame did not reset");Check(h.Cancellations==1,"Shooter cancellation event missing");h.Advance();Check(h.Shots==0,"Ghost shot after pause");});
  Case("layout cancellation stops queued tap",()=>{var h=new Harness();h.StartTap();h.Sticks.Configure(140,100,860,100,70,.12f,.2f);h.Cancel();h.Frame();h.Advance();Check(h.Shots==0,"Layout ghost shot");});
  Case("lost OS contact cancels queued tap",()=>{var h=new Harness();h.StartTap();h.Sticks.BeginFrame();Check(h.Sticks.EndFrame(),"Lost contact not classified as cancelled");h.Cancel();h.Frame();h.Advance();Check(h.Shots==0,"Lost contact ghost shot");});
  Case("source replacement with equal epoch cancels old tap",()=>{var h=new Harness();h.StartTap();h.Release();h.Input=new IntercomPlayerInput{CancellationVersion=1};h.Pose.inputSource=h.Input;h.Frame(0f);h.Advance();Check(h.Shots==0,"Old source tap survived");Check(h.Cancellations==1,"Source identity not observed");});
  Case("removing source cancels old tap before fallback",()=>{var h=new Harness();h.StartTap();h.Release();h.Pose.inputSource=null;h.Frame(0f);h.Advance();Check(h.Shots==0,"Removed source ghost shot");});
  Case("initial snapshot preserves legitimate pre-Start request",()=>{var h=new Harness(false);h.Pose.RequestFire();Call(h.Pose,"Start");h.Advance();Check(h.Shots==1,"Initial snapshot cancelled valid tap");Check(h.Cancellations==0,"Initial baseline is not cancellation");});
  Case("cancellation before Start invalidates pending request",()=>{var h=new Harness(false);h.Pose.RequestFire();h.Cancel();Call(h.Pose,"Start");h.Advance();Check(h.Shots==0,"Pre-Start cancelled request revived");});
  Case("uint epoch wrap cancels correctly",()=>{var gate=new IntercomInputCancellation();var source=new object();Check(!gate.Observe(source,uint.MaxValue),"Initial baseline");Check(gate.Observe(source,0u),"Wrap not detected");Check(!gate.Observe(source,0u),"Unchanged epoch cancelled twice");});
  Case("cancelled frame cannot consume stale fire flags",()=>{var h=new Harness();h.StartTap();h.Cancel();h.Input.FireHeld=h.Input.FirePressed=true;h.Frame();Check(h.Pose.Posture==IntercomWeaponPosture.Relaxed,"Cancelled frame immediately re-raised");h.Input.FireHeld=false;h.Advance();Check(h.Shots==0,"Stale cancelled flags fired");});
  Case("fresh tap works after cancellation",()=>{var h=new Harness();h.StartTap();h.Cancel();h.Frame(0f);h.StartTap();h.Release();h.Advance();Check(h.Shots==1,"Fresh tap did not recover");});
  Case("disable cancels without touching a controllerless Animator",()=>{var h=new Harness();h.StartTap();var a=h.Pose.animator;int calls=a.NativeCalls;a.runtimeAnimatorController=null;Call(h.Pose,"OnDisable");Check(a.NativeCalls==calls,"Disable queried controllerless Animator");Check(h.Pose.Posture==IntercomWeaponPosture.Relaxed,"Disable did not cancel pose");});
  Case("focus and pause callbacks tolerate controller teardown",()=>{var h=new Harness();var a=h.Pose.animator;int calls=a.NativeCalls;a.runtimeAnimatorController=null;Call(h.Pose,"OnApplicationFocus",false);Call(h.Pose,"OnApplicationPause",true);Check(a.NativeCalls==calls,"Pause queried controllerless Animator");Check(h.Cancellations==2,"Pause/focus cancellation events missing");});
  Case("runtime controller removal cancels pending fire and recovers",()=>{var h=new Harness();h.StartTap();h.Release();var a=h.Pose.animator;var controller=a.runtimeAnimatorController;int calls=a.NativeCalls;a.runtimeAnimatorController=null;h.Frame();Check(h.Pose.Posture==IntercomWeaponPosture.Relaxed,"Missing controller left a pending pose");Check(a.NativeCalls==calls,"Update queried controllerless Animator");a.runtimeAnimatorController=controller;h.Advance();Check(h.Shots==0,"Queued tap survived controller removal");h.StartTap();h.Release();h.Advance();Check(h.Shots==1,"Fresh tap failed after controller returned");});
  Case("assigning controller after failed Start permits re-enable",()=>{var h=new Harness(false);h.Pose.animator.runtimeAnimatorController=null;int errors=Debug.ErrorCount;Debug.ThrowOnError=false;try{Call(h.Pose,"Start");}finally{Debug.ThrowOnError=true;}Check(!h.Pose.enabled&&Debug.ErrorCount==errors+1,"Missing initial controller should disable with one diagnostic");var controller=new RuntimeAnimatorController();h.Pose.animator.runtimeAnimatorController=controller;h.Pose.enabled=true;Call(h.Pose,"OnEnable");Check(h.Pose.animator.runtimeAnimatorController==controller,"Re-enable overwrote valid controller with stale null");h.StartTap();h.Release();h.Advance();Check(h.Shots==1,"Re-enabled pose driver did not fire");});
  Case("external recoil notification rejects missing controller",()=>{var h=new Harness();h.StartTap();h.Release();h.Advance();Check(h.Pose.Posture==IntercomWeaponPosture.Combat,"Expected combat hold");var a=h.Pose.animator;int calls=a.NativeCalls;a.runtimeAnimatorController=null;Check(!h.Pose.NotifyShotFired(),"Missing controller accepted a recoil notification");Check(a.NativeCalls==calls,"External notification queried controllerless Animator");});
  Case("central weapon controls pose profile and live fire interval",()=>{
   var h=new Harness(false);var profile=new IntercomWeaponPoseProfile{fireInterval=4f};
   var definition=new IntercomWeaponDefinition{poseProfile=profile,Interval=.1f};
   h.Pose.attachedComponent=new IntercomSimpleShooter{Weapon=definition};Call(h.Pose,"Start");
   Check(h.Pose.profile==profile,"Definition pose was not resolved before initialization");
   h.StartTap();h.Advance(95);Check(h.Shots>=6,"Definition fire interval ignored in favor of legacy profile");
   int before=h.Shots;definition.Interval=.5f;h.Advance(49);Check(h.Shots-before<=1,"Live definition interval was not adopted");
  });
  Case("reload rejects fire intent and cannot replay queued tap",()=>{
   var h=new Harness(false);var mag=new IntercomWeaponMagazine{CanFire=false,IsReloading=true};h.Pose.additionalComponent=mag;
   Call(h.Pose,"Start");h.StartTap();h.Release();h.Advance();Check(h.Shots==0&&h.Pose.animator.RecoilCalls==0,"Reload fired or recoiled");
   h.Pose.RequestFire();mag.CanFire=true;mag.IsReloading=false;h.Advance();Check(h.Shots==0,"Tap during reload was replayed");
   h.StartTap();h.Release();h.Advance();Check(h.Shots==1,"Fresh tap after reload failed");
  });
  Case("real shooter must confirm recoil, failed shot cannot animate",()=>{
   var h=new Harness(false);h.Pose.attachedComponent=new IntercomSimpleShooter();Call(h.Pose,"Start");
   h.StartTap();h.Release();h.Advance();Check(h.Shots==1&&h.Pose.animator.RecoilCalls==0,"Unconfirmed shot recoiled");
   h.Pose.ConfirmShotVisual();Check(h.Pose.animator.RecoilCalls==1,"Confirmed visual did not play");
   int shots=h.Shots;h.Frame();Check(h.Shots==shots,"Visual confirmation registered another shot");
  });
  Console.WriteLine("PASS "+cases+" cases / "+assertions+" assertions. Executed production WeaponPose.Update + clock + cancellation gate + TwinStickState with narrow Unity test doubles; NOT Unity compilation or playback.");
 }
 static void Call(object target,string method,params object[] args){typeof(IntercomWeaponPose).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);}
 static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 static void Case(string name,Action test){test();cases++;Console.WriteLine("PASS "+name);}
}
