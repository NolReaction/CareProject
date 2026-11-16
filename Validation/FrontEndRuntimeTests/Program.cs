using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using IntercomMvp;
using UnityEngine;

internal static class Program
{
    static int assertions;
    static void Check(bool condition,string label){assertions++;if(!condition)throw new Exception(label);}
    static void Call(object target,string method){try{target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,null);}catch(TargetInvocationException e){throw e.InnerException;}}
    static IntercomFrontEnd Fresh(bool mobile=false)
    {
        Input.Clear();Application.isMobilePlatform=mobile;Time.timeScale=1;
        Screen.width=1280;Screen.height=720;Screen.safeArea=new Rect(0,0,1280,720);
        var mission=new IntercomMissionDirector{ResultText=""};
        var input=new IntercomPlayerInput{mission=mission,attached=new IntercomWeaponMagazine()};
        Call(input,"Awake");Call(input,"OnEnable");
        var menu=new IntercomFrontEnd{mission=mission,input=input,weapon=new IntercomWeaponDefinition(),playerHealth=new IntercomHealth(),playerMotor=new IntercomPlayerMotor(),roomCamera=new IntercomFrontEndRoomCamera()};
        Call(menu,"OnEnable");Call(input,"Update");Call(menu,"Update");return menu;
    }
    static Vector2 Centre(IntercomHudLayout.Box b)=>new Vector2(b.X+b.Width*.5f,b.Y+b.Height*.5f);
    static void Release(IntercomFrontEnd f){Input.Clear();Call(f.input,"Update");Call(f,"Update");}
    static void Click(IntercomFrontEnd f,IntercomHudLayout.Box b)
    {
        Input.mousePosition=Centre(b);Input.MouseHeld=Input.MouseDown=true;Call(f.input,"Update");Call(f,"Update");Input.MouseDown=false;
    }
    static bool Quiet(IntercomPlayerInput p)=>p.Move.sqrMagnitude==0&&!p.FireHeld&&!p.FirePressed&&!p.ReloadPressed&&!p.ResetPressed;
    static void Main()
    {
        Registration();CameraLifecycle();NavigationAndModes();MultitouchAndFocus();Results();View();UpgradeRouting();RewardView();UpgradeAndRewardBounds();IllustrationBounds();Previews();
        Console.WriteLine($"PASS {assertions} actual front-end registration, navigation, rendering and input-cancellation assertions. Minimal Unity/director substitutes; no Unity Play Mode.");
    }
    static void CameraLifecycle()
    {
        var f=Fresh();var old=f.roomCamera;var next=new IntercomFrontEndRoomCamera();
        f.roomCamera=next;f.Navigate(IntercomFrontEndSection.Robot);
        Check(!old.IsShown&&next.IsShown,"navigation after camera replacement releases previous owner first");
        f.mission=null;Call(f,"Update");Check(!next.IsShown&&!f.IsVisible,"missing mission releases room presentation");
        f.mission=new IntercomMissionDirector();Call(f,"Update");Check(next.IsShown,"replacement mission reopens room presentation");
        f.roomCamera=null;Call(f,"Update");Check(!next.IsShown,"missing room rig restores old presentation owner");
    }
    static void Registration()
    {
        var f=Fresh();Check(ReferenceEquals(f.input.FrontEnd,f),"front end registered on real input");Check(f.IsVisible&&f.Section==IntercomFrontEndSection.Home,"fresh opens full home menu");
        Check(f.roomCamera.IsShown,"optional room rig enters menu");
        var other=new IntercomFrontEnd();Check(!f.input.RegisterFrontEnd(other),"second controller cannot steal input");
        f.input.UnregisterFrontEnd(other);Check(ReferenceEquals(f.input.FrontEnd,f),"unrelated controller cannot release input");
        Call(f,"OnDisable");Check(f.input.FrontEnd==null&&!f.roomCamera.IsShown,"disable releases input and camera");
        Call(f,"OnEnable");Check(ReferenceEquals(f.input.FrontEnd,f),"re-enable reconnects input");
    }
    static void NavigationAndModes()
    {
        var f=Fresh();var l=f.CurrentLayout();Input.held.Add(KeyCode.W);Input.MouseHeld=Input.MouseDown=true;Input.mousePosition=Centre(l.TabRobot);Call(f.input,"Update");Call(f,"Update");
        Check(f.Section==IntercomFrontEndSection.Robot&&Quiet(f.input),"WASD cannot leak while opening workshop");
        Input.MouseDown=false;Input.mousePosition=Centre(l.ActionPrimary);Call(f.input,"Update");Check(f.mission.Starts==0,"held tab press cannot start run");
        Release(f);Click(f,l.ActionSecondary);Check(f.Section==IntercomFrontEndSection.Armory,"workshop secondary opens armory");
        Release(f);Input.down.Add(KeyCode.Escape);Call(f.input,"Update");Call(f,"Update");Check(f.Section==IntercomFrontEndSection.Home&&Quiet(f.input),"Escape returns home");
        Release(f);Click(f,l.ActionPrimary);Check(f.Section==IntercomFrontEndSection.Play,"home primary opens mode list");
        Release(f);Click(f,l.ModeFirst);Check(f.mission.Mode==IntercomMissionMode.Elimination&&f.mission.Starts==1&&!f.IsVisible&&Quiet(f.input),"elimination card starts one mission and consumes press");
        Check(!f.roomCamera.IsShown,"starting gameplay restores camera owner");
        Input.mousePosition=new Vector2(900,300);Call(f.input,"Update");Check(!f.input.FireHeld,"menu click drag cannot fire");
        Release(f);Release(f);Input.MouseHeld=Input.MouseDown=true;Input.mousePosition=new Vector2(900,300);Call(f.input,"Update");Check(f.input.FireHeld,"fresh gameplay press works after release");
        Release(f);Input.down.Add(KeyCode.Escape);Call(f.input,"Update");Call(f,"Update");Check(f.input.GameplayPaused&&!f.IsVisible&&Quiet(f.input),"game Escape opens pause without switching room camera");
        var h=IntercomHudLayout.Calculate(0,0,Screen.width,Screen.height,104,18);
        Release(f);Click(f,h.PauseExit);Release(f);Click(f,h.PauseConfirm);
        Check(f.IsVisible&&f.Section==IntercomFrontEndSection.Home&&Time.timeScale==1,"confirmed exit returns to hub and restores time");
        Release(f);Click(f,l.TabPlay);Release(f);Click(f,l.ModeSecond);Check(f.mission.Mode==IntercomMissionMode.Rescue&&f.mission.Starts==2,"rescue routed");
        f.mission.ReturnToSelection();Call(f,"Update");Release(f);Click(f,l.TabPlay);Release(f);Click(f,l.ModeThird);Check(f.mission.Mode==IntercomMissionMode.FreePlay&&f.mission.Starts==3,"free mode routed");
    }
    static void MultitouchAndFocus()
    {
        var f=Fresh(true);var l=f.CurrentLayout();
        var p=Centre(l.TabPlay);Input.touches=new[]{new Touch{fingerId=1,position=p,phase=TouchPhase.Began},new Touch{fingerId=2,position=Centre(l.ModeFirst),phase=TouchPhase.Began}};
        Call(f.input,"Update");Call(f,"Update");Check(f.Section==IntercomFrontEndSection.Play&&f.mission.Starts==0&&Quiet(f.input),"two fingers navigate only one control per press");
        Input.touches=new[]{new Touch{fingerId=2,position=Centre(l.ModeFirst),phase=TouchPhase.Moved}};Call(f.input,"Update");Check(f.mission.Starts==0,"second held finger not reinterpreted");
        Release(f);Input.touches=new[]{new Touch{fingerId=3,position=Centre(l.ModeThird),phase=TouchPhase.Began}};Call(f.input,"Update");Call(f,"Update");Check(f.mission.Mode==IntercomMissionMode.FreePlay&&Quiet(f.input),"mobile starts free run without fire");
        f.mission.ReturnToSelection();Call(f,"Update");Release(f);f.input.CancelUntilRelease();
        Input.touches=new[]{new Touch{fingerId=4,position=Centre(l.TabPlay),phase=TouchPhase.Began}};Call(f.input,"Update");Check(f.Section==IntercomFrontEndSection.Home,"focus release gate respected in menu");
        Release(f);Time.timeScale=0;Input.touches=new[]{new Touch{fingerId=5,position=Centre(l.TabRobot),phase=TouchPhase.Began}};Call(f.input,"Update");Check(f.Section==IntercomFrontEndSection.Home,"paused app cannot interact");Time.timeScale=1;
    }
    static void Results()
    {
        var f=Fresh();f.mission.Phase=IntercomMissionPhase.Victory;Call(f,"Update");Release(f);Click(f,f.CurrentLayout().TabRobot);
        Check(f.mission.Phase==IntercomMissionPhase.Selection&&f.Section==IntercomFrontEndSection.Robot,"result tab navigation enters selected page");
        Call(f,"Update");Check(f.Section==IntercomFrontEndSection.Robot,"phase sync does not reset selected result tab");
        f.mission.Phase=IntercomMissionPhase.Defeat;Call(f,"Update");Release(f);Click(f,f.CurrentLayout().ActionPrimary);Check(f.mission.Retries==1&&!f.IsVisible,"retry goes through mission lifecycle");
        f.mission.Phase=IntercomMissionPhase.Defeat;Call(f,"Update");Release(f);Click(f,f.CurrentLayout().ActionSecondary);Check(f.mission.Phase==IntercomMissionPhase.Selection&&f.Section==IntercomFrontEndSection.Home,"result secondary returns home");
    }
    static void View()
    {
        var f=Fresh();GUI.labels.Clear();Call(f.input,"OnGUI");Check(GUI.labels.Count==0,"legacy selection HUD suppressed while front end owns display");
        Call(f,"OnGUI");Check(GUI.labels.Contains("ЗАБОТА")&&GUI.labels.Contains("ОРУЖЕЙНАЯ"),"real view renders full game header and sections");
        f.Navigate(IntercomFrontEndSection.Robot);GUI.labels.Clear();Call(f,"OnGUI");Check(GUI.labels.Contains("ТВОЯ МАСТЕРСКАЯ")&&GUI.labels.Contains("100")&&GUI.labels.Contains("СКОРО"),"robot page actual stats and honest future slots");
        f.Navigate(IntercomFrontEndSection.Armory);GUI.labels.Clear();Call(f,"OnGUI");Check(GUI.labels.Contains("15")&&GUI.labels.Contains("24")&&GUI.labels.Contains("ОРУЖЕЙНАЯ"),"armory current card values");
        f.Navigate(IntercomFrontEndSection.Play);GUI.labels.Clear();Call(f,"OnGUI");Check(GUI.labels.Contains("50 РОБОТОВ")&&GUI.labels.Contains("СПАСЕНИЕ")&&GUI.labels.Contains("СВОБОДНЫЙ РЕЖИМ"),"all three modes visible");
        f.mission.Phase=IntercomMissionPhase.Active;Call(f,"Update");GUI.labels.Clear();Call(f.input,"OnGUI");Check(GUI.labels.Contains("00:00:00")&&!GUI.labels.Contains("ЗАБОТА"),"active HUD restored with pause action");
    }
    static IntercomProgression Service(IntercomFrontEnd f,int wallet=0,int level=0)
    {
        var p=new IntercomProgression{mission=f.mission,Wallet=wallet,MagazineLevel=level,EffectiveMagazineCapacity=24+level*6,
            NextMagazineCapacity=24+Math.Min(3,level+1)*6,NextMagazineCost=level>=3?0:level==2?240:level==1?130:60};
        f.progression=p;return p;
    }
    static void UpgradeRouting()
    {
        var f=Fresh();var service=Service(f,59);f.Navigate(IntercomFrontEndSection.Armory);Release(f);Click(f,f.CurrentLayout().ActionPrimary);
        Check(service.Purchases==0&&service.Wallet==59&&f.Notice=="Не хватает запчастей: 1.","insufficient parts explains exact shortfall without purchase");
        service.Wallet=100;Release(f);Click(f,f.CurrentLayout().ActionPrimary);
        Check(service.Purchases==1&&service.Wallet==40&&service.EffectiveMagazineCapacity==30&&f.Section==IntercomFrontEndSection.Armory,"armory primary routes one successful purchase and stays in workshop");
        Call(f.input,"Update");Call(f,"Update");Check(service.Purchases==1&&Quiet(f.input),"held upgrade press cannot repeat or leak gameplay input");
        Check(f.Notice.Contains("30 патронов"),"successful upgrade reports actual effective capacity");
        GUI.labels.Clear();Call(f,"OnGUI");Check(GUI.labels.Contains("30")&&GUI.labels.Contains("30 → 36 ПАТРОНОВ")&&GUI.labels.Contains("ЗАПЧАСТИ  40"),"current capacity, next benefit and wallet all update on purchase");
        service.EquippedWeapon=new IntercomWeaponDefinition{Damage=27,Interval=.21f,ReloadSeconds=2.1f};GUI.labels.Clear();Call(f,"OnGUI");
        Check(GUI.labels.Contains("27")&&GUI.labels.Contains(2.1f.ToString("0.00"))&&!GUI.labels.Contains("15"),"live equipped weapon supplies damage/rate/reload alongside effective magazine capacity");
        service.Wallet=500;service.SaveProblem="disk unavailable";Release(f);Click(f,f.CurrentLayout().ActionPrimary);
        Check(service.Purchases==1&&f.Notice=="Не удалось сохранить улучшение. Попробуй ещё раз.","save failure keeps money/state and offers useful nontechnical feedback");
        service.SaveProblem="";service.MagazineLevel=3;service.NextMagazineCost=0;Release(f);Click(f,f.CurrentLayout().ActionPrimary);
        Check(service.Purchases==1&&f.Notice.Contains("до предела"),"maximum rank is an explicit harmless no-op");
        f.Navigate(IntercomFrontEndSection.Home);Check(!f.TryUpgradeMagazine(),"public purchase cannot bypass selected workshop page");
        f.Navigate(IntercomFrontEndSection.Armory);Time.timeScale=0;Check(!f.TryUpgradeMagazine(),"paused application cannot purchase");Time.timeScale=1;
        service.mission=new IntercomMissionDirector();Check(f.Progression==null&&!f.TryUpgradeMagazine(),"progression from a different mission cannot control this menu");
        service.mission=f.mission;service.isActiveAndEnabled=false;Check(f.Progression==null,"disabled progression is unavailable rather than stale");
        service.isActiveAndEnabled=true;service.CanStartAnotherRun=false;f.Navigate(IntercomFrontEndSection.Play);Release(f);Click(f,f.CurrentLayout().ModeFirst);
        Check(f.mission.Starts==0&&service.CollectionAttempts>0&&f.Notice.Contains("Сначала сохраним награды")&&Quiet(f.input),"pending receipt is captured and a full unsaved queue cannot silently lose rewards by starting another run");
        f.mission.Phase=IntercomMissionPhase.Defeat;Call(f,"Update");Release(f);Click(f,f.CurrentLayout().ActionPrimary);Check(f.mission.Retries==0&&f.IsResult,"result retry observes the same pending-save guard");
        service.CanStartAnotherRun=true;Release(f);Click(f,f.CurrentLayout().ActionPrimary);Check(f.mission.Retries==1,"a recovered pending-save service permits retry again");
    }
    static void RewardView()
    {
        var f=Fresh();var p=Service(f,165);f.mission.RunRevision=7;f.mission.Phase=IntercomMissionPhase.Victory;
        f.mission.HasRunSummary=true;f.mission.LastRunSummary=new IntercomRunSummary{Kills=50,Rescued=1,ElapsedSeconds=183};
        p.HasResultReward=true;p.ResultRunRevision=7;p.ResultReward=new IntercomRunReward{Kills=50,Rescued=1,KillParts=100,RescueParts=40,VictoryParts=25,Total=165,Eligible=true,Won=true};
        Call(f,"Update");GUI.labels.Clear();Call(f,"OnGUI");
        Check(GUI.labels.Contains("ОТКЛЮЧЕНО  50   ·   СПАСЕНО  1   ·   03:03"),"result uses immutable summary, including elapsed time and rescued count");
        Check(GUI.labels.Contains("ЗАПЧАСТИ  +165")&&GUI.labels.Contains("Подобрано +100  ·  Спасение +40  ·  Выезд +25")&&GUI.labels.Contains("В МАСТЕРСКОЙ  165 ЗАПЧАСТЕЙ"),"reward receipt has collected gear breakdown and current balance");
        object view=typeof(IntercomFrontEnd).GetField("view",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f);
        var facts=typeof(IntercomFrontEndView).GetField("resultFacts",BindingFlags.NonPublic|BindingFlags.Instance);string first=(string)facts.GetValue(view);
        Call(f,"OnGUI");Check(ReferenceEquals(first,facts.GetValue(view)),"unchanged result receipt retains cached strings");
        p.ResultRunRevision=6;GUI.labels.Clear();Call(f,"OnGUI");Check(!GUI.labels.Contains("ЗАПЧАСТИ  +165")&&GUI.labels.Contains("ПОДСЧЁТ ЗАПЧАСТЕЙ"),"previous run reward never appears on a new result");
        p.SaveProblem="test unavailable";GUI.labels.Clear();Call(f,"OnGUI");Check(GUI.labels.Contains("НАГРАДА ЖДЁТ СОХРАНЕНИЯ")&&GUI.labels.Contains("Повторим сохранение автоматически."),"unconfirmed save never presents currency as paid");p.SaveProblem="";
        p.HasPendingRewards=true;GUI.labels.Clear();Call(f,"OnGUI");Check(GUI.labels.Contains("НАГРАДА ЖДЁТ СОХРАНЕНИЯ"),"pending receipt remains explicit when an older save clears the temporary error");p.HasPendingRewards=false;
        p.HasResultReward=false;p.ResultRunRevision=7;p.ResultReward=new IntercomRunReward{Eligible=false};f.mission.Mode=IntercomMissionMode.FreePlay;
        GUI.labels.Clear();Call(f,"OnGUI");Check(GUI.labels.Contains("ТРЕНИРОВКА БЕЗ НАГРАД")&&!GUI.labels.Contains("ЗАПЧАСТИ  +0"),"free play states explicitly that it does not earn parts");
    }
    static void UpgradeAndRewardBounds()
    {
        foreach(var size in new[]{(1280,720),(640,360),(480,900)})foreach(bool result in new[]{false,true})
        {
            var f=Fresh();var p=Service(f,123);Screen.width=size.Item1;Screen.height=size.Item2;Screen.safeArea=new Rect(16,12,size.Item1-32,size.Item2-24);
            if(result){f.mission.Phase=IntercomMissionPhase.Defeat;p.HasResultReward=true;p.ResultReward=new IntercomRunReward{Eligible=true,Total=24,KillParts=24};}
            else f.Navigate(IntercomFrontEndSection.Armory);
            GUI.Record=true;GUI.commands.Clear();Call(f,"OnGUI");GUI.Record=false;
            var l=f.CurrentLayout();float top=Screen.height-l.Content.Y-l.Content.Height,buttons=Screen.height-l.ActionPrimary.Y-l.ActionPrimary.Height;
            using(var document=JsonDocument.Parse(JsonSerializer.Serialize(GUI.commands)))foreach(var c in document.RootElement.EnumerateArray())
            {
                if(c.GetProperty("kind").GetString()!="text")continue;string label=c.GetProperty("text").GetString();
                if(!(label.StartsWith("МАГАЗИНЫ ·")||label.Contains(" → ")||label.StartsWith("ЦЕНА  ")||label.StartsWith("ОТКЛЮЧЕНО  ")||label.StartsWith("ЗАПЧАСТИ  +")||label.StartsWith("В МАСТЕРСКОЙ  ")))continue;
                var b=c.GetProperty("rect");float x=b[0].GetSingle(),y=b[1].GetSingle(),w=b[2].GetSingle(),h=b[3].GetSingle();
                Check(x>=l.Content.X&&x+w<=l.Content.X+l.Content.Width+.001f&&y>=top&&y+h<=buttons+.001f,"reward/upgrade label stays in card above touch actions on "+size);
            }
            Release(f);Click(f,l.ActionSecondary);Check(Quiet(f.input),"new result/upgrade presentation retains pointer ownership on "+size);
        }
    }
    static string Root(){for(var d=new DirectoryInfo(Directory.GetCurrentDirectory());d!=null;d=d.Parent)if(Directory.Exists(Path.Combine(d.FullName,"Assets/IntercomMVP")))return d.FullName;throw new DirectoryNotFoundException();}
    static void IllustrationBounds()
    {
        foreach(var size in new[]{(1280,720),(640,360),(480,900)})
        foreach(var section in new[]{IntercomFrontEndSection.Home,IntercomFrontEndSection.Play})
        {
            var f=Fresh();f.homeDoodle=new Texture2D(1283,1226,TextureFormat.RGBA32,false){name="HomeDoodle"};
            f.rescueDoodle=new Texture2D(1254,1254,TextureFormat.RGBA32,false){name="RescueDoodle"};
            Screen.width=size.Item1;Screen.height=size.Item2;Screen.safeArea=new Rect(14,12,size.Item1-28,size.Item2-24);f.Navigate(section);
            GUI.Record=true;GUI.commands.Clear();Call(f,"OnGUI");GUI.Record=false;
            string image=section==IntercomFrontEndSection.Home?"HomeDoodle":"RescueDoodle";int count=0;
            var l=f.CurrentLayout();var target=section==IntercomFrontEndSection.Home?l.Content:l.ModeSecond;
            using(var json=JsonDocument.Parse(JsonSerializer.Serialize(GUI.commands)))
            foreach(var item in json.RootElement.EnumerateArray())
            {
                if(item.GetProperty("kind").GetString()!="texture" || item.GetProperty("texture").GetString()!=image)continue;
                count++;var box=item.GetProperty("rect");float x=box[0].GetSingle(),y=box[1].GetSingle(),w=box[2].GetSingle(),h=box[3].GetSingle();
                float guiY=Screen.height-target.Y-target.Height;
                Check(x>=target.X&&y>=guiY&&x+w<=target.X+target.Width+.001f&&y+h<=guiY+target.Height+.001f,"illustration stays in its production card on "+size);
                Check(Math.Abs(w/h-(section==IntercomFrontEndSection.Home?1283f/1226f:1f))<.001f,"transparent artwork retains source aspect ratio");
                if(section==IntercomFrontEndSection.Home)Check(y+h<Screen.height-l.ActionPrimary.Y-l.ActionPrimary.Height,"home illustration cannot cover CTA");
                else Check(w>=Math.Min(target.Width*.28f,target.Height*.82f),"rescue art remains a readable spot illustration");
            }
            Check(count==1,"exactly one correct illustration drawn");
            if(section==IntercomFrontEndSection.Home)
            {
                Release(f);Click(f,new IntercomHudLayout.Box(target.X+target.Width*.5f,target.Y+target.Height*.605f,8,8));
                Check(f.mission.Starts==0&&Quiet(f.input),"decorative illustration does not start a mission or leak firing");
            }
        }
    }
    static void Previews()
    {
        string output=Path.Combine(Root(),"Validation/Results/V20UI/FrontEnd");Directory.CreateDirectory(output);
        foreach(var section in new[]{IntercomFrontEndSection.Home,IntercomFrontEndSection.Play,IntercomFrontEndSection.Robot,IntercomFrontEndSection.Armory})
        {
            var f=Fresh();Art(f);Service(f);f.Navigate(section);
            Record(f,output,section.ToString().ToLowerInvariant(),1280,720,new Rect(0,0,1280,720));
        }
        var result=Fresh();result.mission.Phase=IntercomMissionPhase.Victory;result.mission.ResultText="Двор очищен. Хорошая работа!";Call(result,"Update");Record(result,output,"victory",1280,720,new Rect(0,0,1280,720));
        result.mission.Phase=IntercomMissionPhase.Defeat;Record(result,output,"defeat",640,360,new Rect(20,12,600,336));
        var portrait=Fresh();Art(portrait);portrait.Navigate(IntercomFrontEndSection.Play);Record(portrait,output,"portrait",480,900,new Rect(0,20,480,860));
        portrait.Navigate(IntercomFrontEndSection.Home);Record(portrait,output,"portrait_home",480,900,new Rect(0,20,480,860));
        var compact=Fresh();Art(compact);compact.Navigate(IntercomFrontEndSection.Play);Record(compact,output,"compact_play",640,360,new Rect(18,8,604,344));
        var upgrade=Fresh();Art(upgrade);var service=Service(upgrade,120);upgrade.Navigate(IntercomFrontEndSection.Armory);
        Record(upgrade,output,"upgrade_affordable",1280,720,new Rect(0,0,1280,720));service.Wallet=12;
        Record(upgrade,output,"upgrade_insufficient",640,360,new Rect(18,8,604,344));service.Wallet=140;service.MagazineLevel=3;service.EffectiveMagazineCapacity=42;service.NextMagazineCost=0;service.NextMagazineCapacity=42;
        Record(upgrade,output,"upgrade_maximum",480,900,new Rect(16,24,448,852));
        var reward=Fresh();Art(reward);var receipt=Service(reward,150);reward.mission.Phase=IntercomMissionPhase.Victory;reward.mission.HasRunSummary=true;reward.mission.LastRunSummary=new IntercomRunSummary{Kills=50,Rescued=0,ElapsedSeconds=183};
        receipt.HasResultReward=true;receipt.ResultReward=new IntercomRunReward{Kills=50,KillParts=50,VictoryParts=20,Total=70,Eligible=true,Won=true};Call(reward,"Update");
        Record(reward,output,"result_reward",1280,720,new Rect(0,0,1280,720));
        Record(reward,output,"result_reward_compact",640,360,new Rect(20,12,600,336));
        Record(reward,output,"result_reward_portrait",480,900,new Rect(18,22,444,856));
    }
    static void Art(IntercomFrontEnd f)
    {
        f.heroPortrait=new Texture2D(768,1024,TextureFormat.RGBA32,false){name="RobotPortrait"};f.weaponPortrait=new Texture2D(512,512,TextureFormat.RGBA32,false){name="PistolPortrait"};
        f.homeDoodle=new Texture2D(1283,1226,TextureFormat.RGBA32,false){name="HomeDoodle"};f.rescueDoodle=new Texture2D(1254,1254,TextureFormat.RGBA32,false){name="RescueDoodle"};
    }
    static void Record(IntercomFrontEnd f,string output,string name,int w,int h,Rect safe)
    {
        Screen.width=w;Screen.height=h;Screen.safeArea=safe;GUI.Record=true;GUI.commands.Clear();GUI.textures.Clear();Call(f,"OnGUI");GUI.Save(Path.Combine(output,name+".json"));GUI.Record=false;
    }
}
