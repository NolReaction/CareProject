using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
    private static object Call(object value,string method,params object[] arguments)
    {try{return value.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,arguments);}catch(TargetInvocationException error){throw error.InnerException;}}
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    private sealed class Fixture
    {
        public IntercomNpcController Npc;public IntercomNpcDefinition Definition;public IntercomNpcNavigation Nav;
        public IntercomHealth Player;public IntercomPlayerMotor Motor;public CharacterController Capsule;
        public Fixture(Vector3 target)
        {
            var active=(IList)typeof(IntercomNpcController).GetField("active",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);active.Clear();
            Physics.Hits=null;Time.timeScale=1;Time.deltaTime=.01f;Application.isFocused=true;GameObject.BeforeAwake=null;
            UnityEngine.Object.Destroyed.Clear();UnityEngine.Object.DestroyedColliderWasEnabled.Clear();
            var player=new GameObject("Player");Player=player.AddComponent<IntercomHealth>();Player.team=IntercomTeam.Player;Motor=player.AddComponent<IntercomPlayerMotor>();player.transform.position=target;
            Nav=new GameObject("Navigation").AddComponent<IntercomNpcNavigation>();
            Definition=new IntercomNpcDefinition{bodyMaterial=new Material(),detailMaterial=new Material(),telegraphMaterial=new Material()};
            bool properAwake=false;
            GameObject.BeforeAwake=component=>{if(component is IntercomNpcController n){properAwake=n.definition==Definition&&n.navigation==Nav&&n.targetHealth==Player&&n.resetMotor==Motor&&n.visual!=null&&n.telegraph!=null;}};
            Npc=IntercomNpcController.Create(Definition,Nav,Player,Motor,Vector3.zero,0);Capsule=Npc.GetComponent<CharacterController>();
            Check(properAwake,"factory configures references before activation/Awake");GameObject.BeforeAwake=null;
        }
        public void Step(float dt=.01f){Time.deltaTime=dt;Call(Npc,"Update");}
        public void For(float seconds){for(int i=0;i<(int)Math.Round(seconds/.01f);i++)Step();}
        public void BeginWindup(){Step();Step();Check(Npc.Phase==IntercomNpcPhase.Windup,"close clear target enters windup");}
        public Collider Obstacle()=>new GameObject("Wall or low cover").AddComponent<Collider>();
    }
    private static void Main()
    {Factory();AttackLifecycle();Obstructions();DodgeAndPause();DeathAndReset();PoolAndTarget();Investigation();Navigation();Presentation();PresentationWorkBudget();PoolAllocationBudget();Emergence();SightWorkBudget();Console.WriteLine($"NPC runtime/factory: {checks} assertions passed with narrow Unity/health/navigation stand-ins. Not Unity or PhysX execution.");}
    private static void Emergence()
    {
        var f=new Fixture(new Vector3(0,0,1));
        int objects=GameObject.Created.Count, materials=Material.Created;
        Vector3 root=f.Npc.transform.position;
        var ring=Field<Transform>(f.Npc,"emergenceRing");
        f.Npc.BeginEmergence();
        Check(f.Npc.IsEmerging&&!f.Capsule.enabled&&f.Npc.Health.invulnerable,"arrival disables collision and rejects damage while underground");
        Check(ring.gameObject.activeSelf&&f.Npc.visual.localPosition.y<=-1.4f,"arrival warning precedes emergence above ground");
        Check(!f.Npc.Health.TakeDamage(new IntercomDamage(10,Vector3.zero,Vector3.forward,f.Player.gameObject))&&f.Npc.Health.Current==75,
            "hidden arrival cannot lose health or count as a kill");
        f.For(.1f);
        Check(f.Npc.visual.localPosition.y<-1.4f&&f.Player.DamageCalls==0,"initial ground warning gives readable anticipation");
        f.For(.3f);
        Check(f.Npc.visual.localPosition.y>-.9f&&f.Npc.visual.localPosition.y<0&&f.Player.DamageCalls==0,
            "chassis rises over time without an underground melee attack");
        Check((f.Npc.transform.position-root).sqrMagnitude==0,"arrival never displaces the collision root");
        var arrivalProperties=Field<MaterialPropertyBlock>(f.Npc,"emergenceProperties");
        float initialAlpha=arrivalProperties.Colors[Shader.PropertyToID("_BaseColor")].a;
        f.For(.2f);
        Check(arrivalProperties.Colors[Shader.PropertyToID("_BaseColor")].a<initialAlpha*.6f,
            "arrival warning really fades by property-block alpha, including its flat annulus");
        f.For(.09f);
        Check(!f.Npc.IsEmerging&&f.Capsule.enabled&&!f.Npc.Health.invulnerable&&!ring.gameObject.activeSelf,"arrival completion restores live collider and authored vulnerability");
        Check(f.Npc.visual.localPosition.sqrMagnitude==0&&f.Npc.visual.localScale.y==1&&f.Player.DamageCalls==0,"settle ends at exact original pose before a fresh attack");
        f.BeginWindup();f.For(.2f);Check(f.Player.DamageCalls==0,"arrival cannot carry a hidden prepared strike into live play");
        f.For(.11f);Check(f.Player.DamageCalls==1,"visible emerged enemy eventually performs its normal punch");
        Check(GameObject.Created.Count==objects&&Material.Created==materials,"arrival and attack construct no objects or materials");
        foreach(string stop in new[]{"focus","pause","timescale"})
        {
            f=new Fixture(new Vector3(0,0,1));f.Npc.BeginEmergence();f.For(.2f);float p=f.Npc.EmergenceProgress;
            if(stop=="focus")Call(f.Npc,"OnApplicationFocus",false);
            if(stop=="pause")Call(f.Npc,"OnApplicationPause",true);
            if(stop=="timescale")Time.timeScale=0;
            f.For(.6f);
            Check(f.Npc.IsEmerging&&f.Npc.EmergenceProgress==p&&!f.Capsule.enabled&&f.Player.DamageCalls==0,"suspension freezes arrival without live invisible collision");
            if(stop=="focus")Call(f.Npc,"OnApplicationFocus",true);
            if(stop=="pause")Call(f.Npc,"OnApplicationPause",false);
            if(stop=="timescale")Time.timeScale=1;
            f.For(.5f);Check(!f.Npc.IsEmerging&&f.Capsule.enabled&&!f.Npc.Health.invulnerable,"resume completes rather than bypasses arrival");
        }
        foreach(bool aiOnly in new[]{false,true})
        {
            f=new Fixture(new Vector3(0,0,10));f.Npc.BeginEmergence();f.For(.25f);
            if(aiOnly)f.Npc.enabled=false;else f.Npc.gameObject.SetActive(false);
            Check(!f.Npc.IsEmerging&&!f.Npc.Health.invulnerable&&f.Npc.visual.localPosition.sqrMagnitude==0,
                "deactivate restores flags and exact visual instead of leaving a hidden invulnerable actor");
            Check(!Field<Transform>(f.Npc,"emergenceRing").gameObject.activeSelf,"deactivate clears arrival warning");
            if(aiOnly)f.Npc.enabled=true;else f.Npc.gameObject.SetActive(true);
            Check(f.Capsule.enabled,"reenable remains a valid live actor");
        }
        f=new Fixture(new Vector3(0,0,10));f.Npc.Health.invulnerable=true;f.Npc.BeginEmergence();f.For(.7f);
        Check(f.Npc.Health.invulnerable,"arrival preserves authored invulnerability rather than unconditionally clearing it");
        f.Npc.BeginEmergence();f.Npc.gameObject.SetActive(false);Check(f.Npc.Health.invulnerable,"cancellation preserves preexisting invulnerability");
        f=new Fixture(new Vector3(0,0,10));f.Npc.BeginEmergence();f.For(.2f);f.Motor.RespawnVersion++;Time.timeScale=0;f.Step();
        Check(!f.Npc.IsEmerging&&f.Capsule.enabled&&!f.Npc.Health.invulnerable&&f.Npc.visual.localPosition.sqrMagnitude==0,
            "respawn epoch restores pose and collision even while paused");Time.timeScale=1;
        f.Npc.BeginEmergence();f.Npc.Health.invulnerable=false;
        f.Npc.Health.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,f.Player.gameObject));
        Check(!f.Npc.IsEmerging&&!f.Capsule.enabled&&f.Npc.Health.IsDead,"external forced death cancels arrival without re-enabling dead collision");
        f=new Fixture(new Vector3(0,0,30));objects=GameObject.Created.Count;materials=Material.Created;
        int destroys=UnityEngine.Object.Destroyed.Count;
        for(int life=0;life<100;life++)
        {
            f.Npc.gameObject.SetActive(false);f.Npc.PrepareForSpawn(new Vector3(life%4,0,life%7));
            f.Npc.BeginEmergence();f.Npc.gameObject.SetActive(true);f.For(.7f);
            Check(!f.Npc.IsEmerging&&f.Capsule.enabled&&!f.Npc.Health.invulnerable,"each reused arrival restores its own health/collision lease");
            f.Npc.Health.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,f.Player.gameObject));
        }
        Check(GameObject.Created.Count==objects&&Material.Created==materials&&UnityEngine.Object.Destroyed.Count==destroys,
            "100 pooled arrival/death cycles allocate no GameObjects, materials or destruction work");
    }
    private static void SightWorkBudget()
    {
        var f=new Fixture(new Vector3(0,0,12));f.Capsule.BlockHorizontal=true;
        int rays=Physics.RayCalls;f.For(1f);int actual=Physics.RayCalls-rays;
        Console.WriteLine($"Distant eye-ray sampling: {actual} queries /100 updates over1 second (old controller:100). Near-melee rays remain immediate.");
        Check(actual>=10&&actual<=15,"distant pursuit samples line of sight about13 times per second rather than every frame");
        f.Player.transform.position=new Vector3(0,0,1);rays=Physics.RayCalls;f.BeginWindup();
        Check(Physics.RayCalls-rays>=6,"close melee keeps immediate eye and two-height contact rays");
        var blocker=f.Obstacle();Physics.Hits=(a,b,c)=>new[]{blocker};f.For(.4f);
        Check(f.Player.DamageCalls==0,"cached long-range visibility never allows a close strike through newly placed cover");
        f=new Fixture(new Vector3(0,0,12));f.Capsule.BlockHorizontal=true;f.Step();
        rays=Physics.RayCalls;f.Nav.Version++;f.Step();
        Check(Physics.RayCalls==rays+1,"changed navigation geometry invalidates distant visibility immediately");
        Vector3 remembered=Field<Vector3>(f.Npc,"lastSeen");
        blocker=f.Obstacle();Physics.Hits=(a,b,c)=>new[]{blocker};f.Player.transform.position=new Vector3(12,0,0);f.Step();
        Check((Field<Vector3>(f.Npc,"lastSeen")-remembered).sqrMagnitude==0,
            "cached visibility retains last measured location rather than reading live motion behind a new wall");
    }
    private static void PresentationWorkBudget()
    {
        var f=new Fixture(new Vector3(0,0,30));
        int writes=f.Npc.bodyRenderer.PropertyWrites;
        f.For(.2f);
        Check(f.Npc.bodyRenderer.PropertyWrites==writes,"unchanged NPC tint does not submit repeated property blocks");
        int rays=Physics.RayCalls;
        f.Npc.Health.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,f.Player.gameObject));
        Check(Physics.RayCalls==rays,"lethal hit avoids unnecessary alert/line-of-sight ray");
        f.For(.6f);writes=f.Npc.bodyRenderer.PropertyWrites;
        f.For(1);
        Check(f.Npc.bodyRenderer.PropertyWrites==writes,"settled corpse no longer resubmits presentation");
        f.Npc.gameObject.SetActive(false);f.Npc.PrepareForSpawn(Vector3.zero);f.Npc.gameObject.SetActive(true);
        Check(f.Npc.bodyRenderer.PropertyWrites>writes&&f.Npc.visual.localScale.y==1,
            "pooled new life restores live color and full pose after presentation optimization");
    }
    private static void PoolAllocationBudget()
    {
        var f=new Fixture(new Vector3(0,0,30));
        var actors=new IntercomNpcController[8];actors[0]=f.Npc;
        for(int i=1;i<actors.Length;i++)actors[i]=IntercomNpcController.Create(f.Definition,f.Nav,f.Player,f.Motor,new Vector3(i,0,0),i);
        foreach(var actor in actors)actor.gameObject.SetActive(false);
        int created=GameObject.Created.Count,materials=Material.Created,destroyed=UnityEngine.Object.Destroyed.Count;
        for(int life=0;life<100;life++)
        {
            var actor=actors[life%actors.Length];actor.PrepareForSpawn(new Vector3(life%4,0,life%7));
            actor.SetTarget(f.Player);actor.gameObject.SetActive(true);actor.Investigate(f.Player.transform.position);
            actor.Health.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,f.Player.gameObject));
            actor.gameObject.SetActive(false);
        }
        Check(GameObject.Created.Count==created,"100 real controller/factory pool lives construct no additional GameObjects");
        Check(Material.Created==materials&&UnityEngine.Object.Destroyed.Count==destroyed,
            "100 pooled deaths create no materials or deferred destruction requests");
        foreach(var actor in actors)UnityEngine.Object.Destroy(actor.gameObject);
    }
    private static void Factory()
    {
        var f=new Fixture(new Vector3(0,0,6));
        Check(f.Npc.Health.Current==75&&f.Npc.Health.team==IntercomTeam.Enemy,"factory gives configured health and enemy team");
        Check(f.Capsule.enabled&&f.Capsule.height==1.35f&&f.Capsule.radius==.32f,"one enabled body controller at intended capsule dimensions");
        Check(UnityEngine.Object.DestroyedColliderWasEnabled.Count==5&&UnityEngine.Object.DestroyedColliderWasEnabled.All(x=>!x),"every visual primitive collider disabled before deferred destruction");
        Check(f.Npc.bodyRenderer.sharedMaterial==f.Definition.bodyMaterial,"body uses authored shared material");
        Check(f.Npc.GetComponent<IntercomFootstepSurface>().kind==IntercomSurfaceKind.Metal
            && f.Npc.GetComponent<IntercomFootstepSurface>().impactKind==IntercomImpactKind.Robot,
            "robot impact character is explicit and independent from metal contact material");
        int created=GameObject.Created.Count;
        try{IntercomNpcController.Create(new IntercomNpcDefinition(),f.Nav,f.Player,f.Motor,Vector3.zero,0);throw new Exception("missing materials accepted");}catch(InvalidOperationException){checks++;}
        Check(GameObject.Created.Count==created,"invalid factory inputs create no partial enemy");
        try{IntercomNpcController.Create(f.Definition,null,f.Player,f.Motor,Vector3.zero,0);throw new Exception("missing navigation accepted");}catch(InvalidOperationException){checks++;}
    }
    private static void AttackLifecycle()
    {
        var f=new Fixture(new Vector3(0,0,1));f.BeginWindup();var planted=f.Npc.transform.position;
        Check(f.Npc.telegraph.gameObject.activeSelf,"windup warning is visible");
        f.For(.28f);Check(f.Player.DamageCalls==0,"windup has no early contact damage");
        Check((f.Npc.transform.position-planted).sqrMagnitude<.00001f,"windup feet remain planted");
        f.For(.03f);Check(f.Player.DamageCalls==1&&f.Player.Current==88,"one real punch applies configured12damage");
        Check(f.Npc.Phase==IntercomNpcPhase.Recovery&&!f.Npc.telegraph.gameObject.activeSelf,"strike clears telegraph and enters recovery");
        Check(f.Player.LastDamage.Instigator==f.Npc.gameObject,"damage carries enemy ownership");
        f.For(.4f);Check(f.Player.DamageCalls==1,"recovery does not repeat hit while touching player");
        f=new Fixture(new Vector3(0,0,1));f.BeginWindup();f.For(.2f);f.Player.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,null));
        f.For(.4f);Check(f.Player.DamageCalls==1&&(f.Npc.Phase==IntercomNpcPhase.Return||f.Npc.Phase==IntercomNpcPhase.Idle),"dead target cancels pending punch and may finish returning home");
    }
    private static void Obstructions()
    {
        foreach(bool low in new[]{true,false})
        {
            var f=new Fixture(new Vector3(0,0,1));var obstacle=f.Obstacle();
            Physics.Hits=(origin,direction,length)=>(low?origin.y<.4f:origin.y>.8f)?new[]{obstacle}:Array.Empty<Collider>();
            f.Step();f.Step();Check(f.Npc.Phase!=IntercomNpcPhase.Windup&&f.Player.DamageCalls==0,low?"low cover prevents melee despite clear eye ray":"upper wall ray prevents melee");
        }
        var pending=new Fixture(new Vector3(0,0,1));pending.BeginWindup();pending.For(.2f);var blocker=pending.Obstacle();
        Physics.Hits=(origin,direction,length)=>origin.y<.4f?new[]{blocker}:Array.Empty<Collider>();
        pending.For(.2f);Check(pending.Player.DamageCalls==0,"new low obstruction rechecked at strike instant");
        var beside=new Fixture(new Vector3(0,0,1));beside.Nav.CheckSegment=(a,b)=>false;beside.BeginWindup();beside.For(.5f);
        Check(beside.Player.DamageCalls==1,"legal beside-wall melee uses real rays instead of inflated navigation mask");
        var crowded=new Fixture(new Vector3(0,0,1));var own=crowded.Capsule;var ally=new GameObject("Ally").AddComponent<CharacterController>();
        Physics.Hits=(origin,direction,length)=>new Collider[]{own,ally};crowded.BeginWindup();crowded.For(.5f);
        Check(crowded.Player.DamageCalls==1,"actor capsules do not masquerade as world sight blockers");
        var saturated=new Fixture(new Vector3(0,0,1));var wall=saturated.Obstacle();Physics.Hits=(a,b,c)=>Enumerable.Repeat(wall,24).ToArray();saturated.Step();saturated.Step();
        Check(saturated.Player.DamageCalls==0&&saturated.Npc.Phase!=IntercomNpcPhase.Windup,"saturated ray buffer fails closed");
    }
    private static void DodgeAndPause()
    {
        var f=new Fixture(new Vector3(0,0,1));f.BeginWindup();
        f.Player.transform.position=new Vector3(1,0,0);f.For(.31f);
        Check(f.Player.DamageCalls==1,"early sidestep is tracked during readable windup instead of a guaranteed miss");
        Check(f.Npc.transform.forward.x>.8f,"body and telegraph follow tracked early attack heading");
        f=new Fixture(new Vector3(0,0,1));f.BeginWindup();f.For(.24f);var heading=f.Npc.transform.forward;
        f.Player.transform.position=new Vector3(1,0,0);f.For(.07f);
        Check(f.Player.DamageCalls==0,"late sidestep after commit escapes strike arc");
        Check((f.Npc.transform.forward-heading).sqrMagnitude<.0001f,"committed strike does not home during final punch");
        f=new Fixture(new Vector3(0,0,1));f.BeginWindup();f.Player.transform.position=new Vector3(0,0,3);f.For(.31f);
        Check(f.Player.DamageCalls==0,"tracking never extends hit distance to a fleeing target");
        foreach(string interrupt in new[]{"focus","application","timescale","disable"})
        {
            f=new Fixture(new Vector3(0,0,1));f.BeginWindup();f.For(.2f);
            if(interrupt=="focus")Call(f.Npc,"OnApplicationFocus",false);
            if(interrupt=="application")Call(f.Npc,"OnApplicationPause",true);
            if(interrupt=="timescale")Time.timeScale=0;
            if(interrupt=="disable")f.Npc.enabled=false;
            if(interrupt!="disable")f.Step();
            Check(!f.Npc.telegraph.gameObject.activeSelf&&f.Npc.Phase!=IntercomNpcPhase.Windup,"interruption clears pending warning/attack");
            if(interrupt=="focus")Call(f.Npc,"OnApplicationFocus",true);
            if(interrupt=="application")Call(f.Npc,"OnApplicationPause",false);
            if(interrupt=="timescale")Time.timeScale=1;
            if(interrupt=="disable")f.Npc.enabled=true;
            f.For(.2f);Check(f.Player.DamageCalls==0,"resume cannot finish the stale pre-pause attack");
        }
    }
    private static void DeathAndReset()
    {
        var f=new Fixture(new Vector3(0,0,1));f.BeginWindup();f.For(.2f);f.Motor.RespawnVersion++;f.Step(.1f);
        Check(f.Npc.Phase==IntercomNpcPhase.Idle&&f.Player.DamageCalls==0&&!f.Npc.telegraph.gameObject.activeSelf,"respawn aborts pending attack before any damage");
        f.BeginWindup();f.Npc.Health.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,f.Player.gameObject));
        Check(f.Npc.Phase==IntercomNpcPhase.Dead&&!f.Capsule.enabled,"death immediately disables movement collider");
        f.For(.6f);Check(f.Player.DamageCalls==0&&f.Npc.visual.localScale.y<.2f,"dead capsule collapses without damaging player");
        f.Motor.RespawnVersion++;Time.timeScale=0;f.Step();
        Check(f.Npc.Health.Current==75&&f.Capsule.enabled&&f.Npc.Phase==IntercomNpcPhase.Idle,"respawn restores dead enemy even while paused");
        Check(f.Npc.visual.localScale.y==1&&(f.Npc.transform.position-Vector3.zero).sqrMagnitude<.00001f,"reset restores home and visual scale");
        Time.timeScale=1;
        f.Npc.enabled=false;f.Npc.Health.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,f.Player.gameObject));
        Check(!f.Capsule.enabled&&f.Npc.Phase==IntercomNpcPhase.Dead,"health death callback clears collision while AI component is disabled");
        f.Definition.maximumHealth=91;f.Npc.ResetToHome();f.Npc.enabled=true;
        Check(f.Npc.Health.Current==91&&f.Npc.Health.Maximum==91,"reset reapplies edited archetype maximum health");
        var subscribed=(Delegate)typeof(IntercomHealth).GetField("Damaged",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f.Npc.Health);
        Check(subscribed.GetInvocationList().Length==1,"disable/enable keeps exactly one health callback subscription");
    }
    private static void PoolAndTarget()
    {
        var f=new Fixture(new Vector3(0,0,1));f.BeginWindup();f.For(.2f);
        f.Npc.Health.TakeDamage(new IntercomDamage(20,Vector3.zero,Vector3.forward,f.Player.gameObject));
        var civilianObject=new GameObject("Rescue ally");var civilian=civilianObject.AddComponent<IntercomHealth>();
        civilian.team=IntercomTeam.Player;civilianObject.transform.position=new Vector3(1,0,0);
        f.Npc.SetTarget(civilian);
        Check(f.Npc.targetHealth==civilian&&f.Npc.Health.Current==55,"target handoff does not heal an injured enemy");
        Check(f.Npc.Phase!=IntercomNpcPhase.Windup&&!f.Npc.telegraph.gameObject.activeSelf,"target handoff cancels old telegraph and pending punch");
        f.For(.1f);Check(f.Player.DamageCalls==0&&civilian.DamageCalls==0,"new target never receives remaining old windup");
        f.Npc.SetTarget(null);f.For(.4f);
        Check(civilian.DamageCalls==0,"cleared target cannot receive lingering melee damage");
        f.Npc.Health.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,f.Player.gameObject));
        f.Npc.SetTarget(f.Player);
        Check(f.Npc.Health.IsDead&&f.Npc.Phase==IntercomNpcPhase.Dead&&!f.Capsule.enabled,"retarget cannot revive a dead pooled actor");
        f.Npc.gameObject.SetActive(false);
        var spawn=new Vector3(5,0,-3);f.Npc.PrepareForSpawn(spawn);
        Check(f.Npc.Health.Current==75&&f.Npc.Phase==IntercomNpcPhase.Idle,"pool preparation resets health and brain");
        Check((f.Npc.transform.position-spawn).sqrMagnitude<.000001f&&!f.Npc.gameObject.activeSelf,"pool preparation relocates without activating the actor");
        Check(!f.Npc.telegraph.gameObject.activeSelf&&f.Npc.visual.localScale.y==1,"pool preparation clears prior death and windup presentation");
        f.Npc.gameObject.SetActive(true);f.Npc.transform.position=new Vector3(8,0,8);f.Motor.RespawnVersion++;f.Step();
        Check((f.Npc.transform.position-spawn).sqrMagnitude<.000001f,"respawn epoch uses pool actor's latest home");
        var callbacks=(Delegate)typeof(IntercomHealth).GetField("Damaged",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f.Npc.Health);
        Check(callbacks.GetInvocationList().Length==1,"pool reuse retains one health subscription");
        f=new Fixture(new Vector3(0,0,1));f.BeginWindup();f.For(.2f);
        Call(f.Npc,"OnApplicationFocus",false);Call(f.Npc,"OnApplicationPause",true);
        f.Npc.gameObject.SetActive(false);Application.isFocused=true;
        f.Npc.PrepareForSpawn(Vector3.zero);f.Npc.gameObject.SetActive(true);f.BeginWindup();
        f.For(.2f);Check(f.Player.DamageCalls==0,"reused actor needs a fresh windup despite stale paused previous life");
        f.For(.11f);Check(f.Player.DamageCalls==1,"explicit focused new life recovers even when inactive pool missed resume callbacks");
        f=new Fixture(new Vector3(0,0,1));f.Npc.gameObject.SetActive(false);Application.isFocused=false;
        f.Npc.PrepareForSpawn(Vector3.zero);f.Npc.gameObject.SetActive(true);f.For(.5f);
        Check(f.Player.DamageCalls==0&&f.Npc.Phase==IntercomNpcPhase.Idle,"prepare observes current application focus and cannot activate offscreen combat");
        Application.isFocused=true;
    }
    private static void Investigation()
    {
        var f=new Fixture(new Vector3(0,0,6));var wall=f.Obstacle();
        Physics.Hits=(a,b,c)=>new[]{wall};f.Capsule.BlockHorizontal=true;
        var known=new Vector3(5,0,0);f.Npc.Investigate(known);
        f.Player.transform.position=new Vector3(-8,0,7);f.For(3f);
        Check((Field<Vector3>(f.Npc,"lastSeen")-known).sqrMagnitude==0,"one-time alarm remembers its location, not invisible live player motion");
        Check(f.Npc.Phase==IntercomNpcPhase.Chase&&f.Player.DamageCalls==0,"explicit search outlasts short sight memory without attacking through wall");
        Call(f.Npc,"OnApplicationFocus",false);f.Step();
        Check(f.Npc.Phase==IntercomNpcPhase.Idle,"losing focus suspends the active search brain");
        Call(f.Npc,"OnApplicationFocus",true);f.Step();
        Check(f.Npc.Phase==IntercomNpcPhase.Chase&&(Field<Vector3>(f.Npc,"lastSeen")-known).sqrMagnitude==0,"resume continues recorded search without acquiring hidden coordinates");
        f.Player.transform.position=new Vector3(30,0,30);f.Step();
        Check(f.Npc.Phase==IntercomNpcPhase.Chase,"unseen player's remote position does not cancel known-location search");
        f.Npc.Investigate(new Vector3(float.NaN,0,0));
        Check((Field<Vector3>(f.Npc,"lastSeen")-known).sqrMagnitude==0,"invalid alarm cannot corrupt search coordinates");
        f.For(12f);
        Check(Field<float>(f.Npc,"investigateRemaining")==0&&f.Npc.Phase!=IntercomNpcPhase.Chase,"unreachable search eventually gives up instead of pursuing forever");
        f.Npc.Investigate(known);f.Npc.SetTarget(null);
        Check(Field<float>(f.Npc,"investigateRemaining")==0,"retarget clears old search order");
        f.Npc.Health.TakeDamage(new IntercomDamage(20,Vector3.zero,Vector3.forward,f.Player.gameObject));
        Vector3 position=f.Npc.transform.position;f.Npc.PrepareForSpawn(new Vector3(0,float.PositiveInfinity,0));
        Check((f.Npc.transform.position-position).sqrMagnitude==0&&f.Npc.Health.Current==55,"invalid pool spawn changes neither position nor health");
    }
    private static void Navigation()
    {
        var f=new Fixture(new Vector3(0,0,6));f.Step(.05f);
        Check(f.Npc.transform.position.z>0&&f.Npc.transform.position.z<=.026f,"chase accelerates smoothly rather than instant top speed");
        Check(f.Nav.PathRequests==0,"clear open ground tracks directly without consuming path-search budget");
        var direct=(Vector3)Call(f.Npc,"Navigate",new Vector3(6,0,0),.01f);
        Check(direct.x>.99f&&Math.Abs(direct.z)<.01f,"clear moving target updates chase immediately without waiting for a stale route");
        f.Nav.CheckSegment=(a,b)=>b.z<5&&b.x<5;
        f.Nav.Path=(a,b)=>new[]{new Vector3(2,0,2),b};
        var original=(Vector3)Call(f.Npc,"Navigate",new Vector3(0,0,6),.4f);
        var route=Field<List<Vector3>>(f.Npc,"route");Vector3 old=route[0];f.Nav.NextResult=IntercomNavResult.Deferred;
        var deferred=(Vector3)Call(f.Npc,"Navigate",new Vector3(6,0,0),.4f);
        Check(route.Count==2&&(route[0]-old).sqrMagnitude==0&&deferred.z>0,"deferred search preserves and follows still-safe old route");
        f.Nav.CheckSegment=(a,b)=>false;var blocked=(Vector3)Call(f.Npc,"Navigate",new Vector3(6,0,0),.1f);
        Check(route.Count==0&&blocked.sqrMagnitude==0,"new obstruction stops old route even if fresh search deferred");
        Check(Field<float>(f.Npc,"repathRemaining")==0,"deferred search requests retry next frame without a stale cooldown");
        f=new Fixture(new Vector3(0,0,6));f.Nav.CheckSegment=(a,b)=>(b-a).magnitude>.4f;
        f.Step(.05f);Check(f.Npc.transform.position.z==0&&Field<Vector3>(f.Npc,"velocity").sqrMagnitude==0,"final acceleration step checked against nav grid, not only distant waypoint");
        f=new Fixture(new Vector3(0,0,6));
        var other=IntercomNpcController.Create(f.Definition,f.Nav,f.Player,f.Motor,new Vector3(.2f,0,.3f),1);
        f.Nav.CheckSegment=(a,b)=>b.x>=a.x-.00001f;
        var steered=(Vector3)Call(f.Npc,"Navigate",new Vector3(0,0,6),.1f);
        Check(Math.Abs(steered.x)<.00001f&&steered.z>0,"crowd separation cannot steer across blocked corner");
        UnityEngine.Object.Destroy(other.gameObject);
        f=new Fixture(new Vector3(0,0,3));
        other=IntercomNpcController.Create(f.Definition,f.Nav,f.Player,f.Motor,new Vector3(.2f,0,1.8f),1);
        var slot=(Vector3)Call(f.Npc,"ApproachDestination",f.Player.transform.position,true,3f);
        var second=(Vector3)Call(other,"ApproachDestination",f.Player.transform.position,true,(f.Player.transform.position-other.transform.position).magnitude);
        Check((slot-second).sqrMagnitude>.1f,"nearby pursuers receive distinct persistent approach sides");
        Check((slot-f.Player.transform.position).magnitude<=f.Definition.attackRange,"approach slots remain within real melee reach");
        var stable=(Vector3)Call(f.Npc,"ApproachDestination",f.Player.transform.position,true,3f);
        Check((slot-stable).sqrMagnitude==0,"approach choice has no frame-to-frame random jitter");
        var unseen=(Vector3)Call(f.Npc,"ApproachDestination",f.Player.transform.position,false,3f);
        Check((unseen-f.Player.transform.position).sqrMagnitude==0,"unseen target does not trigger fresh group encirclement");
        f.Nav.CheckSegment=(a,b)=>false;
        var unsafeSlot=(Vector3)Call(f.Npc,"ApproachDestination",f.Player.transform.position,true,3f);
        Check((unsafeSlot-f.Player.transform.position).sqrMagnitude==0,"wall-blocked slot falls back to pathfinding towards known destination");
        f=new Fixture(new Vector3(0,0,6));f.Capsule.BlockHorizontal=true;f.For(.5f);
        Check(Field<float>(f.Npc,"recoveryRemaining")>0,"sustained collision starts bounded lateral unsticking");
        var escape=Field<Vector3>(f.Npc,"recoveryDirection");
        Check(Math.Abs(escape.x)>.99f&&Math.Abs(escape.z)<.01f,"unsticking uses a clear side of the blocked forward path");
        f.Capsule.BlockHorizontal=false;f.For(.1f);
        Check(Math.Abs(f.Npc.transform.position.x)>.01f,"released blockage lets the lateral recovery move the capsule");
        f=new Fixture(new Vector3(0,0,6));f.Nav.CheckSegment=(a,b)=>false;Call(f.Npc,"BeginUnstuck",Vector3.forward);
        Check(Field<float>(f.Npc,"recoveryRemaining")==0,"blocked recovery alternatives never force a wall-crossing escape");
    }
    private static void Presentation()
    {
        var f=new Fixture(new Vector3(0,0,6));int materials=Material.Created;f.For(2f);
        Check(Material.Created==materials,"runtime updates create no new material instances");
        Mesh wedge=f.Npc.telegraph.GetComponent<MeshFilter>().sharedMesh;
        Check(wedge.vertices.Length==18&&wedge.triangles.Length==48,"bounded warning fan mesh");
        foreach(var point in wedge.vertices)Check(point.sqrMagnitude<=f.Definition.attackRange*f.Definition.attackRange+.0001f&&point.y==0,"warning lies on ground within actual reach");
        for(int i=0;i<wedge.triangles.Length;i+=3)
        {
            Vector3 a=wedge.vertices[wedge.triangles[i+1]],b=wedge.vertices[wedge.triangles[i+2]];
            Check(a.z*b.x-a.x*b.z>0,"warning fan faces upward");
        }
        Mesh arrival=Field<Mesh>(f.Npc,"ownedEmergenceMesh");
        Check(arrival!=null&&arrival.vertices.Length==114&&arrival.triangles.Length==162,"arrival geometry is one small bounded reused mesh");
        UnityEngine.Object.Destroy(f.Npc.gameObject);Check(UnityEngine.Object.Destroyed.Contains(wedge),"owned temporary warning mesh disposed with actor");
        Check(UnityEngine.Object.Destroyed.Contains(arrival),"owned arrival warning mesh disposed with actor");
    }
}
