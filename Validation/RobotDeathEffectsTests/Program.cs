using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
using IntercomMvp;
using Object = UnityEngine.Object;

static class Program
{
    static int assertions;
    static void Check(bool ok,string label){assertions++;if(!ok)throw new Exception(label);}
    static void Call(object o,string method,params object[] args)
    {try{o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(o);
    static GameObject Template()
    {
        var root=new GameObject("Static parts template");
        for(int i=0;i<6;i++)
        {
            var part=new GameObject("Part "+i);part.transform.SetParent(root.transform,false);
            part.transform.localPosition=new Vector3((i%3-1)*.25f,.3f+(i/3)*.5f,.04f*i);
            var mesh=new Mesh {vertices=new[]{new Vector3(-.1f,-.12f,-.08f),new Vector3(.1f,.12f,.08f),new Vector3(.1f,-.12f,.08f)},triangles=new[]{0,1,2}};mesh.RecalculateBounds();
            part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=new Material();
        }
        return root;
    }
    static IntercomRobotDeathEffects Fresh(bool material=true,GameObject template=null)
    {
        Application.isFocused=true;Time.timeScale=1;Time.deltaTime=1f/60;
        var root=new GameObject("FX fixture");root.SetActive(false);
        var fx=root.AddComponent<IntercomRobotDeathEffects>();
        if(material){fx.gearMaterial=new Material();fx.outlineMaterial=new Material();fx.flashMaterial=new Material();}
        fx.breakupTemplate=template;root.SetActive(true);return fx;
    }
    static void Frames(IntercomRobotDeathEffects fx,int n){for(int i=0;i<n;i++)Call(fx,"Update");}
    static void PoolsAndRecycling()
    {
        var template=Template();var fx=Fresh(template:template);Check(fx.PreparedBursts==1,"Awake prepares one usable native burst before the first kill");
        Frames(fx,12);Check(fx.PreparedBursts==8&&fx.PreparedChunkBursts==2,"menu frames prepare bounded pools");
        int objects=GameObject.Created.Count,meshes=Mesh.Created,materials=Material.Created;
        for(uint i=0;i<200;i++)
        {
            Check(fx.Emit(new Vector3(i*.01f,0,0),Quaternion.Euler(0,i*7,0),i+1),"warm emission always available");
            Check(fx.ActivePieces<=52,"piece count remains within40generic+12real chunks");
            Call(fx,"Update");
        }
        Check(GameObject.Created.Count==objects&&Mesh.Created==meshes&&Material.Created==materials,"200 kills create zero new GameObjects meshes or materials");
        Check(fx.TotalBursts==200,"each accepted event emits once");
        Frames(fx,150);Check(fx.ActivePieces==0,"all chunks and gears expire and return to their warm slots");
        Check(!GameObject.Created.Any(g=>g.activeInHierarchy&&g.name.StartsWith("Mechanical burst ")),"no persistent burst roots remain active");
        fx.Clear();fx.Clear();Check(fx.ActivePieces==0,"Clear is idempotent");
        var shared=template.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToArray();
        int destroyedMeshes=Object.Destroyed.OfType<Mesh>().Count();Call(fx,"OnDestroy");
        Check(Object.Destroyed.OfType<Mesh>().Count()-destroyedMeshes==6,"owned procedural meshes are destroyed exactly once");
        Check(shared.All(m=>!Object.Destroyed.Contains(m)),"authored mesh references are never destroyed with the pool");
        Check(!Object.Destroyed.Contains(fx.gearMaterial)&&!Object.Destroyed.Contains(fx.outlineMaterial),"shared asset materials remain owned by assets");
    }
    static void PauseFocusAndInvalidInputs()
    {
        var fx=Fresh();Frames(fx,10);fx.Emit(Vector3.zero,Quaternion.identity,21);
        object burst=((Array)Field(fx,"bursts")).GetValue(0);float age=(float)Field(burst,"age");
        Time.timeScale=0;Time.deltaTime=20;Frames(fx,50);
        Check((float)Field(burst,"age")==age&&fx.ActivePieces==5,"pause consumes no effect time or queued motion");
        Check(!fx.Emit(Vector3.zero,Quaternion.identity,22),"paused cosmetic emission is rejected");
        Time.timeScale=1;Call(fx,"Update");Check(fx.ActivePieces==0,"long frame expires old debris promptly instead of extending its lifetime");
        Time.deltaTime=.2f;fx.Emit(Vector3.zero,Quaternion.identity,27);
        object piece=((Array)Field(((Array)Field(fx,"bursts")).GetValue(1),"gears")).GetValue(0);
        Vector3 start=(Vector3)Field(piece,"position"),velocity=(Vector3)Field(piece,"velocity");
        Call(fx,"Update");
        Vector3 end=(Vector3)Field(piece,"position");
        Check(Math.Abs((float)Field(piece,"age")-.2f)<.00001f,"active debris age consumes the full finite scaled frame");
        Check(Math.Abs(end.x-start.x-velocity.x*.05f)<.00001f&&Math.Abs(end.z-start.z-velocity.z*.05f)<.00001f,"hitch motion integrates one capped step without catchup bounces");
        Call(fx,"OnApplicationFocus",false);Check(fx.ActivePieces==0&&!fx.Emit(Vector3.zero,Quaternion.identity,23),"focus loss clears stale cosmetic objects");
        Call(fx,"OnApplicationFocus",true);Time.deltaTime=1f/60;Check(fx.Emit(Vector3.zero,Quaternion.identity,24),"focus return reuses the same pool");
        Call(fx,"OnApplicationPause",true);Check(fx.ActivePieces==0,"application pause clears presentation");Call(fx,"OnApplicationPause",false);
        int count=fx.TotalBursts;Check(!fx.Emit(new Vector3(float.NaN,0,0),Quaternion.identity,25)&&fx.TotalBursts==count,"nonfinite position cannot poison pool transforms");
        fx.enabled=false;Check(fx.ActivePieces==0&&!fx.Emit(Vector3.zero,Quaternion.identity,26),"disable clears effect and refuses later callbacks");
    }
    static void DefinitionBindingAndFloor()
    {
        var template=Template();var root=new GameObject();root.SetActive(false);var fx=root.AddComponent<IntercomRobotDeathEffects>();
        fx.gearMaterial=new Material();fx.outlineMaterial=new Material();fx.flashMaterial=new Material();
        fx.definition=new IntercomNpcDefinition{breakupPrefab=template};root.SetActive(true);Frames(fx,12);
        Check(fx.breakupTemplate==template&&fx.PreparedChunkBursts==2,"definition-owned static pieces bind before native pool construction");
        for(uint i=1;i<=3;i++)fx.Emit(new Vector3(2,.18f,-1),Quaternion.Euler(0,73,0),i);
        Check(fx.ActivePieces==21,"third mechanical pop includes six actual parts plus its generic gear bursts");
        for(int frame=0;frame<70;frame++)
        {
            Call(fx,"Update");
            foreach(object b in (Array)Field(fx,"bursts"))if(b!=null)Floor((Array)Field(b,"gears"));
            foreach(object set in (Array)Field(fx,"chunks"))if(set!=null)Floor((Array)Field(set,"pieces"));
        }
        fx.Clear();Check(fx.ActivePieces==0,"mission reset drops all existing generic and real-part instances");
        Check(fx.Emit(Vector3.zero,Quaternion.identity,9),"retry can reuse cleared slots immediately");
    }
    static void Floor(Array pieces)
    {
        foreach(object p in pieces)
        {
            if(!(bool)Field(p,"active"))continue;
            var t=(Transform)Field(p,"transform");var center=(Vector3)Field(p,"center");var e=(Vector3)Field(p,"extents");var s=t.localScale;var q=t.rotation;
            var x=q*new Vector3(1,0,0);var y=q*Vector3.up;var z=q*Vector3.forward;
            float extent=Math.Abs(x.y*e.x*s.x)+Math.Abs(y.y*e.y*s.y)+Math.Abs(z.y*e.z*s.z);
            float bottom=t.position.y+(q*new Vector3(center.x*s.x,center.y*s.y,center.z*s.z)).y-extent;
            Check(bottom>=(float)Field(p,"floor")-.00001f,"rotating bounds never tunnel below authored floor");
        }
    }
    static void MissingAppearance()
    {
        var fx=Fresh(false);int objects=GameObject.Created.Count,meshes=Mesh.Created;
        Check(fx.PreparedBursts==0&&!fx.Emit(Vector3.zero,Quaternion.identity,1),"optional unconfigured effect preserves actor fallback");
        Check(objects==GameObject.Created.Count&&meshes==Mesh.Created,"Emit never lazily creates native fallback resources");
        fx.gearMaterial=new Material();fx.outlineMaterial=new Material();fx.flashMaterial=new Material();Frames(fx,1);
        Check(fx.PreparedBursts==1&&fx.Emit(Vector3.zero,Quaternion.identity,2),"explicit later setup prepares during Update, then reuses it");
        Check(!GameObject.Created.Any(g=>g.components.Any(c=>c is Collider)),"scrap presentation creates no colliders or physics actors");
    }
    static void Main()
    {
        PoolsAndRecycling();PauseFocusAndInvalidInputs();DefinitionBindingAndFloor();MissingAppearance();
        Console.WriteLine("Robot death effects: "+assertions+" assertions PASS; production pool/ballistics/lifecycle with deterministic Unity stand-ins, not Unity rendering or native execution.");
    }
}
