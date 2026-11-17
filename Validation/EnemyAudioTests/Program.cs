using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

static class Program
{
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    static void Call(object target,string name,params object[] args){try{target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    static void Tick(IntercomEnemyAudio audio,float dt=.02f){Time.time+=dt;Time.deltaTime=dt;Call(audio,"Update");}
    static (IntercomMissionDirector,IntercomEnemyAudio) Harness()
    {
        IntercomNpcController.Clear();Time.time=0;Time.timeScale=1;Time.deltaTime=.02f;Application.isFocused=true;
        var root=new GameObject("Mission");var mission=root.AddComponent<IntercomMissionDirector>();mission.playerHealth=new GameObject("Hero").AddComponent<IntercomHealth>();
        root.SetActive(false);int clipCount=AudioClip.Created,voiceCount=AudioSource.Created;
        AudioClip.CreationAllowed=AudioSource.CreationAllowed=false;IntercomEnemyAudio audio;
        try{audio=root.AddComponent<IntercomEnemyAudio>();audio.mission=mission;}finally{AudioClip.CreationAllowed=AudioSource.CreationAllowed=true;}
        Check(AudioClip.Created==clipCount&&AudioSource.Created==voiceCount,"component deserialization does not allocate native audio");
        root.SetActive(true);Tick(audio);
        Check(AudioClip.Created==clipCount+12&&AudioSource.Created==voiceCount+4&&audio.PreparedVoices==4,"Awake warms twelve clips and four voices exactly once");
        return(mission,audio);
    }
    static IntercomNpcController Actor(IntercomMissionDirector mission,int variant=0,bool emerging=false)
    {
        var root=new GameObject("Enemy");root.SetActive(false);root.transform.SetParent(mission.transform,true);
        var hp=root.AddComponent<IntercomHealth>();hp.team=IntercomTeam.Enemy;
        var actor=root.AddComponent<IntercomNpcController>();actor.Health=hp;actor.variant=variant;actor.IsEmerging=emerging;root.SetActive(true);return actor;
    }
    static void Kill(IntercomNpcController actor)=>actor.Health.TakeDamage(new IntercomDamage(1000,Vector3.zero,Vector3.forward,null));
    static AudioSource[] Voices(IntercomEnemyAudio audio)=>Field<AudioSource[]>(audio,"voices");
    static int Listeners(IntercomHealth health)=>((Delegate)typeof(IntercomHealth).GetField("Died",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(health))?.GetInvocationList().Length??0;
    static void Main()
    {
        var(m,a)=Harness();var npc=Actor(m,0,true);Tick(a);Check(a.ReportsPlayed==1,"new emergence reports one arrival cue");
        Tick(a,.2f);Check(a.ReportsPlayed==1,"held arrival does not repeat");npc.IsEmerging=false;npc.Phase=IntercomNpcPhase.Chase;Tick(a);
        npc.Phase=IntercomNpcPhase.Windup;Tick(a,.2f);Check(a.ReportsPlayed==2,"entering windup reports servo");
        Tick(a,.2f);Check(a.ReportsPlayed==2,"held windup does not repeat");npc.Phase=IntercomNpcPhase.Recovery;npc.ResolvedStrikeVersion++;Tick(a);
        Check(a.ReportsPlayed==3,"one accepted strike version reports one whoosh");Tick(a,.15f);Check(a.ReportsPlayed==3,"held strike version does not repeat");
        npc.Phase=IntercomNpcPhase.Chase;Tick(a);npc.Phase=IntercomNpcPhase.Windup;Tick(a,.2f);
        var first=Voices(a).Last(v=>v.LastClip!=null&&v.LastClip.name.Contains("windup")).LastClip;
        npc.Phase=IntercomNpcPhase.Chase;Tick(a,.2f);npc.Phase=IntercomNpcPhase.Windup;Tick(a,.2f);
        var current=Voices(a).First(v=>v.isPlaying&&v.LastClip!=null&&v.LastClip.name.Contains("windup")).LastClip;
        Check(first!=current,"successive windup cue does not repeat same sample");
        Check(Listeners(npc.Health)==1,"one death subscriber per bound actor");
        int before=a.ReportsPlayed;Kill(npc);Check(a.ReportsPlayed==before+1,"death callback plays synchronously without waiting for Update");Tick(a);Check(a.ReportsPlayed==before+1,"dead actor does not repeat cue");
        Check(Voices(a).All(v=>v.priority==180&&v.dopplerLevel==0&&v.volume<=.24f&&v.volume>=0),"enemy mix keeps low gain and neutral doppler");
        int objects=GameObject.Created.Count,clips=AudioClip.Created,voices=AudioSource.Created;
        for(int run=0;run<12;run++){m.RunRevision++;npc.Health.ResetHealth();npc.LifeVersion++;npc.ResolvedStrikeVersion=0;npc.Phase=IntercomNpcPhase.Chase;Tick(a);npc.Phase=IntercomNpcPhase.Windup;Tick(a,.2f);}
        Check(objects==GameObject.Created.Count&&clips==AudioClip.Created&&voices==AudioSource.Created,"retry and cues reuse all native resources");
        before=a.ReportsPlayed;Time.timeScale=0;npc.ResolvedStrikeVersion++;Tick(a);Check(a.ReportsPlayed==before&&Voices(a).All(v=>!v.isPlaying),"pause stops all voices and ignores cue changes");
        Time.timeScale=1;Tick(a);Check(a.ReportsPlayed==before,"resume does not replay paused strike");
        Call(a,"OnApplicationFocus",false);npc.ResolvedStrikeVersion++;Tick(a);Check(a.ReportsPlayed==before&&Voices(a).All(v=>!v.isPlaying),"focus loss silences sources");Call(a,"OnApplicationFocus",true);Tick(a);Check(a.ReportsPlayed==before,"focus return does not catch up cues");
        Call(a,"OnApplicationPause",true);npc.ResolvedStrikeVersion++;Tick(a);Check(a.ReportsPlayed==before,"application pause suppresses cues");Call(a,"OnApplicationPause",false);Tick(a);Check(a.ReportsPlayed==before,"application resume does not catch up cues");
        npc.transform.position=new Vector3(100,0,100);npc.ResolvedStrikeVersion++;Tick(a,.2f);Check(a.ReportsPlayed==before,"distant enemies are silent");npc.transform.position=Vector3.zero;
        m.Phase=IntercomMissionPhase.Selection;npc.ResolvedStrikeVersion++;Tick(a,.2f);Check(a.ReportsPlayed==before&&Listeners(npc.Health)==0,"selection unbinds actors and silences cues");m.Phase=IntercomMissionPhase.Active;Tick(a);
        a.enabled=false;Check(Listeners(npc.Health)==0&&Voices(a).All(v=>!v.isPlaying),"disable releases health subscriptions and voices");a.enabled=true;Tick(a);
        int bound=Listeners(npc.Health);m.RunRevision++;npc.Health.ResetHealth();npc.LifeVersion++;Tick(a);Check(Listeners(npc.Health)==bound,"new run cannot accumulate death listeners");
        var unrelated=new GameObject("Other arena").AddComponent<IntercomMissionDirector>();for(int i=0;i<8;i++)Actor(unrelated,i,true);var own=Actor(m,7,true);Tick(a,.3f);Check(a.ReportsPlayed>0,"foreign active actors do not hide this arena actors");
        // Test the actual synchronous ordering: mission parks the victim before audio's death callback.
        var(m2,a2)=Harness();var victim=Actor(m2);victim.Health.Died+=()=>{m2.Phase=IntercomMissionPhase.Victory;victim.enabled=false;};Tick(a2);before=a2.ReportsPlayed;Kill(victim);
        Check(a2.ReportsPlayed==before+1,"final death cue survives an earlier mission park and Victory transition");Tick(a2);
        Check(Voices(a2).Any(v=>v.isPlaying&&v.LastClip.name.Contains("rattle"))&&Voices(a2).Where(v=>v.isPlaying).All(v=>v.LastClip.name.Contains("rattle")),"victory permits only final death tails");
        m2.RunRevision++;m2.Phase=IntercomMissionPhase.Selection;Tick(a2);Check(Voices(a2).All(v=>!v.isPlaying),"retry/menu transition clears final tails");
        var(m3,a3)=Harness();var stale=Actor(m3);stale.Health.Died+=()=>{m3.RunRevision++;stale.Health.ResetHealth();};Tick(a3);before=a3.ReportsPlayed;Kill(stale);Check(a3.ReportsPlayed==before,"death callback from reset life cannot issue a stale cue");
        // Four staggered deaths occupy the shared pool; lower-priority motion cues cannot steal their tails.
        var(m4,a4)=Harness();var dying=new IntercomNpcController[4];for(int i=0;i<4;i++)dying[i]=Actor(m4,i);var quiet=Actor(m4,7);Tick(a4);
        for(int i=0;i<4;i++){if(i>0)Tick(a4,.11f);Kill(dying[i]);}
        Check(Voices(a4).Count(v=>v.isPlaying)==4&&Voices(a4).All(v=>v.LastClip.name.Contains("rattle")),"burst is bounded by four shared death voices");
        before=a4.ReportsPlayed;quiet.Phase=IntercomNpcPhase.Windup;Tick(a4,.046f);Check(a4.ReportsPlayed==before,"quiet servo cannot steal a higher-priority death tail");
        quiet.ResolvedStrikeVersion++;Tick(a4,.001f);Check(a4.ReportsPlayed==before,"swing cannot replace the four protected death tails");
        quiet.ResolvedStrikeVersion++;Tick(a4,.08f);Check(a4.ReportsPlayed==before+1&&Voices(a4).Count(v=>v.isPlaying)<=4,"finished voice is reused by next swing without growing pool");
        var bank=Field<AudioClip[]>(a3,"clips");
        foreach(var clip in bank){Check(clip.Samples.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<.8f),"PCM finite with headroom: "+clip.name);Check(Math.Abs(clip.Samples[0])<.0001f&&Math.Abs(clip.Samples[^1])<.001f,"PCM has quiet sample boundaries: "+clip.name);}
        WritePreview(bank);
        var pool=Field<GameObject>(a3,"pool");Call(a3,"OnDestroy");Check(UnityEngine.Object.Destroyed.Contains(pool)&&bank.All(UnityEngine.Object.Destroyed.Contains),"owned voice pool and clips released");
        Check(Listeners(stale.Health)==1,"destroy removes audio subscription, preserving unrelated game callback");
        Console.WriteLine("Enemy audio: "+checks+" assertions PASS. Production scheduler and real health lifecycle; Unity audio output/attenuation is separate.");
    }
    static void WritePreview(AudioClip[] clips)
    {
        DirectoryInfo root=new DirectoryInfo(Directory.GetCurrentDirectory());while(root!=null&&!Directory.Exists(Path.Combine(root.FullName,"Assets/IntercomMVP")))root=root.Parent;
        string folder=Path.Combine(root.FullName,"Source/Audio/Previews");Directory.CreateDirectory(folder);
        const int rate=22050,gap=11025;int count=clips.Sum(c=>c.Samples.Length+gap);using var file=File.Create(Path.Combine(folder,"V23_EnemySounds.wav"));using var writer=new BinaryWriter(file);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
        foreach(var clip in clips){foreach(float sample in clip.Samples)writer.Write((short)(sample*32767));for(int i=0;i<gap;i++)writer.Write((short)0);}
    }
}
