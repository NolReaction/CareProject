using System;
using System.IO;
using System.Reflection;
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
        Registration();NavigationAndModes();MultitouchAndFocus();Results();View();Previews();
        Console.WriteLine($"PASS {assertions} actual front-end registration, navigation, rendering and input-cancellation assertions. Minimal Unity/director substitutes; no Unity Play Mode.");
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
        Release(f);Input.down.Add(KeyCode.Escape);Call(f.input,"Update");Call(f,"Update");Check(f.IsVisible&&f.Section==IntercomFrontEndSection.Home&&Quiet(f.input),"game Escape returns to full home");
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
        f.mission.Phase=IntercomMissionPhase.Active;Call(f,"Update");GUI.labels.Clear();Call(f.input,"OnGUI");Check(GUI.labels.Contains("МЕНЮ")&&!GUI.labels.Contains("ЗАБОТА"),"active HUD restored and menu button relabelled");
    }
    static string Root(){for(var d=new DirectoryInfo(Directory.GetCurrentDirectory());d!=null;d=d.Parent)if(Directory.Exists(Path.Combine(d.FullName,"Assets/IntercomMVP")))return d.FullName;throw new DirectoryNotFoundException();}
    static void Previews()
    {
        string output=Path.Combine(Root(),"Validation/Results/V15UI/FrontEnd");Directory.CreateDirectory(output);
        foreach(var section in new[]{IntercomFrontEndSection.Home,IntercomFrontEndSection.Play,IntercomFrontEndSection.Robot,IntercomFrontEndSection.Armory})
        {
            var f=Fresh();f.heroPortrait=new Texture2D(768,1024,TextureFormat.RGBA32,false){name="RobotPortrait"};f.weaponPortrait=new Texture2D(512,512,TextureFormat.RGBA32,false){name="PistolPortrait"};f.Navigate(section);
            Record(f,output,section.ToString().ToLowerInvariant(),1280,720,new Rect(0,0,1280,720));
        }
        var result=Fresh();result.mission.Phase=IntercomMissionPhase.Victory;result.mission.ResultText="Двор очищен. Хорошая работа!";Call(result,"Update");Record(result,output,"victory",1280,720,new Rect(0,0,1280,720));
        result.mission.Phase=IntercomMissionPhase.Defeat;Record(result,output,"defeat",640,360,new Rect(20,12,600,336));
        var portrait=Fresh();portrait.Navigate(IntercomFrontEndSection.Play);Record(portrait,output,"portrait",480,900,new Rect(0,20,480,860));
    }
    static void Record(IntercomFrontEnd f,string output,string name,int w,int h,Rect safe)
    {
        Screen.width=w;Screen.height=h;Screen.safeArea=safe;GUI.Record=true;GUI.commands.Clear();GUI.textures.Clear();Call(f,"OnGUI");GUI.Save(Path.Combine(output,name+".json"));GUI.Record=false;
    }
}
