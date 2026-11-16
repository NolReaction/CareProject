using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using IntercomMvp;
using IntercomMvp.Editor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
static class Program
{
 static int checks;
 static void Check(bool c,string s){checks++;if(!c)throw new Exception(s);}
 static void Private(string name)=>typeof(IntercomNativeVerification).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
 static JsonDocument Report()=>JsonDocument.Parse(File.ReadAllText(IntercomNativeVerification.LatestReportPath));
 static void CleanScene(){SceneManager.Scenes.Clear();var s=new Scene{path="Assets/Original.unity",isLoaded=true};SceneManager.Scenes.Add(s);SceneManager.Active=s;}
 static void ExitPlay(){EditorApplication.Raise(PlayModeStateChange.ExitingPlayMode);EditorApplication.Raise(PlayModeStateChange.EnteredEditMode);}
 static IntercomMissionDirector Fixture(out IntercomProgression progression,bool gunInitiallyEnabled=true)
 {
  var hero=new GameObject();var health=hero.AddComponent<IntercomHealth>();var motor=hero.AddComponent<IntercomPlayerMotor>();hero.AddComponent<CharacterController>();hero.AddComponent<IntercomPlayerInput>();hero.AddComponent<IntercomWeaponPose>();hero.AddComponent<IntercomSimpleShooter>().enabled=gunInitiallyEnabled;hero.AddComponent<IntercomPerformanceCapture>();
  var go=new GameObject();var mission=go.AddComponent<IntercomMissionDirector>();mission.playerHealth=health;mission.playerMotor=motor;mission.navigation=go.AddComponent<IntercomNpcNavigation>();
  var ground=new GameObject().AddComponent<IntercomEnemySpawnPoint>();ground.kind=IntercomEnemySpawnKind.Ground;ground.transform.position=new Vector3(1,0,6);
  var hatch=new GameObject().AddComponent<IntercomEnemySpawnPoint>();hatch.kind=IntercomEnemySpawnKind.Hatch;hatch.transform.position=new Vector3(-6,0,3);
  mission.authoredSpawnPoints=new[]{ground,hatch};
  progression=go.AddComponent<IntercomProgression>();Object.Found[typeof(IntercomMissionDirector)]=mission;Object.Found[typeof(IntercomProgression)]=progression;
  mission.ReturnToSelection();
  return mission;
 }
 static void Main()
 {
  Application.persistentDataPath=Path.Combine(Path.GetTempPath(),"CareNativeHarness-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Application.persistentDataPath);
  try
  {
   CleanScene();var dirty=SceneManager.Scenes[0];dirty.isDirty=true;SceneManager.Scenes[0]=dirty;
   IntercomNativeVerification.RunFromMenu();Check(!EditorApplication.isPlaying,"unsaved scene refused before Play Mode");Check(!SessionState.GetBool("CareProject.NativeVerification.requested",false),"dirty refusal clears request");
   using(var r=Report())Check(r.RootElement.GetProperty("status").GetString()=="FAIL"&&!r.RootElement.GetProperty("nativeEditorExecuted").GetBoolean(),"refused native attempt records fresh non-native FAIL rather than leaving stale PASS");
   CleanScene();EditorApplication.isCompiling=true;IntercomNativeVerification.RunFromMenu();Check(!EditorApplication.isPlaying,"compiling editor refused");EditorApplication.isCompiling=false;
   AssetDatabase.Ready=false;IntercomNativeVerification.RunFromMenu();Check(!EditorApplication.isPlaying,"missing authored scene refused");AssetDatabase.Ready=true;
   var mission=Fixture(out var progression);var original=mission.eliminationDefinition;
   Check(!mission.playerMotor.GetComponent<IntercomSimpleShooter>().enabled,"Selection owns a real disabled shooter lease before diagnostics initialize");
   mission.enemyDefinition.visualPrefab=null;
   IntercomNativeVerification.RunFromMenu();Check(EditorApplication.isPlaying,"clean scene requests real Play Mode");EditorApplication.Raise(PlayModeStateChange.EnteredPlayMode);
   Check(!progression.IsDiagnosticSession&&progression.Begins==1&&progression.Ends==1,"constructor failure always ends temporary progression session");
   using(var r=Report())Check(r.RootElement.GetProperty("status").GetString()=="FAIL"&&r.RootElement.GetProperty("nativeEditorExecuted").GetBoolean(),"entered Play Mode setup failure is explicit FAIL");
   ExitPlay();Check(EditorSceneManager.Restored.Length==1&&EditorSceneManager.Restored[0].path=="Assets/Original.unity","constructor failure restores previous saved scene");
   mission.enemyDefinition.visualPrefab=new GameObject();CleanScene();Time.timeScale=.65f;Application.runInBackground=false;
   IntercomNativeVerification.RunFromMenu();
   using(var r=Report())Check(r.RootElement.GetProperty("status").GetString()=="RUNNING"&&!r.RootElement.GetProperty("nativeEditorExecuted").GetBoolean(),"launch stub is fresh RUNNING before actual Play Mode");
   EditorApplication.Raise(PlayModeStateChange.EnteredPlayMode);
   Check(progression.IsDiagnosticSession,"real scenarios are isolated before mission start");
   Check(!mission.playerMotor.GetComponent<IntercomSimpleShooter>().enabled,"initialization preserves Selection's disabled shooter instead of forcing it on");
   Check(!ReferenceEquals(original,mission.eliminationDefinition)&&original.killTarget==50&&mission.eliminationDefinition.killTarget==18,"accelerated profile is a runtime clone preserving authored data");
   Check(!mission.playerMotor.GetComponent<IntercomPerformanceCapture>().saveWhenStopped,"diagnostic capture cannot overwrite normal stop capture");
   EditorApplication.Step();Check(mission.Phase==IntercomMissionPhase.Active,"warm pool starts production mission route");
   Check(mission.playerMotor.GetComponent<IntercomSimpleShooter>().enabled,"successful StartMission releases the owned Selection lock and restores its cached enabled state");
   Private("StopFromMenu");Check(!progression.IsDiagnosticSession,"manual stop restores real progression profile");
   Check(ReferenceEquals(original,mission.eliminationDefinition),"manual stop restores authored mission reference");
   Check(!mission.playerHealth.invulnerable&&!mission.playerMotor.GetComponent<IntercomWeaponPose>().Held,"manual stop restores invulnerability and releases fire");
   Check(!mission.playerMotor.GetComponent<IntercomSimpleShooter>().enabled,"cancellation leaves final shooter lock owned by Selection");
   Check(mission.playerMotor.GetComponent<IntercomPerformanceCapture>().saveWhenStopped&&!Application.runInBackground,"manual stop restores recording/background settings");
   using(var r=Report())Check(r.RootElement.GetProperty("status").GetString()=="CANCELLED","manual stop cannot report PASS");
   ExitPlay();Check(Math.Abs(Time.timeScale-.65f)<.0001,"previous editor time scale restored after exit");
   CleanScene();int begins=progression.Begins;IntercomNativeVerification.RunFromMenu();
   Application.EmitLog("Awake failed before runner",LogType.Exception);EditorApplication.Raise(PlayModeStateChange.EnteredPlayMode);
   using(var r=Report())Check(r.RootElement.GetProperty("status").GetString()=="FAIL"&&r.RootElement.GetProperty("failures")[0].GetString().Contains("Awake failed before runner"),"Awake errors before EnteredPlayMode cannot be missed");
   Check(progression.Begins==begins,"early engine error aborts before sandbox and gameplay mutations");ExitPlay();
   CleanScene();IntercomNativeVerification.RunFromMenu();EditorApplication.Raise(PlayModeStateChange.EnteredPlayMode);Private("StopFromMenu");
   Application.EmitLog("late OnDisable failure",LogType.Exception);
   using(var r=Report())Check(r.RootElement.GetProperty("status").GetString()=="FAIL","late native shutdown exception rewrites completed report to FAIL");ExitPlay();
   CleanScene();IntercomNativeVerification.RunFromMenu();EditorApplication.Raise(PlayModeStateChange.EnteredPlayMode);EditorApplication.Step();
   Application.EmitLog("native failure probe",LogType.Exception);EditorApplication.Step();
   using(var r=Report())Check(r.RootElement.GetProperty("status").GetString()=="FAIL"&&r.RootElement.GetProperty("failures").GetArrayLength()>0,"runtime exceptions fail run and retain evidence");
   Check(!progression.IsDiagnosticSession,"runtime exception cleans sandbox");ExitPlay();
   CleanScene();IntercomNativeVerification.RunFromMenu();EditorApplication.Raise(PlayModeStateChange.EnteredPlayMode);EditorApplication.Step(151);
   using(var r=Report())Check(r.RootElement.GetProperty("status").GetString()=="FAIL","hard wall-clock timeout cannot report PASS");
   Check(!progression.IsDiagnosticSession,"timeout cleans temporary profile");ExitPlay();
   CleanScene();IntercomNativeVerification.RunFromMenu();EditorApplication.Raise(PlayModeStateChange.EnteredPlayMode);AssemblyReloadEvents.Raise();
   Check(SessionState.GetBool("CareProject.NativeVerification.abortAfterReload",false),"assembly reload persists forced-exit guard across domain reload");
   Check(!progression.IsDiagnosticSession,"assembly reload cleans sandbox before reload");
   using(var r=Report())Check(r.RootElement.GetProperty("status").GetString()=="CANCELLED","recompile is cancellation, never PASS");
   ExitPlay();Check(!SessionState.GetBool("CareProject.NativeVerification.abortAfterReload",true),"edit-mode restoration clears reload guard");
   Check(original.killTarget==50&&original.spawnInterval==1.2f,"all cancellation paths preserve authored profile");
   CleanScene();var untitled=SceneManager.Scenes[0];untitled.path="";SceneManager.Scenes[0]=untitled;SceneManager.Active=untitled;
   IntercomNativeVerification.RunFromCommandLine();Check(EditorApplication.isPlaying,"fresh batch pristine default scene is allowed");
   EditorApplication.Raise(PlayModeStateChange.EnteredPlayMode);Private("StopFromMenu");ExitPlay();Check(EditorApplication.ExitCode==1,"cancelled batch exits nonzero");
   string writable=Application.persistentDataPath;string blocked=Path.Combine(writable,"blocked");File.WriteAllText(blocked,"not a directory");Application.persistentDataPath=blocked;
   CleanScene();IntercomNativeVerification.RunFromMenu();EditorApplication.Raise(PlayModeStateChange.EnteredPlayMode);
   Check(!EditorApplication.isPlaying,"report IO failure cannot leave Play Mode hanging");
   Check(SessionState.GetInt("CareProject.NativeVerification.exitCode",0)==1,"unwritable report path retains nonzero outcome");
   ExitPlay();Application.persistentDataPath=writable;
   CleanScene();IntercomNativeVerification.RunFromMenu();EditorApplication.Raise(PlayModeStateChange.EnteredPlayMode);Application.persistentDataPath=blocked;
   Private("StopFromMenu");Check(!EditorApplication.isPlaying&&!progression.IsDiagnosticSession,"report write failure during Complete still stops Play and restores sandbox");
   Check(SessionState.GetInt("CareProject.NativeVerification.exitCode",0)==1,"completion IO failure cannot return successful code");
   ExitPlay();Application.persistentDataPath=writable;File.Delete(blocked);
   var intentionallyDisabled=Fixture(out var disabledProgression,false);CleanScene();
   IntercomNativeVerification.RunFromMenu();EditorApplication.Raise(PlayModeStateChange.EnteredPlayMode);
   Check(disabledProgression.IsDiagnosticSession,"an intentionally disabled weapon is checked after the mission lease unlock, not during Selection initialization");
   EditorApplication.Step();
   using(var r=Report())Check(r.RootElement.GetProperty("status").GetString()=="FAIL"&&r.RootElement.GetProperty("failures")[0].GetString().Contains("не включила"),"a genuinely disabled active weapon fails with its actual mission-unlock reason");
   Check(!intentionallyDisabled.playerMotor.GetComponent<IntercomSimpleShooter>().enabled&&!disabledProgression.IsDiagnosticSession,"diagnostics never force-enable a designer-disabled weapon or retain sandbox on failure");
   ExitPlay();
   Console.WriteLine($"Native verification harness: {checks} source-compiled safety/lifecycle assertions PASS; precise Unity API substitutes. No native Unity Play Mode/PhysX/Animator/Profiler executed.");
  }
  finally{Directory.Delete(Application.persistentDataPath,true);}
 }
}
