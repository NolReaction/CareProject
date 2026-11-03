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
        Layouts();PointerTransitions();KeyboardAndResults();DisabledDirectorLifecycle();DrawPreviews();
        Console.WriteLine($"PASS {checks} mission menu / active objective / result / safe-area / input transition assertions. Production Input and HUD with narrow Unity and director stand-ins; not Unity Play Mode.");
    }
    static void Layouts()
    {
        foreach(var safe in new[]{new Rect(0,0,1280,720),new Rect(94,21,2152,1038),new Rect(20,12,600,336),new Rect(0,0,320,180),new Rect(0,0,480,900)})
        {
            var l=IntercomHudLayout.Calculate(safe.x,safe.y,safe.width,safe.height,70,32);
            foreach(var b in new[]{l.MissionPanel,l.MissionFirst,l.MissionSecond,l.MissionObjective,l.Modes})
            {
                Check(Inside(b,safe),"mission control outside safe area");
                var centre=Centre(b);Check(b.Contains(centre.x,centre.y),"visible centre must hit");
                Check(!b.Contains(b.X-1,centre.y)&&!b.Contains(centre.x,b.Y+b.Height+1),"outside coordinate accepted");
                Check(!b.Contains(float.NaN,centre.y),"invalid coordinate accepted");
            }
            Check(l.MissionFirst.Y>l.MissionSecond.Y+l.MissionSecond.Height,"mode cards overlap");
            Check(l.Modes.X>l.Health.X+l.Health.Width,"mode button overlaps health");
        }
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
        Check(p.mission.Phase==IntercomMissionPhase.Selection&&p.mission.Selections==1&&NoGameplay(p),"active modes button opens selection and cancels shot");
        Input.mousePosition=Centre(Layout().MissionSecond);Call(p,"Update");Check(p.mission.Starts==1,"drag from modes button selects a new mode");
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
        Call(p,"Update");Check(p.mission.Phase==IntercomMissionPhase.Selection&&NoGameplay(p),"mobile menu while firing did not cancel both pointers");
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
        Input.down.Add(KeyCode.Backspace);Call(p,"Update");Check(p.mission.Retries==1&&NoGameplay(p),"active Backspace did not use mission retry");
        Release(p);Input.down.Add(KeyCode.Escape);Call(p,"Update");Check(p.mission.Selections==1&&NoGameplay(p),"Escape did not use mission selection");
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
        string output=Path.Combine(Root(),"Validation/Results/V9UI/HudPreviews");Directory.CreateDirectory(output);
        Preview(output,"selection",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Selection,IntercomMissionMode.Elimination);
        Preview(output,"elimination",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Elimination);
        Preview(output,"rescue",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Rescue);
        Preview(output,"waiting",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Active,IntercomMissionMode.Rescue,"Союзник ждёт — вернись к нему");
        Preview(output,"victory",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Victory,IntercomMissionMode.Elimination);
        Preview(output,"defeat",1280,720,new Rect(0,0,1280,720),IntercomMissionPhase.Defeat,IntercomMissionMode.Rescue);
        Preview(output,"compact",640,360,new Rect(20,12,600,336),IntercomMissionPhase.Selection,IntercomMissionMode.Elimination);
        Preview(output,"notched",2340,1080,new Rect(94,21,2152,1038),IntercomMissionPhase.Active,IntercomMissionMode.Rescue);
    }
    static void Preview(string output,string name,int width,int height,Rect safe,IntercomMissionPhase phase,IntercomMissionMode mode,string hint="")
    {
        Screen.width=width;Screen.height=height;Screen.safeArea=safe;Application.isMobilePlatform=true;
        var l=Layout();var sticks=new IntercomTwinStickState();sticks.Configure(l.LeftX,l.LeftY,l.RightX,l.RightY,l.Radius,.12f,.2f);
        var mission=new IntercomMissionDirector{Phase=phase,Mode=mode,IsAllyRecruited=true,AllyHealth=new IntercomHealth{Current=42,Maximum=60},ResultText=phase==IntercomMissionPhase.Defeat?"Союзник отключился. Попробуем ещё раз.":"50 ударников отключены. Отличная работа!"};
        mission.HudHint=hint;if(phase==IntercomMissionPhase.Selection)mission.ResultText="";
        var vitality=new IntercomPlayerVitality{IsDead=phase==IntercomMissionPhase.Defeat};
        GUI.commands.Clear();GUI.textures.Clear();GUI.labels.Clear();GUI.Record=true;
        var hud=new IntercomWorkshopHud();hud.Draw(l,sticks,new IntercomWeaponMagazine(),true,.85f,vitality,mission);
        if(phase==IntercomMissionPhase.Selection)Check(GUI.labels.Contains("50 РОБОТОВ")&&GUI.labels.Contains("СПАСЕНИЕ")&&!GUI.labels.Contains("24"),"selection did not replace gameplay HUD");
        if(phase==IntercomMissionPhase.Active)Check(GUI.labels.Contains("РЕЖИМЫ")&&GUI.labels.Contains(mode==IntercomMissionMode.Elimination?"12 / 50":"КОРПУС  42 / 60"),"actual mission objective missing");
        if(phase==IntercomMissionPhase.Defeat)Check(!GUI.labels.Contains("СИСТЕМА ОТКЛЮЧЕНА")&&GUI.labels.Contains("ВЫЕЗД НЕ УДАЛСЯ"),"duplicate legacy defeat overlay");
        Check(GUI.labels.Contains("ВЫБРАТЬ РЕЖИМ")==(phase==IntercomMissionPhase.Victory||phase==IntercomMissionPhase.Defeat),"result selection action missing");
        GUI.Save(Path.Combine(output,name+".json"));GUI.Record=false;hud.Dispose();
    }
}
