using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;using System.Reflection;using IntercomMvp;using UnityEngine;
class Program{
 static int checks;
 static void Call(object x,string name){try{x.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(x,null);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Check(bool x,string s){checks++;if(!x)throw new Exception(s);}
 static bool Equal(Quaternion a,Quaternion b)=>Math.Abs(Quaternion.Dot(a,b))>.9999999f;
 static void CheckSceneRegistration(){
  string scene=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"IntercomMVP.unity"));
  var docs=Regex.Split(scene,"(?=^--- !u!)",RegexOptions.Multiline);
  foreach(string name in new[]{"IntercomCharacterAnimation","IntercomWeaponMagazine"}){
   string meta=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,name+".cs.meta"));
   string guid=Regex.Match(meta,@"(?m)^guid: ([0-9a-f]{32})").Groups[1].Value;
   Check(guid.Length==32,"new component has stable script GUID: "+name);
   var blocks=docs.Where(d=>d.Contains("m_EditorClassIdentifier: Assembly-CSharp::IntercomMvp."+name+"\n")).ToArray();
   Check(blocks.Length==1,"exactly one registered scene component: "+name);
   string block=blocks[0];
   Check(block.Contains("guid: "+guid),"scene references actual script: "+name);
   string componentId=Regex.Match(block,@"^--- !u!114 &(-?\d+)").Groups[1].Value;
   string ownerId=Regex.Match(block,@"m_GameObject: \{fileID: (-?\d+)\}").Groups[1].Value;
   var owner=docs.Single(d=>d.StartsWith("--- !u!1 &"+ownerId+"\n"));
   Check(owner.Contains("component: {fileID: "+componentId+"}"),"GameObject component list owns script: "+name);
   Check(owner.Contains("m_Name: Robot\n"),"new component belongs to Robot: "+name);
  }
 }
 static void Main(){
 var root=new GameObject("Robot");root.AddComponent<CharacterController>();
 var motor=root.AddComponent<IntercomPlayerMotor>();Call(motor,"Awake");
 var animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=new RuntimeAnimatorController();
 var magazine=root.AddComponent<IntercomWeaponMagazine>();
 var wp=root.AddComponent<IntercomWeaponPose>();wp.Posture=IntercomWeaponPosture.Combat;
 var la=new GameObject("L_Upperarm").transform;var ra=new GameObject("R_Upperarm").transform;
 var lh=new GameObject("L_Hand").transform;var rh=new GameObject("R_Hand").transform;
 la.parent=ra.parent=root.transform;la.localPosition=new Vector3(-.3f,1,0);ra.localPosition=new Vector3(.3f,1,0);lh.parent=la;rh.parent=ra;lh.localPosition=rh.localPosition=new Vector3(0,-.3f,.3f);
 var authored=Quaternion.Euler(4,5,6);var handAuthored=Quaternion.Euler(3,-2,4);la.localRotation=ra.localRotation=authored;lh.localRotation=rh.localRotation=handAuthored;
 var g=root.AddComponent<IntercomGroundFeedback>();g.leftUpperArm=la;g.rightUpperArm=ra;g.motor=motor;g.animator=animator;
 var c=root.AddComponent<IntercomCharacterAnimation>();c.leftUpperArm=la;c.rightUpperArm=ra;c.leftHand=lh;c.rightHand=rh;c.animator=animator;c.motor=motor;c.magazine=magazine;c.weaponPose=wp;
 Call(c,"Awake");Call(c,"OnEnable");
 Action apply=()=>{Time.frameCount++;Call(g,"SavePose");la.localRotation=Quaternion.Euler(0,4,0)*authored;ra.localRotation=Quaternion.Euler(0,-4,0)*authored;Call(c,"LateUpdate");};
 Action assertAuthored=()=>{Check(Equal(la.localRotation,authored),"left upperarm authored");Check(Equal(ra.localRotation,authored),"right upperarm authored");Check(Equal(lh.localRotation,handAuthored),"left hand authored");Check(Equal(rh.localRotation,handAuthored),"right hand authored");};
 for(int i=0;i<1000;i++){apply();Check(!Equal(lh.localRotation,handAuthored),"reload visibly affects hand");Call(g,"Update");Call(c,"Update");assertAuthored();}
 apply();Call(g,"OnDisable");Call(c,"OnDisable");assertAuthored();
 Call(c,"OnEnable");apply();Call(c,"OnDisable");Call(g,"OnDisable");assertAuthored();
 Call(c,"OnEnable");apply();var external=Quaternion.Euler(31,27,-13);lh.localRotation=external;Call(c,"OnDisable");Check(Equal(lh.localRotation,external),"external hand pose preserved");Call(g,"OnDisable");lh.localRotation=handAuthored;
 Call(c,"OnEnable");apply();animator.runtimeAnimatorController=null;Call(g,"Update");Call(c,"Update");Call(c,"LateUpdate");assertAuthored();
 CheckSceneRegistration();
 Console.WriteLine("PASS "+checks+" combined production GroundFeedback/Character restore assertions, 1000 frames, both disable orders, external pose and controller removal. Scene component registration checked from YAML; narrow Unity doubles, no engine execution.");
 }
}
