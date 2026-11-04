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
        SpawnPointLifecycle(); AllyFactory(); Recruitment(); Following(); AllyLifecycle();
        MissionConnection(); MissionSelection(); FreePlay(); WarmPoolBudget(); SpawnSafety(); AuthoredArrivals(); EliminationPoolAndWaves(); DeathBurstConnection(); MissionResetAndCleanup(); MissionRescue(); MissionPauseAndTargets();
        MissionSessionClock(); MissingRuntimeDependencies(); MissionCameraLifecycle();
        Console.WriteLine($"Mission/rescue runtime: {checks} assertions passed against narrow Unity/navigation stand-ins. Not Unity or PhysX execution.");
    }
    private static void SpawnPointLifecycle()
    {
        Time.timeScale=1;Time.deltaTime=.1f;Application.isFocused=true;
        var point=new GameObject("Authored hatch point").AddComponent<IntercomEnemySpawnPoint>();
        point.kind=IntercomEnemySpawnKind.Hatch;point.cooldownSeconds=4;
        point.hatchLid=new GameObject("Hatch lid pivot").transform;
        point.hatchLid.localRotation=Quaternion.Euler(4,8,2);
        point.warningVisual=new GameObject("Arrival warning").transform;
        point.warningVisual.localScale=new Vector3(1.2f,.25f,.8f);point.warningVisual.gameObject.SetActive(false);
        Vector3 rest=point.hatchLid.localRotation*Vector3.forward;
        Vector3 warningScale=point.warningVisual.localScale;
        int objects=GameObject.Created.Count,materials=Material.Created;
        Check(point.IsAvailable(0),"fresh authored marker is available");
        point.BeginArrival(10,1.03f);
        Check(point.IsAnimating&&!point.IsAvailable(10)&&point.warningVisual.gameObject.activeSelf,"arrival leases marker and reveals its warning");
        Check(Math.Abs(point.ReadyAt-14)<.0001f,"marker cooldown starts at actual arrival time");
        for(int i=0;i<5;i++)Send(point,"Update");
        Check((point.hatchLid.localRotation*Vector3.forward-rest).sqrMagnitude>.1f,"authored hatch visibly opens while robot rises");
        Vector3 open=point.hatchLid.localRotation*Vector3.forward;
        Time.timeScale=0;for(int i=0;i<20;i++)Send(point,"Update");Time.timeScale=1;
        Check(point.IsAnimating&&(point.hatchLid.localRotation*Vector3.forward-open).sqrMagnitude<.000001f,"zero timescale freezes hatch without skipping arrival");
        Signal(point,"OnApplicationFocus",false);for(int i=0;i<20;i++)Send(point,"Update");
        Check(point.IsAnimating&&(point.hatchLid.localRotation*Vector3.forward-open).sqrMagnitude<.000001f,"focus loss freezes marker alongside its emerging actor");
        Signal(point,"OnApplicationFocus",true);Signal(point,"OnApplicationPause",true);for(int i=0;i<20;i++)Send(point,"Update");
        Check(point.IsAnimating&&(point.hatchLid.localRotation*Vector3.forward-open).sqrMagnitude<.000001f,"application pause cannot close hatch over a frozen emergence");
        Signal(point,"OnApplicationPause",false);for(int i=0;i<7;i++)Send(point,"Update");
        Check(point.IsAnimating,"hatch remains open until the complete robot clears the authored duration");
        for(int i=0;i<3;i++)Send(point,"Update");
        Check(!point.IsAnimating&&!point.warningVisual.gameObject.activeSelf,"arrival ends with its temporary warning hidden");
        Check((point.hatchLid.localRotation*Vector3.forward-rest).sqrMagnitude<.000001f&&(point.warningVisual.localScale-warningScale).sqrMagnitude==0,"hatch and warning restore authored transforms exactly");
        Check(!point.IsAvailable(13.99f)&&point.IsAvailable(14),"marker is reused only after cooldown");
        point.BeginArrival(20,1.03f);Send(point,"Update");point.enabled=false;
        Check(!point.IsAnimating&&!point.warningVisual.gameObject.activeSelf&&(point.hatchLid.localRotation*Vector3.forward-rest).sqrMagnitude<.000001f,"disable cancels arrival without leaving hatch open");
        Check(!point.IsAvailable(100),"disabled authored marker cannot spawn");
        point.enabled=true;point.BeginArrival(30,1.03f);Send(point,"Update");point.ResetForMission();
        Check(!point.IsAnimating&&point.ReadyAt==0&&point.IsAvailable(0)&&!point.warningVisual.gameObject.activeSelf,"new mission resets cooldown and temporary presentation");
        point.kind=IntercomEnemySpawnKind.Ground;point.BeginArrival(40,1.03f);for(int i=0;i<5;i++)Send(point,"Update");
        Check((point.hatchLid.localRotation*Vector3.forward-rest).sqrMagnitude<.000001f,"ordinary ground marker does not animate an unrelated optional lid");
        point.ResetForMission();
        for(int life=0;life<100;life++)
        {
            point.BeginArrival(life*5,1.03f);for(int i=0;i<12;i++)Send(point,"Update");point.ResetForMission();
        }
        Check(GameObject.Created.Count==objects&&Material.Created==materials,"100 marker arrival/reset cycles create no objects or material instances");
    }

    private static IntercomEnemySpawnPoint Arrival(float x,float z,IntercomEnemySpawnKind kind=IntercomEnemySpawnKind.Ground)
    {
        var point=MissionFixture.Marker(x,z).gameObject.AddComponent<IntercomEnemySpawnPoint>();
        point.kind=kind;point.warningVisual=new GameObject("Authored warning").transform;point.warningVisual.gameObject.SetActive(false);
        if(kind==IntercomEnemySpawnKind.Hatch)point.hatchLid=new GameObject("Authored lid").transform;
        return point;
    }
    private static Camera SpawnCamera(Func<Vector3,Vector3> projection)
    {
        var camera=new GameObject("Spawn camera fixture").AddComponent<Camera>();camera.ProjectPoint=projection;Camera.main=camera;return camera;
    }
    private static void AuthoredArrivals()
    {
        var f=new MissionFixture();var ground=Arrival(0,8);f.Director.spawnPoints=Array.Empty<Transform>();
        f.Director.authoredSpawnPoints=new[]{ground};
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"authored-only mission starts without any legacy perimeter markers");
        f.Advance(.9f);
        Check(f.Director.SpawnedEnemies==1&&f.Living.Length==1,"safe ground point remains a fallback when no camera/perimeter marker exists");
        Check(f.Living[0].ArrivalWarning>=.2f&&ground.IsAnimating&&ground.warningVisual.gameObject.activeSelf,"ground arrival starts the real marker and grants additional actor warning");
        Check((f.Living[0].transform.position-ground.transform.position).sqrMagnitude==0,"authored actor emerges from its actual hatch/floor point");
        f.Director.ReturnToSelection();
        Check(!ground.IsAnimating&&ground.ReadyAt==0&&!ground.warningVisual.gameObject.activeSelf,"return to menu releases authored marker lease and warning");

        f=new MissionFixture();ground=Arrival(0,8);var hatch=Arrival(-8,0,IntercomEnemySpawnKind.Hatch);
        f.Director.authoredSpawnPoints=new[]{ground,hatch};
        f.Director.spawnPoints=new[]{MissionFixture.Marker(12,0),MissionFixture.Marker(-12,0),MissionFixture.Marker(0,-12),MissionFixture.Marker(12,-12)};
        f.Elimination.maximumAlive=6;
        SpawnCamera(p=>p.z>=0&&Math.Abs(p.x)<9?new Vector3(640,360,1):new Vector3(-100,360,1));
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"mixed perimeter and visible points start normally");f.Advance(3);
        var order=f.Living.OrderBy(a=>a.ordinal).ToArray();
        Check(order.Length==6,"mixed spawn cadence reaches bounded active count");
        for(int i=0;i<order.Length;i++)
        {
            bool authored=order[i].ArrivalWarning>0;
            Check(authored==(i==2||i==5),"every third activation gets the authored visible arrival; ordinary entries remain mixed in");
            Check((order[i].transform.position-f.Hero.transform.position).sqrMagnitude>=25,"mixed arrival respects player minimum distance");
        }
        Check(order[2].transform.position.z==8&&order[5].transform.position.x==-8,"different available authored points rotate instead of stacking in one hatch");
        int count=GameObject.Created.Count,materials=Material.Created;
        f.Advance(1);Check(GameObject.Created.Count==count&&Material.Created==materials,"live arrival animations do not create objects/materials after pool warmup");

        f=new MissionFixture();ground=Arrival(0,8);ground.cooldownSeconds=4;
        f.Director.spawnPoints=null;f.Director.authoredSpawnPoints=new[]{ground};f.Elimination.maximumAlive=1;
        f.Advance(.5f);count=GameObject.Created.Count;
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"single authored point starts with already prepared pool");f.Advance(.9f);
        Check(f.Director.SpawnedEnemies==1,"first authored arrival consumes exactly one pool slot");
        f.Living[0].Kill();f.Advance(1.5f);
        Check(f.Director.SpawnedEnemies==1,"free pool slot does not bypass marker cooldown after a kill");
        f.Advance(3);Check(f.Director.SpawnedEnemies==2,"same hatch can be reused after its cooldown");
        Check(GameObject.Created.Count==count,"reuse of authored point does not construct new actors or marker objects");
        f.Director.ReturnToSelection();Check(f.Director.StartMission(IntercomMissionMode.Elimination),"new run accepts existing authored pool");f.Advance(.9f);
        Check(f.Director.SpawnedEnemies==1,"new run resets old run's marker cooldown");

        f=new MissionFixture();ground=Arrival(0,3);f.Director.spawnPoints=Array.Empty<Transform>();f.Director.authoredSpawnPoints=new[]{ground};
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"near authored marker defers safely instead of resetting player during validation");f.Advance(4);
        Check(f.Director.SpawnedEnemies==0&&!ground.IsAnimating,"ground marker cannot emerge within protected player radius");
        f.Motor.transform.position=new Vector3(0,0,-10);f.Advance(.3f);
        Check(f.Director.SpawnedEnemies==1,"moving away naturally frees a valid authored point");

        f=new MissionFixture();ground=Arrival(0,7);f.Director.spawnPoints=Array.Empty<Transform>();f.Director.authoredSpawnPoints=new[]{ground};
        Check(f.Director.StartMission(IntercomMissionMode.Rescue),"authored-only rescue setup starts");f.Advance(2);
        Check(f.Director.SpawnedEnemies==0&&!ground.IsAnimating,"visible ground arrival cannot appear on the waiting ally's four-metre safety area");

        f=new MissionFixture();ground=Arrival(0,8);f.Director.spawnPoints=Array.Empty<Transform>();f.Director.authoredSpawnPoints=new[]{ground};
        f.Nav.NextResult=IntercomNavResult.Deferred;Check(f.Director.StartMission(IntercomMissionMode.Elimination),"deferred route budget does not refuse otherwise valid mission");f.Advance(2);
        Check(f.Director.SpawnedEnemies==0&&!ground.IsAnimating,"deferred navigation never leases marker or spawns without a verified route");
        f.Nav.NextResult=IntercomNavResult.Success;f.Advance(.3f);Check(f.Director.SpawnedEnemies==1,"authored spawn resumes once shared navigation budget succeeds");

        f=new MissionFixture();var edge=Arrival(12,0,IntercomEnemySpawnKind.Perimeter);f.Director.spawnPoints=Array.Empty<Transform>();f.Director.authoredSpawnPoints=new[]{edge};
        SpawnCamera(p=>new Vector3(32,360,1));
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"authored perimeter can wait for valid camera geometry");f.Advance(2);
        Check(f.Director.SpawnedEnemies==0,"screen-edge point is visible even outside readable interior, so authored perimeter cannot pretend it is offscreen");
        Camera.main.ProjectPoint=p=>new Vector3(-100,360,1);f.Advance(.3f);
        Check(f.Director.SpawnedEnemies==1&&f.Living[0].ArrivalWarning==0,"offscreen authored perimeter uses ordinary short arrival after leaving view");

        f=new MissionFixture();f.Director.spawnPoints=new[]{MissionFixture.Marker(12,0),MissionFixture.Marker(-12,0)};
        SpawnCamera(p=>p.x>0?new Vector3(32,360,1):new Vector3(-100,360,1));
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"legacy perimeter setup remains supported");f.Advance(.9f);
        Check(f.Living.Length==1&&f.Living[0].transform.position.x==-12,"perimeter search prefers genuinely offscreen point over visible HUD edge");
    }

    private static void FreePlay()
    {
        var f=new MissionFixture();
        // Free play does not depend on AI profiles, materials, navigation or spawn markers.
        f.Director.navigation=null;f.Director.enemyDefinition=null;f.Director.spawnPoints=null;
        f.Director.eliminationDefinition=f.Director.rescueDefinition=null;
        f.Director.allySpawn=f.Director.extractionPoint=null;
        Check(f.Director.StartMission(IntercomMissionMode.FreePlay),"free mode starts with only hero/control links");
        Check(f.Director.Mode==IntercomMissionMode.FreePlay&&!f.Director.BlocksGameplay&&!f.Vitality.Blocked,"free mode releases hero controls");
        f.Advance(120);
        Check(f.Director.Phase==IntercomMissionPhase.Active&&f.Director.Goal==0,"free mode does not win on empty arena");
        Check(f.Director.SpawnedEnemies==0&&f.Director.AliveEnemies==0&&f.Director.PreparedEnemyCount==0,"free mode neither constructs nor spawns AI");
        Check(f.Ally==null && f.Living.Length==0,"no rescue ally or enemy is created");
        Check(f.Director.ObjectiveText.Contains("без врагов"),"free mode has distinct objective text");
        int retries=f.Vitality.RetryCalls;
        Check(f.Director.RetryMission()&&f.Vitality.RetryCalls==retries+1,"free retry goes through normal hero/ammo/prop reset epoch");
        f.Advance(2);Check(f.Director.Mode==IntercomMissionMode.FreePlay&&f.Director.SpawnedEnemies==0,"free retry retains no-AI mode");
        f.Director.ReturnToSelection();Check(f.Director.Phase==IntercomMissionPhase.Selection&&f.Vitality.Blocked,"return to selection restores visible menu lock");

        f=new MissionFixture();f.Director.StartMission(IntercomMissionMode.Rescue);f.Advance(3);
        Check(f.Ally!=null&&f.Living.Length>0,"combat-to-free fixture contains active actors");
        var ally=f.Ally;
        Check(f.Director.StartMission(IntercomMissionMode.FreePlay),"switch from live rescue to free succeeds");
        int prepared=f.Director.PreparedEnemyCount;
        f.Advance(10);
        Check(f.Living.Length==0&&!ally.gameObject.activeSelf,"switch to free removes prior enemy and ally actors");
        Check(f.Director.PreparedEnemyCount==prepared&&f.Director.SpawnedEnemies==0&&f.Director.Kills==0,"retained inactive pool does not grow or simulate");
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"free mode can switch back to combat");
        f.Advance(2);Check(f.Living.Length>0,"combat still spawns after free mode");
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
            Time.timeScale=1;Time.time=0;Time.deltaTime=.01f;Application.isPlaying=true;Application.isFocused=true;Camera.main=null;Physics.Hits=null;
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
            if(Director.authoredSpawnPoints!=null)
                foreach(var point in Director.authoredSpawnPoints)if(point!=null&&point.isActiveAndEnabled)Send(point,"Update");
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
        Check(f.Living[0].EmergenceCalls==1,"mission activation explicitly starts ground arrival exactly once");
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
    private static void DeathBurstConnection()
    {
        var f=new MissionFixture(x=>x.Director.gameObject.AddComponent<IntercomRobotDeathEffects>());
        var fx=f.Director.deathEffects;
        Check(fx!=null&&fx.definition==f.Director.enemyDefinition,"Director discovers local death presentation and connects its NPC definition before prewarm");
        Check(f.Director.StartMission(IntercomMissionMode.Elimination),"death presentation fixture starts through normal mission route");f.Advance(1);
        var actor=f.Living[0];int clears=fx.ClearCalls;actor.Kill();
        Check(f.Director.Kills==1&&fx.EmitCalls==1&&actor.HiddenBursts==1,"accepted death scores once and replaces the visual with one burst");
        Check(actor.gameObject.activeInHierarchy,"ordinary lethal callback leaves pooled actor root active until normal recycle");
        actor.Kill();typeof(IntercomMissionDirector).GetMethod("OnEnemyDied",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.Director,new object[]{actor.ordinal});
        Check(f.Director.Kills==1&&fx.EmitCalls==1&&actor.HiddenBursts==1,"duplicate death callback cannot emit or hide twice within one life");
        Check(f.Director.RetryMission()&&fx.ClearCalls>clears,"retry clears old cosmetic pieces before accepting a new life");
        int calls=fx.EmitCalls;typeof(IntercomMissionDirector).GetMethod("OnEnemyDied",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.Director,new object[]{actor.ordinal});
        Check(f.Director.Kills==0&&fx.EmitCalls==calls,"stale prior-run callback cannot emit a fresh cosmetic burst");
        f.Advance(1);actor=f.Living[0];int hidden=actor.HiddenBursts;fx.Ready=false;actor.Kill();
        Check(f.Director.Kills==1&&fx.EmitCalls==calls+1&&actor.HiddenBursts==hidden,"unready optional presentation keeps actor fallback while preserving gameplay score");
        clears=fx.ClearCalls;f.Director.ReturnToSelection();Check(fx.ClearCalls>clears,"return to menu clears pooled death pieces");
        f.Director.StartMission(IntercomMissionMode.Elimination);f.Advance(1);clears=fx.ClearCalls;f.Director.enabled=false;
        Check(fx.ClearCalls>clears,"Director disable clears presentation even if FX remains enabled");

        f=new MissionFixture(x=>{x.Elimination.killTarget=1;x.Director.deathEffects=x.Director.gameObject.AddComponent<IntercomRobotDeathEffects>();});
        fx=f.Director.deathEffects;f.Director.StartMission(IntercomMissionMode.Elimination);f.Advance(1);clears=fx.ClearCalls;
        f.Living[0].Kill();
        Check(f.Director.Phase==IntercomMissionPhase.Victory&&fx.EmitCalls==1&&fx.ClearCalls==clears,"final kill's short burst survives synchronous mission victory cleanup");
        f.Director.ReturnToSelection();Check(fx.ClearCalls>clears,"leaving victory explicitly clears its remaining short presentation");
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
    private static void MissionSessionClock()
    {
        var f=new MissionFixture();
        f.Advance(1);
        Check(f.Director.ElapsedSeconds==0f&&f.Director.RunRevision==0,"menu prewarm cannot start the run timer");
        Check(f.Director.StartMission(IntercomMissionMode.FreePlay),"clock fixture starts free play");
        uint revision=f.Director.RunRevision;
        Check(revision!=0&&f.Director.ElapsedSeconds==0,"successful start creates a new empty clock session");
        f.Step(.25f);
        Check(Math.Abs(f.Director.ElapsedSeconds-.25f)<.00001f,"timer retains the complete active frame duration during a hitch");
        foreach(string interruption in new[]{"focus","pause","timescale"})
        {
            if(interruption=="focus")Signal(f.Director,"OnApplicationFocus",false);
            if(interruption=="pause")Signal(f.Director,"OnApplicationPause",true);
            if(interruption=="timescale")Time.timeScale=0;
            float elapsed=f.Director.ElapsedSeconds;f.Advance(2);
            Check(f.Director.ElapsedSeconds==elapsed&&f.Director.RunRevision==revision,"suspension freezes the same run clock: "+interruption);
            if(interruption=="focus")Signal(f.Director,"OnApplicationFocus",true);
            if(interruption=="pause")Signal(f.Director,"OnApplicationPause",false);
            if(interruption=="timescale")Time.timeScale=1;
        }
        f.Step();float atDeath=f.Director.ElapsedSeconds;
        f.Hero.TakeDamage(new IntercomDamage(100,Vector3.zero,Vector3.forward,null));f.Advance(1);
        Check(f.Director.ElapsedSeconds==atDeath&&f.Director.Phase==IntercomMissionPhase.Defeat,"result retains the final active timer value");
        Check(f.Director.RetryMission()&&f.Director.RunRevision!=revision&&f.Director.ElapsedSeconds==0,"retry gets a fresh session clock");
        revision=f.Director.RunRevision;
        Check(!f.Director.StartMission((IntercomMissionMode)999)&&f.Director.RunRevision==revision,"invalid selection cannot manufacture a run revision");
        f.Step();f.Director.ReturnToSelection();
        Check(f.Director.ElapsedSeconds==0&&f.Director.RunRevision==revision,"return clears clock without inventing another run");
        Signal(f.Director,"OnApplicationFocus",false);Signal(f.Director,"OnApplicationPause",true);
        f.Director.enabled=false;Application.isFocused=true;f.Director.enabled=true;
        Check(f.Director.StartMission(IntercomMissionMode.FreePlay),"explicit re-enable recovers application callbacks missed while disabled");
        f.Step();f.Director.enabled=false;
        Check(f.Director.ElapsedSeconds==0&&f.Director.ResultText=="","disable clears prior session presentation");
        f=new MissionFixture(_=>Application.isFocused=false);
        Check(!f.Director.StartMission(IntercomMissionMode.FreePlay),"component enabled without application focus cannot begin gameplay");
        Application.isFocused=true;Signal(f.Director,"OnApplicationFocus",true);
        Check(f.Director.StartMission(IntercomMissionMode.FreePlay),"focused selection recovers naturally after initial background activation");
        f=new MissionFixture();f.Advance(1);
        Check(f.Director.StartMission(IntercomMissionMode.Elimination)&&f.Director.PreparedEnemyCount==8,"long-frame fixture has a fully prepared enemy pool");
        f.Step(1.5f);
        Check(Math.Abs(f.Director.ElapsedSeconds-1.5f)<.00001f&&f.Director.SpawnedEnemies==0,
            "long active frame advances the full HUD clock but only a bounded spawn simulation step");
        f.Advance(.75f);
        Check(f.Director.SpawnedEnemies>0,"spawn grace continues normally after the isolated long frame");
    }
    private static void MissingRuntimeDependencies()
    {
        foreach(string mutation in new[]{"disable-ai","inactive-root","missing-health","missing-actor"})
        {
            var f=new MissionFixture();Check(f.Director.StartMission(IntercomMissionMode.Elimination),"pool fault fixture starts");f.Advance(1);
            var actor=f.Living[0];GameObject owned=actor.gameObject;
            if(mutation=="disable-ai")actor.enabled=false;
            else if(mutation=="inactive-root")owned.SetActive(false);
            else if(mutation=="missing-health")actor.Health=null;
            else
            {
                // Unity's destroyed-component null semantics, represented
                // explicitly here without pretending to simulate native objects.
                Array pool=Field<Array>(f.Director,"pool");object slot=pool.GetValue(actor.ordinal);
                slot.GetType().GetField("actor").SetValue(slot,null);
            }
            f.Step();
            Check(f.Director.Phase==IntercomMissionPhase.Defeat&&f.Director.ResultText.Length>0&&f.Vitality.Blocked,
                "lost pool life fails clearly rather than leaving an unwinnable wave: "+mutation);
            Check(!owned.activeInHierarchy,"mission retains ownership and parks the old root: "+mutation);
            Check(f.Director.RetryMission(),"broken pool can enter an explicit retry: "+mutation);f.Advance(1);
            Check(f.Director.Phase==IntercomMissionPhase.Active&&f.Director.SpawnedEnemies>0&&f.Living.All(n=>n.isActiveAndEnabled),
                "retry repairs or re-enables the owned pooled life: "+mutation);
            UnityEngine.Object.Destroy(f.Director.gameObject);
            Check(UnityEngine.Object.Destroyed.Contains(owned),"cleanup removes retained root even without an AI component: "+mutation);
        }
        foreach(string mutation in new[]{"profile","navigation","exit","ally","motor"})
        {
            var f=new MissionFixture();f.Director.StartMission(IntercomMissionMode.Rescue);
            if(mutation=="profile")typeof(IntercomMissionDirector).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f.Director,null);
            else if(mutation=="navigation")f.Nav.enabled=false;
            else if(mutation=="exit")f.Director.extractionPoint=null;
            else if(mutation=="ally")f.Ally.enabled=false;
            else f.Motor.enabled=false;
            f.Step();
            Check(f.Director.Phase==IntercomMissionPhase.Defeat&&f.Vitality.Blocked,
                "missing active mission dependency produces a stable recoverable result: "+mutation);
            float elapsed=f.Director.ElapsedSeconds;f.Advance(.5f);
            Check(f.Director.ElapsedSeconds==elapsed&&f.Living.Length==0,"failed dependency stops simulation and spawning: "+mutation);
        }
    }
    private static void MissionCameraLifecycle()
    {
        var f=new MissionFixture();var old=SpawnCamera(_=>new Vector3(-100,360,1));
        typeof(IntercomMissionDirector).GetField("displayCamera",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f.Director,old);
        old.enabled=false;var current=SpawnCamera(_=>new Vector3(640,360,1));
        var method=typeof(IntercomMissionDirector).GetMethod("SpawnOnScreen",BindingFlags.Instance|BindingFlags.NonPublic);
        Check((bool)method.Invoke(f.Director,new object[]{Vector3.zero,true})&&Field<Camera>(f.Director,"displayCamera")==current,
            "spawn visibility replaces an inactive cached camera after a menu/scene transition");
        current.enabled=false;
        Check(!(bool)method.Invoke(f.Director,new object[]{Vector3.zero,true}),"disabled tagged camera cannot classify an on-screen arrival");
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
