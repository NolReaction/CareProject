using System;
using System.IO;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

static class Program
{
    static int checks;
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static void Call(IntercomPlayerInput p,string name)=>typeof(IntercomPlayerInput).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
    static IntercomPlayerInput Fresh(bool mobile=false)
    {
        Input.Clear();Application.isMobilePlatform=mobile;Time.timeScale=1;
        Screen.width=1280;Screen.height=720;Screen.safeArea=new Rect(0,0,1280,720);
        var p=new IntercomPlayerInput{attached=new IntercomWeaponMagazine(),mission=new IntercomMissionDirector()};
        Call(p,"Awake");Call(p,"OnEnable");Call(p,"Update");return p;
    }
    static IntercomHudLayout Layout()=>IntercomHudLayout.Calculate(Screen.safeArea.x,Screen.safeArea.y,Screen.safeArea.width,Screen.safeArea.height,70,32);
    static Vector2 Centre(IntercomHudLayout.Box b)=>new Vector2(b.X+b.Width*.5f,b.Y+b.Height*.5f);
    static Touch Touch(int id,Vector2 p,TouchPhase phase)=>new Touch{fingerId=id,position=p,phase=phase};
    static bool NoGameplay(IntercomPlayerInput p)=>!p.FireHeld&&!p.FirePressed&&!p.ReloadPressed&&!p.ResetPressed&&p.Move.sqrMagnitude==0;
    static bool Inside(IntercomHudLayout.Box b,Rect a)=>b.X>=a.x-.01f&&b.Y>=a.y-.01f&&b.X+b.Width<=a.xMax+.01f&&b.Y+b.Height<=a.yMax+.01f;
    static void Click(IntercomPlayerInput p,IntercomHudLayout.Box b)
    {Input.mousePosition=Centre(b);Input.MouseHeld=Input.MouseDown=true;Call(p,"Update");Input.MouseDown=false;}
    static void Release(IntercomPlayerInput p){Input.Clear();Call(p,"Update");}

