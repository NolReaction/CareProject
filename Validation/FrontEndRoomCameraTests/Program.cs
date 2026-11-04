using System;
using System.Reflection;
using IntercomMvp;
using UnityEngine;
class Program {
 static int count;static void Check(bool a,string s){count++;if(!a)throw new Exception(s);}static bool At(Vector3 a,float x)=>Math.Abs(a.x-x)<1e-5;static void Call(object x,string m)=>x.GetType().GetMethod(m,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(x,null);
 static IntercomFrontEndRoomCamera Rig()=>new IntercomFrontEndRoomCamera{roomCamera=new Camera{enabled=false,transform=new Transform{position=new Vector3(1,2,3)}},gameplayCamera=new Camera{enabled=true,transform=new Transform{position=new Vector3(99,3,3)}},homeAnchor=new Transform{position=new Vector3(10,2,3)},robotAnchor=new Transform{position=new Vector3(20,2,3)},armoryAnchor=new Transform{position=new Vector3(30,2,3)}};
 static void Main(){
  var r=Rig();r.Show(IntercomFrontEndSection.Home);Check(r.IsShown&&r.roomCamera.enabled&&!r.gameplayCamera.enabled,"room enters menu with separate camera");Check(At(r.roomCamera.transform.position,10)&&At(r.gameplayCamera.transform.position,99),"gameplay camera transform preserved");
  r.Show(IntercomFrontEndSection.Robot);Check(At(r.roomCamera.transform.position,10),"tab change does not snap rooms");Call(r,"LateUpdate");Check(r.roomCamera.transform.position.x>10&&r.roomCamera.transform.position.x<20,"unscaled smooth room movement");r.Hide();Check(!r.IsShown&&!r.roomCamera.enabled&&r.gameplayCamera.enabled&&At(r.roomCamera.transform.position,1),"hide restores original camera state and transform");r.Hide();Check(r.gameplayCamera.enabled,"idempotent hide");
  r=Rig();r.roomCamera.enabled=true;r.gameplayCamera.enabled=false;r.Show(IntercomFrontEndSection.Play);Check(r.IsShown&&At(r.roomCamera.transform.position,10),"missing play anchor falls back home");Call(r,"OnDisable");Check(r.roomCamera.enabled&&!r.gameplayCamera.enabled,"disable restores preexisting disabled gameplay state");
  r=Rig();r.roomCamera=r.gameplayCamera;r.Show(IntercomFrontEndSection.Home);Check(!r.IsShown&&r.gameplayCamera.enabled&&At(r.gameplayCamera.transform.position,99),"same camera is never hijacked");
  r=Rig();r.homeAnchor=null;r.Show(IntercomFrontEndSection.Home);Check(!r.IsShown&&r.gameplayCamera.enabled,"missing anchor does not blank screen");
  r=Rig();var originalRoom=r.roomCamera;var originalGame=r.gameplayCamera;r.Show(IntercomFrontEndSection.Home);r.roomCamera=new Camera();r.gameplayCamera=new Camera();r.Hide();Check(!originalRoom.enabled&&originalGame.enabled&&At(originalRoom.transform.position,1),"runtime reference changes restore original owners");
  r=Rig();originalGame=r.gameplayCamera;r.Show(IntercomFrontEndSection.Home);r.gameplayCamera=new Camera();Call(r,"LateUpdate");Check(!r.IsShown&&originalGame.enabled,"rebind during transition releases old camera");
  r=Rig();r.Show((IntercomFrontEndSection)999);Check(!r.IsShown&&r.gameplayCamera.enabled,"invalid section ignored");
  r=Rig();r.Show(IntercomFrontEndSection.Home);r.travelSharpness=float.NaN;r.Show(IntercomFrontEndSection.Armory);Call(r,"LateUpdate");Check(!float.IsNaN(r.roomCamera.transform.position.x),"invalid sharpness safe fallback");
  Console.WriteLine($"PASS {count} real optional room-camera ownership/lifecycle assertions; stand-in transform interpolation, no Unity engine.");
 }
}
