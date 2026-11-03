using System;
using IntercomMvp;
using UnityEngine;
static class Program {
 static int assertions;
 static void Check(bool v,string s){assertions++;if(!v)throw new Exception(s);}
 sealed class Hero {
  public GameObject root=new GameObject("Hero");public IntercomHealth health;public IntercomPlayerInput input;public IntercomPlayerMotor motor;public IntercomPlayerVitality vitality;public IntercomMissionDirector mission;public IntercomSimpleShooter shooter;public IntercomWeaponPose pose;
  public Hero(bool blockBeforeAwake){Input.Clear();Time.timeScale=1;Time.deltaTime=.02f;Camera.main=new GameObject("Camera").AddComponent<Camera>();root.AddComponent<CharacterController>();health=root.AddComponent<IntercomHealth>();input=root.AddComponent<IntercomPlayerInput>();motor=root.AddComponent<IntercomPlayerMotor>();vitality=root.AddComponent<IntercomPlayerVitality>();shooter=root.AddComponent<IntercomSimpleShooter>();pose=root.AddComponent<IntercomWeaponPose>();root.AddComponent<IntercomWeaponMagazine>();mission=new GameObject("Mission").AddComponent<IntercomMissionDirector>();mission.playerVitality=vitality;mission.playerMotor=motor;mission.playerHealth=health;mission.navigation=new GameObject("Nav").AddComponent<IntercomNpcNavigation>();mission.enemyDefinition=new IntercomNpcDefinition();mission.eliminationDefinition=new IntercomMissionDefinition();mission.rescueDefinition=new IntercomMissionDefinition{mode=IntercomMissionMode.Rescue};mission.spawnPoints=new[]{new GameObject("Spawn").transform};mission.spawnPoints[0].position=new Vector3(8,0,8);mission.allySpawn=new GameObject("Ally spawn").transform;mission.extractionPoint=new GameObject("Exit").transform;input.mission=null;input.vitality=vitality;motor.inputSource=input;motor.weaponPose=pose;vitality.health=health;vitality.motor=motor;vitality.input=input;vitality.shooter=shooter;vitality.weaponPose=pose;
   Lifecycle.Call(mission,"Awake");if(blockBeforeAwake)Lifecycle.Call(mission,"OnEnable");
   Lifecycle.Call(input,"Awake");Lifecycle.Call(input,"OnEnable");Lifecycle.Call(motor,"Awake");Lifecycle.Call(vitality,"Awake");Lifecycle.Call(vitality,"OnEnable");Lifecycle.Call(health,"Awake");if(!blockBeforeAwake)Lifecycle.Call(mission,"OnEnable");Lifecycle.Call(mission,"Start");Step();
  }
  public void Step(){Time.time+=Time.deltaTime;Lifecycle.Call(input,"Update");Lifecycle.Call(mission,"Update");if(motor.enabled)Lifecycle.Call(motor,"Update");Lifecycle.Call(vitality,"Update");}
  public void Walk(){Input.held.Add(KeyCode.W);for(int i=0;i<20;i++)Step();Input.Clear();}
 }
 static void HiddenLinkScreenshot(){
  var h=new Hero(false);Application.isMobilePlatform=true;
  GUI.labels.Clear();Lifecycle.Call(h.input,"OnGUI");bool visible=GUI.labels.Exists(label=>label.Contains("50 РОБОТОВ"));
  var layout=IntercomHudLayout.Calculate(Screen.safeArea.x,Screen.safeArea.y,Screen.safeArea.width,Screen.safeArea.height,70,32);
  var thumb=new Vector2(layout.LeftX,layout.LeftY+layout.Radius*.8f);var start=h.root.transform.position;
  Input.touches=new[]{new Touch{fingerId=22,phase=TouchPhase.Began,position=thumb}};h.Step();Input.touches[0].phase=TouchPhase.Moved;
  for(int i=0;i<10;i++)h.Step();
  Console.WriteLine($"Missing-link fixture: visibleMenu={visible}, inputMove={h.input.Move.y}, motorEnabled={h.motor.enabled}, travel={(h.root.transform.position-start).magnitude}");
  Check(visible||h.motor.enabled||h.input.Move.sqrMagnitude<.01f,"missing mission link cannot leave normal HUD with deflected stick and a locked Motor");
  Check(h.input.mission==h.mission&&visible,"missing mission link is repaired and selection rendered");
  Check(!h.motor.enabled&&h.input.Move.sqrMagnitude==0,"left stick cannot bypass visible selection lock");
  Check((h.root.transform.position-start).sqrMagnitude==0,"visible selection keeps actual Motor still");
  Application.isMobilePlatform=false;Input.Clear();
 }
 static void Main(string[] args){
  if(args.Length>0){HiddenLinkScreenshot();return;}
  HiddenLinkScreenshot();
 foreach(bool early in new[]{false,true}){
  var h=new Hero(early);Check(!h.motor.enabled,"selection disables motor");var before=h.root.transform.position;h.Walk();Check(h.root.transform.position.z==before.z,"menu rejects physical movement");h.Step();Check(h.mission.StartMission(IntercomMissionMode.Elimination),"mission start succeeds after real motor Awake");h.Step();h.Walk();Check(h.root.transform.position.z>before.z+.1f,"keyboard input reaches real Motor after mission start");
  h.health.TakeDamage(new IntercomDamage(1000,Vector3.zero,Vector3.forward,null));Check(!h.motor.enabled,"real health death disables movement");Check(h.mission.Phase==IntercomMissionPhase.Defeat,"actual Director observes hero death");h.Step();Check(h.mission.RetryMission(),"retry invokes real motor reset while disabled");h.Step();before=h.root.transform.position;h.Walk();Check(h.root.transform.position.z>before.z+.1f,"retry restores real movement");
 }
 var held=new Hero(true);
 held.input.CancelUntilRelease();Input.held.Add(KeyCode.W);held.Step();
 var menu=IntercomHudLayout.Calculate(Screen.safeArea.x,Screen.safeArea.y,Screen.safeArea.width,Screen.safeArea.height,70,32);
 Input.mousePosition=new Vector2(menu.MissionFirst.X+menu.MissionFirst.Width*.5f,menu.MissionFirst.Y+menu.MissionFirst.Height*.5f);
 Input.MouseHeld=Input.MouseDown=true;held.Step();Input.MouseDown=false;
 Check(held.mission.Phase==IntercomMissionPhase.Active,"holding movement key does not prevent choosing a mission after cancellation");
 var start=held.root.transform.position;
 for(int i=0;i<15;i++)held.Step();
 Check(held.root.transform.position.z>start.z+.1f,"held movement reaches real Motor while old menu pointer is still held");
 Check(!held.input.FireHeld&&!held.input.FirePressed,"consumed menu pointer cannot shoot while keyboard movement works");
 Input.MouseHeld=false;held.Step();
 Lifecycle.Call(held.input,"OnApplicationFocus",false);Lifecycle.Call(held.input,"OnApplicationFocus",true);
 start=held.root.transform.position;for(int i=0;i<10;i++)held.Step();
 Check(held.root.transform.position.z>start.z+.1f,"focus return does not require releasing movement key");
 held.vitality.SetMissionBlocked(true);Check(held.vitality.Retry(),"retry while movement key held succeeds");
 held.vitality.SetMissionBlocked(false);start=held.root.transform.position;for(int i=0;i<10;i++)held.Step();
 Check(held.root.transform.position.z>start.z+.1f,"retry restores real movement without neutral keyboard frame");
 Input.held.Clear();Input.held.Add(KeyCode.UpArrow);held.input.CancelUntilRelease();start=held.root.transform.position;
 for(int i=0;i<10;i++)held.Step();
 Check(held.root.transform.position.z>start.z+.1f,"arrow-key movement also reaches real Motor during cancellation gate");
 Input.Clear();held.Step();Input.mousePosition=new Vector2(950,350);Input.MouseHeld=Input.MouseDown=true;held.Step();
 Check(held.input.FireHeld&&held.input.FirePressed,"fresh fire pointer works after old pointer release");
 Input.MouseDown=false;Input.held.Add(KeyCode.W);held.health.TakeDamage(new IntercomDamage(1000,Vector3.zero,Vector3.forward,null));
 start=held.root.transform.position;for(int i=0;i<10;i++)held.Step();
 Check(held.input.Move.sqrMagnitude==0&&!held.input.FireHeld,"held movement/fire cannot bypass actual health death lock");
 Check((held.root.transform.position-start).sqrMagnitude==0,"dead actor stays still despite held keyboard");
 Input.Clear();
 var mobile=new Hero(false);Application.isMobilePlatform=true;
 Check(mobile.input.mission==mobile.mission,"actual Director repairs missing Input.mission before acquiring its lock");
 GUI.labels.Clear();Lifecycle.Call(mobile.input,"OnGUI");
 Check(GUI.labels.Exists(label=>label.Contains("50 РОБОТОВ")),"repaired input draws the actual mission selection instead of ordinary locked HUD");
 var mobileLayout=IntercomHudLayout.Calculate(Screen.safeArea.x,Screen.safeArea.y,Screen.safeArea.width,Screen.safeArea.height,70,32);
 Input.touches=new[]{new Touch{fingerId=11,phase=TouchPhase.Began,position=new Vector2(mobileLayout.MissionFirst.X+mobileLayout.MissionFirst.Width*.5f,mobileLayout.MissionFirst.Y+mobileLayout.MissionFirst.Height*.5f)}};mobile.Step();
 Check(mobile.mission.Phase==IntercomMissionPhase.Active&&mobile.motor.enabled,"touch card starts actual Director and unlocks actual Motor");
 Input.Clear();mobile.Step();
 var thumb=new Vector2(mobileLayout.LeftX,mobileLayout.LeftY+mobileLayout.Radius*.8f);
 Input.touches=new[]{new Touch{fingerId=12,phase=TouchPhase.Began,position=thumb}};mobile.Step();
 var mobileStart=mobile.root.transform.position;
 Input.touches=new[]{new Touch{fingerId=12,phase=TouchPhase.Moved,position=thumb}};
 for(int i=0;i<15;i++)mobile.Step();
 Check(mobile.input.Move.y>.5f,"left touch stick publishes deflection after actual mission start");
 Check(mobile.root.transform.position.z>mobileStart.z+.1f,"left touch stick moves actual Motor after previously missing mission link is repaired");
 mobile.mission.ReturnToSelection();Check(!mobile.motor.enabled,"real Director reacquires its menu lock after movement");
 mobile.mission.enabled=false;Check(mobile.motor.enabled&&mobile.input.mission==null,"disabling actual Director releases only its own lock and input link");
 Application.isMobilePlatform=false;Input.Clear();
 Console.WriteLine($"Player control lifecycle: {assertions} checks passed. Production Input/Motor/Vitality/Health/Director with enabled callbacks and narrow Unity/NPC/navigation stand-ins; not Unity Play Mode.");
 }
}
