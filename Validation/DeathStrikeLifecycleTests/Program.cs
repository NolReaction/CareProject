using System;
using System.Reflection;
using System.Collections;
using IntercomMvp;
using UnityEngine;
static class Program {
 static int checks;
 static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
 static object Call(object value,string method){try{return value.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,null);}catch(TargetInvocationException e){throw e.InnerException;}}
 static object Field(object value,string name)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
 sealed class Fixture {
  public IntercomHealth hero;public IntercomMissionDirector director;public IntercomNpcController npc;public CharacterController controller;public IntercomPlayerMotor motor;
  public Fixture(bool authored=false){
   ((IList)typeof(IntercomNpcController).GetField("active",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Clear();
   Time.timeScale=1;Time.time=0;Time.deltaTime=.01f;Physics.Hits=null;
   var player=new GameObject("Hero");player.transform.position=new Vector3(0,0,1);
   hero=player.AddComponent<IntercomHealth>();hero.team=IntercomTeam.Player;hero.maximumHealth=10;hero.ResetHealth();motor=player.AddComponent<IntercomPlayerMotor>();
   var input=player.AddComponent<IntercomPlayerInput>();var vitality=player.AddComponent<IntercomPlayerVitality>();vitality.health=hero;vitality.motor=motor;vitality.input=input;input.vitality=vitality;
   var definition=new IntercomNpcDefinition{damage=12,bodyMaterial=new Material(),detailMaterial=new Material(),telegraphMaterial=new Material()};
   if(authored){var model=new GameObject("Rigged model prefab");model.SetActive(false);var animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=new RuntimeAnimatorController();model.AddComponent<MeshRenderer>().sharedMaterial=new Material();definition.visualPrefab=model;}

   var nav=new GameObject("Nav").AddComponent<IntercomNpcNavigation>();
   npc=IntercomNpcController.Create(definition,nav,hero,motor,Vector3.zero,0);controller=npc.GetComponent<CharacterController>();
   var arena=new GameObject("Mission");arena.SetActive(false);director=arena.AddComponent<IntercomMissionDirector>();director.playerHealth=hero;director.playerMotor=motor;director.playerVitality=vitality;director.enemyDefinition=definition;director.navigation=nav;
   arena.SetActive(true);
   var state=(IntercomMissionState)Field(director,"state");state.Begin(IntercomMissionMode.Elimination,50,50);
   var pool=(Array)Field(director,"pool");var slot=pool.GetValue(0);slot.GetType().GetField("actor").SetValue(slot,npc);slot.GetType().GetField("living").SetValue(slot,true);
  }
  public void Step(){Time.time+=Time.deltaTime;Call(npc,"Update");}
 }
 static void Main(){
  for(int life=0;life<100;life++){
   var f=new Fixture();for(int i=0;i<100&&!f.hero.IsDead;i++)f.Step();
   Check(f.hero.IsDead,"production Health receives lethal NPC strike");
   Check(f.director.Phase==IntercomMissionPhase.Defeat,"production Director handles real health death");
   Check(!f.npc.gameObject.activeInHierarchy,"death synchronously parks the striking actor");
  }
  foreach(string mutation in new[]{"disable-ai","reuse","respawn","retarget","kill-source"}){
   var f=new Fixture();f.hero.maximumHealth=100;f.hero.ResetHealth();
   bool callback=false;int movesAtCallback=-1;
   f.hero.Damaged+=damage=>{
    callback=true;movesAtCallback=f.controller.MoveCalls;
    if(mutation=="disable-ai")f.npc.enabled=false;
    else if(mutation=="reuse"){
     f.npc.gameObject.SetActive(false);f.npc.PrepareForSpawn(new Vector3(4,0,4));f.npc.gameObject.SetActive(true);
    }
    else if(mutation=="respawn")f.motor.RespawnVersion++;
    else if(mutation=="retarget")f.npc.SetTarget(new GameObject("Replacement target").AddComponent<IntercomHealth>());
    else f.npc.Health.TakeDamage(new IntercomDamage(1000,Vector3.zero,Vector3.forward,f.hero.gameObject));
   };
   for(int i=0;i<100&&!callback;i++)f.Step();
   Check(callback,"strike invokes real accepted damage callback: "+mutation);
   Check(f.controller.MoveCalls==movesAtCallback,"old strike continuation cannot move changed source: "+mutation);
   Check((float)Field(f.npc,"strikeRemaining")==0,"old strike continuation cannot overwrite new/reset punch pose: "+mutation);
   if(mutation=="reuse")Check((f.npc.transform.position-new Vector3(4,0,4)).sqrMagnitude==0,"reused actor stays at new safe spawn");
  }
  {
   var f=new Fixture(true);var animator=(Animator)Field(f.npc,"modelAnimator");int samplesAtCallback=-1;
   f.hero.Damaged+=damage=>samplesAtCallback=animator.UpdateCalls;
   for(int i=0;i<100&&!f.hero.IsDead;i++)f.Step();
   Check(f.hero.IsDead&&!f.npc.gameObject.activeInHierarchy,"authored robot uses same real lethal mission parking boundary");
   Check(samplesAtCallback>=0&&animator.UpdateCalls==samplesAtCallback,"old lethal strike continuation never samples parked authored Animator");
  }
  Console.WriteLine("Lethal strike completed without moving the parked CharacterController.");
  Console.WriteLine($"Death strike lifecycle: {checks} assertions passed; production Health + NPC + Director, strict inactive CharacterController stand-in. Not a native Unity crash reproduction.");
 }
}
