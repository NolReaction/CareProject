using System;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool value,string why) { checks++; if(!value) throw new Exception(why); }
    private static void Equal(float a,float b,string why) => Check(Math.Abs(a-b)<.0001f,why+" ("+a+" / "+b+")");
    private static void Call(object target,string method,params object[] args)
    { try { target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args); }catch(TargetInvocationException e){throw e.InnerException;} }
    private static T Field<T>(object target,string name) => (T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    private static void Tick(IntercomMedkitPickups pickup,int count=1) { Time.deltaTime=.02f; for(int i=0;i<count;i++) Call(pickup,"Update"); }
    private static IntercomDamage Hit(float amount) => new IntercomDamage(amount,Vector3.zero,Vector3.forward,null);
    private static (IntercomMissionDirector,IntercomMedkitPickups) Harness()
    {
        Time.timeScale=1;Time.time=0;Application.isFocused=true;
        var root=new GameObject("Medkit mission");var mission=root.AddComponent<IntercomMissionDirector>();
        var hero=new GameObject("Hero");mission.playerHealth=hero.AddComponent<IntercomHealth>();mission.playerMotor=hero.AddComponent<IntercomPlayerMotor>();mission.playerVitality=hero.AddComponent<IntercomPlayerVitality>();
        root.SetActive(false);var pickup=root.AddComponent<IntercomMedkitPickups>();pickup.mission=mission;
        pickup.bodyMaterial=new Material();pickup.accentMaterial=new Material();pickup.glowMaterial=new Material();
        root.SetActive(true);Tick(pickup);mission.playerMotor.transform.position=pickup.firstPosition;
        return (mission,pickup);
    }
    private static void NativeSetupLifecycle()
    {
        var root=new GameObject("Loading medkit scene");root.SetActive(false);
        root.AddComponent<IntercomMissionDirector>();
        int nativeBlocks=MaterialPropertyBlock.Created;
        IntercomMedkitPickups pickup;
        // Unity constructs/deserializes MonoBehaviours before main-thread Awake.
        // Reject the native constructor here so the original field initializer fails this regression.
        MaterialPropertyBlock.CreationAllowed=false;
        try { pickup=root.AddComponent<IntercomMedkitPickups>(); }
        finally { MaterialPropertyBlock.CreationAllowed=true; }
        Check(MaterialPropertyBlock.Created==nativeBlocks,"scene construction does not allocate native material state");
        root.SetActive(true);
        Check(MaterialPropertyBlock.Created==nativeBlocks,"missing materials defer native preparation");
        pickup.bodyMaterial=new Material();pickup.accentMaterial=new Material();pickup.glowMaterial=new Material();
        Tick(pickup);
        Check(MaterialPropertyBlock.Created==nativeBlocks+1,"main-thread delayed preparation creates exactly one property block");
        Tick(pickup,4);root.SetActive(false);root.SetActive(true);Tick(pickup);
        Check(MaterialPropertyBlock.Created==nativeBlocks+1,"updates and enable cycles reuse the native property block");
        Call(pickup,"OnDestroy");
    }
    private static void Main()
    {
        NativeSetupLifecycle();
        var model=new IntercomHealthState(100);model.Apply(40);Equal(model.Heal(35),35,"pure healing amount");Equal(model.Current,95,"pure healing adds HP");Equal(model.Heal(35),5,"pure healing clamps to max");
        foreach(float bad in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity}) Equal(model.Heal(bad),0,"invalid pure heal refused");
        model.Apply(100);Equal(model.Heal(100),0,"pure heal does not revive");
        var (m,p)=Harness();var hp=m.playerHealth;int events=0;float actual=0;hp.Healed+=value=>{events++;actual=value;};
        Check(p.AvailableCount==2,"two prewarmed ground medkits");Check(!p.TryCollect(0),"full HP preserves medkit");Check(p.AvailableCount==2&&p.CollectedThisRun==0,"no false consumption");
        hp.TakeDamage(Hit(60));Check(p.TryCollect(0),"approach heals injured hero");Equal(hp.Current,75,"pickup restores 35HP");Equal(actual,35,"healed event reports actual amount");Check(events==1&&p.AvailableCount==1&&p.CollectedThisRun==1,"one pickup and one event");
        Check(!p.TryCollect(0)&&!p.TryCollect(-1)&&!p.TryCollect(2),"same pickup and bad index cannot consume");
        m.playerMotor.transform.position=p.secondPosition;Check(p.TryCollect(1),"second pickup available");Equal(hp.Current,100,"second pickup cannot overheal");Equal(actual,25,"event reports partial actual heal");Equal(p.LastRestored,25,"pickup receipt reports actual heal");
        Check(p.AvailableCount==0&&events==2,"two kits consumed exactly once");
        int objects=GameObject.Created.Count,meshes=Mesh.Created,clips=AudioClip.Created,materials=Material.Created;
        for(int i=0;i<20;i++){m.RunRevision++;hp.ResetHealth();hp.TakeDamage(Hit(40));m.playerMotor.transform.position=p.firstPosition;Tick(p);Check(p.CollectedThisRun==1&&p.AvailableCount==1,"retry resets kit and receipt once");Tick(p,30);}
        Check(objects==GameObject.Created.Count&&meshes==Mesh.Created&&clips==AudioClip.Created&&materials==Material.Created,"retry and collection reuse all native resources");
        m.RunRevision++;hp.ResetHealth();hp.TakeDamage(Hit(40));m.playerMotor.transform.position=new Vector3(100,0,100);Tick(p);m.playerMotor.transform.position=p.firstPosition;
        Time.timeScale=0;Check(!p.TryCollect(0),"pause rejects collection");Time.timeScale=1;
        Call(p,"OnApplicationFocus",false);Check(!p.TryCollect(0),"lost focus rejects collection");Call(p,"OnApplicationFocus",true);
        Application.isFocused=false;Check(!p.TryCollect(0),"current app focus checked");Application.isFocused=true;
        Call(p,"OnApplicationPause",true);Check(!p.TryCollect(0),"application pause rejects collection");Call(p,"OnApplicationPause",false);
        m.playerVitality.MissionBlocked=true;Check(!p.TryCollect(0),"mission control lock rejects collection");m.playerVitality.MissionBlocked=false;
        m.Phase=IntercomMissionPhase.Selection;Check(!p.TryCollect(0),"selection rejects collection");m.Phase=IntercomMissionPhase.Victory;Check(!p.TryCollect(0),"results reject collection");m.Phase=IntercomMissionPhase.Active;
        m.playerMotor.enabled=false;Check(!p.TryCollect(0),"disabled player rejects collection");m.playerMotor.enabled=true;
        m.enabled=false;Check(!p.TryCollect(0),"disabled mission rejects collection");m.enabled=true;
        p.enabled=false;Check(!p.TryCollect(0),"disabled pickup rejects collection");p.enabled=true;
        Check(p.AvailableCount==2,"all blocked paths preserve medkits");
        m.playerMotor.transform.position=p.firstPosition+Vector3.up;Check(!p.TryCollect(0),"different floor rejects collection");m.playerMotor.transform.position=p.firstPosition;
        var wall=new GameObject("Wall").AddComponent<Collider>();Physics.Hits=(_,_,_)=>new[]{wall};Check(!p.TryCollect(0),"cannot collect through a wall");Physics.Hits=null;
        hp.TakeDamage(Hit(1000));Check(!p.TryCollect(0)&&p.AvailableCount==2,"dead hero cannot consume or revive");hp.ResetHealth();hp.TakeDamage(Hit(20));
        m.Mode=IntercomMissionMode.FreePlay;Check(p.TryCollect(0),"freeplay supports test medkit");Equal(hp.Current,100,"freeplay heals normally");
        hp.TakeDamage(Hit(10));Time.timeScale=0;Check(!hp.Heal(10),"public heal rejects pause");Time.timeScale=1;hp.enabled=false;Check(!hp.Heal(10),"public heal rejects inactive health");hp.enabled=true;
        hp.postHitInvulnerability=1f;hp.ResetHealth();hp.TakeDamage(Hit(20));
        Check(hp.Heal(10),"healing works inside post-hit immunity");Check(!hp.TakeDamage(Hit(10)),"healing does not reset post-hit immunity");
        Time.time=1.01f;Check(hp.TakeDamage(Hit(10)),"normal immunity expiry survives healing");
        uint before=hp.ResetRevision;hp.ResetHealth();Check(hp.ResetRevision!=before,"explicit reset has distinct revision");
        Check(!hp.Heal(10),"full public heal emits no event");hp.TakeDamage(Hit(10));foreach(float bad in new[]{0f,-1f,float.NaN,float.PositiveInfinity})Check(!hp.Heal(bad),"public invalid heal refused");
        m.RunRevision++;hp.ResetHealth();hp.TakeDamage(Hit(40));m.playerMotor.transform.position=new Vector3(100,0,100);Tick(p);m.playerMotor.transform.position=p.firstPosition;
        hp.Healed+=_=>Check(!p.TryCollect(0),"recursive heal callback cannot consume kit twice");Check(p.TryCollect(0),"recursive case heals once");
        var ownedPool=Field<GameObject>(p,"pool");var ring=Field<Mesh>(p,"ringMesh");var chime=Field<AudioClip>(p,"pickupSound");var shared=p.bodyMaterial;Call(p,"OnDestroy");
        Check(UnityEngine.Object.Destroyed.Contains(ownedPool)&&UnityEngine.Object.Destroyed.Contains(ring)&&UnityEngine.Object.Destroyed.Contains(chime)&&!UnityEngine.Object.Destroyed.Contains(shared),"owned pools mesh and sound released; authored material preserved");
        Console.WriteLine("Medkit pickups: "+checks+" assertions PASS. Production health, collection, lifecycle and bounded resource contracts; no Unity rendering claim.");
    }
}
