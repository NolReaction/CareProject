using System;
using System.Linq;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
    private static void Send(object target,string method)
    {try{target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(target,null);}catch(TargetInvocationException error){throw error.InnerException;}}
    private static void Signal(object target,string method,bool value)
    {try{target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(target,new object[]{value});}catch(TargetInvocationException error){throw error.InnerException;}}
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    private sealed class AllyFixture
    {
        public IntercomHealth Player;
        public IntercomNpcNavigation Nav;
        public IntercomMissionDefinition Definition;
        public IntercomNpcDefinition Appearance;
        public IntercomRescueAlly Ally;
        public CharacterController Capsule;
        public AllyFixture(float distance=1.2f)
        {
            Time.timeScale=1;Time.deltaTime=.01f;Physics.Hits=null;GameObject.BeforeAwake=null;
            UnityEngine.Object.Destroyed.Clear();UnityEngine.Object.DestroyedColliderWasEnabled.Clear();
            Player=new GameObject("Hero").AddComponent<IntercomHealth>();Player.team=IntercomTeam.Player;
            Player.transform.position=new Vector3(0,0,distance);
            Nav=new GameObject("Navigation").AddComponent<IntercomNpcNavigation>();
            Definition=new IntercomMissionDefinition{mode=IntercomMissionMode.Rescue};
            Appearance=new IntercomNpcDefinition{bodyMaterial=new Material(),detailMaterial=new Material(),telegraphMaterial=new Material()};
            bool initialized=false;
            GameObject.BeforeAwake=c=>{if(c is IntercomRescueAlly a)initialized=a.navigation==Nav&&a.playerHealth==Player&&a.definition==Definition&&a.visual!=null;};
            Ally=IntercomRescueAlly.Create(Nav,Player,Appearance,Definition,Vector3.zero);
            GameObject.BeforeAwake=null;Capsule=Ally.GetComponent<CharacterController>();
            Check(initialized,"ally references are set before activation/Awake");
        }
        public void Step(float dt=.01f){Time.deltaTime=dt;Send(Ally,"Update");}
        public void Advance(float seconds){for(int i=0;i<(int)Math.Ceiling(seconds/.01f);i++)Step();}
        public Collider Wall()=>new GameObject("Wall").AddComponent<Collider>();
    }
    private static void Main()
    {
        AllyFactory(); Recruitment(); Following(); AllyLifecycle();
        MissionConnection(); MissionSelection(); WarmPoolBudget(); SpawnSafety(); EliminationPoolAndWaves(); MissionResetAndCleanup(); MissionRescue(); MissionPauseAndTargets();
        Console.WriteLine($"Mission/rescue runtime: {checks} assertions passed against narrow Unity/navigation stand-ins. Not Unity or PhysX execution.");
    }
    private static void AllyFactory()
    {
        var f=new AllyFixture();
        Check(f.Ally.Health.team==IntercomTeam.Player&&f.Ally.Health.Current==60,"rescued robot uses player team and authored health");
        Check(f.Capsule.enabled&&f.Capsule.radius==.29f&&f.Capsule.height==1.22f,"ally has one intended movement capsule");
        Check(UnityEngine.Object.DestroyedColliderWasEnabled.Count==3&&UnityEngine.Object.DestroyedColliderWasEnabled.All(x=>!x),"all ally primitive colliders disabled before deferred removal");
        Check(!f.Ally.Recruited&&f.Ally.RecruitmentProgress==0,"new ally waits for recruitment");
        Check(f.Ally.GetComponent<IntercomFootstepSurface>().kind==IntercomSurfaceKind.Metal,"ally impact surface is metal");
        int created=GameObject.Created.Count;
        try{IntercomRescueAlly.Create(null,f.Player,f.Appearance,f.Definition,Vector3.zero);throw new Exception("invalid factory accepted");}catch(InvalidOperationException){checks++;}
        Check(GameObject.Created.Count==created,"invalid ally factory creates no partial objects");
    }
    private static void Recruitment()
    {
        var f=new AllyFixture();f.Advance(.5f);
        Check(!f.Ally.Recruited&&f.Ally.RecruitmentProgress>.7f,"recruitment requires continuous nearby presence");
        f.Player.transform.position=new Vector3(0,0,5);f.Step();
        Check(!f.Ally.Recruited&&f.Ally.RecruitmentProgress==0,"walking away resets recruitment");
        f.Player.transform.position=new Vector3(0,0,1.2f);f.Advance(.65f);
        Check(f.Ally.Recruited,"continuous clear proximity recruits ally");
        f=new AllyFixture();var wall=f.Wall();Physics.Hits=(a,b,c)=>new[]{wall};f.Advance(1);
        Check(!f.Ally.Recruited&&f.Ally.RecruitmentProgress==0,"wall prevents through-wall recruitment");
        Physics.Hits=(a,b,c)=>Enumerable.Repeat(wall,24).ToArray();f.Advance(.7f);
        Check(!f.Ally.Recruited,"saturated visibility query rejects recruitment");
        Physics.Hits=(a,b,c)=>new Collider[]{f.Capsule};f.Advance(.65f);
        Check(f.Ally.Recruited,"own movement capsule is not a recruitment wall");
    }
    private static void Following()
    {
        var f=new AllyFixture();f.Advance(.65f);f.Player.transform.position=new Vector3(0,0,5);
        Vector3 prior=f.Ally.transform.position;f.Advance(.5f);
        Check(f.Ally.transform.position.z>prior.z+.2f,"recruited ally follows with actual Move calls");
        Check((f.Ally.transform.position-prior).magnitude<2f,"following is speed bounded rather than teleporting");
        f.Player.transform.position=new Vector3(0,0,20);f.Advance(.5f);prior=f.Ally.transform.position;f.Advance(.5f);
        Check(f.Ally.WaitingForPlayer&&(f.Ally.transform.position-prior).sqrMagnitude<.0001f,"ally waits if hero runs too far away");
        f.Player.transform.position=f.Ally.transform.position+new Vector3(0,0,4);f.Advance(.2f);
        Check(!f.Ally.WaitingForPlayer&&f.Capsule.LastMove.z>0,"returning nearby resumes escort");
        f.Nav.CheckSegment=(a,b)=>false;prior=f.Ally.transform.position;f.Advance(.3f);
        Check((f.Ally.transform.position-prior).sqrMagnitude<.0001f,"final navigation guard forbids movement through blocked cells");
        Check(f.Nav.PathRequests>0,"blocked direct pursuit asks shared navigator for path");
        f.Nav.NextResult=IntercomNavResult.Deferred;prior=f.Ally.transform.position;f.Advance(.3f);
        Check((f.Ally.transform.position-prior).sqrMagnitude<.0001f,"deferred blocked path cannot teleport ally");
        f.Nav.CheckSegment=null;f.Nav.NextResult=IntercomNavResult.Success;f.Advance(.2f);
        Check(f.Ally.transform.position.z>prior.z,"opening passage resumes movement");
    }
    private static void AllyLifecycle()
    {
        foreach(string interruption in new[]{"focus","pause","timescale","suspended","disable"})
        {
            var f=new AllyFixture();f.Advance(.4f);Vector3 prior=f.Ally.transform.position;
            if(interruption=="focus")Signal(f.Ally,"OnApplicationFocus",false);
            if(interruption=="pause")Signal(f.Ally,"OnApplicationPause",true);
            if(interruption=="timescale")Time.timeScale=0;
            if(interruption=="suspended")f.Ally.Suspended=true;
            if(interruption=="disable")f.Ally.enabled=false;
            if(interruption!="disable")f.Advance(.5f);
            Check(!f.Ally.Recruited&&(f.Ally.transform.position-prior).sqrMagnitude<.0001f,"interrupted ally neither recruits nor moves");
            if(interruption=="focus")Signal(f.Ally,"OnApplicationFocus",true);
            if(interruption=="pause")Signal(f.Ally,"OnApplicationPause",false);
            if(interruption=="timescale")Time.timeScale=1;
            if(interruption=="suspended")f.Ally.Suspended=false;
            if(interruption=="disable")f.Ally.enabled=true;
            f.Advance(.7f);Check(f.Ally.Recruited,"ally can recruit after resumed active time");
        }
        var dead=new AllyFixture();dead.Advance(.65f);dead.Ally.Health.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,null));
        Check(dead.Ally.Health.IsDead&&!dead.Capsule.enabled,"ally death immediately disables collision");
        dead.Advance(.5f);Check(dead.Ally.visual.localScale.y<.2f,"dead ally presentation collapses");
        dead.Ally.ResetForMission(dead.Definition,new Vector3(4,0,3));
        Check(dead.Ally.Health.Current==60&&dead.Capsule.enabled&&!dead.Ally.Recruited&&!dead.Ally.Suspended,"retry restores health controller and recruitment state");
        Check(dead.Ally.transform.position.x==4&&dead.Ally.transform.position.z==3&&dead.Ally.visual.localScale.y==1,"retry restores authored spawn and presentation");
        Signal(dead.Ally,"OnApplicationFocus",false);Signal(dead.Ally,"OnApplicationPause",true);
        dead.Ally.gameObject.SetActive(false);
        Application.isFocused=true;
        dead.Ally.ResetForMission(dead.Definition,Vector3.zero);dead.Ally.gameObject.SetActive(true);
        dead.Player.transform.position=new Vector3(0,0,1.2f);dead.Advance(.7f);
        Check(dead.Ally.Recruited,"explicit restart refreshes focus missed while ally was inactive");
        Application.isFocused=false;dead.Ally.ResetForMission(dead.Definition,Vector3.zero);dead.Advance(.7f);
        Check(!dead.Ally.Recruited,"restart cannot simulate focus while application is unfocused");
        Application.isFocused=true;
        var noHero=new AllyFixture();noHero.Advance(.65f);noHero.Player.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,null));
        Vector3 position=noHero.Ally.transform.position;noHero.Advance(1);
        Check((noHero.Ally.transform.position-position).sqrMagnitude<.0001f,"dead hero cannot lead ally");
    }

    private sealed class MissionFixture
    {
        public IntercomMissionDirector Director;
        public IntercomNpcNavigation Nav;
        public IntercomHealth Hero;
        public IntercomPlayerMotor Motor;
        public IntercomPlayerVitality Vitality;
        public IntercomMissionDefinition Elimination,Rescue;
        public MissionFixture(Action<MissionFixture> beforeActivate=null)
        {
            Time.timeScale=1;Time.time=0;Time.deltaTime=.01f;Application.isPlaying=true;Physics.Hits=null;
            IntercomNpcController.Created.Clear();UnityEngine.Object.Destroyed.Clear();GameObject.BeforeAwake=null;
            var hero=new GameObject("Hero");Hero=hero.AddComponent<IntercomHealth>();Hero.team=IntercomTeam.Player;
            Motor=hero.AddComponent<IntercomPlayerMotor>();Vitality=hero.AddComponent<IntercomPlayerVitality>();
            Vitality.motor=Motor;Vitality.health=Hero;Vitality.input=hero.AddComponent<IntercomPlayerInput>();
            Nav=new GameObject("Nav").AddComponent<IntercomNpcNavigation>();
            Elimination=new IntercomMissionDefinition{mode=IntercomMissionMode.Elimination,maximumAlive=3,killTarget=50,waveSize=10,spawnInterval=.25f,waveRestSeconds=.5f};
            Rescue=new IntercomMissionDefinition{mode=IntercomMissionMode.Rescue,maximumAlive=3,rescueEnemySupply=12,waveSize=4,spawnInterval=.25f,waveRestSeconds=.5f};
            var root=new GameObject("Mission root");root.SetActive(false);Director=root.AddComponent<IntercomMissionDirector>();
            Director.playerHealth=Hero;Director.playerMotor=Motor;Director.playerVitality=Vitality;Director.navigation=Nav;
            Director.enemyDefinition=new IntercomNpcDefinition{bodyMaterial=new Material(),detailMaterial=new Material(),telegraphMaterial=new Material()};
            Director.eliminationDefinition=Elimination;Director.rescueDefinition=Rescue;
            Director.spawnPoints=new[]{Marker(8,0),Marker(-8,0),Marker(0,8),Marker(0,-8),Marker(8,8),Marker(-8,-8)};
            Director.allySpawn=Marker(0,4);Director.extractionPoint=Marker(0,0);
            beforeActivate?.Invoke(this);
            root.SetActive(true);Send(Director,"Start");
        }
        public static Transform Marker(float x,float z){var go=new GameObject("Marker");go.transform.position=new Vector3(x,0,z);return go.transform;}
        public void Step(float dt=.05f,bool updateAlly=true)
        {
            Time.deltaTime=dt;if(Time.timeScale>0)Time.time+=dt;
            Send(Director,"Update");
            var ally=Ally;if(updateAlly&&ally!=null&&ally.isActiveAndEnabled)Send(ally,"Update");
        }
        public void Advance(float seconds,bool updateAlly=true){for(int i=0;i<(int)Math.Ceiling(seconds/.05f);i++)Step(.05f,updateAlly);}
        public IntercomRescueAlly Ally=>Field<IntercomRescueAlly>(Director,"ally");
        public IntercomNpcController[] Living=>IntercomNpcController.Created.Where(n=>n.gameObject.activeInHierarchy&&!n.Health.IsDead).ToArray();
    }
    private static void MissionConnection()
    {
        // Saved/unsaved V8 scenes can contain the new Director but leave the
        // reverse Input.mission reference empty. That must never create an
        // invisible menu lock behind an otherwise responsive gameplay HUD.
        var f=new MissionFixture();var input=f.Vitality.input;
        Check(input.mission==f.Director && input.vitality==f.Vitality,"director startup connects the exact input that displays its menu");
        Check(f.Vitality.Blocked && f.Vitality.HiddenLockCalls==0,"selection lock is acquired only after a visible input/HUD route exists");
        int cancellations=input.Cancellations;f.Advance(1);
        Check(input.Cancellations==cancellations,"stable selection does not cancel the menu pointer on every frame");
        input.mission=null;f.Step();
        Check(input.mission==f.Director && f.Vitality.HiddenLockCalls==0,"lost reverse link is repaired without an invisible lock");
        var oldVitality=f.Vitality;
        var nextHero=new GameObject("Replacement hero");
        var nextHealth=nextHero.AddComponent<IntercomHealth>();
        var nextMotor=nextHero.AddComponent<IntercomPlayerMotor>();
        var nextVitality=nextHero.AddComponent<IntercomPlayerVitality>();
        var nextInput=nextHero.AddComponent<IntercomPlayerInput>();
        nextVitality.health=nextHealth;nextVitality.motor=nextMotor;nextVitality.input=nextInput;
        f.Director.playerHealth=nextHealth;f.Director.playerMotor=nextMotor;f.Director.playerVitality=nextVitality;f.Step();
        Check(!oldVitality.Blocked && nextVitality.Blocked && nextInput.mission==f.Director,"rebinding hero releases old owned lock and binds replacement menu before blocking");
        Check(input.mission==null && nextVitality.HiddenLockCalls==0,"rebinding clears the old reverse reference without creating a hidden lock");
        nextVitality.enabled=false;f.Step();
        Check(!nextVitality.Blocked && nextInput.mission==null,"loss of vitality releases the block and clears the now unusable HUD reverse link");

        f=new MissionFixture(x=>x.Vitality.input.enabled=false);input=f.Vitality.input;
        Check(!f.Vitality.Blocked && f.Vitality.HiddenLockCalls==0,"disabled input never invisibly blocks hero on startup");
        Check(!f.Director.StartMission(IntercomMissionMode.Elimination),"mission cannot start without an active menu input");
        input.enabled=true;f.Step();
        Check(input.mission==f.Director && input.vitality==f.Vitality && f.Vitality.Blocked,"reenabled input restores the visible selection and its lock");

        f=new MissionFixture(x=>x.Vitality.input=null);
        input=f.Hero.GetComponent<IntercomPlayerInput>();
        Check(f.Vitality.input==input && input.mission==f.Director,"missing explicit input reference resolves the same hero component");

        f=new MissionFixture(x=>
        {
            UnityEngine.Object.Destroy(x.Vitality.input);x.Vitality.input=null;
        });
        Check(!f.Vitality.Blocked && f.Vitality.HiddenLockCalls==0,"missing input component cannot leave startup movement frozen");
        Check(!f.Director.StartMission(IntercomMissionMode.Elimination),"missing input refuses mission start instead of partial lock");
        input=f.Hero.gameObject.AddComponent<IntercomPlayerInput>();f.Step();
        Check(f.Vitality.input==input && input.mission==f.Director && f.Vitality.Blocked,"adding the missing input recovers selection without scene reload");

        f=new MissionFixture();input=f.Vitality.input;
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"connected mission starts for loss-of-input check");
        f.Advance(1);input.enabled=false;f.Step();
        Check(!f.Vitality.Blocked && f.Living.Length==0,"losing active input stops mission actors and releases the owned block");
        input.enabled=true;f.Step();
        Check(input.mission==f.Director && f.Vitality.HiddenLockCalls==0,"reactivated input does not retain stale hidden-block state");

        IntercomMissionDirector foreign=null;
        f=new MissionFixture(x=>
        {
            foreign=new GameObject("Other active mission").AddComponent<IntercomMissionDirector>();
            x.Vitality.input.mission=foreign;x.Vitality.Blocked=true;
        });
        Check(f.Vitality.input.mission==foreign,"a second director cannot steal another active mission's input");
        Check(f.Vitality.Blocked && f.Vitality.HiddenLockCalls==0,"rejected director does not release another owner's lock");
        Check(!f.Director.StartMission(IntercomMissionMode.Elimination),"conflicting director cannot start behind another menu");
        f.Director.enabled=false;
        Check(f.Vitality.Blocked && f.Vitality.input.mission==foreign,"disabling rejected director preserves the other owner's lock and link");

        f=new MissionFixture();input=f.Vitality.input;input.mission=null;
        var competingRoot=new GameObject("Competing mission after broken reverse link");competingRoot.SetActive(false);
        var competing=competingRoot.AddComponent<IntercomMissionDirector>();
        competing.playerVitality=f.Vitality;competing.playerMotor=f.Motor;competing.playerHealth=f.Hero;
        competingRoot.SetActive(true);
        Check(input.mission==null && f.Vitality.Blocked,"competing director cannot bind an empty reverse link while another director owns the lock");
        f.Step();
        Check(input.mission==f.Director && f.Vitality.Blocked && f.Vitality.HiddenLockCalls==0,"original owner repairs its broken reverse link after a competitor is refused");
        competing.enabled=false;
        Check(input.mission==f.Director && f.Vitality.Blocked,"refused competitor disable cannot clear repaired owner link or lock");

        f=new MissionFixture(x=>x.Vitality.enabled=false);
        Check(!f.Vitality.Blocked,"disabled vitality cannot be used to acquire an invisible mission block");
    }

    private static void MissionSelection()
    {
        var f=new MissionFixture();
        Check(f.Director.Phase==IntercomMissionPhase.Selection&&f.Director.BlocksGameplay&&f.Vitality.Blocked,"mission first launch blocks gameplay at selection");
        f.Advance(4);Check(f.Living.Length==0&&f.Director.SpawnedEnemies==0,"selection keeps prepared enemies inactive");
        Check(!f.Director.StartMission((IntercomMissionMode)99),"undefined mode rejected");
        f.Director.spawnPoints=Array.Empty<Transform>();
        Check(!f.Director.StartMission(IntercomMissionMode.Elimination)&&f.Vitality.RetryCalls==0,"invalid setup cannot reset hero or partially start");
        f=new MissionFixture();Time.timeScale=0;
        Check(!f.Director.StartMission(IntercomMissionMode.Elimination),"paused session cannot start");
        Time.timeScale=1;Signal(f.Director,"OnApplicationFocus",false);
        Check(!f.Director.StartMission(IntercomMissionMode.Elimination),"unfocused session cannot start");
        Signal(f.Director,"OnApplicationFocus",true);
        Check(f.Director.StartMission(IntercomMissionMode.Elimination)&&!f.Vitality.Blocked&&!f.Director.BlocksGameplay,"valid start releases control lock");
        Check(f.Motor.ResetCalls==1&&f.Vitality.RetryCalls==1,"mode start resets hero exactly once");
        f.Advance(.6f);Check(f.Director.SpawnedEnemies==0,"initial spawn grace period respected");
        f.Advance(.3f);Check(f.Director.SpawnedEnemies==1,"initial grace period eventually spawns");
        f.Director.ReturnToSelection();
        Check(f.Director.Phase==IntercomMissionPhase.Selection&&f.Vitality.Blocked&&f.Living.Length==0,"return to menu cancels active enemies and controls");
    }
    private static void WarmPoolBudget()
    {
        var f=new MissionFixture();
        for(int i=1;i<=8;i++)
        {
            f.Step();
            Check(IntercomNpcController.Created.Count==i,"selection prepares at most one actor per update");
            Check(f.Director.PreparedEnemyCount==i&&f.Director.SpawnedEnemies==0&&f.Living.Length==0,
                "prewarming initializes parked actors without mission spawns or visible enemies");
        }
        f.Advance(1);
        Check(IntercomNpcController.Created.Count==8,"completed warmup makes no additional actors");
        int objects=GameObject.Created.Count;
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"warmed mission starts normally");
        f.Advance(2);
        Check(GameObject.Created.Count==objects&&f.Living.Length>0,"real spawn frames create no objects once pool is warm");
        foreach(var actor in f.Living)actor.Kill();
        f.Advance(2);
        Check(GameObject.Created.Count==objects,"kills and replenishment do not construct objects");
        string label=f.Director.ObjectiveText;
        for(int i=0;i<20;i++)Check(ReferenceEquals(label,f.Director.ObjectiveText),"unchanged objective reuses its formatted string");
        var paused=new MissionFixture();Signal(paused.Director,"OnApplicationPause",true);paused.Advance(1);
        Check(paused.Director.PreparedEnemyCount==0,"paused menu does not do background warmup");
        Signal(paused.Director,"OnApplicationPause",false);paused.Step();
        Check(paused.Director.PreparedEnemyCount==1,"resume continues one-at-a-time preparation");
        var fast=new MissionFixture();Check(fast.Director.StartMission(IntercomMissionMode.Elimination),"immediate click remains accepted");
        fast.Step();Check(!fast.Vitality.Blocked&&fast.Director.PreparedEnemyCount==1&&fast.Director.SpawnedEnemies==0,
            "incomplete pool never creates an invisible player lock");
        fast.Advance(1);Check(fast.Director.PreparedEnemyCount==8&&fast.Director.SpawnedEnemies>0,
            "initial grace period finishes prewarming even after an immediate click");
    }
    private static void SpawnSafety()
    {
        var f=new MissionFixture();f.Director.spawnPoints=new[]{MissionFixture.Marker(1,0)};
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"valid nearby marker may defer spawn at runtime");
        f.Advance(2);Check(f.Director.SpawnedEnemies==0,"enemy cannot appear directly on hero");
        f.Hero.transform.position=new Vector3(10,0,0);f.Advance(.5f);
        Check(f.Director.SpawnedEnemies==1,"walking clear permits deferred nearby marker");
        f=new MissionFixture();f.Nav.NextResult=IntercomNavResult.Deferred;f.Director.StartMission(IntercomMissionMode.Elimination);f.Advance(2);
        Check(f.Director.SpawnedEnemies==0&&f.Nav.PathRequests>0,"shared path budget defers rather than spawning blindly");
        f.Nav.NextResult=IntercomNavResult.Unreachable;f.Advance(.8f);
        Check(f.Director.SpawnedEnemies==0,"unreachable marker path rejected");
        f.Nav.NextResult=IntercomNavResult.Success;f.Advance(.5f);
        Check(f.Director.SpawnedEnemies>0,"navigation recovery permits retry without new mission");
        f=new MissionFixture();f.Director.spawnPoints=new[]{MissionFixture.Marker(8,0)};f.Elimination.maximumAlive=8;
        f.Director.StartMission(IntercomMissionMode.Elimination);f.Advance(3);
        Check(f.Director.SpawnedEnemies==1,"occupied single marker cannot stack enemies");
        f.Living[0].transform.position=new Vector3(12,0,0);f.Advance(.5f);
        Check(f.Director.SpawnedEnemies==2,"leaving marker frees next spawn");
        f=new MissionFixture();f.Nav.ProjectAllowed=p=>false;
        Check(!f.Director.StartMission(IntercomMissionMode.Elimination)&&f.Director.SpawnedEnemies==0,"no projectable marker rejects start");
    }
    private static void EliminationPoolAndWaves()
    {
        var f=new MissionFixture();f.Director.StartMission(IntercomMissionMode.Elimination);f.Advance(4);
        Check(f.Director.AliveEnemies==3&&f.Living.Length==3&&IntercomNpcController.Created.Count==8,"maximumAlive bound enforced while waiting");
        var first=f.Living[0];first.Kill();int kills=f.Director.Kills;
        first.Kill();Check(f.Director.Kills==kills,"same health death cannot count twice");
        f.Advance(.2f);Check(first.Health.IsDead,"dead pooled actor not revived before corpse delay");
        f.Advance(1);Check(IntercomNpcController.Created.Count==8&&f.Director.AliveEnemies<=3,"vacancy replenished with bounded reusable pool");
        int priorWave=1,maxAlive=0;
        for(int tick=0;tick<2500&&f.Director.Phase==IntercomMissionPhase.Active;tick++)
        {
            foreach(var actor in f.Living)actor.Kill();
            maxAlive=Math.Max(maxAlive,f.Director.AliveEnemies);f.Step();
            Check(f.Director.SpawnedEnemies<=50&&f.Director.AliveEnemies<=3,"mission never exceeds supply or concurrency");
            Check(f.Director.Wave>=priorWave,"wave index monotonic");priorWave=f.Director.Wave;
        }
        Check(f.Director.Phase==IntercomMissionPhase.Victory&&f.Director.Kills==50&&f.Director.SpawnedEnemies==50,"full runtime elimination ends exactly at50");
        Check(f.Director.Wave==5&&IntercomNpcController.Created.Count<=8,"five authored waves reuse bounded pool");
        Check(f.Vitality.Blocked&&f.Living.Length==0,"victory synchronously locks hero and stops enemies");
        int total=IntercomNpcController.Created.Count;f.Advance(20);
        Check(f.Director.SpawnedEnemies==50&&IntercomNpcController.Created.Count==total,"terminal victory does not create more enemies");
    }
    private static void MissionResetAndCleanup()
    {
        var f=new MissionFixture();f.Director.StartMission(IntercomMissionMode.Elimination);f.Advance(2);
        var old=f.Living[0];old.Kill();Check(f.Director.Kills==1,"reset fixture has progress");
        int created=IntercomNpcController.Created.Count;
        Check(f.Director.RetryMission(),"explicit retry succeeds");
        Check(f.Director.Kills==0&&f.Director.SpawnedEnemies==0&&f.Director.AliveEnemies==0&&f.Motor.ResetCalls==2,"retry clears mission counters and resets hero once");
        old.Health.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,null));
        Check(f.Director.Kills==0,"inactive previous life cannot award new mission kill");
        f.Advance(2);Check(IntercomNpcController.Created.Count<=created+1,"retry reuses existing pool");
        int beforeStale=f.Director.Kills;
        typeof(IntercomMissionDirector).GetMethod("OnEnemyDied",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.Director,new object[]{0});
        Check(f.Director.Kills==beforeStale,"old callback cannot report a currently alive pooled actor as dead");
        int resets=f.Motor.ResetCalls;f.Motor.ResetToSpawn();f.Step();
        Check(f.Director.SpawnedEnemies==0&&f.Director.Kills==0&&f.Motor.ResetCalls==resets+1,"external respawn epoch restarts without second teleport");
        f.Advance(1);Check(f.Director.SpawnedEnemies>0,"epoch reset resumes mission after grace period");
        f.Hero.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,null));
        Check(f.Director.Phase==IntercomMissionPhase.Defeat&&f.Vitality.Blocked&&f.Living.Length==0,"hero death synchronously ends mission");
        f.Advance(2);Check(f.Director.Phase==IntercomMissionPhase.Defeat,"defeat persists rather than implicit reset");
        Check(f.Director.RetryMission()&&!f.Hero.IsDead,"retry heals defeated hero through vitality route");
        f.Advance(1);f.Director.enabled=false;
        Check(f.Director.Phase==IntercomMissionPhase.Selection&&!f.Vitality.Blocked&&f.Living.Length==0,"component disable cancels actors and releases its lock");
        f.Director.enabled=true;f.Step();
        Check(f.Director.Phase==IntercomMissionPhase.Selection&&f.Vitality.Blocked,"reenable shows selection without duplicate actors");
        var outsider=new GameObject("Unrelated object");
        UnityEngine.Object.Destroy(f.Director.gameObject);
        Check(IntercomNpcController.Created.All(n=>UnityEngine.Object.Destroyed.Contains(n.gameObject)),"destroy removes owned pool actors");
        Check(!UnityEngine.Object.Destroyed.Contains(outsider)&&f.Hero.gameObject.activeSelf,"cleanup preserves unrelated objects and hero");
    }
    private static void MissionRescue()
    {
        var f=new MissionFixture();Check(f.Director.StartMission(IntercomMissionMode.Rescue),"rescue mission starts");
        Check(f.Ally!=null&&f.Ally.Health.Current==60&&!f.Ally.Recruited,"rescue creates healthy waiting ally");
        var exit=Field<GameObject>(f.Director,"extractionVisual");Check(exit.activeSelf,"rescue extraction marker active");
        f.Step();Check(f.Director.Phase==IntercomMissionPhase.Active,"hero alone in exit cannot win");
        f.Hero.transform.position=new Vector3(0,0,3);f.Advance(.7f);
        Check(f.Director.IsAllyRecruited,"real ally recruitment visible through mission API");
        f.Hero.transform.position=new Vector3(0,0,0);f.Ally.transform.position=new Vector3(0,0,4);f.Step(.05f,false);
        Check(f.Director.Phase==IntercomMissionPhase.Active,"recruited ally still outside exit cannot win");
        f.Ally.transform.position=new Vector3(0,0,1);f.Hero.transform.position=new Vector3(0,0,5);f.Step(.05f,false);
        Check(f.Director.Phase==IntercomMissionPhase.Active,"ally alone in exit cannot win");
        f.Hero.transform.position=new Vector3(0,0,0);f.Step(.05f,false);
        Check(f.Director.Phase==IntercomMissionPhase.Victory&&f.Ally.Suspended&&f.Vitality.Blocked,"recruited ally and hero in exit win and freeze escort");
        Check(f.Director.RetryMission()&&!f.Ally.Recruited&&f.Ally.Health.Current==60,"rescue retry reuses fresh un-recruited ally");
        f.Ally.Health.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,null));
        Check(f.Director.Phase==IntercomMissionPhase.Defeat&&f.Director.ResultText.Contains("Союзник"),"ally death triggers rescue failure");
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"switching from failed rescue to elimination works");
        Check(!f.Ally.gameObject.activeSelf&&!exit.activeSelf,"elimination hides ally and extraction marker");
        f=new MissionFixture();f.Director.allySpawn=null;
        Check(!f.Director.StartMission(IntercomMissionMode.Rescue)&&f.Ally==null,"missing ally marker rejects partial mission");
    }
    private static void MissionPauseAndTargets()
    {
        foreach(string interruption in new[]{"focus","pause","timescale"})
        {
            var f=new MissionFixture();f.Director.StartMission(IntercomMissionMode.Elimination);f.Advance(.4f);
            if(interruption=="focus")Signal(f.Director,"OnApplicationFocus",false);
            if(interruption=="pause")Signal(f.Director,"OnApplicationPause",true);
            if(interruption=="timescale")Time.timeScale=0;
            f.Advance(3);Check(f.Director.SpawnedEnemies==0,"interruption does not consume grace period or spawn enemies");
            if(interruption=="focus")Signal(f.Director,"OnApplicationFocus",true);
            if(interruption=="pause")Signal(f.Director,"OnApplicationPause",false);
            if(interruption=="timescale")Time.timeScale=1;
            f.Advance(.2f);Check(f.Director.SpawnedEnemies==0,"resume does not catch up missed spawning time");
            f.Advance(.3f);Check(f.Director.SpawnedEnemies>0,"active time resumes spawn timer normally");
        }
        var waves=new MissionFixture();waves.Elimination.killTarget=4;waves.Elimination.waveSize=2;waves.Elimination.maximumAlive=2;
        waves.Elimination.waveRestSeconds=1;waves.Director.StartMission(IntercomMissionMode.Elimination);waves.Advance(1.3f);
        Check(waves.Director.SpawnedEnemies==2&&waves.Director.Wave==1,"first wave respects authored batch size");
        foreach(var actor in waves.Living)actor.Kill();
        waves.Advance(.6f);Check(waves.Director.Wave==1&&waves.Director.SpawnedEnemies==2,"wave rest waits after last enemy dies");
        waves.Advance(.7f);Check(waves.Director.Wave==2&&waves.Director.SpawnedEnemies>2,"rest then next wave starts");
        var rescue=new MissionFixture();rescue.Director.spawnPoints=new[]{MissionFixture.Marker(0,8)};
        rescue.Director.allySpawn=MissionFixture.Marker(0,7);rescue.Director.StartMission(IntercomMissionMode.Rescue);rescue.Advance(2);
        Check(rescue.Director.SpawnedEnemies==0,"enemy cannot spawn on waiting rescue ally");
        rescue=new MissionFixture();rescue.Director.StartMission(IntercomMissionMode.Rescue);
        rescue.Hero.transform.position=new Vector3(0,0,3);rescue.Advance(.7f);
        Check(rescue.Ally.Recruited,"target choice fixture has recruited ally");
        rescue.Hero.transform.position=new Vector3(0,0,-5);rescue.Advance(2f,false);
        var enemy=rescue.Living[0];enemy.transform.position=new Vector3(0,0,5);enemy.Phase=IntercomNpcPhase.Chase;
        Send(rescue.Director,"RefreshTargets");
        Check(enemy.targetHealth==rescue.Ally.Health,"nearer visible recruited ally can become enemy target");
        var wall=new GameObject("Wall").AddComponent<Collider>();Physics.Hits=(a,b,c)=>new[]{wall};
        enemy.SetTarget(rescue.Hero);rescue.Advance(1.6f,false);Send(rescue.Director,"RefreshTargets");
        Check(enemy.targetHealth==rescue.Hero,"wall blocks acquisition of rescued ally");
        Physics.Hits=null;enemy.Phase=IntercomNpcPhase.Windup;Send(rescue.Director,"RefreshTargets");
        Check(enemy.targetHealth==rescue.Hero,"mission target selection cannot replace target mid-swing");
    }
}
