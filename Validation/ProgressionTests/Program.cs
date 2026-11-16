using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

internal static class Program
{
    private const string A="CareProject.Progression.v1.a",B="CareProject.Progression.v1.b";
    private static int checks,cases;
    private static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    private static void Near(double actual,double expected,double tolerance,string why)=>Check(Math.Abs(actual-expected)<=tolerance,why+" "+actual+" != "+expected);
    private static void Case(string name,Action run){run();cases++;Console.WriteLine("PASS "+name);}
    private static void Call(object instance,string method)=>instance.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(instance,null);
    private static T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);
    private static string Id(int i)=>i.ToString("x32");
    private static string Snapshot()=>string.Join(";",PlayerPrefs.Values.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value));
    private static void Reset(){PlayerPrefs.Reset();Time.timeScale=1;Time.unscaledTime=0;Time.deltaTime=0;Application.isFocused=true;}
    private sealed class Harness
    {
        internal readonly IntercomWeaponDefinition Weapon=new IntercomWeaponDefinition{magazineSize=24,referenceTargetHealth=1500,casingMaterial=new Material()};
        internal readonly GameObject Hero=new GameObject("Hero"),Missions=new GameObject("Missions");
        internal readonly IntercomMissionDirector Mission;
        internal readonly IntercomWeaponMagazine Magazine;
        internal IntercomProgression Progression;
        internal readonly IntercomSimpleShooter Shooter;
        internal Harness()
        {
            var motor=Hero.AddComponent<IntercomPlayerMotor>();
            Shooter=Hero.AddComponent<IntercomSimpleShooter>();Shooter.weapon=Weapon;
            Shooter.muzzleLeft=new GameObject("MuzzleL").transform;Shooter.muzzleLeft.SetParent(Hero.transform,false);
            Shooter.muzzleRight=new GameObject("MuzzleR").transform;Shooter.muzzleRight.SetParent(Hero.transform,false);
            Magazine=Hero.AddComponent<IntercomWeaponMagazine>();Magazine.shooter=Shooter;Call(Magazine,"Awake");
            Mission=Missions.AddComponent<IntercomMissionDirector>();Mission.playerMotor=motor;
            RecreateProgression();
        }
        internal void RecreateProgression()
        {
            Progression=Missions.AddComponent<IntercomProgression>();Progression.mission=Mission;
            Progression.magazine=Magazine;Progression.weapon=Weapon;Call(Progression,"Awake");Call(Progression,"Start");
        }
        internal void Finish(string id,int kills=50,bool won=true,IntercomMissionMode mode=IntercomMissionMode.Elimination,int rescued=0)
        {
            Mission.RunRevision++;Mission.ResultRevision++;
            Mission.Phase=won?IntercomMissionPhase.Victory:IntercomMissionPhase.Defeat;
            Mission.LastRunSummary=new IntercomMissionRunSummary(id,Mission.RunRevision,Mission.ResultRevision,mode,Mission.Phase,kills,rescued,5,180);
            Mission.HasRunSummary=true;Call(Progression,"Update");
        }
        internal bool Buy(){Mission.Phase=IntercomMissionPhase.Selection;return Progression.TryBuyMagazineUpgrade();}
    }
    private static void Main()
    {
        Case("actual terminal rewards, no free-play or unfinished credit",()=>
        {
            Reset();var h=new Harness();Check(h.Progression.Wallet==0&&!h.Progression.HasResultReward,"new profile starts empty");
            Call(h.Progression,"Update");Check(PlayerPrefs.Saves==0,"active/menu frames cannot save invented rewards");
            h.Finish(Id(1));Check(h.Progression.Wallet==70,"50 kills plus20 victory");
            var r=h.Progression.ResultReward;Check(r.Kills==50&&r.KillParts==50&&r.VictoryParts==20&&r.RescueParts==0&&r.Total==70,"honest breakdown");
            Check(h.Progression.HasResultReward&&h.Progression.ResultRunRevision==h.Mission.RunRevision,"reward belongs to current run");
            for(int i=0;i<30;i++)Call(h.Progression,"Update");Check(h.Progression.Wallet==70&&PlayerPrefs.Saves==1,"rendered result never pays twice");
            h.Finish(Id(2),17,false);Check(h.Progression.Wallet==87&&!h.Progression.ResultReward.Won&&h.Progression.ResultReward.Total==17,"defeat grants only kills");
            h.Finish(Id(3),10,true,IntercomMissionMode.Rescue,1);Check(h.Progression.Wallet==142,"rescue kill10 plus25 rescue plus20 victory");
            Check(h.Progression.ResultReward.Rescued==1&&h.Progression.ResultReward.RescueParts==25,"actual rescued count");
            h.Finish(Id(4),500,true,IntercomMissionMode.FreePlay,1);Check(h.Progression.Wallet==142&&!h.Progression.HasResultReward,"free play cannot farm currency");
        });
        Case("exactly-once journal survives component recreation and a fresh application counter",()=>
        {
            Reset();var h=new Harness();h.Finish(Id(10));int saves=PlayerPrefs.Saves;
            Call(h.Progression,"OnDestroy");h.RecreateProgression();Call(h.Progression,"Update");
            Check(h.Progression.Wallet==70&&PlayerPrefs.Saves==saves&&h.Progression.HasResultReward,"saved run ID restores result without another credit");
            var fresh=new Harness();fresh.Finish(Id(11));Check(fresh.Mission.RunRevision==1&&fresh.Progression.Wallet==140,"fresh application runRevision1 still pays a different GUID");
            var replay=new Harness();replay.Finish(Id(11));Check(replay.Progression.Wallet==140,"last completed GUID cannot pay again after application recreation");
            Check(IntercomProgressionState.TryDecode(PlayerPrefs.GetString(B),out var state)||IntercomProgressionState.TryDecode(PlayerPrefs.GetString(A),out state),"journal is parseable");
        });
        Case("immediate retry cannot erase a terminal reward before progression's next Update",()=>
        {
            Reset();var h=new Harness();
            h.Mission.RunRevision=1;h.Mission.ResultRevision=1;
            h.Mission.LastRunSummary=new IntercomMissionRunSummary(Id(12),1,1,IntercomMissionMode.Elimination,IntercomMissionPhase.Victory,50,0,5,180);
            h.Mission.HasRunSummary=true;
            // OnGUI Retry has already begun a new run before the progression Update.
            h.Mission.RunRevision=2;h.Mission.HasRunSummary=false;h.Mission.Phase=IntercomMissionPhase.Active;
            Call(h.Progression,"Update");Check(h.Progression.Wallet==70&&h.Progression.ResultRunRevision==1,"retained terminal snapshot paid after retry");
            Check(h.Progression.ResultRunRevision!=h.Mission.RunRevision,"old reward cannot masquerade as new result");
            for(int i=0;i<10;i++)Call(h.Progression,"Update");Check(PlayerPrefs.Saves==1,"retained prior summary pays once");
        });
        Case("finite purchase branch, one capacity source and shared definitions unchanged",()=>
        {
            Reset();var h=new Harness();Check(!h.Buy()&&h.Progression.NextMagazineCost==60,"no currency means no purchase");
            h.Finish(Id(20));Check(!h.Progression.TryBuyMagazineUpgrade(),"cannot buy in result/gameplay phase");
            h.Mission.Phase=IntercomMissionPhase.Selection;Time.timeScale=0;Check(!h.Progression.TryBuyMagazineUpgrade(),"paused purchase blocked");Time.timeScale=1;
            Application.isFocused=false;Check(!h.Progression.TryBuyMagazineUpgrade(),"unfocused purchase blocked");Application.isFocused=true;
            Check(h.Buy()&&h.Progression.Wallet==10&&h.Progression.MagazineLevel==1,"first upgrade debits60 once");
            Check(h.Magazine.Capacity==30&&h.Magazine.CurrentAmmo==30&&h.Progression.EffectiveMagazineCapacity==30,"actual reload/HUD capacity upgraded");
            Check(h.Weapon.magazineSize==24&&h.Weapon.MagazineCapacity==24,"shared asset remains authored24");
            Check(h.Progression.NextMagazineCapacity==36&&h.Progression.NextMagazineCost==130,"next benefit and cost exposed");
            Check(h.Magazine.CommitVolley(1)&&h.Magazine.CurrentAmmo==29,"live ammo consumed");h.Magazine.SetCapacityBonus(6);Check(h.Magazine.CurrentAmmo==29,"same upgrade cannot refill ammo");
            h.Magazine.RequestReload();Time.deltaTime=2;Call(h.Magazine,"Update");Check(h.Magazine.CurrentAmmo==30,"reload fills upgraded capacity");
            var motor=h.Hero.GetComponent<IntercomPlayerMotor>();h.Magazine.CommitVolley(1);motor.RespawnVersion++;Call(h.Magazine,"Update");Check(h.Magazine.CurrentAmmo==30,"respawn retains upgrade");
            Near(h.Progression.EffectiveIdealTtk,IntercomWeaponBalance.IdealTtkWithReload(1500,15,1,.12f,30,1.15f),1e-6,"calculator uses effective capacity");
            Check(h.Progression.EffectiveIdealTtk<h.Weapon.IdealTtk&&h.Progression.EffectiveSustainedDps>h.Weapon.SustainedDps,"extended magazine improves reload-aware balance");
            h.Finish(Id(21));h.Finish(Id(22));Check(h.Buy()&&h.Progression.MagazineLevel==2&&h.Magazine.Capacity==36,"second purchase");
            for(int i=23;i<=26;i++)h.Finish(Id(i));Check(h.Buy()&&h.Progression.MagazineLevel==3&&h.Magazine.Capacity==42,"third purchase");
            int wallet=h.Progression.Wallet;Check(h.Progression.MagazineMaxed&&h.Progression.NextMagazineCost==0&&h.Progression.NextMagazineCapacity==42&&!h.Buy()&&h.Progression.Wallet==wallet,"maximum rank cannot charge again");
        });
        Case("alternating banks, damaged-data recovery and bounded format",()=>
        {
            Reset();var h=new Harness();h.Finish(Id(30));h.Finish(Id(31));h.Finish(Id(32));
            Check(PlayerPrefs.Values.Count==2,"two bounded preference banks");PlayerPrefs.Values[B]="truncated";
            var recovered=new Harness();Check(recovered.Progression.Wallet==140&&!string.IsNullOrEmpty(recovered.Progression.LoadWarning),"last valid bank recovered");
            string originalA=PlayerPrefs.Values[A];PlayerPrefs.Values[A]="CP9|future-version";string oldB=PlayerPrefs.Values[B];
            var empty=new Harness();Check(empty.Progression.Wallet==0&&PlayerPrefs.Values[A]=="CP9|future-version"&&PlayerPrefs.Values[B]==oldB,"unknown/corrupt records preserved, no auto deletion");
            Check(IntercomProgressionState.TryDecode(originalA,out var data)&&data.CompletedRuns==2&&data.TotalKills==100,"valid statistics survive encode");
            string encoded=data.Encode();for(int i=0;i<encoded.Length;i++)Check(!IntercomProgressionState.TryDecode(encoded.Substring(0,i),out _),"every truncation rejected");
            for(int i=0;i<encoded.Length;i++){char[] chars=encoded.ToCharArray();chars[i]=chars[i]=='x'?'y':'x';Check(!IntercomProgressionState.TryDecode(new string(chars),out _),"single-byte corruption rejected");}
            Check(!IntercomProgressionState.TryDecode(new string('x',257),out _)&&!IntercomProgressionState.ValidRunId("bad"),"unbounded or invalid identity rejected");
        });
        Case("effective balance follows the equipped weapon rather than a stale menu asset",()=>
        {
            Reset();var h=new Harness();h.Finish(Id(33));Check(h.Buy(),"upgrade setup");
            var equipped=new IntercomWeaponDefinition{magazineSize=40,damagePerProjectile=12,referenceTargetHealth=2000,fireInterval=.09f,reloadSeconds=1.4f};
            h.Shooter.weapon=equipped;Time.deltaTime=.01f;Call(h.Magazine,"Update");
            Check(h.Magazine.Capacity==46&&h.Magazine.CurrentAmmo==46&&h.Progression.EffectiveMagazineCapacity==46,"new weapon plus saved bonus uses one capacity");
            Check(h.Progression.NextMagazineCapacity==52&&h.Progression.weapon==h.Weapon,"next benefit uses equipped asset, serialized fallback unchanged");
            Near(h.Progression.EffectiveIdealTtk,IntercomWeaponBalance.IdealTtkWithReload(2000,12,1,.09f,46,1.4f),1e-5,"effective calculator follows new balance");
        });
        Case("save failure cannot debit or confirm an unpersisted reward",()=>
        {
            Reset();var h=new Harness();PlayerPrefs.FailSave=true;h.Finish(Id(40));
            Check(h.Progression.Wallet==0&&!h.Progression.HasResultReward&&!string.IsNullOrEmpty(h.Progression.SaveProblem),"failed reward stays unconfirmed");
            Check(PlayerPrefs.Values.All(p=>p.Value==""),"failed bank write restored in-memory preference bytes");
            int sets=PlayerPrefs.Sets;for(int i=0;i<20;i++)Call(h.Progression,"Update");Check(PlayerPrefs.Sets==sets,"failed save does not retry every frame");
            PlayerPrefs.FailSave=false;Time.unscaledTime=3;Call(h.Progression,"Update");Check(h.Progression.Wallet==70&&h.Progression.HasResultReward&&h.Progression.SaveProblem=="","pending reward retries once");
            string snapshot=Snapshot();PlayerPrefs.FailSave=true;Check(!h.Buy()&&h.Progression.Wallet==70&&h.Progression.MagazineLevel==0&&h.Magazine.Capacity==24,"failed purchase leaves wallet/rank/ammo unchanged");
            Check(Snapshot()==snapshot,"failed purchase restores exact previous preference keys and bytes");
            PlayerPrefs.FailSave=false;Check(h.Buy()&&h.Progression.Wallet==10&&h.Magazine.Capacity==30,"retry purchase charges once");
        });
        Case("multiple results survive a lasting save failure across retries",()=>
        {
            Reset();var h=new Harness();PlayerPrefs.FailSave=true;h.Finish(Id(41));
            Check(h.Progression.PendingRewardCount==1&&h.Progression.HasPendingRewards&&h.Progression.CanStartAnotherRun,"first unpaid receipt queued without blocking normal retry");
            h.Mission.HasRunSummary=false;h.Mission.Phase=IntercomMissionPhase.Active;
            h.Finish(Id(42),10,true,IntercomMissionMode.Rescue,1);
            Check(h.Progression.PendingRewardCount==2&&h.Progression.Wallet==0,"second terminal captured while first backs off");
            Check(!h.Progression.BeginDiagnosticSession(),"diagnostics cannot discard real pending receipts");
            PlayerPrefs.FailSave=false;Time.unscaledTime=3;Call(h.Progression,"Update");
            Check(h.Progression.Wallet==70&&h.Progression.PendingRewardCount==1&&!h.Progression.HasResultReward,"one old receipt committed, latest screen still pending");
            Call(h.Progression,"Update");Check(h.Progression.Wallet==125&&h.Progression.PendingRewardCount==0&&h.Progression.HasResultReward,"both earned receipts settle once");
            Check(h.Progression.ResultReward.Total==55&&h.Progression.ResultRunRevision==h.Mission.RunRevision,"latest rescue breakdown not overwritten by older payout");
            int saves=PlayerPrefs.Saves;for(int i=0;i<10;i++)Call(h.Progression,"Update");Check(h.Progression.Wallet==125&&PlayerPrefs.Saves==saves,"queue drain remains exactly once");
        });
        Case("bounded queue reserves unseen terminal before allowing another run",()=>
        {
            Reset();var h=new Harness();PlayerPrefs.FailSave=true;
            for(int i=0;i<31;i++)h.Finish(Id(200+i),1,false);
            Check(h.Progression.PendingRewardCount==31&&h.Progression.CanStartAnotherRun,"31 stored receipts still leave one slot");
            h.Mission.RunRevision++;h.Mission.ResultRevision++;
            h.Mission.LastRunSummary=new IntercomMissionRunSummary(Id(231),h.Mission.RunRevision,h.Mission.ResultRevision,IntercomMissionMode.Elimination,IntercomMissionPhase.Defeat,1,0,1,5);
            h.Mission.HasRunSummary=true;
            Check(!h.Progression.CanStartAnotherRun&&h.Progression.HasPendingRewards,"unseen32nd result reserves final slot before next Update");
            Call(h.Progression,"Update");Check(h.Progression.PendingRewardCount==32&&!h.Progression.CanStartAnotherRun,"queue remains bounded and stops further retries through UI contract");
            PlayerPrefs.FailSave=false;Time.unscaledTime=3;int before=PlayerPrefs.Saves;Call(h.Progression,"Update");
            Check(PlayerPrefs.Saves==before+1&&h.Progression.PendingRewardCount==31&&h.Progression.CanStartAnotherRun,"recovery writes at most one receipt per frame and reopens run selection");
            for(int i=0;i<31;i++)Call(h.Progression,"Update");Check(h.Progression.Wallet==32&&!h.Progression.HasPendingRewards,"all32 receipts preserved and drained");
        });
        Case("missing dependencies or a capped magazine cannot take purchase money",()=>
        {
            Reset();var h=new Harness();h.Finish(Id(43));h.Mission.Phase=IntercomMissionPhase.Selection;
            h.Progression.magazine=null;Check(!h.Progression.TryBuyMagazineUpgrade()&&h.Progression.Wallet==70,"missing magazine cannot sell an ineffective upgrade");
            h.Progression.magazine=h.Magazine;h.Magazine.isActiveAndEnabled=false;Check(!h.Progression.TryBuyMagazineUpgrade(),"disabled magazine cannot charge");h.Magazine.isActiveAndEnabled=true;
            h.Weapon.magazineSize=999;Call(h.Magazine,"Update");Check(!h.Progression.CanBuyMagazine&&!h.Progression.TryBuyMagazineUpgrade()&&h.Progression.Wallet==70,"no capacity benefit means no purchase");
        });
        Case("native diagnostic session cannot alter the real wallet or preferences",()=>
        {
            Reset();var h=new Harness();h.Finish(Id(50));Check(h.Buy(),"real upgrade setup");string saved=Snapshot();int sets=PlayerPrefs.Sets,saves=PlayerPrefs.Saves;
            Check(h.Progression.BeginDiagnosticSession()&&!h.Progression.BeginDiagnosticSession(),"single diagnostic scope");
            Check(h.Progression.Wallet==0&&h.Progression.MagazineLevel==0&&h.Magazine.Capacity==24,"fresh sandbox profile");
            h.Finish(Id(51));Check(h.Buy()&&h.Magazine.Capacity==30,"sandbox can exercise actual reward/purchase path");
            Check(Snapshot()==saved&&PlayerPrefs.Sets==sets&&PlayerPrefs.Saves==saves,"sandbox performs zero preference writes");
            h.Progression.EndDiagnosticSession();Check(h.Progression.Wallet==10&&h.Progression.MagazineLevel==1&&!h.Progression.HasResultReward,"real profile restored, diagnostic result discarded");
            Call(h.Progression,"Update");Check(h.Progression.Wallet==10&&Snapshot()==saved,"discarded diagnostic terminal cannot credit real wallet");
            h.Finish(Id(52));Check(h.Progression.Wallet==80,"later real run still settles normally");
            saved=Snapshot();Check(h.Progression.BeginDiagnosticSession(),"second sandbox");h.Finish(Id(53));Call(h.Progression,"OnDisable");
            Check(!h.Progression.IsDiagnosticSession&&h.Progression.Wallet==80&&Snapshot()==saved,"disable restores without saving diagnostics");
        });
        Case("magazine extension is prepared once, stays behind muzzles and has closed outward geometry",()=>
        {
            Reset();int meshes=Mesh.Created;var h=new Harness();Check(Mesh.Created==meshes+1,"one shared owned mesh for both guns");
            var left=Field<Transform>(h.Progression,"leftExtension");var right=Field<Transform>(h.Progression,"rightExtension");
            Check(left!=null&&right!=null&&!left.gameObject.activeSelf&&!right.gameObject.activeSelf,"zero-level add-ons remain hidden");
            h.Finish(Id(60));Check(h.Buy()&&left.gameObject.activeSelf&&right.gameObject.activeSelf,"purchase visibly activates both extensions");
            Check(left.localPosition.z<0&&right.localPosition.z<0&&left.localPosition.y<0,"parts stay behind and below barrel tips");
            Check(left.gameObject.GetComponent<MeshRenderer>().sharedMaterial==h.Weapon.casingMaterial,"uses existing shared material");
            Mesh mesh=Field<Mesh>(h.Progression,"extensionMesh");Check(mesh.vertices.Length==34&&mesh.triangles.Length==192,"small64-triangle shared bevel mesh");
            var edges=new Dictionary<string,int>();double volume=0;
            for(int i=0;i<mesh.triangles.Length;i+=3)
            {
                int ia=mesh.triangles[i],ib=mesh.triangles[i+1],ic=mesh.triangles[i+2];var a=mesh.vertices[ia];var b=mesh.vertices[ib];var c=mesh.vertices[ic];
                volume+=(a.x*(b.y*c.z-b.z*c.y)+a.y*(b.z*c.x-b.x*c.z)+a.z*(b.x*c.y-b.y*c.x))/6d;
                foreach(var edge in new[]{(ia,ib),(ib,ic),(ic,ia)}){string key=Math.Min(edge.Item1,edge.Item2)+":"+Math.Max(edge.Item1,edge.Item2);edges[key]=edges.TryGetValue(key,out int count)?count+1:1;}
            }
            Check(edges.Values.All(v=>v==2)&&volume>0,"closed consistently outward shell");
            int objects=GameObject.Created;for(int i=0;i<1000;i++)Call(h.Progression,"Update");Check(GameObject.Created==objects&&Mesh.Created==meshes+1,"menu/result frames allocate no extra native presentation");
            Call(h.Progression,"OnDestroy");Check(mesh.destroyed&&left.gameObject.destroyed&&right.gameObject.destroyed&&!h.Weapon.casingMaterial.destroyed,"owned mesh/objects released, shared material retained");
        });
        Case("reward/capacity bounds never overflow or create negative money",()=>
        {
            var data=new IntercomProgressionState();for(int i=0;i<110;i++)
            {
                Check(data.TrySettle(Id(1000+i),int.MaxValue,int.MaxValue,true,true,out var next,out var reward),"bounded settlement");
                Check(next.Wallet>=data.Wallet&&next.Wallet<=IntercomProgressionState.MaximumWallet&&reward.Total==next.Wallet-data.Wallet,"credited total respects wallet cap");data=next;
            }
            Check(data.Wallet==IntercomProgressionState.MaximumWallet&&data.Encode().Length<256,"bounded persistent state");
            Check(IntercomProgressionState.TryDecode(data.Encode(),out var decoded)&&decoded.Wallet==data.Wallet,"large valid profile roundtrip");
            foreach(int authored in new[]{int.MinValue,-1,0,1,24,999,int.MaxValue})foreach(int bonus in new[]{int.MinValue,-1,0,6,975,int.MaxValue})
            {int capacity=IntercomWeaponBalance.EffectiveMagazineCapacity(authored,bonus,2);Check(capacity>=2&&capacity<=999,"capacity remains legal");}
        });
        Console.WriteLine("Progression: "+cases+" cases / "+checks+" assertions PASS. Actual progression/model/magazine sources with narrow Unity/preferences substitutes; no native Unity or device execution.");
    }
}
