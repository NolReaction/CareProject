using System;
using System.Linq;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    private static bool Near(float a,float b)=>Math.Abs(a-b)<.00001f;
    private static void Call(object owner,string method,params object[] args)
    {try{owner.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    private static object Field(object owner,string field)=>owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(owner);
    private static object[] Pool(IntercomCombatEffects e,string name)=>((Array)Field(e,name)).Cast<object>().ToArray();
    private static object[] Active(IntercomCombatEffects e,string name,int kind=-1)=>Pool(e,name).Where(p=>(float)Field(p,"remaining")>0&&(kind<0||(byte)Field(p,"kind")==kind)).ToArray();
    private sealed class Fixture
    {
        internal readonly GameObject Owner=new GameObject("hero");
        internal readonly IntercomWeaponDefinition Weapon=new IntercomWeaponDefinition();
        internal readonly IntercomSimpleShooter Shooter;
        internal readonly IntercomCombatEffects Effects;
        internal readonly Transform Left=new GameObject("left muzzle").transform,Right=new GameObject("right muzzle").transform;
        internal Fixture()
        {
            Time.timeScale=1;Time.deltaTime=.001f;Time.frameCount++;
            Shooter=Owner.AddComponent<IntercomSimpleShooter>();Shooter.Weapon=Weapon;Shooter.ownerRoot=Owner.transform;
            Left.SetParent(Owner.transform,false);Right.SetParent(Owner.transform,false);
            var flash=new Material();var shell=new Material();var impact=new Material();
            var wood=new Material();var stone=new Material();var soil=new Material();
            GameObject.BeforeAwake=c=>{if(c is IntercomCombatEffects e){e.shooter=Shooter;e.flashMaterial=flash;e.casingMaterial=shell;e.impactMaterial=impact;e.woodImpactMaterial=wood;e.stoneImpactMaterial=stone;e.soilImpactMaterial=soil;e.shellPoolSize=16;e.sparkPoolSize=16;}};
            Effects=Owner.AddComponent<IntercomCombatEffects>();GameObject.BeforeAwake=null;
        }
        internal void Tick(float dt=.001f){Time.deltaTime=dt;Time.frameCount++;Call(Effects,"LateUpdate");}
        internal void Shot(bool right){Shooter.Shot(right?Right:Left,right);Tick();}
        internal void Hit(IntercomSurfaceKind kind,bool robot=false)
        {
            var surface=new GameObject("surface");var collider=surface.AddComponent<Collider>();var tag=surface.AddComponent<IntercomFootstepSurface>();tag.kind=kind;tag.impactKind=robot?IntercomImpactKind.Robot:IntercomImpactKind.Surface;
            Shooter.Hit(IntercomProjectileImpact.Resolve(collider,Vector3.zero,Vector3.up,true));Tick();
        }
        internal void Clear()=>Call(Effects,"Clear");
    }
    private static void CasingCadence()
    {
        var f=new Fixture();
        Check(f.Weapon.casingEveryNthShot==2&&f.Weapon.CasingCadence==2,"default reduces cosmetic ejections to half per hand");
        f.Shot(false);f.Shot(true);
        Check(f.Effects.ActiveShellCount==0&&Active(f.Effects,"sparks",1).Length==2,"every shot flashes while first shot of each hand does not eject");
        f.Shot(false);Check(f.Effects.ActiveShellCount==1,"left hand ejects on second shot");
        f.Shot(true);Check(f.Effects.ActiveShellCount==2,"right hand also ejects on second shot");
        var shells=Active(f.Effects,"shells");
        Check(shells.Any(p=>((Vector3)Field(p,"velocity")).x<0)&&shells.Any(p=>((Vector3)Field(p,"velocity")).x>0),"reduced casing schedule preserves both hands, not only one");
        var size=(Vector3)Field(shells[0],"size");Check(Near(size.x,.05625f)&&Near(size.y,.125f)&&Near(size.z,.05625f),"default casing is 25 percent larger");
        f.Clear();f.Shot(false);Check(f.Effects.ActiveShellCount==0,"clear resets cadence rather than reusing prior hand phase");
        f.Weapon.casingEveryNthShot=1;f.Shot(false);Check(f.Effects.ActiveShellCount==1,"every shot cadence can be configured");
        f.Clear();f.Weapon.ejectCasings=false;f.Shot(false);f.Shot(true);Check(f.Effects.ActiveShellCount==0&&Active(f.Effects,"sparks",1).Length==2,"casing disable leaves flash emission active");
        f.Weapon.ejectCasings=true;f.Weapon.casingEveryNthShot=0;Check(f.Weapon.CasingCadence==1,"invalid low cadence clamped");
        f.Weapon.casingEveryNthShot=int.MaxValue;Check(f.Weapon.CasingCadence==8,"cadence has bounded upper limit");
        f.Weapon.casingScale=new Vector3(float.NaN,float.PositiveInfinity,-1);f.Weapon.casingVelocityScale=float.NaN;f.Weapon.casingEveryNthShot=1;f.Clear();f.Shot(false);
        var safe=Active(f.Effects,"shells").Single();size=(Vector3)Field(safe,"size");var velocity=(Vector3)Field(safe,"velocity");
        Check(Near(size.x,.05625f)&&Near(size.y,.125f)&&Near(size.z,.005f)&&float.IsFinite(velocity.x)&&float.IsFinite(velocity.y),"invalid cosmetic settings produce finite bounded casing");
    }
    private static void PoolLifecycle()
    {
        var f=new Fixture();int objects=GameObject.Created.Count,materials=Material.Created,meshes=Mesh.Created;
        for(int i=0;i<1000;i++)f.Shot((i&1)==1);
        Check(f.Effects.ActiveShellCount==16&&f.Effects.ActiveEffectCount<=16,"heavy burst reuses fixed-size shell/effect pools");
        Check(GameObject.Created.Count==objects&&Material.Created==materials&&Mesh.Created==meshes,"emitting does not instantiate objects, materials or meshes");
        f.Tick(4);Check(f.Effects.ActiveShellCount==0&&f.Effects.ActiveEffectCount==0,"long frame expires every pooled cosmetic safely");
        f.Weapon.casingEveryNthShot=1;f.Shot(false);Call(f.Effects,"OnApplicationFocus",false);
        Check(f.Effects.ActiveShellCount==0&&f.Effects.ActiveEffectCount==0,"focus loss clears effects immediately");
        f.Shot(false);Check(f.Effects.ActiveShellCount==0&&f.Effects.ActiveEffectCount==0,"unfocused shots cannot emit");
        Call(f.Effects,"OnApplicationFocus",true);f.Shot(false);Check(f.Effects.ActiveShellCount==1,"focus regain permits fresh ejection");
        Call(f.Effects,"OnApplicationPause",true);f.Shot(true);Check(f.Effects.ActiveShellCount==0&&f.Effects.ActiveEffectCount==0,"application pause clears and blocks effects");
        Call(f.Effects,"OnApplicationPause",false);Time.timeScale=0;f.Shot(false);Check(f.Effects.ActiveShellCount==0,"time pause cannot emit shells");Time.timeScale=1;
        f.Shot(true);Call(f.Effects,"OnDisable");f.Shooter.Shot(f.Left,false);f.Tick();Check(f.Effects.ActiveShellCount==0&&f.Effects.ActiveEffectCount==0,"disable unsubscribes source and clears pools");
        Call(f.Effects,"OnEnable");f.Shot(false);Check(f.Effects.ActiveShellCount==1,"reenable subscribes once");
        int count=f.Effects.ActiveShellCount;f.Tick(float.NaN);Check(f.Effects.ActiveShellCount==count,"invalid delta is inert");
    }
    private static void ComicImpacts()
    {
        var f=new Fixture();
        f.Hit(IntercomSurfaceKind.Metal);Check(Active(f.Effects,"sparks",6).Length==1&&Active(f.Effects,"sparks",2).Length==4,"metal uses one comic pop and four broad glints");
        var glint=Active(f.Effects,"sparks",2).First();var size=(Vector3)Field(glint,"size");Check(size.x>=.08f&&size.y<=.17f,"metal glints are broad chunks instead of thin needles");
        var stamp=Active(f.Effects,"sparks",6).Single();Check((float)Field(stamp,"duration")<=.13f,"comic stamp is brief and readable");
        f.Clear();f.Hit(IntercomSurfaceKind.Wood);Check(Active(f.Effects,"sparks",6).Length==1&&Active(f.Effects,"sparks",3).Length==3,"wood keeps separate chips and comic pop");
        f.Clear();f.Hit(IntercomSurfaceKind.Soil);Check(Active(f.Effects,"sparks",6).Length==0&&Active(f.Effects,"sparks",5).Length==3,"soft ground uses dust rather than hard-surface star");
        f.Clear();f.Hit(IntercomSurfaceKind.Metal,true);Check(Active(f.Effects,"sparks",2).Length==0&&f.Effects.ActiveEffectCount==3,"robot hit uses compact pop and two chassis chips, not scenery metal sparks");
        f.Tick(.3f);Check(f.Effects.ActiveEffectCount==0,"impact pieces expire within short feedback window");
        var scenery=new GameObject("repeat metal");var collider=scenery.AddComponent<Collider>();var tag=scenery.AddComponent<IntercomFootstepSurface>();tag.kind=IntercomSurfaceKind.Metal;
        var hit=IntercomProjectileImpact.Resolve(collider,Vector3.zero,Vector3.up,false);
        int objects=GameObject.Created.Count,materials=Material.Created,meshes=Mesh.Created;
        for(int i=0;i<1000;i++){f.Shooter.Hit(hit);f.Tick(.001f);}
        Check(f.Effects.ActiveEffectCount<=16&&GameObject.Created.Count==objects&&Material.Created==materials&&Mesh.Created==meshes,"sustained material impacts recycle fixed effects without objects/materials/meshes");
    }
    private static void Main(){CasingCadence();PoolLifecycle();ComicImpacts();Console.WriteLine("Combat effects: "+checks+" assertions PASS; actual pooled effects/weapon/impact route with narrow Unity stand-ins, no engine rendering or PhysX.");}
}