    static void Main()
    {
        Layouts();PointerTransitions();KeyboardAndResults();DisabledDirectorLifecycle();MovementAfterCancellation();MissionProgressCache();PauseLifecycle();TimerAndToast();FreePlayUi();MobileReferenceHud();DesktopInstrumentOwnership();DrawPreviews();
        Console.WriteLine($"PASS {checks} mission menu / active objective / result / safe-area / input transition assertions. Production Input and HUD with narrow Unity and director stand-ins; not Unity Play Mode.");
    }
    static void TimerAndToast()
    {
        var hud=new IntercomWorkshopHud();
        var mission=new IntercomMissionDirector{Phase=IntercomMissionPhase.Active,RunRevision=1,ElapsedSeconds=84.9f};
        string time=hud.TimerText(mission);Check(time=="00:01:24","timer uses active mission elapsed seconds");
        bool reused=true;long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++)reused &= ReferenceEquals(time,hud.TimerText(mission));
        Check(reused&&GC.GetAllocatedBytesForCurrentThread()-before==0,"stable timer generated strings during repaint");
        mission.ElapsedSeconds=85;Check(hud.TimerText(mission)=="00:01:25","timer advances on next second");
        Check(hud.SessionToast(mission)=="","opening objective does not remain on screen through a wave");
        mission.HudHint="Союзник ждёт";Check(hud.SessionToast(mission)==mission.HudHint,"changed gameplay hint appears immediately");
        mission.ElapsedSeconds=91;Check(hud.SessionToast(mission)=="","stable hint expires instead of covering gameplay forever");
        mission.RunRevision++;mission.ElapsedSeconds=0;mission.HudHint="";
        Check(hud.TimerText(mission)=="00:00:00"&&hud.SessionToast(mission).Length>0,"new run resets timer and opening objective");
        mission.ElapsedSeconds=5;Check(hud.SessionToast(mission)=="","opening toast expires at five active seconds");
        mission.ElapsedSeconds=float.NaN;Check(hud.TimerText(mission)=="00:00:00","invalid elapsed time does not corrupt HUD");
        hud.Dispose();
    }
    static void PauseLifecycle()
    {
        var p=Fresh();p.mission.Phase=IntercomMissionPhase.Active;Time.timeScale=.4f;
        Input.held.Add(KeyCode.W);Input.MouseHeld=Input.MouseDown=true;Input.mousePosition=new Vector2(900,300);Call(p,"Update");
        Check(p.Move.y>0&&p.FireHeld,"precondition running input");
        Check(p.Pause()&&Time.timeScale==0&&NoGameplay(p),"pause stops scaled simulation and cancels both controls");
        GUI.Record=true;GUI.commands.Clear();GUI.labels.Clear();Call(p,"OnGUI");
        Check(GUI.labels.Contains("ПРОДОЛЖИТЬ")&&GUI.labels.Contains("НАЧАТЬ ЗАНОВО"),"actual pause actions drawn while timeScale is zero");
        bool drawHitMatch=false;var resume=Layout().PauseResume;
        foreach(object command in GUI.commands)
        {
            var type=command.GetType();var label=type.GetProperty("text");
            if(label==null||(string)label.GetValue(command)!="ПРОДОЛЖИТЬ")continue;
            var bounds=(float[])type.GetProperty("rect").GetValue(command);
            drawHitMatch=bounds[0]==resume.X&&bounds[1]==Screen.height-resume.Y-resume.Height&&bounds[2]==resume.Width&&bounds[3]==resume.Height;
        }
        Check(drawHitMatch,"pause draw must use exact same rectangle as pointer hit test");GUI.Record=false;
        Input.MouseDown=false;Call(p,"Update");Check(NoGameplay(p),"held firing pointer cannot operate paused game");
        Release(p);Click(p,Layout().PauseRetry);
        Check(p.ConfirmingRestart&&p.GameplayPaused&&p.mission.Retries==0&&Time.timeScale==0,"restart first asks confirmation");
        Release(p);Input.down.Add(KeyCode.Escape);Call(p,"Update");
        Check(!p.ConfirmingRestart&&p.GameplayPaused&&Time.timeScale==0,"Escape cancels confirmation while keeping pause");
        Release(p);Click(p,Layout().PauseResume);
        Check(!p.GameplayPaused&&Time.timeScale==.4f&&NoGameplay(p),"resume restores previous scale and consumes its pointer");
        Input.MouseDown=false;Input.mousePosition=new Vector2(900,300);Call(p,"Update");Check(!p.FireHeld,"resume drag cannot become a shot");
        Release(p);Input.held.Add(KeyCode.W);Call(p,"Update");Check(p.Move.y>0&&!p.FireHeld,"movement resumes after pause");
        Check(p.Pause(),"second pause acquired");Time.timeScale=.75f;p.Resume();
        Check(Time.timeScale==.75f,"resume must respect another owner's changed time scale");
        Time.timeScale=0;Check(!p.Pause(),"externally paused world cannot be claimed by HUD");p.Resume();Check(Time.timeScale==0,"no lease cannot resume external pause");
        Time.timeScale=.65f;Check(p.Pause(),"disable cleanup lease acquired");Call(p,"OnDisable");
        Check(!p.GameplayPaused&&Time.timeScale==.65f,"disable releases own pause without freezing editor");
        typeof(IntercomPlayerInput).GetMethod("OnApplicationPause",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{true});
        Call(p,"OnEnable");Release(p);Input.held.Add(KeyCode.W);Call(p,"Update");Check(p.Move.y>0,"reenable clears missed application resume state");
        Release(p);Check(p.Pause(),"destroy cleanup lease acquired");Call(p,"OnDestroy");
        Check(!p.GameplayPaused&&Time.timeScale==.65f,"destroy releases own pause");
        p=Fresh();p.mission.Phase=IntercomMissionPhase.Active;Check(p.Pause(),"replacement precondition");
        p.mission=new IntercomMissionDirector();Call(p,"Update");Check(!p.GameplayPaused&&Time.timeScale==1&&NoGameplay(p),"mission replacement releases pause and cancels old input");
        p=Fresh();p.mission.Phase=IntercomMissionPhase.Active;Check(p.Pause(),"external restart precondition");
        p.mission.RetryMission();Call(p,"Update");Check(!p.GameplayPaused&&Time.timeScale==1&&NoGameplay(p),"external new run cannot inherit old pause ownership");
        p=Fresh();p.mission.Phase=IntercomMissionPhase.Active;Check(p.Pause(),"result precondition");
        p.mission.Phase=IntercomMissionPhase.Defeat;Call(p,"Update");Check(!p.GameplayPaused&&Time.timeScale==1,"mission result releases pause ownership");
        p=Fresh(true);p.mission.Phase=IntercomMissionPhase.Active;var l=Layout();
        Input.touches=new[]{Touch(1,new Vector2(l.RightX+l.Radius*.8f,l.RightY),TouchPhase.Began)};Call(p,"Update");Check(p.FireHeld,"touch precondition firing");
        Input.touches=new[]{Touch(1,new Vector2(l.RightX,l.RightY),TouchPhase.Moved),Touch(2,Centre(l.Modes),TouchPhase.Began)};Call(p,"Update");
        Check(p.GameplayPaused&&Time.timeScale==0&&NoGameplay(p),"pause touch while aiming clears every pointer");
        Release(p);Input.touches=new[]{Touch(3,Centre(l.PauseResume),TouchPhase.Began)};Call(p,"Update");
        Check(!p.GameplayPaused&&NoGameplay(p)&&Time.timeScale==1,"fresh touch can resume timeScale zero menu");
        Input.touches=new[]{Touch(3,new Vector2(l.RightX,l.RightY),TouchPhase.Moved)};Call(p,"Update");Check(!p.FireHeld,"resume finger cannot become aiming stick");
        Release(p);Input.touches=new[]{Touch(4,new Vector2(l.RightX+l.Radius*.8f,l.RightY),TouchPhase.Began)};Call(p,"Update");Check(p.FireHeld,"fresh touch after resume can fire");
    }
    static void MissionProgressCache()
    {
        var hud=new IntercomWorkshopHud();
        var mission=new IntercomMissionDirector{Mode=IntercomMissionMode.Elimination,Kills=7,Goal=50};
        string first=hud.MissionProgress(mission);
        Check(first=="7 / 50","kill objective text");
        bool reused=true;
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++) reused &= ReferenceEquals(first,hud.MissionProgress(mission));
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(reused && allocated==0,"stable objective generated strings during repeated HUD passes");
        mission.Kills=8;Check(hud.MissionProgress(mission)=="8 / 50","kill updates immediately");
        mission.Mode=IntercomMissionMode.Rescue;
        Check(hud.MissionProgress(mission)=="СОЮЗНИК ЖДЁТ","mode change invalidates cached kills");
        mission.AllyHealth=new IntercomHealth{Current=42,Maximum=60};
        Check(hud.MissionProgress(mission)=="КОРПУС  42 / 60","ally appearance updates objective");
        mission.AllyHealth.Current=30;Check(hud.MissionProgress(mission)=="КОРПУС  30 / 60","ally damage updates objective");
        mission.AllyHealth=null;Check(hud.MissionProgress(mission)=="СОЮЗНИК ЖДЁТ","ally disappearance invalidates objective");
        mission.Mode=IntercomMissionMode.Elimination;mission.Kills=0;
        Check(hud.MissionProgress(mission)=="0 / 50","retry clears displayed count");
    }
    static void Layouts()
    {
        foreach(var safe in new[]{new Rect(0,0,1280,720),new Rect(94,21,2152,1038),new Rect(20,12,600,336),new Rect(0,0,320,180),new Rect(0,0,480,900)})
        {
            var l=IntercomHudLayout.Calculate(safe.x,safe.y,safe.width,safe.height,70,32);
            foreach(var b in new[]{l.MissionPanel,l.MissionFirst,l.MissionSecond,l.MissionThird,l.MissionObjective,l.Modes})
            {
                Check(Inside(b,safe),"mission control outside safe area");
                var centre=Centre(b);Check(b.Contains(centre.x,centre.y),"visible centre must hit");
                Check(!b.Contains(b.X-1,centre.y)&&!b.Contains(centre.x,b.Y+b.Height+1),"outside coordinate accepted");
                Check(!b.Contains(float.NaN,centre.y),"invalid coordinate accepted");
            }
            Check(l.MissionFirst.Y>l.MissionSecond.Y+l.MissionSecond.Height,"mode cards overlap");
            Check(l.MissionSecond.Y>l.MissionThird.Y+l.MissionThird.Height,"free mode card overlaps rescue");
            Check(l.Health.X>l.Modes.X+l.Modes.Width,"health must sit beside the upper-left pause target");
        }
    }
    static object Property(object command,string name)=>command.GetType().GetProperty(name)?.GetValue(command);
    static bool LabelInside(string text,IntercomHudLayout.Box bounds)
    {
        foreach(object command in GUI.commands)
        {
            if((string)Property(command,"text")!=text)continue;
            var r=(float[])Property(command,"rect");
            return r[0]>=bounds.X-.01f&&r[1]>=Screen.height-bounds.Y-bounds.Height-.01f
                &&r[0]+r[2]<=bounds.X+bounds.Width+.01f&&r[1]+r[3]<=Screen.height-bounds.Y+.01f;
        }
        return false;
    }
    static void MobileReferenceHud()
    {
        // The reference puts information up top and reserves both bottom corners for thumbs.
        // Exercise the real Input -> HUD draw and pointer path in a small, notched play window.
        var p=Fresh(true);Screen.width=640;Screen.height=360;Screen.safeArea=new Rect(20,12,600,336);
        p.mission.Phase=IntercomMissionPhase.Active;p.vitality=new IntercomPlayerVitality();p.vitality.Health.Current=37;
        Release(p);var l=Layout();GUI.commands.Clear();GUI.labels.Clear();GUI.Record=true;Call(p,"OnGUI");GUI.Record=false;
        Check(LabelInside("37 / 100",l.Health),"actual health label ignored new upper-left gauge");
        Check(LabelInside("24",l.Reload)&&LabelInside("ПЕРЕЗАРЯДИТЬ",l.Reload),"actual ammo/reload must draw inside upper-right input target");
        Check(LabelInside("12 / 50",l.MissionObjective),"actual objective must use compact secondary row");
        Check(GUI.textures.ContainsKey("Ink heart power core"),"cartoon repair-heart artwork missing from real renderer");
        var move=new Vector2(l.LeftX+l.Radius*.8f,l.LeftY);
        var aim=new Vector2(l.RightX+l.Radius*.8f,l.RightY);
        Input.touches=new[]{Touch(1,move,TouchPhase.Began),Touch(2,Centre(l.Reload),TouchPhase.Began)};Call(p,"Update");
        Check(p.Move.x>0&&p.ReloadPressed&&!p.FireHeld&&!p.FirePressed,"upper-right reload press stole movement or fired");
        Input.touches=new[]{Touch(1,move,TouchPhase.Moved),Touch(2,aim,TouchPhase.Moved)};Call(p,"Update");
        Check(p.Move.x>0&&!p.FireHeld,"dragging reload sticker into aim region recaptured a shot");
        Input.touches=new[]{Touch(1,move,TouchPhase.Moved),Touch(2,aim,TouchPhase.Ended)};Call(p,"Update");
        Input.touches=new[]{Touch(1,move,TouchPhase.Moved),Touch(2,aim,TouchPhase.Began)};Call(p,"Update");
        Check(p.Move.x>0&&p.FireHeld,"fresh aim touch after sticker release cannot fire");
        Input.touches=new[]{Touch(1,move,TouchPhase.Moved),Touch(2,aim,TouchPhase.Moved),Touch(3,Centre(l.Modes),TouchPhase.Began)};Call(p,"Update");
        Check(p.GameplayPaused&&Time.timeScale==0&&NoGameplay(p),"upper-left pause cannot cancel simultaneous move/aim");
        Release(p);Input.touches=new[]{Touch(4,Centre(l.PauseResume),TouchPhase.Began)};Call(p,"Update");
        Check(!p.GameplayPaused&&Time.timeScale==1&&!p.FireHeld&&!p.FirePressed,"fresh resume touch leaked into shooting");
        Release(p);
    }
    static void DesktopInstrumentOwnership()
    {
        foreach(string instrument in new[]{"health","objective","timer","toast"})
        {
            var p=Fresh();p.mission.Phase=IntercomMissionPhase.Active;p.vitality=new IntercomPlayerVitality();Release(p);
            var l=Layout();var box=instrument=="health" ? l.Health : instrument=="objective" ? l.MissionObjective : instrument=="timer" ? l.Timer : l.Toast;
            Input.held.Add(KeyCode.W);Click(p,box);
            Check(p.Move.y>0&&!p.FireHeld&&!p.FirePressed&&!p.ReloadPressed,"passive "+instrument+" click must consume shooting but preserve keyboard movement");
            Input.MouseDown=false;Input.mousePosition=new Vector2(900,300);Call(p,"Update");
            Check(p.Move.y>0&&!p.FireHeld&&!p.FirePressed,"dragging passive "+instrument+" into play acquired a shot");
            Release(p);Input.MouseHeld=Input.MouseDown=true;Input.mousePosition=new Vector2(900,300);Call(p,"Update");
            Check(p.FireHeld&&p.FirePressed,"fresh click after "+instrument+" release cannot shoot");
        }
        var hidden=Fresh();hidden.mission.Phase=IntercomMissionPhase.Active;hidden.mission.ElapsedSeconds=20;Release(hidden);
        Click(hidden,Layout().Toast);Check(hidden.FireHeld,"expired toast must not leave an invisible click blocker");
    }
    static void FreePlayUi()
    {
        var p=Fresh();GUI.labels.Clear();Call(p,"OnGUI");
        Check(GUI.labels.Contains("СВОБОДНЫЙ РЕЖИМ"),"free card is visible in actual HUD");
        Click(p,Layout().MissionThird);
        Check(p.mission.Mode==IntercomMissionMode.FreePlay&&p.mission.Starts==1&&NoGameplay(p),"third card starts free without firing");
        Release(p);GUI.labels.Clear();Call(p,"OnGUI");
        Check(GUI.labels.Contains("СВОБОДНАЯ ТРЕНИРОВКА")&&GUI.labels.Contains("БЕЗ ВРАГОВ"),"free HUD contains no fake kill objective");
        Check(!GUI.labels.Contains("0 / 50"),"free HUD does not show kill count");
        Release(p);Click(p,Layout().Modes);
        Check(p.GameplayPaused&&p.mission.Phase==IntercomMissionPhase.Active&&Time.timeScale==0,"free mode pauses without abandoning the run");p.Resume();
        p=Fresh(true);var l=Layout();Input.touches=new[]{Touch(81,Centre(l.MissionThird),TouchPhase.Began)};Call(p,"Update");
        Check(p.mission.Mode==IntercomMissionMode.FreePlay&&NoGameplay(p),"touch selects free mode with no shot leakage");
        p=Fresh();p.mission.Phase=IntercomMissionPhase.Defeat;Click(p,Layout().MissionThird);
        Check(p.mission.Mode==IntercomMissionMode.FreePlay&&p.mission.Starts==1,"result menu offers direct free play");
    }
    static void PointerTransitions()
    {
        var p=Fresh();Input.held.Add(KeyCode.W);Input.MouseHeld=Input.MouseDown=true;Input.mousePosition=new Vector2(15,350);Call(p,"Update");
        Check(NoGameplay(p)&&p.mission.Starts==0,"modal outside press or WASD leaks into gameplay");
        Release(p);Click(p,Layout().MissionFirst);
        Check(p.mission.Starts==1&&p.mission.Mode==IntercomMissionMode.Elimination&&NoGameplay(p),"first card starts elimination without shooting");
        Input.mousePosition=new Vector2(900,300);Call(p,"Update");Check(NoGameplay(p),"start click drag becomes fire");
        Release(p);Input.MouseHeld=Input.MouseDown=true;Call(p,"Update");Check(p.FireHeld&&p.FirePressed,"fresh gameplay press works after menu");
        Release(p);Click(p,Layout().Modes);
        Check(p.GameplayPaused&&p.mission.Phase==IntercomMissionPhase.Active&&p.mission.Selections==0&&NoGameplay(p),"active pause button freezes run and cancels shot");
        Input.mousePosition=Centre(Layout().MissionSecond);Call(p,"Update");Check(p.mission.Starts==1,"drag from modes button selects a new mode");
        Release(p);Click(p,Layout().PauseExit);Check(p.ConfirmingExit,"exit needs confirmation");
        Release(p);Click(p,Layout().PauseConfirm);Check(p.mission.Phase==IntercomMissionPhase.Selection&&Time.timeScale==1,"confirmed exit returns to mode selection");
        Release(p);Click(p,Layout().MissionSecond);
        Check(p.mission.Mode==IntercomMissionMode.Rescue&&p.mission.Starts==2&&NoGameplay(p),"second card selects rescue");

        p=Fresh(true);var l=Layout();
        Input.touches=new[]{Touch(1,Centre(l.MissionSecond),TouchPhase.Began),Touch(2,new Vector2(l.RightX+l.Radius*.8f,l.RightY),TouchPhase.Began)};
        Call(p,"Update");Check(p.mission.Mode==IntercomMissionMode.Rescue&&p.mission.Starts==1&&NoGameplay(p),"multi-touch menu start leaked second finger");
        Input.touches=new[]{Touch(2,new Vector2(l.RightX+l.Radius*.8f,l.RightY),TouchPhase.Moved)};
        Call(p,"Update");Check(NoGameplay(p),"held second finger reacquired aim after mode start");
        Release(p);Input.touches=new[]{Touch(2,new Vector2(l.RightX+l.Radius*.8f,l.RightY),TouchPhase.Began)};
        Call(p,"Update");Check(p.FireHeld,"released and reused finger can fire");
        Input.touches=new[]{Touch(2,new Vector2(l.RightX+l.Radius*.8f,l.RightY),TouchPhase.Moved),Touch(3,Centre(l.Modes),TouchPhase.Began)};
        Call(p,"Update");Check(p.GameplayPaused&&p.mission.Phase==IntercomMissionPhase.Active&&NoGameplay(p),"mobile pause while firing did not cancel both pointers");
        p.Resume();p.mission.ReturnToSelection();
        Release(p);Input.touches=new[]{Touch(5,Centre(l.MissionFirst),TouchPhase.Canceled)};Call(p,"Update");
        Check(p.mission.Starts==1&&NoGameplay(p),"cancelled touch started mission");
        Release(p);p.CancelUntilRelease();Input.touches=new[]{Touch(6,Centre(l.MissionFirst),TouchPhase.Began)};Call(p,"Update");
        Check(p.mission.Starts==1,"held touch after focus gate restarted mission");
        Release(p);Time.timeScale=0;Input.touches=new[]{Touch(7,Centre(l.MissionFirst),TouchPhase.Began)};Call(p,"Update");
        Check(p.mission.Starts==1&&NoGameplay(p),"paused selection accepted input");Time.timeScale=1;
    }
    static void KeyboardAndResults()
    {
        var p=Fresh();p.mission.Phase=IntercomMissionPhase.Active;
        Input.down.Add(KeyCode.Backspace);Call(p,"Update");Check(p.GameplayPaused&&p.ConfirmingRestart&&p.mission.Retries==0&&NoGameplay(p),"active Backspace must confirm before restart");
        Release(p);Click(p,Layout().PauseConfirm);Check(p.mission.Retries==1&&!p.GameplayPaused,"confirmed retry restarts");
        Release(p);Input.down.Add(KeyCode.Escape);Call(p,"Update");Check(p.GameplayPaused&&p.mission.Selections==0&&NoGameplay(p),"Escape must pause current run");
        Release(p);Click(p,Layout().PauseExit);Release(p);Click(p,Layout().PauseConfirm);Check(p.mission.Selections==1,"confirmed exit uses mission selection");
        Release(p);Input.down.Add(KeyCode.Backspace);Call(p,"Update");Check(p.mission.Retries==1,"selection Backspace starts old mission");
        Release(p);p.mission.Phase=IntercomMissionPhase.Defeat;p.vitality=new IntercomPlayerVitality{IsDead=true};
        Click(p,Layout().MissionFirst);Check(p.mission.Retries==2&&p.vitality.Retries==0&&NoGameplay(p),"defeat must retry director, not hero alone");
        Release(p);p.mission.Phase=IntercomMissionPhase.Victory;Click(p,Layout().MissionSecond);
        Check(p.mission.Selections==2&&NoGameplay(p),"victory return-to-selection failed");
        Release(p);p.mission.Phase=IntercomMissionPhase.Active;Call(p,"Update");
        Check(NoGameplay(p),"one-frame dead hero before director defeat was allowed to shoot");
        p.mission.HudHint="Союзник ждёт — вернись к нему";GUI.labels.Clear();Call(p,"OnGUI");
        Check(GUI.labels.Contains(p.mission.HudHint),"waiting or spawn blocked hint is hidden");
        p.mission.Phase=IntercomMissionPhase.Selection;p.mission.ResultText="Навигация двора ещё не готова";
        GUI.labels.Clear();Call(p,"OnGUI");
        Check(GUI.labels.Contains(p.mission.ResultText),"mission setup failure is hidden in selection");
    }
    static void MovementAfterCancellation()
    {
        var p=Fresh();p.mission.Phase=IntercomMissionPhase.Active;
        p.CancelUntilRelease();Input.held.Add(KeyCode.W);
        Input.MouseHeld=true;Input.mousePosition=new Vector2(900,300);
        Call(p,"Update");
        Check(p.Move.y>0&&!p.FireHeld&&!p.FirePressed,"held movement after cancellation must not be blocked by a stale firing pointer");
        Input.MouseHeld=false;Call(p,"Update");Call(p,"Update");
        Check(p.Move.y>0&&!p.FireHeld,"W held across menu/focus must resume without a neutral keyboard frame");
        Input.MouseHeld=Input.MouseDown=true;Call(p,"Update");
        Check(p.Move.y>0&&p.FireHeld,"fresh shot after release remains independent from moving");
        p.vitality=new IntercomPlayerVitality{IsDead=true};p.CancelUntilRelease();Call(p,"Update");
        Check(NoGameplay(p),"dead hero cannot move through the cancellation gate");
        p=Fresh();p.CancelUntilRelease();Input.held.Add(KeyCode.W);
        Call(p,"Update");Click(p,Layout().MissionFirst);
        Check(p.mission.Starts==1&&!p.FireHeld,"movement key held in selection must not prevent a fresh mode click");
        Input.MouseDown=false;Input.MouseHeld=false;Call(p,"Update");Call(p,"Update");
        Check(p.Move.y>0,"holding W during mode selection must move after start");
    }
    static string Root()
    {
        for(var d=new DirectoryInfo(Directory.GetCurrentDirectory());d!=null;d=d.Parent)
            if(Directory.Exists(Path.Combine(d.FullName,"Assets/IntercomMVP")))return d.FullName;
        throw new DirectoryNotFoundException();
    }
    static void DisabledDirectorLifecycle()
    {
        var p=Fresh();p.mission.isActiveAndEnabled=false;
        Release(p);Release(p);
        Input.held.Add(KeyCode.W);Input.MouseHeld=Input.MouseDown=true;Input.mousePosition=new Vector2(900,300);Call(p,"Update");
        Check(p.Move.y>0&&p.FireHeld,"disabled director still blocks gameplay");
        GUI.labels.Clear();Call(p,"OnGUI");
        Check(GUI.labels.Contains("24")&&!GUI.labels.Contains("СЕРВИСНЫЙ ВЫЕЗД"),"disabled director still draws modal selection");
        p.vitality=new IntercomPlayerVitality{IsDead=true};Release(p);
        GUI.labels.Clear();Call(p,"OnGUI");
        Check(GUI.labels.Contains("СИСТЕМА ОТКЛЮЧЕНА")&&!GUI.labels.Contains("ВЫЕЗД НЕ УДАЛСЯ"),"disabled director suppresses legacy defeat HUD");
        Input.down.Add(KeyCode.Backspace);Call(p,"Update");
        Check(p.vitality.Retries==1&&p.mission.Retries==0,"disabled director intercepts legacy retry");
        Release(p);Input.mousePosition=Centre(Layout().MissionFirst);Input.MouseHeld=Input.MouseDown=true;
        p.mission.isActiveAndEnabled=true;Call(p,"Update");
        Check(p.mission.Starts==0&&NoGameplay(p),"reenabled director uses previous held press to start a mission");
        Input.MouseDown=false;Call(p,"Update");
        Check(p.mission.Starts==0&&NoGameplay(p),"reenabled selection leaks held input");
        Release(p);Click(p,Layout().MissionFirst);
        Check(p.mission.Starts==1&&NoGameplay(p),"fresh press after reenable cannot select mode");
    }
    static void DrawPreviews()
    {
        string output=Path.Combine(Root(),"Validation/Results/V19UI/MissionPreviews");Directory.CreateDirectory(output);
        Preview(output,"selection",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Selection,IntercomMissionMode.Elimination);
        Preview(output,"elimination",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Elimination);
        Preview(output,"opening",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Elimination,elapsed:0f);
        Preview(output,"pause",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Elimination,gameplayPaused:true);
        Preview(output,"confirm_exit",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Elimination,gameplayPaused:true,confirmingExit:true);
        Preview(output,"confirm_restart",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Elimination,gameplayPaused:true,confirmingRestart:true);
        Preview(output,"portrait_pause",480,900,new Rect(18,22,444,856),IntercomMissionPhase.Active,IntercomMissionMode.Rescue,gameplayPaused:true);
        Preview(output,"portrait_active",480,900,new Rect(18,22,444,856),IntercomMissionPhase.Active,IntercomMissionMode.Elimination);
        Preview(output,"compact_active",640,360,new Rect(20,12,600,336),IntercomMissionPhase.Active,IntercomMissionMode.Elimination);
        Preview(output,"low_ammo",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Elimination,ammo:3,health:24);
        Preview(output,"reloading",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Elimination,ammo:0,loading:true);
        Preview(output,"damaged",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Elimination,health:62,hurt:true);
        Preview(output,"rescue",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Rescue);
        Preview(output,"free",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.FreePlay);
        Preview(output,"waiting",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Rescue,"Союзник ждёт — вернись к нему");
        Preview(output,"victory",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Victory,IntercomMissionMode.Elimination);
        Preview(output,"defeat",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Defeat,IntercomMissionMode.Rescue);
        Preview(output,"compact",640,360,new Rect(20,12,600,336),IntercomMissionPhase.Selection,IntercomMissionMode.Elimination);
        Preview(output,"notched",2340,1080,new Rect(94,21,2152,1038),IntercomMissionPhase.Active,IntercomMissionMode.Rescue);
    }
    static void Preview(string output,string name,int width,int height,Rect safe,IntercomMissionPhase phase,IntercomMissionMode mode,string hint="",bool gameplayPaused=false,bool confirmingExit=false,bool confirmingRestart=false,float elapsed=84f,int ammo=24,bool loading=false,float health=100f,bool hurt=false)
    {
        Screen.width=width;Screen.height=height;Screen.safeArea=safe;Application.isMobilePlatform=true;
        var l=Layout();var sticks=new IntercomTwinStickState();sticks.Configure(l.LeftX,l.LeftY,l.RightX,l.RightY,l.Radius,.12f,.2f);
        var mission=new IntercomMissionDirector{Phase=phase,Mode=mode,RunRevision=1,ElapsedSeconds=elapsed,IsAllyRecruited=true,AllyHealth=new IntercomHealth{Current=42,Maximum=60},ResultText=phase==IntercomMissionPhase.Defeat?"Союзник отключился. Попробуем ещё раз.":"50 ударников отключены. Отличная работа!"};
        mission.HudHint=hint;if(phase==IntercomMissionPhase.Selection)mission.ResultText="";
        var vitality=new IntercomPlayerVitality{IsDead=phase==IntercomMissionPhase.Defeat};
        vitality.Health.Current=health;vitality.ShowHitRemaining=hurt ? .15f : 0f;
        GUI.commands.Clear();GUI.textures.Clear();GUI.labels.Clear();GUI.Record=true;
        var portrait=new Texture2D(1,1,TextureFormat.RGBA32,false){name="PistolPortrait"};
        var hud=new IntercomWorkshopHud();hud.Draw(l,sticks,new IntercomWeaponMagazine{CurrentAmmo=ammo,IsReloading=loading,ReloadProgress=.42f},true,.85f,vitality,mission,false,gameplayPaused,confirmingExit,confirmingRestart,portrait);
        if(phase==IntercomMissionPhase.Selection)Check(GUI.labels.Contains("50 РОБОТОВ")&&GUI.labels.Contains("СПАСЕНИЕ")&&!GUI.labels.Contains("24"),"selection did not replace gameplay HUD");
        if(phase==IntercomMissionPhase.Active)Check(GUI.labels.Contains(elapsed==0 ? "00:00:00" : "00:01:24")&&GUI.labels.Contains(mode==IntercomMissionMode.FreePlay?"БЕЗ ВРАГОВ":mode==IntercomMissionMode.Elimination?"12 / 50":"КОРПУС  42 / 60"),"actual mission objective missing");
        if(phase==IntercomMissionPhase.Defeat)Check(!GUI.labels.Contains("СИСТЕМА ОТКЛЮЧЕНА")&&GUI.labels.Contains("ВЫЕЗД НЕ УДАЛСЯ"),"duplicate legacy defeat overlay");
        Check(GUI.labels.Contains("ВЫБРАТЬ РЕЖИМ")==(phase==IntercomMissionPhase.Victory||phase==IntercomMissionPhase.Defeat||(gameplayPaused&&!confirmingExit&&!confirmingRestart)),"result selection action missing");
        GUI.Save(Path.Combine(output,name+".json"));GUI.Record=false;hud.Dispose();
    }
}
