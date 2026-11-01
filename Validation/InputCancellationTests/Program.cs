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
  Console.WriteLine("PASS "+cases+" cases / "+assertions+" assertions. Executed production WeaponPose.Update + clock + cancellation gate + TwinStickState with narrow Unity test doubles; NOT Unity compilation or playback.");
 }
 static void Call(object target,string method){typeof(IntercomWeaponPose).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);}
 static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 static void Case(string name,Action test){test();cases++;Console.WriteLine("PASS "+name);}
}
