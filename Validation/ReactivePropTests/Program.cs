using System;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

static class Program
{
    private static int assertions;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int CustomProperty = 731;
    static void Check(bool value, string message) {assertions++;if(!value)throw new Exception(message);}
    static bool Near(float a,float b,float epsilon=.0001f)=>Math.Abs(a-b)<epsilon;
    static bool Same(Vector3 a,Vector3 b)=>Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.z,b.z);
    static bool Same(Quaternion a,Quaternion b)=>Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.z,b.z)&&Near(a.w,b.w);
    static bool Same(Color a,Color b)=>Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);
    static void Call(object target,string name)
    {
        try {target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);}
        catch(TargetInvocationException exception) {throw exception.InnerException;}
    }
    static IntercomDamage Damage(float amount)=>new IntercomDamage(amount,Vector3.zero,Vector3.forward,null);
    static void Advance(IntercomReactiveProp prop,float seconds,int fps=60)
    {
        Time.deltaTime=1f/fps;
        for(int i=0;i<(int)Math.Ceiling(seconds*fps);i++)Call(prop,"Update");
    }
    sealed class Fixture
    {
        public IntercomReactiveProp Prop;
        public Transform Visual,Flap,ColliderTransform,Debris;
        public Collider Collider,DisabledCollider;
        public Renderer Lamp;
        public IntercomPlayerMotor Motor;
        public Vector3 Position,Scale;
        public Quaternion Rotation,FlapRotation;
        public Color OriginalColor,OriginalCustom;
        public Fixture(IntercomPropKind kind,bool fallbackCollider=false,bool withDebris=false)
        {
            Time.timeScale=1;Time.deltaTime=1f/60;
            var owner=new GameObject();ColliderTransform=owner.transform;
            Collider=owner.AddComponent<Collider>();DisabledCollider=owner.AddComponent<Collider>();DisabledCollider.enabled=false;
            Visual=new GameObject().transform;Visual.parent=new GameObject().transform;
            Visual.localPosition=Position=new Vector3(1,.03f,2);
            Visual.localScale=Scale=new Vector3(.8f,.9f,.75f);
            Visual.localRotation=Rotation=Quaternion.Euler(0,25,0);
            Flap=new GameObject().transform;Flap.localRotation=FlapRotation=Quaternion.Euler(0,10,0);
            Lamp=new GameObject().AddComponent<Renderer>();Lamp.sharedMaterial=new Material();
            OriginalColor=new Color(.4f,.7f,.3f,1);OriginalCustom=new Color(.1f,.2f,.3f,.4f);
            Lamp.sharedMaterial.Colors[BaseColor]=OriginalColor;
            Lamp.Colors[CustomProperty]=OriginalCustom;
            Motor=new GameObject().AddComponent<IntercomPlayerMotor>();
            Prop=owner.AddComponent<IntercomReactiveProp>();Prop.kind=kind;Prop.visual=Visual;
            if(withDebris)
            {
                Debris=new GameObject().transform;Debris.parent=Visual.parent;Debris.gameObject.SetActive(false);Prop.spentVisual=Debris;
            }
            Prop.flap=kind==IntercomPropKind.Relay?Flap:null;Prop.indicator=kind==IntercomPropKind.Relay?Lamp:null;
            Prop.blockingColliders=fallbackCollider?null:new[]{Collider,DisabledCollider};Prop.resetMotor=Motor;
            Prop.maxHealth=kind==IntercomPropKind.Relay?15:kind==IntercomPropKind.Bollard?30:45;
            Prop.recoverSeconds=4;Call(Prop,"Awake");
        }
        public void CheckRest(string name)
        {
            Check(Prop.Phase==IntercomPropPhase.Ready&&Near(Prop.Health,Prop.maxHealth),name+" restores health");
            Check(Collider.enabled&&!DisabledCollider.enabled,name+" restores authored collider states");
            Check(Same(Visual.localPosition,Position)&&Same(Visual.localScale,Scale)&&Same(Visual.localRotation,Rotation),name+" restores authored mesh transform");
            if(Debris!=null)Check(Visual.gameObject.activeSelf&&!Debris.gameObject.activeSelf,name+" restores original visual visibility");
            if(Prop.kind==IntercomPropKind.Relay)
            {
                Check(Same(Flap.localRotation,FlapRotation),name+" restores hinge");
                Check(Lamp.Colors.Count==1&&Same(Lamp.Colors[CustomProperty],OriginalCustom),name+" restores original property block");
            }
        }
    }
    static void PureState()
    {
        foreach(var kind in new[]{IntercomPropKind.Crate,IntercomPropKind.Bollard,IntercomPropKind.Relay})
        {
            var state=new IntercomPropState();state.Reset(kind,45,4);
            Check(state.Phase==IntercomPropPhase.Ready&&state.BlocksMovement&&state.Health==45,"reset defines initial state");
            foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
                Check(!state.TakeDamage(invalid)&&state.Health==45&&state.Phase==IntercomPropPhase.Ready,"invalid damage is inert");
            Check(state.TakeDamage(15)&&state.Health==30&&state.Phase==IntercomPropPhase.Damaged,"first valid shot damages");
            Check(state.TakeDamage(10000)&&state.Health==0,"overkill clamps health");
            var phase=state.Phase;
            Check(!state.TakeDamage(1)&&state.Phase==phase,"terminal/tripped prop rejects repeated hits");
            Check(state.BlocksMovement==(kind==IntercomPropKind.Relay),"only spent obstacles open passage");
            if(kind!=IntercomPropKind.Relay)Check(!state.Tick(1000)&&state.Phase==phase,"destroyed prop remains spent until reset");
            state.Reset(kind,45,4);Check(state.Health==45&&state.BlocksMovement,"reset restores obstruction");
        }
        foreach(int fps in new[]{30,60,120})
        {
            var relay=new IntercomPropState();relay.Reset(IntercomPropKind.Relay,15,4);relay.TakeDamage(15);
            foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity})Check(!relay.Tick(invalid)&&relay.RecoveryProgress==0,"invalid time cannot repair relay");
            int repairs=0;
            for(int frame=1;frame<=fps*4;frame++)
            {
                if(relay.Tick(1f/fps))repairs++;
                if(frame<fps*4)Check(relay.Phase==IntercomPropPhase.Tripped,"relay remains tripped before scheduled repair");
            }
            Check(repairs==1&&relay.Phase==IntercomPropPhase.Ready&&relay.Health==15,"relay repairs once after four seconds at "+fps+"fps");
            Check(!relay.Tick(1000),"repair does not repeat");
        }
        var invalidConfig=new IntercomPropState();invalidConfig.Reset((IntercomPropKind)999,float.NaN,float.NaN);
        Check(invalidConfig.Kind==IntercomPropKind.Crate&&invalidConfig.Health==45,"invalid config sanitizes to supported defaults");
        invalidConfig.Reset(IntercomPropKind.Relay,-1,-1);invalidConfig.TakeDamage(1);
        Check(!invalidConfig.Tick(.2f)&&invalidConfig.Tick(.05f),"negative health and delay clamp safely");
        var longFrame=new IntercomPropState();longFrame.Reset(IntercomPropKind.Relay,15,4);longFrame.TakeDamage(15);
        Check(longFrame.Tick(20)&&longFrame.Phase==IntercomPropPhase.Ready,"long frame finishes one relay cycle");
    }
    static void Runtime()
    {
        var crate=new Fixture(IntercomPropKind.Crate,true);crate.CheckRest("crate initial");
        Check(crate.Prop.TakeDamage(Damage(15)),"crate first hit accepted");Advance(crate.Prop,.1f);
        Check(crate.Prop.Phase==IntercomPropPhase.Damaged&&crate.Collider.enabled,"damaged crate remains solid");
        Check(!Same(crate.Visual.localScale,crate.Scale),"crate reacts visibly to impact");
        Check(crate.Prop.TakeDamage(Damage(30))&&!crate.Collider.enabled,"destroyed crate opens collision immediately");
        Advance(crate.Prop,.3f);Check(crate.Visual.localScale.y<crate.Scale.y*.2f,"crate becomes low rubble");
        Check(Same(crate.ColliderTransform.localPosition,Vector3.zero)&&Same(crate.ColliderTransform.localScale,Vector3.one),"visual damage never animates collider transform");
        Check(!crate.Prop.TakeDamage(Damage(15)),"spent crate rejects hit");
        crate.Motor.RespawnVersion++;Time.timeScale=0;Time.deltaTime=0;Call(crate.Prop,"Update");crate.CheckRest("paused respawn");
        Check(!crate.Prop.TakeDamage(Damage(15)),"paused damage rejected");
        Time.timeScale=1;Check(crate.Prop.TakeDamage(Damage(45)),"crate can be destroyed after reset");Advance(crate.Prop,.3f);
        crate.Prop.enabled=false;Call(crate.Prop,"OnDisable");crate.CheckRest("disable");Check(!crate.Prop.TakeDamage(Damage(15)),"disabled component rejects damage");
        crate.Prop.enabled=true;crate.CheckRest("reenable");Check(crate.Prop.TakeDamage(Damage(15)),"reenabled component accepts damage");
        crate.Prop.resetMotor=new GameObject().AddComponent<IntercomPlayerMotor>();Time.deltaTime=0;Call(crate.Prop,"Update");crate.CheckRest("replacement reset motor");
        var bollard=new Fixture(IntercomPropKind.Bollard);bollard.Prop.TakeDamage(Damage(30));Advance(bollard.Prop,.4f);
        Vector3 leaned=bollard.Visual.localRotation*Vector3.up;
        Check(bollard.Prop.IsSpent&&!bollard.Collider.enabled&&leaned.y<.3f,"bollard folds and clears passage");
        Check(bollard.Visual.localPosition.y>bollard.Position.y,"folded base rests above ground");
        bollard.Prop.ResetProp();bollard.CheckRest("bollard manual reset");
        var relay=new Fixture(IntercomPropKind.Relay);relay.Prop.TakeDamage(Damage(15));Advance(relay.Prop,.3f);
        Check(relay.Prop.IsTripped&&relay.Collider.enabled,"tripped relay keeps housing solid");
        Check(!Same(relay.Flap.localRotation,relay.FlapRotation),"relay visibly opens door");
        Check(Same(relay.Visual.localPosition,relay.Position)&&Same(relay.Visual.localScale,relay.Scale)&&Same(relay.Visual.localRotation,relay.Rotation),"relay housing remains stationary");
        Check(relay.Lamp.Colors[BaseColor].g<relay.OriginalColor.g,"relay lamp visibly dims");
        Check(Same(relay.Lamp.Colors[CustomProperty],relay.OriginalCustom),"unrelated property block data preserved while animating");
        Check(Same(relay.Lamp.sharedMaterial.GetColor(BaseColor),relay.OriginalColor),"shared material not mutated");
        int materials=Material.Created,blocks=MaterialPropertyBlock.Created;
        Time.timeScale=0;Time.deltaTime=0;for(int i=0;i<300;i++)Call(relay.Prop,"Update");
        Check(relay.Prop.IsTripped,"paused Update cannot finish repair");
        Time.timeScale=1;Advance(relay.Prop,4.2f);
        Check(relay.Prop.Phase==IntercomPropPhase.Ready&&relay.Prop.Health==15&&Same(relay.Flap.localRotation,relay.FlapRotation),"relay recovers health and closes door");
        Check(relay.Collider.enabled&&!relay.DisabledCollider.enabled,"relay never changes collision while recovering");
        Check(Material.Created==materials&&MaterialPropertyBlock.Created==blocks,"Update creates no material/property-block instances");
        relay.Prop.TakeDamage(Damage(15));Advance(relay.Prop,.3f);relay.Prop.ResetProp();relay.CheckRest("relay manual reset");
        relay.Prop.TakeDamage(Damage(15));Advance(relay.Prop,.3f);relay.Prop.enabled=false;Call(relay.Prop,"OnDisable");relay.CheckRest("relay disable");
        var noVisual=new GameObject();var solid=noVisual.AddComponent<Collider>();var prop=noVisual.AddComponent<IntercomReactiveProp>();prop.visual=noVisual.transform;
        Call(prop,"Awake");Check(prop.visual==null,"unsafe collider visual reference rejected");Check(prop.TakeDamage(Damage(45))&&!solid.enabled,"missing visual remains logically safe");
        prop.ResetProp();Check(solid.enabled,"missing visual can reset collision");
    }
    static void DebrisLifecycle()
    {
        var crate=new Fixture(IntercomPropKind.Crate,withDebris:true);crate.CheckRest("debris initial");
        for(int cycle=0;cycle<5;cycle++)
        {
            Check(crate.Prop.TakeDamage(Damage(45))&&!crate.Collider.enabled,"broken crate clears collider before debris swap");
            Advance(crate.Prop,.1f);
            Check(crate.Visual.gameObject.activeInHierarchy&&!crate.Debris.gameObject.activeSelf,"last impact shows a brief intact squash");
            Advance(crate.Prop,.2f);
            Check(!crate.Visual.gameObject.activeSelf&&crate.Debris.gameObject.activeInHierarchy,"authored debris replaces intact mesh");
            Check(crate.Prop.gameObject.activeSelf&&crate.Prop.isActiveAndEnabled,"debris swap leaves damage owner running");
            Check(!crate.Collider.enabled&&!crate.DisabledCollider.enabled,"debris swap does not restore an obstruction");
            if(cycle%2==0)
            {
                crate.Motor.RespawnVersion++;Time.timeScale=0;Time.deltaTime=0;Call(crate.Prop,"Update");Time.timeScale=1;
                crate.CheckRest("debris paused respawn cycle "+cycle);
            }
            else
            {
                crate.Prop.enabled=false;Call(crate.Prop,"OnDisable");crate.CheckRest("debris disable cycle "+cycle);
                crate.Prop.enabled=true;crate.CheckRest("debris reenable cycle "+cycle);
            }
        }
        crate.Prop.TakeDamage(Damage(45));Advance(crate.Prop,.3f);crate.Prop.ResetProp();crate.CheckRest("debris explicit reset");
        foreach(bool ancestor in new[]{false,true})
        {
            var owner=new GameObject();var root=ancestor?new GameObject():owner;
            if(ancestor)owner.transform.parent=root.transform;
            var solid=owner.AddComponent<Collider>();var unsafeProp=owner.AddComponent<IntercomReactiveProp>();
            unsafeProp.visual=root.transform;unsafeProp.spentVisual=root.transform;
            Call(unsafeProp,"Awake");
            Check(unsafeProp.visual==null&&unsafeProp.spentVisual==null,"owner/ancestor visual refs are rejected");
            unsafeProp.TakeDamage(Damage(45));Advance(unsafeProp,.4f);
            Check(root.activeSelf&&owner.activeInHierarchy&&unsafeProp.isActiveAndEnabled,"unsafe refs never disable receiver hierarchy");
            unsafeProp.ResetProp();Check(solid.enabled,"unsafe-ref prop can still restore solid state");
        }
        var same=new GameObject();var duplicate=same.AddComponent<IntercomReactiveProp>();var mesh=new GameObject().transform;
        duplicate.visual=mesh;duplicate.spentVisual=mesh;Call(duplicate,"Awake");
        Check(duplicate.spentVisual==null,"same object cannot represent intact and debris variants");
        foreach(bool debrisIsChild in new[]{false,true})
        {
            var owner=new GameObject();var prop=owner.AddComponent<IntercomReactiveProp>();
            var intact=new GameObject().transform;var debris=new GameObject().transform;
            if(debrisIsChild)debris.parent=intact;else intact.parent=debris;
            prop.visual=intact;prop.spentVisual=debris;Call(prop,"Awake");
            Check(prop.visual==intact&&prop.spentVisual==null,"nested intact/debris variants are rejected before swapping");
            prop.TakeDamage(Damage(45));Advance(prop,.4f);
            Check(intact.gameObject.activeInHierarchy&&debris.gameObject.activeInHierarchy,"rejected nested refs preserve hierarchy visibility");
        }
    }
    static void BollardClearance()
    {
        var bumper=new Fixture(IntercomPropKind.Bollard);bumper.Prop.foldLift=.46f;
        bumper.Prop.TakeDamage(Damage(15));Advance(bumper.Prop,.3f);
        Check(bumper.Prop.Phase==IntercomPropPhase.Damaged&&bumper.Collider.enabled,"bent bumper remains a live obstacle");
        Check(bumper.Visual.localPosition.y>bumper.Position.y,"bent pose lifts base before final fold");
        bumper.Prop.TakeDamage(Damage(15));Advance(bumper.Prop,.4f);
        Check(Near(bumper.Visual.localPosition.y-bumper.Position.y,.46f*Math.Abs(bumper.Scale.y)),"completed fold uses authored .46 lift with mesh scale");
        Check(!bumper.Collider.enabled,"folded bumper clears collision");
        bumper.Prop.ResetProp();bumper.CheckRest("lifted bumper reset");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            var safe=new Fixture(IntercomPropKind.Bollard);safe.Prop.foldLift=invalid;safe.Prop.foldAngle=invalid;
            safe.Prop.TakeDamage(Damage(30));Advance(safe.Prop,.4f);
            Check(Near(safe.Visual.localPosition.y-safe.Position.y,.14f*Math.Abs(safe.Scale.y)),"invalid lift falls back to finite authored default");
            var q=safe.Visual.localRotation;
            Check(float.IsFinite(q.x)&&float.IsFinite(q.y)&&float.IsFinite(q.z)&&float.IsFinite(q.w),"invalid fold angle yields finite pose");
            safe.Prop.ResetProp();safe.CheckRest("invalid-lift reset");
        }
    }
    static void Main() {PureState();Runtime();DebrisLifecycle();BollardClearance();Console.WriteLine("Reactive props: "+assertions+" assertions passed. Production state + component lifecycle with narrow Unity stand-ins; not a Unity Editor test.");}
}
