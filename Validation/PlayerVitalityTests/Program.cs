using System;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

static class Program
{
    static int checks;
    static void Check(bool ok,string description)
    {if(!ok)throw new Exception(description);checks++;}
    static void Equal(float actual,float expected,string description)
    {Check(Math.Abs(actual-expected)<.0001f,description+" (actual "+actual+", expected "+expected+")");}
    static void Call(object instance,string method,params object[] arguments)
    {
        var member=instance.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic);
        if(member==null)throw new Exception("Missing lifecycle method "+method);
        try{member.Invoke(instance,arguments);}catch(TargetInvocationException e){throw e.InnerException??e;}
    }
    static void Clock(float time=0)
    {Time.time=Time.unscaledTime=time;Time.deltaTime=1f/60;Time.timeScale=1;Application.isPlaying=true;}
    static IntercomHealth Health(IntercomTeam team=IntercomTeam.Neutral,float maximum=100)
    {
        var h=new GameObject().AddComponent<IntercomHealth>();h.team=team;h.maximumHealth=maximum;Call(h,"Awake");return h;
    }
    static IntercomDamage Hit(float amount=12,GameObject source=null)
    {return new IntercomDamage(amount,Vector3.zero,Vector3.forward,source);}
    sealed class Hero
    {
        public GameObject go;
        public IntercomHealth health;
        public IntercomPlayerVitality vitality;
        public IntercomPlayerMotor motor;
        public IntercomPlayerInput input;
        public IntercomSimpleShooter shooter;
        public IntercomWeaponPose pose;
        public Animator animator;
        public Hero(bool shooterEnabled=true,bool motorEnabled=true,bool poseEnabled=true)
        {
            Clock();go=new GameObject();health=go.AddComponent<IntercomHealth>();health.team=IntercomTeam.Player;
            health.postHitInvulnerability=.6f;
            motor=go.AddComponent<IntercomPlayerMotor>();motor.enabled=motorEnabled;
            input=go.AddComponent<IntercomPlayerInput>();motor.inputSource=input;
            animator=go.AddComponent<Animator>();motor.animator=animator;animator.SetFloat("Speed",1);
            shooter=go.AddComponent<IntercomSimpleShooter>();shooter.enabled=shooterEnabled;
            pose=go.AddComponent<IntercomWeaponPose>();pose.enabled=poseEnabled;
            vitality=go.AddComponent<IntercomPlayerVitality>();vitality.health=health;vitality.motor=motor;
            vitality.input=input;vitality.shooter=shooter;vitality.weaponPose=pose;
            Call(health,"Awake");Call(vitality,"Awake");Call(vitality,"OnEnable");
        }
        public void Update(float dt=1f/60)
        {Time.deltaTime=dt;Time.time+=dt;Time.unscaledTime+=dt;Call(vitality,"Update");}
        public void Fatal(){Check(health.TakeDamage(Hit(1000)),"fatal damage accepted");}
    }

    static void ArithmeticAndGuards()
    {
        Clock();var h=Health();Equal(h.Current,100,"initial health");Equal(h.Maximum,100,"actual maximum exposed");
        int damaged=0,dead=0,damagedEvent=0,deathEvent=0;
        h.Damaged+=_=>damaged++;h.Died+=()=>dead++;
        h.onDamaged.AddListener(()=>damagedEvent++);h.onDeath.AddListener(()=>deathEvent++);
        foreach(float bad in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {Check(!h.TakeDamage(Hit(bad)),"reject invalid damage");Equal(h.Current,100,"invalid damage changes nothing");}
        Check(damaged==0&&dead==0&&damagedEvent==0&&deathEvent==0,"invalid input emits no callbacks");
        Check(h.TakeDamage(Hit(25)),"valid hit accepted");Equal(h.Current,75,"health reduced");
        Check(damaged==1&&damagedEvent==1&&dead==0,"damage events once");
        h.invulnerable=true;Check(!h.TakeDamage(Hit()),"explicit immunity");h.invulnerable=false;
        h.enabled=false;Check(!h.TakeDamage(Hit()),"disabled receiver");h.enabled=true;
        h.gameObject.SetActive(false);Check(!h.TakeDamage(Hit()),"inactive object");h.gameObject.SetActive(true);
        Time.timeScale=0;Check(!h.TakeDamage(Hit()),"pause rejects damage");Time.timeScale=1;
        Equal(h.Current,75,"guards preserve current health");
        Check(h.TakeDamage(Hit(9999)),"lethal hit");Equal(h.Current,0,"health clamped at zero");
        Check(h.IsDead&&damaged==2&&dead==1&&deathEvent==1,"one death notification");
        for(int i=0;i<8;i++)Check(!h.TakeDamage(Hit(9999)),"dead target rejects later damage");
        Check(dead==1&&deathEvent==1,"death remains one-shot");
        h.ResetHealth();Equal(h.Current,100,"explicit reset heals");Check(!h.IsDead,"reset revives");
        h.maximumHealth=75;h.ResetHealth();Equal(h.Maximum,75,"reset adopts authored maximum");Equal(h.Current,75,"reset uses new maximum");
        foreach(float maximum in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {h.maximumHealth=maximum;h.ResetHealth();Equal(h.Maximum,100,"invalid maximum uses safe fallback");}
        h.maximumHealth=-10;h.ResetHealth();Equal(h.Maximum,1,"negative maximum bounded");
    }

    static void OwnershipAndTeams()
    {
        Clock();var victim=Health(IntercomTeam.Player);var ally=Health(IntercomTeam.Player);
        var enemy=Health(IntercomTeam.Enemy);var neutral=Health();
        Check(!victim.TakeDamage(Hit(10,victim.gameObject)),"self damage rejected");
        var ownChild=new GameObject();ownChild.transform.parent=victim.transform;
        Check(!victim.TakeDamage(Hit(10,ownChild)),"self child resolves to same receiver");
        Check(!victim.TakeDamage(Hit(10,ally.gameObject)),"same player team rejected");
        Check(victim.TakeDamage(Hit(10,enemy.gameObject)),"opposing enemy damage accepted");
        Check(victim.TakeDamage(Hit(10,neutral.gameObject)),"neutral source remains compatible");
        Check(victim.TakeDamage(Hit(10,new GameObject())),"unowned source remains compatible");
        Equal(victim.Current,70,"only accepted hits consume health");
        var otherEnemy=Health(IntercomTeam.Enemy);Check(!enemy.TakeDamage(Hit(10,otherEnemy.gameObject)),"same enemy team rejected");
        Check(neutral.TakeDamage(Hit(10,Health().gameObject)),"neutral scenery is not faction immune");
    }

    static void TimedImmunity()
    {
        Clock(1);var hero=Health(IntercomTeam.Player);hero.postHitInvulnerability=.6f;
        Check(hero.TakeDamage(Hit()),"first hit starts immunity");
        Check(!hero.TakeDamage(Hit()),"same-frame second enemy cannot stack damage");
        Time.time=1.59f;Check(!hero.TakeDamage(Hit()),"inside immunity window");
        Time.timeScale=0;Time.unscaledTime=100;Check(!hero.TakeDamage(Hit()),"pause cannot advance scaled immunity");Time.timeScale=1;
        Time.time=1.601f;Check(hero.TakeDamage(Hit()),"immunity expires with scaled time");Equal(hero.Current,76,"two accepted hits total");
        hero.ResetHealth();Check(hero.TakeDamage(Hit()),"explicit reset clears old immunity deadline");
        var enemy=Health(IntercomTeam.Enemy);enemy.postHitInvulnerability=0;
        Check(enemy.TakeDamage(Hit(15))&&enemy.TakeDamage(Hit(15)),"paired pistol bullets both damage NPC in same frame");Equal(enemy.Current,70,"no enemy blanket immunity");
        foreach(float interval in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f})
        {
            var malformed=Health();malformed.postHitInvulnerability=interval;
            Check(malformed.TakeDamage(Hit(1))&&malformed.TakeDamage(Hit(1)),"invalid immunity cannot permanently lock receiver");
        }
        var finiteClock=Health();
        foreach(float badTime in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {Time.time=badTime;Check(!finiteClock.TakeDamage(Hit()),"nonfinite game clock rejected");Equal(finiteClock.Current,100,"bad clock preserves health");}
        Clock();
    }

    static void CallbackReentrancy()
    {
        Clock();var h=Health();int deaths=0;
        h.Died+=()=>deaths++;
        h.Damaged+=_=>Check(!h.TakeDamage(Hit(1)),"recursive damage cannot damage dead target");
        Check(h.TakeDamage(Hit(100)),"lethal callback case");Check(deaths==1,"recursive callback emits one death");
        var resetDuringDamage=Health();int staleDeaths=0,unityStaleDeaths=0;
        resetDuringDamage.Damaged+=_=>resetDuringDamage.ResetHealth();resetDuringDamage.Died+=()=>staleDeaths++;
        resetDuringDamage.onDeath.AddListener(()=>unityStaleDeaths++);
        Check(resetDuringDamage.TakeDamage(Hit(100)),"damage accepted before callback reset");
        Check(!resetDuringDamage.IsDead&&staleDeaths==0&&unityStaleDeaths==0,"damage callback reset prevents stale death dispatch");
        var resetDuringDeath=Health();int oldUnityDeaths=0;
        resetDuringDeath.Died+=()=>resetDuringDeath.ResetHealth();resetDuringDeath.onDeath.AddListener(()=>oldUnityDeaths++);
        Check(resetDuringDeath.TakeDamage(Hit(100)),"death callback reset hit");
        Check(!resetDuringDeath.IsDead&&oldUnityDeaths==0,"death callback reset suppresses subsequent stale UnityEvent");
    }

    static void DeathAndRetry()
    {
        var f=new Hero();Check(f.vitality.Health==f.health,"HUD reads actual health component");
        Check(f.health.TakeDamage(Hit(12)),"nonlethal player hit");Equal(f.health.Current,88,"player HP consumed");
        Check(f.vitality.ShowHitRemaining>0,"hit produces visual feedback timer");
        float flash=f.vitality.ShowHitRemaining;f.Update(0);Equal(f.vitality.ShowHitRemaining,flash,"pause does not advance flash");
        for(int i=0;i<60;i++)f.Update();Equal(f.vitality.ShowHitRemaining,0,"hit feedback ends");
        uint cancelled=f.input.CancellationVersion;f.Fatal();
        Check(f.vitality.IsDead,"HUD death state follows health");
        Check(!f.motor.enabled&&!f.shooter.enabled&&!f.pose.enabled,"death locks movement shooter and weapon pose");
        Check(f.input.CancellationVersion>cancelled,"death cancels held inputs");Equal(f.animator.GetFloat("Speed"),0,"death stops run blend");
        Equal(Time.timeScale,1,"death does not freeze world clock");
        f.Update();Check(!f.motor.enabled&&!f.shooter.enabled,"dead update remains locked");
        uint respawn=f.motor.RespawnVersion;Check(f.vitality.Retry(),"retry accepted");
        Check(f.motor.ResetCalls==1&&f.motor.RespawnVersion==respawn+1,"retry performs one explicit respawn");
        Check(f.motor.enabled&&f.shooter.enabled&&f.pose.enabled,"retry restores enabled gameplay components");
        Check(!f.vitality.IsDead,"retry leaves death screen");Equal(f.health.Current,100,"retry restores player health");
        Equal(Time.timeScale,1,"retry leaves world clock alone");
    }

    static void RespawnVersusCancellation()
    {
        var f=new Hero();Check(f.health.TakeDamage(Hit(30)),"damage before focus test");
        Call(f.vitality,"OnApplicationFocus",false);f.Update();Equal(f.health.Current,70,"focus loss does not heal");
        Call(f.vitality,"OnApplicationFocus",true);f.Update();Equal(f.health.Current,70,"focus return does not heal");
        Call(f.vitality,"OnApplicationPause",true);f.Update();Equal(f.health.Current,70,"app pause does not heal");
        Call(f.vitality,"OnApplicationPause",false);f.input.CancellationVersion++;f.Update();Equal(f.health.Current,70,"input cancellation does not heal");
        f.motor.ResetToSpawn();f.Update();Equal(f.health.Current,100,"explicit motor respawn restores health");
        f.Fatal();Call(f.vitality,"OnApplicationFocus",false);Check(!f.vitality.Retry(),"unfocused retry rejected");
        Call(f.vitality,"OnApplicationFocus",true);Call(f.vitality,"OnApplicationPause",true);Check(!f.vitality.Retry(),"paused app retry rejected");
        Call(f.vitality,"OnApplicationPause",false);Time.timeScale=0;Check(!f.vitality.Retry(),"timeScale pause retry rejected");Time.timeScale=1;
        Check(f.vitality.Retry(),"retry resumes after actual return");
    }

    static void AuthoredStatesAndLifecycle()
    {
        foreach(bool motor in new[]{false,true})foreach(bool shooter in new[]{false,true})foreach(bool pose in new[]{false,true})
        {
            var f=new Hero(shooter,motor,pose);f.Fatal();
            Check(!f.motor.enabled&&!f.shooter.enabled&&!f.pose.enabled,"fatal lock independent of authored states");
            Call(f.vitality,"OnDisable");f.vitality.enabled=false;
            Check(f.motor.enabled==motor&&f.shooter.enabled==shooter&&f.pose.enabled==pose,"disable restores authored enabled flags");
            Check(f.health.IsDead,"disabling vitality does not resurrect");
            f.vitality.enabled=true;Call(f.vitality,"OnEnable");
            Check(!f.motor.enabled&&!f.shooter.enabled&&!f.pose.enabled,"reenable dead hero reacquires lock");
            Check(f.vitality.Retry(),"retry from restored owner");
            Check(f.motor.enabled==motor&&f.shooter.enabled==shooter&&f.pose.enabled==pose,"retry preserves originally disabled components");
        }
        var alive=new Hero();Check(alive.health.TakeDamage(Hit(20)),"damage before alive disable");
        Call(alive.vitality,"OnDisable");alive.vitality.enabled=false;Equal(alive.health.Current,80,"disable alive retains HP");
        alive.vitality.enabled=true;Call(alive.vitality,"OnEnable");Equal(alive.health.Current,80,"reenable alive retains HP");
    }

    static void ReferenceReplacement()
    {
        var f=new Hero();f.Fatal();
        var next=new GameObject();var nextMotor=next.AddComponent<IntercomPlayerMotor>();
        var nextShooter=next.AddComponent<IntercomSimpleShooter>();var nextPose=next.AddComponent<IntercomWeaponPose>();
        nextShooter.enabled=false;
        f.vitality.motor=nextMotor;f.vitality.shooter=nextShooter;f.vitality.weaponPose=nextPose;f.Update();
        Check(f.motor.enabled&&f.shooter.enabled&&f.pose.enabled,"rebind releases only old owned component locks");
        Check(!nextMotor.enabled&&!nextShooter.enabled&&!nextPose.enabled,"dead actor locks replacement references");
        Check(f.health.IsDead,"reference replacement is not a free respawn");
        Check(f.vitality.Retry(),"retry through replacement motor");
        Check(nextMotor.enabled&&!nextShooter.enabled&&nextPose.enabled,"new reference original flags restored");
        var oldHealth=f.health;var nextHealth=Health(IntercomTeam.Player);f.vitality.health=nextHealth;f.Update();
        Check(oldHealth.TakeDamage(Hit(1000)),"old detached health can die independently");
        Check(nextMotor.enabled&&!nextShooter.enabled&&nextPose.enabled,"old health callbacks no longer lock replacement actor");
        Check(nextHealth.TakeDamage(Hit(1000)),"replacement health receives damage");
        Check(!nextMotor.enabled&&!nextPose.enabled,"replacement health now owns death lock");
    }

    static void MissionControlOwnership()
    {
        var f=new Hero();
        f.vitality.SetMissionBlocked(true);
        Check(f.vitality.MissionBlocked&&!f.motor.enabled&&!f.shooter.enabled&&!f.pose.enabled,"mission selection locks controls synchronously");
        f.Update();Check(!f.motor.enabled&&!f.shooter.enabled,"alive update cannot remove mission lock");
        uint cancel=f.input.CancellationVersion;
        f.vitality.SetMissionBlocked(true);
        Check(f.input.CancellationVersion==cancel,"unchanged menu state does not keep cancelling pointer input");
        Check(f.vitality.Retry(),"mission may reset while blocked");
        Check(!f.motor.enabled&&!f.shooter.enabled,"retry cannot unlock the visible menu");
        f.motor.ResetToSpawn();f.Update();
        Check(!f.motor.enabled&&!f.shooter.enabled,"external respawn preserves mission lock");
        f.vitality.SetMissionBlocked(false);
        Check(f.motor.enabled&&f.shooter.enabled&&f.pose.enabled,"mission start restores authored gameplay states");
        f.Fatal();f.vitality.SetMissionBlocked(true);f.vitality.SetMissionBlocked(false);
        Check(!f.motor.enabled&&!f.shooter.enabled,"closing menu cannot unlock a dead hero");
        Check(f.vitality.Retry()&&f.motor.enabled,"dead hero can retry after mission lock released");
        var disabled=new Hero(false,true,false);disabled.vitality.SetMissionBlocked(true);
        disabled.vitality.Retry();disabled.vitality.SetMissionBlocked(false);
        Check(disabled.motor.enabled&&!disabled.shooter.enabled&&!disabled.pose.enabled,"nested menu/reset preserves authored disabled components");
    }

    static void MissionLockOwners()
    {
        var f=new Hero();var first=new object();var second=new object();
        Check(f.vitality.SetMissionBlocked(first,true),"first mission controller acquires lock");
        Check(!f.vitality.SetMissionBlocked(second,false)&&!f.vitality.SetMissionBlocked(second,true),"another controller cannot change owned lock");
        f.vitality.SetMissionBlocked(false);
        Check(f.vitality.MissionBlocked&&!f.motor.enabled,"legacy release cannot override another controller");
        Check(f.vitality.Retry()&&!f.motor.enabled,"retry preserves owner and lock");
        Check(f.vitality.SetMissionBlocked(first,false)&&f.motor.enabled,"owner can release lock after retry");
        Check(f.vitality.SetMissionBlocked(second,true),"released lock can be acquired by next controller");
        Check(!f.vitality.SetMissionBlocked(first,false)&&!f.motor.enabled,"old controller cannot release new owner lock");
        f.Fatal();Check(f.vitality.SetMissionBlocked(second,false)&&!f.motor.enabled,"release after death cannot revive controls");
        Check(!f.vitality.SetMissionBlocked(null,true),"missing owner rejected");
    }
    static void Main()
    {
        ArithmeticAndGuards();OwnershipAndTeams();TimedImmunity();CallbackReentrancy();
        DeathAndRetry();RespawnVersusCancellation();AuthoredStatesAndLifecycle();ReferenceReplacement();MissionControlOwnership();MissionLockOwners();
        Console.WriteLine("Player health/vitality: "+checks+" assertions passed. Production health and lifecycle with narrow Unity stand-ins; not a Unity Editor test.");
    }
}
