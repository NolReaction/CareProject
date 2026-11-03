using System;
using System.IO;
using IntercomMvp;
using UnityEngine;

static class Program
{
    static int assertions;
    static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    static bool Inside(IntercomHudLayout.Box b, float x, float y, float width, float height)
        => b.X >= x && b.Y >= y && b.X+b.Width <= x+width && b.Y+b.Height <= y+height;
    static void Main()
    {
        float[][] areas = {
            new float[]{0,0,1280,720}, new float[]{0,0,1920,1080},
            new float[]{94,21,2152,1038}, new float[]{0,0,640,360},
            new float[]{44,18,552,324}, new float[]{0,0,320,180},
            new float[]{0,0,480,900}, new float[]{60,20,1800,1040}
        };
        foreach (var a in areas)
            foreach (float radius in new[]{48f,70f,110f})
                foreach (float margin in new[]{16f,32f,80f})
                {
                    var layout = IntercomHudLayout.Calculate(a[0],a[1],a[2],a[3],radius,margin);
                    Check(Inside(layout.Reload,a[0],a[1],a[2],a[3]), "reload escaped safe area");
                    Check(Inside(layout.Health,a[0],a[1],a[2],a[3]), "health escaped safe area");
                    Check(Inside(layout.Defeat,a[0],a[1],a[2],a[3]), "defeat escaped safe area");
                    Check(Inside(layout.Retry,layout.Defeat.X,layout.Defeat.Y,layout.Defeat.Width,layout.Defeat.Height), "retry escaped defeat panel");
                    var retry = new IntercomReloadControl();var rb=layout.Retry;retry.Configure(rb.X,rb.Y,rb.Width,rb.Height);
                    Check(retry.PointerDown(9,rb.X+rb.Width*.5f,rb.Y+rb.Height*.5f),"retry visible centre misses input");
                    if (layout.ShowAreaBadge) Check(Inside(layout.AreaBadge,a[0],a[1],a[2],a[3]), "badge escaped safe area");
                    Check(layout.Reload.Y > layout.RightY + layout.Radius*1.3f, "reload overlaps stick capture radius");
                    Check(layout.LeftX-layout.Radius*1.3f >= a[0]-.001f && layout.RightX+layout.Radius*1.3f <= a[0]+a[2]+.001f, "stick captures notch");
                    Check(layout.LeftY-layout.Radius*1.3f >= a[1]-.001f, "stick captures home indicator");
                    Check(layout.RightX-layout.LeftX > layout.Radius*2.6f, "stick capture regions overlap");
                    var sticks=new IntercomTwinStickState();
                    sticks.Configure(layout.LeftX,layout.LeftY,layout.RightX,layout.RightY,layout.Radius,.12f,.2f);
                    var button=new IntercomReloadControl();var b=layout.Reload;
                    button.Configure(b.X,b.Y,b.Width,b.Height);
                    Check(button.PointerDown(11,b.X+b.Width*.5f,b.Y+b.Height*.5f), "visible button misses input");
                    Check(!sticks.PointerDown(12,b.X+b.Width*.5f,b.Y+b.Height*.5f), "reload centre also captures stick");
                    Check(!button.PointerDown(12,layout.RightX,layout.RightY), "stick centre also captures reload");
                }
        // Actual renderer draw commands are exported for an offline layout preview.
        string output=Path.Combine(ProjectRoot(),"Validation/Results/V8UI/HudPreviews");Directory.CreateDirectory(output);
        Preview(output,"landscape",1280,720,new Rect(0,0,1280,720),24,false,false);
        Preview(output,"notched",2340,1080,new Rect(94,21,2152,1038),8,true,true);
        Preview(output,"compact",640,360,new Rect(20,12,600,336),4,false,true);
        Preview(output,"defeat",1280,720,new Rect(0,0,1280,720),0,false,false,true);
        Console.WriteLine("PASS "+assertions+" safe-area / pointer-boundary / HUD state assertions. Offline previews use actual HUD draw commands; no Unity renderer or device run.");
    }
    static string ProjectRoot()
    {
        foreach(string start in new[]{Directory.GetCurrentDirectory(),AppContext.BaseDirectory})
            for(var directory=new DirectoryInfo(start);directory!=null;directory=directory.Parent)
                if(Directory.Exists(Path.Combine(directory.FullName,"Assets/IntercomMVP"))) return directory.FullName;
        throw new DirectoryNotFoundException("CareProject root not found");
    }
    static void Preview(string output,string name,int width,int height,Rect safe,int ammo,bool reloading,bool active,bool dead=false)
    {
        Screen.width=width;Screen.height=height;Application.isMobilePlatform=true;
        var l=IntercomHudLayout.Calculate(safe.x,safe.y,safe.width,safe.height,70,32);
        var sticks=new IntercomTwinStickState();sticks.Configure(l.LeftX,l.LeftY,l.RightX,l.RightY,l.Radius,.12f,.2f);
        if(active){sticks.PointerDown(1,l.LeftX+l.Radius*.45f,l.LeftY+l.Radius*.65f);sticks.PointerDown(2,l.RightX+l.Radius*.7f,l.RightY+l.Radius*.3f);}
        var magazine=new IntercomWeaponMagazine{CurrentAmmo=ammo,IsReloading=reloading,ReloadProgress=.46f};
        var vitality=new IntercomPlayerVitality{IsDead=dead};vitality.Health.Current=dead?0:active?64:100;
        GUI.commands.Clear();GUI.textures.Clear();GUI.labels.Clear();GUI.Record=true;
        var hud=new IntercomWorkshopHud();hud.Draw(l,sticks,magazine,true,.85f,vitality);
        Check(GUI.labels.Contains(vitality.Health.Current+" / 100"),"actual health missing");
        if(dead)Check(GUI.labels.Contains("ПОПРОБОВАТЬ СНОВА")&&!GUI.labels.Contains("∞"),"defeat must replace weaponcontrols");
        else {
            Check(GUI.labels.Contains(ammo.ToString("00")),"actual ammo missing");
            Check(GUI.labels.Contains("∞"),"infinite reserve missing");
            Check(GUI.labels.Contains(reloading?"ПЕРЕЗАРЯДКА":"ПЕРЕЗАРЯДИТЬ"),"actual reload state missing");
        }
        Check(GUI.textures.Count>=3&&GUI.textures.Count<=5,"unexpected texture count");
        var plate=GUI.textures["Workshop plate"];
        hud.Draw(l,sticks,magazine,true,.85f,vitality);
        Check(ReferenceEquals(plate,GUI.textures["Workshop plate"]),"textures rebuilt each repaint");
        // Clear the duplicate redraw before recording the final frame.
        GUI.commands.Clear();hud.Draw(l,sticks,magazine,true,.85f,vitality);
        GUI.Save(Path.Combine(output,name+".json"));GUI.Record=false;hud.Dispose();
    }
}
