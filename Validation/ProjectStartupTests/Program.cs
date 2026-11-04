using System;
using System.IO;
using IntercomMvp.Editor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
internal static class Program
{
    private static int assertions;
    private sealed class UserComponent:Component { }
    private static void Check(bool c,string label) { assertions++;if(!c)throw new Exception(label); }
    private sealed class Fixture:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"CareProjectStartup_"+Guid.NewGuid().ToString("N"));
        private readonly string previous=Environment.CurrentDirectory;
        internal Fixture(bool marker=true)
        {
            Directory.CreateDirectory(Path.Combine(Root,"Assets"));Directory.CreateDirectory(Path.Combine(Root,"ProjectSettings"));
            Environment.CurrentDirectory=Root;Application.dataPath=Path.Combine(Root,"Assets");Application.isBatchMode=false;
            if(marker)File.WriteAllText(Path.Combine(Root,"ProjectSettings/IntercomMvpProject.json"),"{}");
            EditorApplication.isCompiling=EditorApplication.isUpdating=EditorApplication.isPlayingOrWillChangePlaymode=false;
            EditorApplication.timeSinceStartup=0;EditorApplication.Menus.Clear();Debug.Warnings.Clear();
            SceneManager.sceneCount=1;SceneManager.Active=new Scene{Data=new SceneData()};AssetDatabase.Ready=true;EditorSceneManager.Opens=0;EditorSceneManager.Throw=false;
        }
        public void Dispose() { Environment.CurrentDirectory=previous;Directory.Delete(Root,true); }
        internal void Begin() { IntercomProjectStartup.TrySchedule(); }
        internal void Tick() { EditorApplication.Tick(); }
        internal string Key { get { return IntercomProjectStartup.ProjectPreferenceKey(Application.dataPath); } }
    }
    private static GameObject[] Defaults(bool urp=false)
    {
        var camera=new GameObject("Main Camera",new Camera(),new AudioListener());
        var light=new GameObject("Directional Light",new Light{type=LightType.Directional});
        if(urp) { camera.components.Add(new UniversalAdditionalCameraData());light.components.Add(new UniversalAdditionalLightData()); }
        return new[]{camera,light};
    }
    private static void Main()
    {
        using(var f=new Fixture())
        {
            f.Begin();f.Tick();Check(EditorSceneManager.Opens==1,"empty pristine scene opens authored scene once");
            Check(SceneManager.Active.path==IntercomProjectPaths.Path,"opens canonical scene");Check(EditorPrefs.GetBool(f.Key,false),"persistent project marker");
            Check(EditorApplication.Menus.Count==1&&EditorApplication.Menus[0]=="Tools/CareProject/Start Here","opens developer navigator");
            f.Tick();f.Begin();f.Tick();Check(EditorSceneManager.Opens==1&&EditorApplication.Menus.Count==1,"repeat/domain scheduling does not override scene");
        }
        using(var f=new Fixture()) { SceneManager.Active.Data.Roots=Defaults(true);f.Begin();f.Tick();Check(EditorSceneManager.Opens==1,"standard URP Camera+Light accepted"); }
        using(var f=new Fixture()) { SceneManager.Active.Data.Roots=Defaults();f.Begin();f.Tick();Check(EditorSceneManager.Opens==1,"standard built-in Camera+Light accepted"); }
        using(var f=new Fixture())
        {
            SceneManager.Active.Data.Dirty=true;f.Begin();f.Tick();Check(EditorSceneManager.Opens==0,"dirty scene retained");Check(EditorPrefs.GetBool(f.Key,false),"dirty edit disables future auto override");
        }
        using(var f=new Fixture())
        {
            SceneManager.Active.Data.Path="Assets/MyWork.unity";f.Begin();f.Tick();Check(EditorSceneManager.Opens==0&&EditorApplication.Menus.Count==0,"authored user scene retained without focus stealing");
        }
        using(var f=new Fixture()) { SceneManager.Active.Data.Roots=new[]{new GameObject("Unstored user object")};f.Begin();f.Tick();Check(EditorSceneManager.Opens==0,"untitled custom object retained"); }
        using(var f=new Fixture()) { SceneManager.Active.Data.Roots=Defaults();SceneManager.Active.Data.Roots[0].components.Add(new UserComponent());f.Begin();f.Tick();Check(EditorSceneManager.Opens==0,"custom camera component retained"); }
        using(var f=new Fixture()) { SceneManager.Active.Data.Roots=Defaults();SceneManager.Active.Data.Roots[0].components.Add(null);f.Begin();f.Tick();Check(EditorSceneManager.Opens==0,"missing script retained"); }
        using(var f=new Fixture()) { SceneManager.Active.Data.Roots=Defaults();SceneManager.Active.Data.Roots[1].transform.childCount=1;f.Begin();f.Tick();Check(EditorSceneManager.Opens==0,"child content retained"); }
        using(var f=new Fixture()) { SceneManager.sceneCount=2;f.Begin();f.Tick();Check(EditorSceneManager.Opens==0,"multi-scene workspace retained"); }
        using(var f=new Fixture())
        {
            EditorApplication.isCompiling=true;f.Begin();f.Tick();Check(EditorSceneManager.Opens==0,"compilation deferred");
            EditorApplication.isCompiling=false;EditorApplication.isUpdating=true;f.Tick();Check(EditorSceneManager.Opens==0,"import deferred");
            EditorApplication.isUpdating=false;f.Tick();Check(EditorSceneManager.Opens==1,"opens after compile/import settles");
        }
        using(var f=new Fixture()) { AssetDatabase.Ready=false;f.Begin();f.Tick();Check(EditorSceneManager.Opens==0,"waits for scene importer");AssetDatabase.Ready=true;f.Tick();Check(EditorSceneManager.Opens==1,"eventual scene becomes available"); }
        using(var f=new Fixture()) { SceneManager.sceneCount=0;f.Begin();f.Tick();Check(EditorSceneManager.Opens==0,"waits for initial scene");SceneManager.sceneCount=1;f.Tick();Check(EditorSceneManager.Opens==1,"opens once initial scene ready"); }
        using(var f=new Fixture())
        {
            AssetDatabase.Ready=false;f.Begin();EditorApplication.timeSinceStartup=181;f.Tick();Check(EditorSceneManager.Opens==0&&Debug.Warnings.Count==1,"missing import bounded timeout");
            AssetDatabase.Ready=true;f.Tick();Check(EditorSceneManager.Opens==0&&Debug.Warnings.Count==1,"no endless timeout callback");
        }
        using(var f=new Fixture()) { f.Begin();EditorApplication.isPlayingOrWillChangePlaymode=true;f.Tick();EditorApplication.isPlayingOrWillChangePlaymode=false;f.Tick();Check(EditorSceneManager.Opens==0,"Play cancels scheduled scene change"); }
        using(var f=new Fixture()) { Application.isBatchMode=true;f.Begin();f.Tick();Check(EditorSceneManager.Opens==0,"batch processing never changes scene"); }
        using(var f=new Fixture(false)) { f.Begin();f.Tick();Check(EditorSceneManager.Opens==0,"asset-only import into another project does not auto-open"); }
        using(var f=new Fixture())
        {
            f.Begin();EditorSceneManager.Throw=true;f.Tick();Check(EditorSceneManager.Opens==1&&Debug.Warnings.Count==1,"import error handled without crash");
            f.Tick();Check(EditorSceneManager.Opens==1&&Debug.Warnings.Count==1,"error stops retry loop");
        }
        using(var f=new Fixture())
        {
            string first=f.Key;string same=IntercomProjectStartup.ProjectPreferenceKey(Path.Combine(f.Root,"Assets","..","Assets"));
            Check(first==same,"normalized path gets same persistent key");Check(first!=IntercomProjectStartup.ProjectPreferenceKey(Path.Combine(f.Root,"OtherProject/Assets")),"projects have isolated persistent keys");
        }
        Console.WriteLine("Project startup: "+assertions+" assertions PASS; actual editor startup policy with minimal API doubles, no Unity execution.");
    }
}
