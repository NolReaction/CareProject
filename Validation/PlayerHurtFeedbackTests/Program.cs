using System;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

static class Program
{
    static int checks;
    static void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;}
    static void Equal(float a,float b,string message){Check(Math.Abs(a-b)<.00001f,message+" actual="+a+" expected="+b);}
    static void Call(object instance,string method,params object[] args)
    {
        try{instance.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(instance,args);}
        catch(TargetInvocationException e){throw e.InnerException??e;}
    }
    static T Private<T>(object instance,string field)=>(T)instance.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);
    sealed class Hero
    {
        public GameObject go=new GameObject();
        public IntercomHealth health;
        public IntercomPlayerMotor motor;
        public IntercomPlayerVitality vitality;
        public IntercomPlayerHurtFeedback hurt;
        public AudioSource Voice=>Private<AudioSource>(hurt,"voice");
        public Hero()
        {
            Time.time=0;Time.timeScale=1;Time.deltaTime=1f/60;AudioListener.pause=false;
            health=go.AddComponent<IntercomHealth>();health.team=IntercomTeam.Player;Call(health,"Awake");
            motor=go.AddComponent<IntercomPlayerMotor>();vitality=go.AddComponent<IntercomPlayerVitality>();
            hurt=go.AddComponent<IntercomPlayerHurtFeedback>();hurt.hurtClips=new[]{new AudioClip(),new AudioClip()};
            Call(hurt,"Awake");Call(hurt,"OnEnable");
        }
        public bool Hit(float amount=12,Vector3? direction=null,GameObject instigator=null)
            =>health.TakeDamage(new IntercomDamage(amount,Vector3.zero,direction??Vector3.forward,instigator));
        public void Tick(float dt=1f/60){Time.deltaTime=dt;Time.time+=dt;Call(hurt,"Update");}
    }

    static void Envelope()
    {
        Equal(IntercomHurtResponse.Envelope(0),0,"no instantaneous pose jump");
        Equal(IntercomHurtResponse.Envelope(.045f),1,"soft peak at 45 ms");
        Equal(IntercomHurtResponse.Envelope(.48f),0,"exact settled pose");
        Equal(IntercomHurtResponse.Envelope(float.NaN),0,"invalid time safe");
        var response=new IntercomHurtResponse();response.Push(5,3);response.Advance(.045f);
        Equal(response.Pitch,5,"specified pitch impulse");Equal(response.Roll,3,"specified roll impulse");
        response.Advance(.5f);Equal(response.Pitch,0,"settles without residual tilt");
        for(int i=0;i<200;i++){response.Push(100,100);response.Advance(.001f);Check(response.Pitch<=6&&response.Roll<=4.5f,"rapid hits remain bounded");}
        response.Reset();response.Push(float.NaN,0);response.Advance(.03f);Equal(response.Pitch,0,"invalid impulse rejected");
        response.Push(5,0);response.Advance(float.PositiveInfinity);Equal(response.Pitch,0,"invalid delta resets safely");
        foreach(int fps in new[]{30,60,120})
        {
            response.Reset();response.Push(5,-3);
            for(int i=0;i<fps/5;i++)response.Advance(1f/fps);
            Equal(response.Pitch,IntercomHurtResponse.Envelope(.2f)*5,"frame independent pitch at "+fps);
        }
        response.Reset();
        for(int i=0;i<100;i++){response.Push(1,1);response.Advance(.01f);}
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++){response.Push(1,1);response.Advance(.01f);}
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(allocated==0,"steady response loop allocates zero managed bytes, actual "+allocated);
    }

    static void AcceptedDamageAndAudio()
    {
        var hero=new Hero();Check(hero.hurt.health==hero.health&&hero.hurt.motor==hero.motor,"references auto resolve on same hero");
        Check(!hero.Voice.playOnAwake&&!hero.Voice.loop&&hero.Voice.spatialBlend==0,"dedicated finite local audio voice");
        int created=GameObject.Created;
        Check(hero.Hit(),"accepted hit");hero.Tick(.045f);
        Check(hero.hurt.HurtPitch>4&&hero.hurt.HurtPitch<=5.5f,"accepted damage feeds chassis recoil");
        Equal(hero.hurt.HurtRoll,0,"frontal hit no sideways jolt");
        Check(hero.Voice.PlayCalls==1,"accepted hit plays once");
        Check(hero.Voice.volume>0&&hero.Voice.volume<=.38f,"hurt gain bounded");
        AudioClip first=hero.Voice.clip;
        Check(hero.Hit(),"second damage can be accepted when immunity disabled");
        Check(hero.Voice.PlayCalls==1,"audio cooldown suppresses stacked thuds");
        hero.Tick(.2f);Check(hero.Hit(),"later accepted hit");
        Check(hero.Voice.PlayCalls==2&&hero.Voice.clip!=first,"successive audible hits alternate variants");
        Check(GameObject.Created==created,"hits never create objects or new voices");
        Check(hero.motor.enabled,"hurt does not stun or disable movement");
        hero.Tick(.6f);Equal(hero.hurt.HurtPitch,0,"hurt completes without drift");
        var side=new Hero();side.Hit(direction:Vector3.right);side.Tick(.045f);
        Check(side.hurt.HurtRoll<0,"right-directed impact tips chassis right");Equal(side.hurt.HurtPitch,0,"side impact has no fore-aft drift");
        var backward=new Hero();backward.Hit(direction:-Vector3.forward);backward.Tick(.045f);
        Check(backward.hurt.HurtPitch<0,"rearward incoming force reverses pitch");
    }

    static void RejectedHitsAndReset()
    {
        var hero=new Hero();hero.health.postHitInvulnerability=.6f;
        Check(hero.Hit(),"first immunity hit");hero.Tick(.045f);float pitch=hero.hurt.HurtPitch;
        Check(!hero.Hit(),"immunity rejects second hit");Equal(hero.hurt.HurtPitch,pitch,"rejected hit does not change response");
        Check(hero.Voice.PlayCalls==1,"rejected hit silent");
        hero.health.invulnerable=true;Check(!hero.Hit(),"invulnerable rejects");hero.health.invulnerable=false;
        Check(!hero.Hit(-10)&&!hero.Hit(float.NaN),"invalid/healing amounts rejected by actual health");
        hero.motor.RespawnVersion++;hero.health.ResetHealth();hero.Tick();
        Equal(hero.hurt.HurtPitch,0,"retry clears old life impulse");Check(!hero.Voice.isPlaying,"retry stops old life sound");
        hero.Hit();hero.Tick(.045f);Check(hero.hurt.HurtPitch>0,"new life can react");
        hero.vitality.MissionBlocked=true;hero.Tick();Equal(hero.hurt.HurtPitch,0,"menu lock clears chassis pose");
        int played=hero.Voice.PlayCalls;hero.health.ResetHealth();hero.Hit();Check(hero.Voice.PlayCalls==played,"no menu feedback");
        hero.vitality.MissionBlocked=false;hero.health.ResetHealth();
        hero.health.Died+=()=>hero.vitality.MissionBlocked=true;
        hero.Hit(1000);hero.Tick();
        Check(hero.health.IsDead,"fatal accepted by actual health");Equal(hero.hurt.HurtPitch,0,"dead hero never retains twitch");
        Check(hero.Voice.isPlaying,"lethal thud survives synchronous mission result lock");
        played=hero.Voice.PlayCalls;Check(!hero.Hit(),"dead receiver rejects");Check(hero.Voice.PlayCalls==played,"dead repeat silent");
    }

    static void Lifecycle()
    {
        var hero=new Hero();hero.Hit();hero.Tick(.045f);
        Call(hero.hurt,"OnApplicationFocus",false);Equal(hero.hurt.HurtPitch,0,"focus loss clears pose");Check(!hero.Voice.isPlaying,"focus loss stops thud");
        int played=hero.Voice.PlayCalls;hero.health.ResetHealth();hero.Hit();Check(hero.Voice.PlayCalls==played,"unfocused damage silent");
        Call(hero.hurt,"OnApplicationFocus",true);hero.health.ResetHealth();hero.Hit();hero.Tick(.045f);Check(hero.hurt.HurtPitch>0,"focus return accepts fresh damage");
        Call(hero.hurt,"OnApplicationPause",true);Equal(hero.hurt.HurtPitch,0,"app pause clears pose");
        Call(hero.hurt,"OnApplicationPause",false);hero.health.ResetHealth();hero.Hit();hero.Tick(.045f);
        Time.timeScale=0;hero.Tick();Equal(hero.hurt.HurtPitch,0,"scaled pause clears stale reaction");Time.timeScale=1;
        hero.health.ResetHealth();hero.Hit();hero.Tick(.045f);AudioListener.pause=true;hero.Tick();Check(!hero.Voice.isPlaying,"listener pause silences reaction");AudioListener.pause=false;
        hero.health.ResetHealth();hero.Hit();Call(hero.hurt,"OnDisable");hero.hurt.enabled=false;
        Equal(hero.hurt.HurtPitch,0,"disabled response resets");played=hero.Voice.PlayCalls;hero.health.ResetHealth();hero.Hit();Check(hero.Voice.PlayCalls==played,"disabled unsubscribes");
        hero.hurt.enabled=true;Call(hero.hurt,"OnEnable");Call(hero.hurt,"OnEnable");hero.health.ResetHealth();hero.Hit();Check(hero.Voice.PlayCalls==played+1,"reenable subscribes once");
        var other=new GameObject().AddComponent<IntercomHealth>();Call(other,"Awake");hero.hurt.health=other;hero.Tick();
        played=hero.Voice.PlayCalls;hero.health.ResetHealth();hero.Hit();Check(hero.Voice.PlayCalls==played,"old rebound health no longer drives feedback");
        other.TakeDamage(new IntercomDamage(12,Vector3.zero,Vector3.forward,null));Check(hero.Voice.PlayCalls==played+1,"new health owns feedback");
    }

    static void Main(){Envelope();AcceptedDamageAndAudio();RejectedHitsAndReset();Lifecycle();Console.WriteLine("Player hurt feedback: "+checks+" checks PASS (real Health, HurtFeedback, response; narrow Unity stand-ins).");}
}
