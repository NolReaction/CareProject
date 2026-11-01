using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using IntercomMvp;
class P {
 static int checks;
 static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static void Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
 static T Field<T>(object obj,string n)=>(T)obj.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
 static IntercomAudioProfile Profile()=>new IntercomAudioProfile {pistolShots=new[]{new AudioClip(),new AudioClip(),new AudioClip()},footsteps=new[]{new AudioClip(),new AudioClip()},ambience=new AudioClip()};
 static int Playing(AudioSource[] sources)=>sources.Count(s=>s.isPlaying);
 static int Plays(AudioSource[] sources)=>sources.Sum(s=>s.playCalls);
 static int Main(){
 var go=new GameObject("Robot");var motor=go.AddComponent<IntercomPlayerMotor>();var shooter=go.AddComponent<IntercomSimpleShooter>();var animator=go.AddComponent<Animator>();var audio=go.AddComponent<IntercomPlayerAudio>();audio.profile=Profile();
 Call(audio,"Awake");Call(audio,"OnEnable");var shots=Field<AudioSource[]>(audio,"shots");var steps=Field<AudioSource[]>(audio,"steps");var ambient=Field<AudioSource>(audio,"ambient");
 Check(shots.Length==6&&steps.Length==2,"bounded voices");Check(shots.All(s=>!s.playOnAwake&&s.spatialBlend==0&&s.dopplerLevel==0),"player voices configuration");
 Call(audio,"LateUpdate");Check(ambient.isPlaying&&ambient.loop,"ambient starts loop");Check(ambient.volume>0&&ambient.volume<audio.profile.masterVolume*audio.profile.ambienceVolume,"ambient fades in");
 int initialObjects=GameObject.Created;for(int i=0;i<100;i++)shooter.Emit();Check(GameObject.Created==initialObjects,"no shot allocations");Check(Playing(shots)==6&&Plays(shots)==100,"pool bounded during burst");
 int before=Plays(shots);shooter.Emit(0);Check(Plays(shots)==before,"no empty volley report");
 audio.profile=Profile();shooter.Emit();Call(audio,"LateUpdate");Check(Playing(shots)==1,"replacement profile first shot survives LateUpdate");Check(shots.Single(s=>s.isPlaying).clip!=null,"new shot has clip");
 Call(audio,"OnApplicationFocus",false);Check(Playing(shots)==0&&ambient.paused,"focus loss stops shots and pauses ambience");before=Plays(shots);shooter.Emit();Check(Plays(shots)==before,"focus loss suppresses newshots");Call(audio,"OnApplicationFocus",true);Call(audio,"LateUpdate");Check(ambient.isPlaying&&ambient.unpauseCalls==1,"focus return resumes ambience");Check(Playing(shots)==0,"oldshots not resumed");
 Time.timeScale=0;Call(audio,"LateUpdate");Check(ambient.paused,"timeScale pause");before=Plays(shots);shooter.Emit();Check(Plays(shots)==before,"timescale shot suppression");Time.timeScale=1;Call(audio,"LateUpdate");Check(ambient.isPlaying,"timescale resume");
 Call(audio,"OnApplicationPause",true);Call(audio,"LateUpdate");Check(ambient.paused,"OSpause");Call(audio,"OnApplicationPause",false);Call(audio,"LateUpdate");Check(ambient.isPlaying,"OSresume");
 AudioListener.pause=true;Call(audio,"LateUpdate");before=Plays(shots);shooter.Emit();Check(Plays(shots)==before,"listenerpause suppression");AudioListener.pause=false;Call(audio,"LateUpdate");
 motor.ActualPlanarSpeed=1.8f;animator.state=new AnimatorStateInfo{normalizedTime=.45f,fullPathHash=1};Call(audio,"LateUpdate");animator.state.normalizedTime=.51f;Call(audio,"LateUpdate");Check(Plays(steps)==1,"contact crossing triggers one step");
 motor.ActualPlanarSpeed=0;animator.state.normalizedTime=1.01f;Call(audio,"LateUpdate");Check(Plays(steps)==1,"stationary nofootstep");motor.ActualPlanarSpeed=1.8f;motor.IsGrounded=false;animator.state.normalizedTime=1.51f;Call(audio,"LateUpdate");Check(Plays(steps)==1,"airborne nofootstep");
 motor.IsGrounded=true;animator.state.normalizedTime=1.52f;Call(audio,"LateUpdate");Check(Plays(steps)==1,"landing resync no queuedfootsteps");animator.state.normalizedTime=2.01f;Call(audio,"LateUpdate");Check(Plays(steps)==2,"next truecontact after landing");
 Call(audio,"OnDisable");Check(Playing(shots)==0&&Playing(steps)==0&&!ambient.isPlaying,"disable stops all audio");before=Plays(shots);shooter.Emit();Check(Plays(shots)==before,"disable unsubscribes");Call(audio,"OnEnable");Call(audio,"LateUpdate");Check(ambient.isPlaying,"reenable restores ambience");shooter.Emit();Check(Plays(shots)==before+1,"reenable one subscription");
 audio.profile.pistolShots=new AudioClip[]{null};before=Plays(shots);shooter.Emit();Check(Plays(shots)==before,"nullclip safe");audio.profile=null;Call(audio,"LateUpdate");Check(!ambient.isPlaying&&Playing(shots)==0,"nullprofile suspends");
 Call(audio,"OnDestroy");Console.WriteLine("PlayerAudio fake-Unity lifecycle: "+checks+" assertions passed. Not a Unity Editor test.");return 0;
 }
}
