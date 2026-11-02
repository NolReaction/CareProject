using System;using System.Linq;using System.Collections.Generic;using System.Reflection;using IntercomMvp;using UnityEngine;
class Program {
 static int checks,cases;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Near(float a,float b,string s)=>Check(Math.Abs(a-b)<.00001f,s+": "+a+" != "+b);
 static void Call(object x,string n,params object[] args){try{x.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(x,args);}catch(TargetInvocationException e){throw e.InnerException;}}
 static T Field<T>(object x,string n)=>(T)x.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Public).GetValue(x);
 static void Case(string name,Action run){run();cases++;Console.WriteLine("PASS "+name);}
 sealed class Dummy:MonoBehaviour,IIntercomDamageable {public List<float> hits=new List<float>();public bool TakeDamage(IntercomDamage hit){hits.Add(hit.Amount);return true;}}
 sealed class Rig {
  public GameObject root;public IntercomSimpleShooter shooter;public IntercomWeaponMagazine mag;public IntercomWeaponPose pose;public IntercomWeaponDefinition weapon;
  public int volleys,barrels,reportedRounds,reloads;public List<string> order=new List<string>();
  public Rig(int capacity=24,int pool=32){
   Physics.Overlap=null;Physics.Sweep=null;Time.timeScale=1;Time.deltaTime=.001f;
   root=new GameObject("Robot");root.AddComponent<CharacterController>();root.AddComponent<IntercomPlayerMotor>();root.AddComponent<IntercomPlayerInput>();pose=root.AddComponent<IntercomWeaponPose>();
   shooter=root.AddComponent<IntercomSimpleShooter>();weapon=new IntercomWeaponDefinition{magazineSize=capacity};shooter.weapon=weapon;shooter.poolSize=pool;shooter.weaponPose=pose;
   shooter.muzzleRight=new GameObject("Right muzzle").transform;shooter.muzzleRight.SetParent(root.transform,false);shooter.muzzleRight.localPosition=new Vector3(.12f,1,.5f);shooter.muzzleRight.localRotation=Quaternion.Euler(0,-8,0);
   shooter.muzzleLeft=new GameObject("Left muzzle").transform;shooter.muzzleLeft.SetParent(root.transform,false);shooter.muzzleLeft.localPosition=new Vector3(-.12f,1,.5f);shooter.muzzleLeft.localRotation=Quaternion.Euler(0,8,0);
   mag=root.AddComponent<IntercomWeaponMagazine>();mag.shooter=shooter;Call(mag,"Awake");Call(shooter,"Awake");Call(shooter,"OnEnable");
   shooter.BarrelFired+=(m,right)=>{barrels++;order.Add("barrel:"+mag.CurrentAmmo);};
   shooter.VolleyFired+=(p,n)=>{volleys++;reportedRounds+=n;order.Add("volley:"+mag.CurrentAmmo);};mag.ReloadStarted+=()=>reloads++;
  }
  public void Shot(){pose.EmitShot();Call(shooter,"LateUpdate");}
  public object[] Active()=>Field<Array>(shooter,"pool").Cast<object>().Where(x=>Field<bool>(x,"active")).ToArray();
  public void Clear(){Call(shooter,"OnDisable");Call(shooter,"OnDestroy");}
 }
 static void Main(){
  Case("actual two-barrel Spawn uses one heading and consumes once before callbacks",()=>{
   var r=new Rig();Vector3 ro=r.shooter.muzzleRight.position,lo=r.shooter.muzzleLeft.position;r.Shot();var p=r.Active();
   Check(p.Length==2&&r.mag.CurrentAmmo==22&&r.barrels==2&&r.volleys==1&&r.reportedRounds==2,"two successful barrels produce one volley");
   Check(r.pose.Recoils==1,"one confirmed visual recoil");Check(r.order.All(x=>x.EndsWith(":22")),"all callbacks observe committed ammo");
   Near(Field<Vector3>(p[0],"position").x,ro.x,"right actual origin");Near(Field<Vector3>(p[1],"position").x,lo.x,"left actual origin");
   Near(Field<Vector3>(p[0],"direction").x,0,"right parallel heading");Near(Field<Vector3>(p[1],"direction").x,0,"left parallel heading");
   for(int i=0;i<10;i++)Call(r.shooter,"LateUpdate");p=r.Active();Near((Field<Vector3>(p[0],"position")-Field<Vector3>(p[1],"position")).x,.24f,"actual advanced bullets keep separation");r.Clear();
  });
  Case("both muzzle overlaps block bullets ammo reports and recoil",()=>{
   var r=new Rig();var wall=new GameObject("Wall").AddComponent<Collider>();Physics.Overlap=p=>Math.Abs(p.x)>.05f?new[]{wall}:Array.Empty<Collider>();r.Shot();
   Check(r.Active().Length==0&&r.mag.CurrentAmmo==24&&r.volleys==0&&r.barrels==0&&r.pose.Recoils==0,"blocked volley emits nothing");r.Clear();
  });
  Case("body-to-muzzle obstruction blocks even a clear muzzle point",()=>{
   var r=new Rig();var wall=new GameObject("Thin wall").AddComponent<Collider>();Physics.Sweep=(p,d,n)=>new[]{new RaycastHit{collider=wall,distance=n*.5f,normal=new Vector3(0,0,-1)}};r.Shot();
   Check(r.Active().Length==0&&r.mag.CurrentAmmo==24&&r.volleys==0,"no shooting through thinwall");r.Clear();
  });
  Case("one obstructed barrel consumes only the one real projectile",()=>{
   var r=new Rig();var wall=new GameObject("Right wall").AddComponent<Collider>();Physics.Overlap=p=>p.x>.05f?new[]{wall}:Array.Empty<Collider>();r.Shot();
   Check(r.Active().Length==1&&r.mag.CurrentAmmo==23&&r.barrels==1&&r.volleys==1&&r.reportedRounds==1&&r.pose.Recoils==1,"one clear left barrel");Near(Field<Vector3>(r.Active()[0],"position").x,-.12f,"clear muzzle origin retained");r.Clear();
  });
  Case("empty magazine starts reload after exactly twelve actual volleys",()=>{
   var r=new Rig();for(int i=0;i<12;i++)r.Shot();Check(r.Active().Length==24&&r.mag.CurrentAmmo==0&&r.mag.IsReloading&&r.reloads==1&&r.volleys==12,"last real projectile starts one reload");
   for(int i=0;i<10;i++)r.Shot();Check(r.volleys==12&&r.barrels==24&&r.pose.Recoils==12,"reload suppresses shot and visual callbacks");
   Time.deltaTime=1.15f;Call(r.mag,"Update");Time.deltaTime=.001f;r.Shot();Check(r.mag.CurrentAmmo==22&&r.volleys==13&&!r.mag.IsReloading,"fresh completed magazine resumes actual volley");r.Clear();
  });
  Case("queued attempts cannot fire beyond a one-volley magazine",()=>{
   var r=new Rig(2);for(int i=0;i<4;i++)r.pose.EmitShot();Call(r.shooter,"LateUpdate");Check(r.Active().Length==2&&r.mag.CurrentAmmo==0&&r.reloads==1&&r.volleys==1,"pending queue cannot overdraw ammo");r.Clear();
  });
  Case("pool exhaustion consumes no additional round or visual event",()=>{
   var r=new Rig(24,4);r.Shot();r.Shot();r.Shot();Check(r.Active().Length==4&&r.mag.CurrentAmmo==20&&r.volleys==2&&r.pose.Recoils==2,"no phantom shot when boundedpoolfull");r.Clear();
  });
  Case("reload requested between queue and spawn cancels the pending attempt",()=>{
   var r=new Rig();r.Shot();r.pose.EmitShot();Check(r.mag.RequestReload(),"partial manualreload allowed");Call(r.shooter,"LateUpdate");Check(r.mag.CurrentAmmo==22&&r.volleys==1,"queuedattempt seesnewreloadstate");r.Clear();
  });
  Case("focus pause and explicit cancellation drop pending shots",()=>{
   foreach(string method in new[]{"OnApplicationFocus","OnApplicationPause"}){
    var r=new Rig();r.pose.EmitShot();Call(r.shooter,method,method=="OnApplicationPause");Call(r.shooter,"LateUpdate");Call(r.shooter,method,method!="OnApplicationPause");Call(r.shooter,"LateUpdate");Check(r.volleys==0&&r.mag.CurrentAmmo==24,"noqueuedresume "+method);r.Clear();
   }
   var q=new Rig();q.pose.EmitShot();q.pose.Cancel();Call(q.shooter,"LateUpdate");Check(q.volleys==0,"FireCancelleddropsqueue");q.Shot();Call(q.shooter,"OnDisable");Check(q.Active().Length==0,"disable releasesactiveprojectiles");q.pose.EmitShot();Call(q.shooter,"OnEnable");Call(q.shooter,"LateUpdate");Check(q.volleys==1,"disabled unsubscribed pose cannotqueueshot");q.Clear();
  });
  Case("real swept hit routes material and uses per-projectile damage snapshot",()=>{
   foreach(var kind in new[]{IntercomSurfaceKind.Metal,IntercomSurfaceKind.Wood,IntercomSurfaceKind.Concrete}){
    var r=new Rig();var target=new GameObject("Target");var collider=target.AddComponent<Collider>();target.AddComponent<IntercomFootstepSurface>().kind=kind;var receiver=target.AddComponent<Dummy>();
    int hits=0;r.shooter.DetailedProjectileHit+=hit=>{Check(hit.Surface==kind&&hit.TargetHit,"actualHit routes exactsurface");hits++;};r.Shot();r.weapon.damagePerProjectile=50;
    Physics.Sweep=(p,d,n)=>p.z>=.49f?new[]{new RaycastHit{collider=collider,distance=.01f,normal=new Vector3(0,0,-1),point=p+d*.01f}}:Array.Empty<RaycastHit>();
    Call(r.shooter,"LateUpdate");Check(hits==2&&receiver.hits.Count==2&&receiver.hits.All(x=>x==15)&&r.Active().Length==0,"both originaldamage bullets hit once");r.Clear();
   }
  });
  Console.WriteLine("PASS "+cases+" complete Shooter/Magazine integration cases, "+checks+" assertions. Production shot spawn, actual ammo commit, hit routing and pool lifecycle with controlled physics and pose doubles; no Unity/PhysX/rendering.");
 }
}
