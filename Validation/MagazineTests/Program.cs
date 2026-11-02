using System;
using System.Collections.Generic;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

static class Program
{
    static int assertions,cases;
    static void Check(bool value,string reason){assertions++;if(!value)throw new Exception(reason);}
    static void Near(double actual,double expected,double tolerance,string reason)=>Check(Math.Abs(actual-expected)<=tolerance,reason+" "+actual+" != "+expected);
    static void Case(string name,Action action){action();cases++;Console.WriteLine("PASS "+name);}
    static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
    sealed class Harness
    {
        internal IntercomWeaponDefinition Weapon=new IntercomWeaponDefinition();
        internal IntercomPlayerInput Input=new IntercomPlayerInput();
        internal IntercomPlayerMotor Motor=new IntercomPlayerMotor();
        internal IntercomWeaponMagazine Magazine=new IntercomWeaponMagazine();
        internal int Started,Completed;
        internal Harness()
        {
            Magazine.shooter=new IntercomSimpleShooter{Weapon=Weapon};Magazine.inputSource=Input;
            Magazine.components[typeof(IntercomPlayerMotor)]=Motor;
            Magazine.ReloadStarted+=()=>Started++;Magazine.ReloadCompleted+=()=>Completed++;
            Time.timeScale=1;Call(Magazine,"Awake");
        }
        internal void Frame(float dt){Time.deltaTime=dt;Call(Magazine,"Update");Input.ReloadPressed=false;}
    }
    static void Main()
    {
        Case("finite magazine, successful projectiles only",()=>{
            var h=new Harness();Check(h.Magazine.CurrentAmmo==24&&h.Magazine.CanFire,"fresh full magazine");
            Check(!h.Magazine.CommitVolley(0)&&h.Magazine.CurrentAmmo==24,"failed/blocked volley consumes nothing");
            Check(!h.Magazine.CommitVolley(-1)&&!h.Magazine.CommitVolley(30),"invalid commit rejected");
            Check(h.Magazine.CommitVolley(1)&&h.Magazine.CurrentAmmo==23,"one clear barrel consumes one");
            Check(h.Magazine.CommitVolley(2)&&h.Magazine.CurrentAmmo==21,"two clear barrels consume two");
        });
        Case("automatic reload starts exactly after last complete volley",()=>{
            foreach(float dt in new[]{1f/30,1f/60,1f/120}){
                var h=new Harness();for(int i=0;i<12;i++)Check(h.Magazine.CommitVolley(2),"actual volley");
                Check(h.Magazine.CurrentAmmo==0&&h.Magazine.IsReloading&&!h.Magazine.CanFire&&h.Started==1,"empty auto reload");
                Check(!h.Magazine.CommitVolley(2)&&!h.Magazine.RequestReload(),"no fire/restart while reloading");
                float elapsed=0;while(h.Magazine.IsReloading){Check(h.Magazine.CurrentAmmo==0,"ammo not granted early");h.Frame(dt);elapsed+=dt;}
                Near(elapsed,1.15,dt+.00001,"reload duration");Check(h.Completed==1&&h.Magazine.CurrentAmmo==24&&h.Magazine.CanFire,"once completion fills unlimitedreserve");
                h.Frame(dt);Check(h.Completed==1,"completion not repeated");
            }
        });
        Case("manual partial reload and pause do not refill or advance",()=>{
            var h=new Harness();Check(!h.Magazine.RequestReload(),"full magazine not reloadable");h.Magazine.CommitVolley(2);
            h.Input.ReloadPressed=true;h.Frame(.01f);Check(h.Magazine.IsReloading&&h.Started==1,"manual input beginsreload");
            h.Frame(.2f);float progress=h.Magazine.ReloadProgress;Time.timeScale=0;h.Frame(10f);Near(h.Magazine.ReloadProgress,progress,1e-7,"paused time");
            Time.timeScale=1;Call(h.Magazine,"OnApplicationFocus",false);h.Frame(10f);Near(h.Magazine.ReloadProgress,progress,1e-7,"focuslost time");
            Check(h.Magazine.CurrentAmmo==22,"focus cannot fill magazine");Call(h.Magazine,"OnApplicationFocus",true);
            Call(h.Magazine,"OnApplicationPause",true);h.Frame(10f);Near(h.Magazine.ReloadProgress,progress,1e-7,"app pause");Call(h.Magazine,"OnApplicationPause",false);
            h.Frame(.95f);Check(h.Completed==1&&h.Magazine.CurrentAmmo==24,"remaining duration completes");
        });
        Case("respawn resets ammo but ordinary motion cancellation does not",()=>{
            var h=new Harness();h.Magazine.CommitVolley(2);h.Motor.MotionResetVersion++;h.Frame(.01f);Check(h.Magazine.CurrentAmmo==22,"layout/focus cancellation cannot refill");
            h.Motor.RespawnVersion++;h.Frame(.01f);Check(h.Magazine.CurrentAmmo==24&&!h.Magazine.IsReloading,"respawn resets");
            h.Magazine.CommitVolley(2);h.Magazine.RequestReload();Call(h.Magazine,"OnDisable");Check(h.Magazine.CurrentAmmo==24&&!h.Magazine.IsReloading,"disable clears stale reload");
        });
        Case("reload pointer never becomes fire or steals movement",()=>{
            var ui=new IntercomReloadControl();ui.Configure(830,200,140,54);
            var sticks=new IntercomTwinStickState();sticks.Configure(100,100,900,100,70,.12f,.2f);
            sticks.BeginFrame();sticks.PointerDown(1,150,100);Check(ui.PointerDown(2,900,220),"reload touchcaptured");sticks.EndFrame();
            Check(sticks.Left.Active&&!sticks.Right.Active&&ui.Owns(2),"leftmove remains, nofire");
            sticks.BeginFrame();sticks.PointerMove(1,150,100);if(!ui.Owns(2))sticks.PointerMove(2,970,100);sticks.EndFrame();
            Check(sticks.Left.Active&&!sticks.Right.Active,"dragging reloadfinger into rightstick cannot shoot");
            ui.PointerUp(2);Check(!ui.Owns(2),"release");Check(!ui.PointerDown(2,970,100),"fresh reused ID outsideUI notcaptured");
            Check(ui.PointerDown(int.MinValue,900,220)&&ui.Owns(int.MinValue),"mouseUIcapture");ui.Reset();Check(!ui.Owns(int.MinValue),"cancellation releasesUI");
        });
        Case("parallel dual barrels never cross at near or far ranges",()=>{
            var r=new Vector3(-.1f,0,1);var l=new Vector3(.1f,0,1);var ro=new Vector3(.12f,1,0);var lo=new Vector3(-.12f,1,0);
            Check((ro+r.normalized*3).x<(lo+l.normalized*3).x,"fixture must reproduce oldX aftercrossing");
            var d=IntercomProjectileDirection.Parallel(r,l,Vector3.forward,true);
            foreach(float distance in new[]{0f,.1f,1f,3f,10f,20f})Near(((ro+d*distance)-(lo+d*distance)).x,.24,1e-6,"lateral separation staysconstant");
            Near(d.y,0,1e-7,"planar");Near(d.sqrMagnitude,1,1e-6,"normalized");
            for(int degrees=0;degrees<360;degrees++){
                double angle=degrees*Math.PI/180;var a=new Vector3((float)Math.Sin(angle-.12),.04f,(float)Math.Cos(angle-.12));var b=new Vector3((float)Math.Sin(angle+.12),-.03f,(float)Math.Cos(angle+.12));
                var heading=IntercomProjectileDirection.Parallel(a,b,Vector3.forward,true);
                Near(heading.x,Math.Sin(angle),1e-5,"shared heading X");Near(heading.z,Math.Cos(angle),1e-5,"shared heading Z");
            }
            Near(IntercomProjectileDirection.Parallel(new Vector3(),new Vector3(),new Vector3(1,0,0),true).x,1,1e-6,"missing marker fallback");
        });
        Case("held poseclock and magazine agree with reload-aware TTK at mobile rates",()=>{
            foreach(float dt in new[]{1f/30,1f/60,1f/120}){
                var h=new Harness();var clock=new IntercomWeaponPoseClock(8f/24,.5f,h.Weapon.Interval,2f);var shots=new List<double>();double time=0;float hp=1000;
                for(int frame=0;frame<2000&&hp>0;frame++){
                    time+=dt;h.Frame(dt);bool fire=h.Magazine.CanFire;if(!fire)clock.CancelFireRequests();clock.Tick(dt,fire,h.Magazine.IsReloading);
                    if(clock.ShotThisTick){Check(h.Magazine.CanFire,"no shotduringreload");Check(h.Magazine.CommitVolley(2),"actual shotcommitted");shots.Add(time);hp-=30;}
                }
                Check(shots.Count==34&&h.Started==2&&h.Completed==2,"34 volleys / two reloads for1000HP");
                Near(shots[12]-shots[11],1.15,dt+.0001,"reload from lastactualshot, no extra raise");
                Near(shots[24]-shots[23],1.15,dt+.0001,"secondreload consistent");
                Near(shots[33]-shots[0],IntercomWeaponBalance.IdealTtkWithReload(1000,15,2,.18f,24,1.15f),dt*3+.0001,"live clockTTK includes reloads");
            }
            var w=new IntercomWeaponDefinition();Near(w.IdealTtk,.54,1e-6,"100HP unchanged");Near(w.SustainedDps,360/(11*.18+1.15),1e-4,"sustained DPS");
        });
        Case("long custom reload keeps raised baseline without replaying intent",()=>{
            var h=new Harness();h.Weapon.reloadSeconds=3f;var clock=new IntercomWeaponPoseClock(.01f,.5f,.18f,2f);
            clock.Tick(.01f,true);clock.Tick(.01f,true);Check(clock.ShotThisTick,"raisedfirstshot");h.Magazine.CommitVolley(2);h.Magazine.RequestReload();
            for(int i=0;i<299;i++){h.Frame(.01f);clock.CancelFireRequests();clock.Tick(.01f,false,h.Magazine.IsReloading);Check(!clock.ShotThisTick&&clock.Posture==IntercomWeaponPosture.Combat,"longreload lowered orfired");}
            h.Frame(.01f);clock.Tick(.01f,true,h.Magazine.IsReloading);Check(clock.ShotThisTick&&h.Magazine.CanFire,"completion permitsnextshotwithout extra armraise");
        });
        Case("invalid time and tiny custom magazines remain safe",()=>{
            var s=new IntercomMagazineState(0);Check(s.Capacity==1,"capacity sanitized");s.TryConsume(1);s.RequestReload(float.NaN);
            foreach(float bad in new[]{0,-1,float.NaN,float.PositiveInfinity})Check(!s.Tick(bad)&&s.CurrentAmmo==0,"invalidtime cannot reload");
            Check(s.Tick(1.15f)&&s.CurrentAmmo==1,"safe fallback duration");
            var w=new IntercomWeaponDefinition{magazineSize=1};Check(w.MagazineCapacity==2,"dualguncapacity supports one volley");
        });
        Console.WriteLine("PASS "+cases+" magazine/parallel-fire cases, "+assertions+" assertions. Production state, component, pose clock and direction; narrow Unity stand-ins, no PhysX or actual GUI execution.");
    }
}
