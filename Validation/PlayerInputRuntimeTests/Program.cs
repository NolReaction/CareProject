using System;using System.Reflection;using IntercomMvp;using UnityEngine;
static class Program{
 static int assertions,cases;static void Check(bool b,string s){assertions++;if(!b)throw new Exception(s);}static void Case(string s,Action a){a();cases++;Console.WriteLine("PASS "+s);}
 static void Call(IntercomPlayerInput p,string n)=>typeof(IntercomPlayerInput).GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
 static IntercomPlayerInput Fresh(bool mobile){Input.Clear();Application.isMobilePlatform=mobile;Time.timeScale=1;Screen.safeArea=new Rect(0,0,1920,1080);var p=new IntercomPlayerInput{attached=new IntercomWeaponMagazine()};Call(p,"Awake");Call(p,"OnEnable");Call(p,"Update");return p;}
 static Touch T(int id,float x,float y,TouchPhase phase)=>new Touch{fingerId=id,position=new Vector2(x,y),phase=phase};
 static void Main(){
  Case("desktop reload panel click never fires and movement continues",()=>{
   var p=Fresh(false);Input.held.Add(KeyCode.W);Input.mousePosition=new Vector2(1767,320);Input.MouseHeld=Input.MouseDown=true;Call(p,"Update");
   Check(p.ReloadPressed&&!p.FireHeld&&!p.FirePressed,"reload click becamefire");Check(p.Move.y>0,"reload stoppedmovement");
   Input.MouseDown=false;Input.mousePosition=new Vector2(900,500);Call(p,"Update");Check(!p.ReloadPressed&&!p.FireHeld&&p.Move.y>0,"held reloadpointer becamefireoutsidepanel");
   Input.MouseHeld=false;Input.MouseUp=true;Call(p,"Update");Input.MouseUp=false;Input.MouseHeld=Input.MouseDown=true;Call(p,"Update");Check(p.FireHeld&&p.FirePressed&&!p.ReloadPressed,"fresh clickoutsidepanel not restored");
  });
  Case("mobile reload and movement remain independent through drag and reused ID",()=>{
   var p=Fresh(true);Input.touches=new[]{T(1,223,153,TouchPhase.Began),T(2,1767,320,TouchPhase.Began)};Call(p,"Update");
   Check(p.Move.x>0&&p.ReloadPressed&&!p.FireHeld,"mobile UI press stoleleft orfired");
   Input.touches=new[]{T(1,223,153,TouchPhase.Moved),T(2,1870,153,TouchPhase.Moved)};Call(p,"Update");Check(p.Move.x>0&&!p.FireHeld,"UI drag acquiredrightstick");
   Input.touches=new[]{T(1,223,153,TouchPhase.Moved),T(2,1870,153,TouchPhase.Ended)};Call(p,"Update");Check(!p.FireHeld,"release fired");
   Input.touches=new[]{T(1,223,153,TouchPhase.Moved),T(2,1870,153,TouchPhase.Began)};Call(p,"Update");Check(p.FireHeld&&p.FirePressed&&p.Move.x>0,"reused pointercannotaim");
  });
  Case("R reload and Backspace respawn are separate",()=>{
   var p=Fresh(false);Input.down.Add(KeyCode.R);Call(p,"Update");Check(p.ReloadPressed&&!p.ResetPressed,"R stillrespawns");
   Input.down.Clear();Input.down.Add(KeyCode.Backspace);Call(p,"Update");Check(p.ResetPressed&&!p.ReloadPressed,"Backspace mustonlyreset");
  });
  Case("cancelled touch and paused frame clear button and sticks",()=>{
   var p=Fresh(true);Input.touches=new[]{T(2,1767,320,TouchPhase.Began)};Call(p,"Update");uint version=p.CancellationVersion;
   Input.touches=new[]{T(2,1767,320,TouchPhase.Canceled)};Call(p,"Update");Check(p.CancellationVersion!=version&&!p.ReloadPressed&&!p.FireHeld,"cancelkeptreloadintent");
   Input.touches=Array.Empty<Touch>();Call(p,"Update");Input.touches=new[]{T(3,1767,320,TouchPhase.Began)};Time.timeScale=0;Call(p,"Update");Check(!p.ReloadPressed&&!p.FireHeld,"pausedUIproducedintent");Time.timeScale=1;
  });
  Case("actual HUD publishes ammo and reload status without GUI input handler",()=>{
   var p=Fresh(false);GUI.labels.Clear();Call(p,"OnGUI");Check(GUI.labels.Contains("24")&&GUI.labels.Contains("/ 24")&&GUI.labels.Contains("∞")&&GUI.labels.Contains("R · ПЕРЕЗАРЯДИТЬ"),"ammoHUDlabels missing");
   ((IntercomWeaponMagazine)p.attached).IsReloading=true;GUI.labels.Clear();Call(p,"OnGUI");Check(GUI.labels.Contains("ПЕРЕЗАРЯДКА"),"reloadstatusmissing");
  });
  Case("death blocks both sticks and old held input cannot retry",()=>{
   var p=Fresh(true);p.vitality=new IntercomPlayerVitality{IsDead=true};p.CancelUntilRelease();
   Input.touches=new[]{T(8,223,153,TouchPhase.Began),T(9,1870,153,TouchPhase.Began)};Call(p,"Update");
   Check(p.Move.sqrMagnitude==0&&!p.FireHeld&&!p.ReloadPressed&&p.vitality.Retries==0,"dead input leaked or retried");
   Input.touches=Array.Empty<Touch>();Call(p,"Update");
   var b=IntercomHudLayout.Calculate(0,0,1920,1080,70,32).Retry;
   Input.touches=new[]{T(10,b.X+b.Width*.5f,b.Y+b.Height*.5f,TouchPhase.Began)};Call(p,"Update");
   Check(p.vitality.Retries==1&&!p.FireHeld&&!p.FirePressed,"retry touch must restart once without fire");
   Input.touches=new[]{T(10,1870,153,TouchPhase.Moved)};Call(p,"Update");
   Check(!p.FireHeld&&!p.FirePressed&&p.Move.sqrMagnitude==0,"retry held finger became a new stick");
   Input.touches=Array.Empty<Touch>();Call(p,"Update");
   Input.touches=new[]{T(10,1870,153,TouchPhase.Began)};Call(p,"Update");Check(p.FireHeld,"fresh postretry finger must work");
  });
  Case("defeat uses Backspace and dedicated button, never reload or arbitrary click",()=>{
   var p=Fresh(false);p.vitality=new IntercomPlayerVitality{IsDead=true};
   Input.down.Add(KeyCode.R);Call(p,"Update");Check(p.vitality.Retries==0&&!p.ReloadPressed,"R retried whiledead");
   Input.down.Clear();Input.MouseHeld=Input.MouseDown=true;Input.mousePosition=new Vector2(50,500);Call(p,"Update");
   Check(p.vitality.Retries==0&&!p.FireHeld,"outsideclick retried orfired");
   Input.Clear();Input.down.Add(KeyCode.Backspace);Call(p,"Update");Check(p.vitality.Retries==1,"Backspace failedwhilemotorlocked");
  });
  Case("actual HUD shows health and dead screen, hides ammunition whiledead",()=>{
   var p=Fresh(false);p.vitality=new IntercomPlayerVitality();p.vitality.Health.Current=37;
   GUI.labels.Clear();Call(p,"OnGUI");Check(GUI.labels.Contains("37 / 100")&&GUI.labels.Contains("КОРПУС"),"health not real");
   p.vitality.IsDead=true;p.vitality.Health.Current=0;GUI.labels.Clear();Call(p,"OnGUI");
   Check(GUI.labels.Contains("0 / 100")&&GUI.labels.Contains("СИСТЕМА ОТКЛЮЧЕНА")&&GUI.labels.Contains("ПОПРОБОВАТЬ СНОВА")&&!GUI.labels.Contains("24"),"defeatHUD invalid");
  });
  Console.WriteLine("PASS "+cases+" actual PlayerInput/GUI call-path cases, "+assertions+" assertions with Legacy backend and narrow Unity fakes. No real device/UI rendering claim.");
 }
}
