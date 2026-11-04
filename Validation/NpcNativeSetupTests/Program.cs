using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using IntercomMvp;
using IntercomMvp.Editor;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using O=UnityEngine.Object;

static class Program
{
 static int checks;
 static readonly string[] Legacy={"Idle","Walk","Windup","Strike","Recover","Hurt","Death","Emerge"};
 static readonly string[] Current=Legacy.Concat(new[]{"EmergeGround","EmergeHatch"}).ToArray();
 static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
 static void PreservedError(Action run,string label){try{run();throw new Exception("Expected preservation refusal: "+label);}catch(InvalidOperationException e){Check(e.Message.Contains("сохранён"),"custom authoring refusal explains preservation: "+label);}}
 sealed class Fixture:IDisposable
 {
  public string Root=Path.Combine(Path.GetTempPath(),"CareProjectNativeSetup_"+Guid.NewGuid().ToString("N"));
  public IntercomNpcDefinition Definition;public GameObject Model,Prefab;public AnimatorController Controller;public Material Atlas;public ModelImporter Importer;
  public Dictionary<string,AnimationClip> Clips=new Dictionary<string,AnimationClip>();
  public Fixture(bool legacy=false,bool breakup=false)
  {
   Directory.CreateDirectory(Path.Combine(Root,"Assets"));Application.dataPath=Path.Combine(Root,"Assets");
   AssetDatabase.Clear();SessionState.Clear();PrefabUtility.SaveCounts.Clear();Undo.Recorded.Clear();Debug.Warnings.Clear();
   EditorApplication.isCompiling=EditorApplication.isUpdating=EditorApplication.isPlayingOrWillChangePlaymode=false;EditorApplication.delayCall=null;
   EditorSceneManager.OpenPreviews=EditorSceneManager.ClosedPreviews=0;
   Definition=new IntercomNpcDefinition();AssetDatabase.Register(Definition,IntercomNpcSetup.DefinitionPath);
   Atlas=new Material(Shader.Find("URP/Lit"));AssetDatabase.Register(Atlas,IntercomNpcSetup.RobotMaterialPath);
   AssetDatabase.Register(new Texture2D(),IntercomNpcSetup.RobotAtlasPath);
   Model=new GameObject("Udarnik source");Model.AddComponent<Animator>();var body=new GameObject("UdarnikBody");body.transform.SetParent(Model.transform,false);body.AddComponent<SkinnedMeshRenderer>().sharedMaterial=Atlas;
   AssetDatabase.Register(Model,IntercomNpcSetup.RobotModelPath);
   foreach(string n in Current){var c=new AnimationClip{name="UdarnikRig|"+n};Clips[n]=c;AssetDatabase.Paths[c]=IntercomNpcSetup.RobotModelPath;}
   AssetDatabase.SubAssets[IntercomNpcSetup.RobotModelPath]=new O[]{Model,new AnimationClip{name="__preview__Idle"},new AnimationClip{name="ignored"}}.Concat(Clips.Values).ToArray();
   Importer=new ModelImporter{importAnimation=true,animationType=ModelImporterAnimationType.Generic,avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel};AssetImporter.importers[IntercomNpcSetup.RobotModelPath]=Importer;
   if(legacy)
   {
    Controller=AnimatorController.CreateAnimatorControllerAtPath(IntercomNpcSetup.RobotControllerPath);
    foreach(string n in Legacy){var s=Controller.layers[0].stateMachine.AddState(n,Vector3.zero);s.motion=Clips[n];if(n=="Idle")Controller.layers[0].stateMachine.defaultState=s;}
    Prefab=(GameObject)PrefabUtility.InstantiatePrefab(Model,new UnityEngine.SceneManagement.Scene());Prefab.GetComponent<Animator>().runtimeAnimatorController=Controller;
    PrefabUtility.SaveAsPrefabAsset(Prefab,IntercomNpcSetup.RobotPrefabPath);Definition.visualPrefab=Prefab;
   }
   if(breakup)AddBreakup(6);
  }
  public GameObject AddBreakup(int count)
  {
   var root=new GameObject("Udarnik chunks source");
   // Nested import nodes intentionally test flattening and local centered pivot preservation.
   var intermediary=new GameObject("Export container");intermediary.transform.localPosition=new Vector3(.2f,.1f,-.3f);intermediary.transform.SetParent(root.transform,false);
   for(int i=0;i<count;i++){var chunk=new GameObject("Part"+i);chunk.transform.SetParent(intermediary.transform,false);chunk.transform.localPosition=new Vector3(i*.1f,.2f,.3f);chunk.AddComponent<MeshFilter>().sharedMesh=new Mesh{name="native mesh "+i};chunk.AddComponent<MeshRenderer>().sharedMaterial=new Material(Shader.Find("ignored source material"));}
   AssetDatabase.Register(root,IntercomNpcSetup.RobotBreakupModelPath);return root;
  }
  public string[] BackupFiles()=>Directory.Exists(Path.Combine(Root,"ProjectBackups"))?Directory.GetFiles(Path.Combine(Root,"ProjectBackups"),"*",SearchOption.AllDirectories):Array.Empty<string>();
  public void Dispose()=>Directory.Delete(Root,true);
 }
 static void Main()
 {
  NativeCreation();UpgradeV18();CustomPreservation();ImportAndFailureGuards();AutoUpgrade();
  Console.WriteLine($"NPC native setup: {checks} assertions passed; actual editor builder and real backup files with narrow AssetDatabase/Prefab/AnimatorController stand-ins. Not native Unity import/serialization.");
 }
 static void NativeCreation()
 {
  using var f=new Fixture(breakup:true);IntercomNpcSetup.EnsureRobotVisualAssets();
  var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(IntercomNpcSetup.RobotControllerPath);var prefab=f.Definition.visualPrefab;
  Check(c!=null&&c.layers[0].stateMachine.states.Length==10,"fresh native controller has10 known states");
  Check(AssetDatabase.LoadAllCalls==1,"native clip discovery reads imported subassets directly");
  foreach(string n in Current)Check(c.layers[0].stateMachine.states.Single(s=>s.state.name==n).state.motion==f.Clips[n],"native motion object retained instead of guessed ID: "+n);
  Check(prefab.GetComponent<Animator>()!=null&&prefab.GetComponent<Animator>().runtimeAnimatorController==c,"Animator/controller remains on imported model root");
  Check(prefab.transform.children.Count==1&&prefab.transform.children[0].gameObject.name=="UdarnikBody","no extra wrapper alters imported FBX bone paths");
  Check(!prefab.GetComponent<Animator>().applyRootMotion&&prefab.GetComponentsInChildren<Collider>(true).Length==0,"native visual has no gameplay collision/root motion");
  Check(prefab.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterial==f.Atlas),"visual shares authored atlas material");
  var chunks=f.Definition.breakupPrefab;Check(chunks!=null&&chunks.transform.children.Count==6,"optional static template flattens six direct chunks");
  Check(chunks.GetComponentsInChildren<Animator>(true).Length==0&&chunks.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==0&&chunks.GetComponentsInChildren<Collider>(true).Length==0,"death template has static meshes and no skin/animator/collider");
  Check(chunks.GetComponentsInChildren<MeshRenderer>(true).All(r=>r.sharedMaterial==f.Atlas),"breakup shares existing atlas rather than per-piece materials");
  var first=chunks.transform.children[0];Check(Math.Abs(first.localPosition.x-.2f)<.00001f&&Math.Abs(first.localPosition.y-.3f)<.00001f&&Math.Abs(first.localPosition.z)<.00001f,"flattened chunk keeps centered source pivot translation");
  Check(AssetDatabase.GetLabels(c).Contains("CareProject_Udarnik_V19")&&AssetDatabase.GetLabels(prefab).Contains("CareProject_Udarnik_V19"),"generated native assets record V19 revision label");
  int backups=f.BackupFiles().Length,creates=AssetDatabase.CreateCalls,saves=PrefabUtility.SaveCounts.Values.Sum();
  IntercomNpcSetup.EnsureRobotVisualAssets();
  Check(f.Definition.visualPrefab==prefab&&f.Definition.breakupPrefab==chunks,"repeated preparation keeps assigned native object references");
  Check(AssetDatabase.CreateCalls==creates&&PrefabUtility.SaveCounts.Values.Sum()==saves&&f.BackupFiles().Length==backups,"repeat preparation creates no duplicate native assets or backups");
  Check(EditorSceneManager.OpenPreviews==EditorSceneManager.ClosedPreviews,"all authoring preview scenes close");
 }
 static void UpgradeV18()
 {
  using var f=new Fixture(legacy:true,breakup:true);var original=f.Controller;var prefab=f.Prefab;
  var originalStates=original.layers[0].stateMachine.states.Select(s=>s.state).ToArray();
  int visualSaves=PrefabUtility.SaveCounts[IntercomNpcSetup.RobotPrefabPath];
  string sourceController=File.ReadAllText(Path.Combine(f.Root,IntercomNpcSetup.RobotControllerPath));
  IntercomNpcSetup.EnsureRobotVisualAssets();
  Check(AssetDatabase.LoadAssetAtPath<AnimatorController>(IntercomNpcSetup.RobotControllerPath)==original,"V18 migration retains controller object/GUID identity");
  Check(f.Definition.visualPrefab==prefab&&PrefabUtility.SaveCounts[IntercomNpcSetup.RobotPrefabPath]==visualSaves,"existing imported model variant is retained and never rebuilt");
  Check(original.layers[0].stateMachine.states.Length==10&&original.layers[0].stateMachine.defaultState.name=="Idle","recognized V18 graph upgrades to10 states and Idle default");
  Check(originalStates.All(s=>original.layers[0].stateMachine.states.Any(c=>ReferenceEquals(c.state,s))),"implicit upgrade retains original eight state objects and authored layout IDs");
  string[] backup=f.BackupFiles();
  foreach(string n in new[]{"Udarnik.controller","Udarnik.controller.meta","UdarnikVisual.prefab","UdarnikVisual.prefab.meta","UdarnikCapsule.asset","UdarnikCapsule.asset.meta"})Check(backup.Any(p=>Path.GetFileName(p)==n),"migration writes prechange backup including metadata: "+n);
  Check(backup.Where(p=>Path.GetFileName(p)=="Udarnik.controller").Any(p=>File.ReadAllText(p)==sourceController),"controller backup retains original serialized contents");
  Check(Undo.Recorded.Contains(original)&&Undo.Recorded.Contains(original.layers[0].stateMachine),"migration records controller/machine undo before changing states");
  foreach(string n in Current)Check(original.layers[0].stateMachine.states.Single(s=>s.state.name==n).state.motion==f.Clips[n],"upgraded graph uses exact imported native clip: "+n);
  int backupCount=backup.Length;IntercomNpcSetup.EnsureRobotVisualAssets();Check(f.BackupFiles().Length==backupCount,"recognized current graph is idempotent after migration");
 }
 static void CustomPreservation()
 {
  foreach(string mutation in new[]{"parameter","transition","behaviour","state-speed","foreign-motion","extra-state","custom-prefab","custom-controller-on-prefab"})
  {
   using var f=new Fixture(legacy:true);var m=f.Controller.layers[0].stateMachine;var idle=m.states[0].state;
   if(mutation=="parameter")f.Controller.parameters=new O[]{new O()};
   else if(mutation=="transition")idle.transitions=new O[]{new O()};
   else if(mutation=="behaviour")m.behaviours=new O[]{new O()};
   else if(mutation=="state-speed")idle.speed=.8f;
   else if(mutation=="foreign-motion"){idle.motion=new AnimationClip{name="Idle"};AssetDatabase.Paths[idle.motion]="Assets/Custom/Idle.anim";}
   else if(mutation=="extra-state")m.AddState("My custom state",Vector3.zero);
   else if(mutation=="custom-prefab")f.Prefab.source=null;
   else f.Prefab.GetComponent<Animator>().runtimeAnimatorController=new AnimatorController();
   var states=m.states.ToArray();int files=f.BackupFiles().Length,saveCount=PrefabUtility.SaveCounts.Values.Sum();
   PreservedError(()=>IntercomNpcSetup.EnsureRobotVisualAssets(),mutation);
   Check(m.states.SequenceEqual(states)&&f.Definition.visualPrefab==f.Prefab,"custom graph/prefab remains untouched: "+mutation);
   Check(f.BackupFiles().Length==files&&PrefabUtility.SaveCounts.Values.Sum()==saveCount&&!AssetDatabase.GetLabels(f.Controller).Contains("CareProject_Udarnik_V19"),"refused custom migration writes no backup/revision/prefab: "+mutation);
  }
  using(var f=new Fixture())
  {
   var custom=new GameObject("My robot");AssetDatabase.Register(custom,"Assets/MyRobot.prefab");f.Definition.visualPrefab=custom;IntercomNpcSetup.EnsureRobotVisualAssets();
   Check(f.Definition.visualPrefab==custom&&AssetDatabase.LoadAllCalls==0&&f.BackupFiles().Length==0,"different assigned prefab is preserved before importing or creating native assets");
  }
  using(var f=new Fixture(breakup:true))
  {
   var custom=new GameObject("My disassembly");f.Definition.breakupPrefab=custom;IntercomNpcSetup.EnsureRobotVisualAssets();
   Check(f.Definition.breakupPrefab==custom,"custom breakup assignment is never replaced by generated template");
  }
  using(var f=new Fixture(legacy:true))
  {
   AssetDatabase.Assets.Remove(IntercomNpcSetup.RobotControllerPath);
   PreservedError(()=>IntercomNpcSetup.EnsureRobotVisualAssets(),"canonical prefab missing controller asset");
   Check(f.Definition.visualPrefab==f.Prefab&&AssetDatabase.LoadAssetAtPath<AnimatorController>(IntercomNpcSetup.RobotControllerPath)==null,"orphaned canonical prefab is preserved instead of linked to a guessed replacement controller");
  }
  using(var f=new Fixture(legacy:true))
  {
   var idle=f.Controller.layers[0].stateMachine.states[0].state;idle.motion=null;
   IntercomNpcSetup.EnsureRobotVisualAssets();
   Check(idle.motion==f.Clips["Idle"]&&f.Controller.layers[0].stateMachine.states.Any(s=>s.state==idle),"known imported clip lost during FBX reimport reconnects without replacing its state object");
  }
 }
 static void ImportAndFailureGuards()
 {
  using(var f=new Fixture())
  {
   f.Importer.importAnimation=false;f.Importer.animationType=ModelImporterAnimationType.None;f.Importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
   IntercomNpcSetup.EnsureRobotVisualAssets();Check(f.Importer.Reimports==1&&f.Importer.importAnimation&&f.Importer.animationType==ModelImporterAnimationType.Generic&&f.Importer.avatarSetup==ModelImporterAvatarSetup.CreateFromThisModel,"native importer repairs Generic animation/avatar flags exactly once");
   Check(f.Definition.breakupPrefab==null,"missing optional chunk FBX keeps generic runtime gear fallback available");
  }
  using(var f=new Fixture())
  {
   AssetDatabase.SubAssets[IntercomNpcSetup.RobotModelPath]=f.Clips.Where(p=>p.Key!="EmergeHatch").Select(p=>(O)p.Value).ToArray();
   try{IntercomNpcSetup.EnsureRobotVisualAssets();throw new Exception("missingclip accepted");}catch(InvalidOperationException e){Check(e.Message.Contains("EmergeHatch"),"missing imported clip reports exact name before native asset mutation");}
   Check(f.Definition.visualPrefab==null&&AssetDatabase.LoadAssetAtPath<AnimatorController>(IntercomNpcSetup.RobotControllerPath)==null,"missing clips leave native graph and profile untouched");
  }
  using(var f=new Fixture())
  {
   f.AddBreakup(7);
   try{IntercomNpcSetup.EnsureRobotVisualAssets();throw new Exception("too many chunks accepted");}catch(InvalidOperationException){Check(true,"static template refuses more than six chunks");}
   Check(f.Definition.breakupPrefab==null&&EditorSceneManager.OpenPreviews==EditorSceneManager.ClosedPreviews,"invalid static import never assigns template and closes its preview scene");
  }
  foreach(string busy in new[]{"play","compile","update"})using(var f=new Fixture())
  {
   EditorApplication.isPlayingOrWillChangePlaymode=busy=="play";EditorApplication.isCompiling=busy=="compile";EditorApplication.isUpdating=busy=="update";
   try{IntercomNpcSetup.EnsureRobotVisualAssets();throw new Exception("busy accepted");}catch(InvalidOperationException){Check(f.Definition.visualPrefab==null&&AssetDatabase.LoadAllCalls==0,"busy editor guard prevents asset mutation: "+busy);}
  }
 }
 static void AutoUpgrade()
 {
  using var f=new Fixture(legacy:true);
  SessionState.SetBool("CareProject.UdarnikPrepared."+Application.dataPath,true); // V18 session attempt must not mask V19.
  var method=typeof(IntercomNpcSetup).GetMethod("PrepareImportedRobotWhenReady",BindingFlags.Static|BindingFlags.NonPublic);
  method.Invoke(null,null);
  Check(f.Controller.layers[0].stateMachine.states.Length==10,"first V19 load upgrades existing nonnull known V18 native prefab");
  Check(SessionState.GetBool("CareProject.UdarnikPrepared.V19."+Application.dataPath,false),"auto preparation has version-specific session guard");
  int calls=AssetDatabase.LoadAllCalls;method.Invoke(null,null);Check(AssetDatabase.LoadAllCalls==calls,"repeat auto callback does not import/build a second time this session");
 }
}
