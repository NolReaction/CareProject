using System;
using System.IO;
using IntercomMvp;
using UnityEngine;

static class Program
{
    static int assertions;
    static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    static bool Inside(IntercomHudLayout.Box b, float x, float y, float width, float height)
        => b.Width > 0f && b.Height > 0f && b.X >= x-.001f && b.Y >= y-.001f && b.X+b.Width <= x+width+.001f && b.Y+b.Height <= y+height+.001f;
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
                    Check(layout.Health.Y > layout.LeftY + layout.Radius*1.3f, "hull overlaps movement capture radius");
                    Check(layout.Modes.X < layout.Health.X && layout.Health.X < layout.Timer.X, "phone pause and hull order changed");
                    Check(layout.Reload.X > layout.Timer.X, "weapon no longer sits on upper right");
                    if(a[2]>=360f && a[3]>=300f) Check(layout.Modes.Width>=44f && layout.Modes.Height>=44f,"pause touch target below44px on normalphone");
                    Check(layout.MissionObjective.Y+layout.MissionObjective.Height <= layout.Health.Y+layout.Health.Height, "objective should sit below the health row");
                    VerifyLayout(layout,a[0],a[1],a[2],a[3]);
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
        // Vary the safe area independently of orientation and physical screen
        // size; this catches notch offsets and control size extremes together.
        var random = new Random(0x16);
        for (int i=0; i<1200; i++)
        {
            float x=random.Next(0,150), y=random.Next(0,80);
            float width=random.Next(160,2600), height=random.Next(120,1600);
            var layout=IntercomHudLayout.Calculate(x,y,width,height,i%2==0?48:110,i%3==0?80:16);
            VerifyLayout(layout,x,y,width,height);
        }
        foreach (var area in new[]{new[]{1f,1f},new[]{20f,200f},new[]{200f,20f}})
            VerifyLayout(IntercomHudLayout.Calculate(2,3,area[0],area[1],110,80),2,3,area[0],area[1]);
        bool rejected=false;
        try { IntercomHudLayout.Calculate(0,0,float.NaN,720,70,32); } catch(ArgumentOutOfRangeException) { rejected=true; }
        Check(rejected,"invalid safe area must be rejected before it reaches GUI");
        // Actual renderer draw commands are exported for an offline layout preview.
        string output=Path.Combine(ProjectRoot(),"Validation/Results/V19UI/HudPreviews");Directory.CreateDirectory(output);
        Preview(output,"landscape",1280,720,new Rect(0,0,1280,720),24,false,false);
        Preview(output,"notched",2340,1080,new Rect(94,21,2152,1038),8,true,true);
        Preview(output,"compact",640,360,new Rect(20,12,600,336),4,false,true);
        Preview(output,"portrait",480,900,new Rect(16,24,448,846),16,false,true);
        Preview(output,"small_window",320,180,new Rect(0,0,320,180),3,true,true);
        Preview(output,"defeat",1280,720,new Rect(0,0,1280,720),0,false,false,true);
        Console.WriteLine("PASS "+assertions+" safe-area / pointer-boundary / HUD state assertions. Offline previews use actual HUD draw commands; no Unity renderer or device run.");
    }
    static bool Overlap(IntercomHudLayout.Box a,IntercomHudLayout.Box b)
        => a.X < b.X+b.Width-.001f && a.X+a.Width > b.X+.001f && a.Y < b.Y+b.Height-.001f && a.Y+a.Height > b.Y+.001f;
    static bool HitsCapture(IntercomHudLayout.Box b,float cx,float cy,float radius)
    {
        float x=Math.Max(b.X,Math.Min(cx,b.X+b.Width)), y=Math.Max(b.Y,Math.Min(cy,b.Y+b.Height));
        return (x-cx)*(x-cx)+(y-cy)*(y-cy)<radius*radius-.001f;
    }
    static void VerifyLayout(IntercomHudLayout l,float x,float y,float width,float height)
    {
        foreach(var box in new[]{l.Reload,l.Health,l.MissionObjective,l.Timer,l.Modes,l.Toast,l.PausePanel,l.Defeat,l.MissionPanel})
            Check(Inside(box,x,y,width,height),"HUD instrument escaped safe area");
        foreach(var box in new[]{l.PauseResume,l.PauseRetry,l.PauseExit,l.PauseConfirm,l.PauseCancel})
        {
            Check(Inside(box,l.PausePanel.X,l.PausePanel.Y,l.PausePanel.Width,l.PausePanel.Height),"pause action escaped modal");
            var hit=new IntercomReloadControl();hit.Configure(box.X,box.Y,box.Width,box.Height);
            float epsilon=Math.Min(box.Width,box.Height)*.001f;
            Check(hit.PointerDown(1,box.X+epsilon,box.Y+epsilon),"visible action bottom-left misses input");hit.Reset();
            Check(hit.PointerDown(2,box.X+box.Width-epsilon,box.Y+box.Height-epsilon),"visible action top-right misses input");hit.Reset();
            Check(!hit.PointerDown(3,box.X-epsilon,box.Y+box.Height*.5f),"invisible action left margin captures");
            Check(!hit.PointerDown(4,box.X+box.Width+epsilon,box.Y+box.Height*.5f),"invisible action right margin captures");
        }
        Check(!Overlap(l.Health,l.Reload),"weapon overlaps hull strip");
        var instruments=new[]{l.Modes,l.Health,l.Timer,l.Reload,l.MissionObjective};
        for(int i=0;i<instruments.Length;i++)
            for(int j=i+1;j<instruments.Length;j++)
                Check(!Overlap(instruments[i],instruments[j]),"phone top hierarchy instruments overlap: "+i+" / "+j);
        Check(l.Health.X>=l.Modes.X+l.Modes.Width,"health must follow pause on the left");
        Check(l.MissionObjective.Y+l.MissionObjective.Height<=l.Modes.Y+.001f,"objective must sit below the header row");
        Check(l.Reload.Y>=y+height*.5f && l.Modes.Y>=y+height*.5f,"touch actions left top half of phone");
        Check(!Overlap(l.PauseResume,l.PauseRetry)&&!Overlap(l.PauseRetry,l.PauseExit)&&!Overlap(l.PauseConfirm,l.PauseCancel),"modal actions overlap");
        foreach(var box in new[]{l.Modes,l.Health,l.Reload,l.Timer,l.MissionObjective,l.Toast})
        {
            Check(!HitsCapture(box,l.LeftX,l.LeftY,l.Radius*1.3f),"instrument obscures movement capture");
            Check(!HitsCapture(box,l.RightX,l.RightY,l.Radius*1.3f),"instrument obscures aim capture");
        }
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
        var plate=GUI.textures["Uneven painted label"];
        hud.Draw(l,sticks,magazine,true,.85f,vitality);
        Check(ReferenceEquals(plate,GUI.textures["Uneven painted label"]),"textures rebuilt each repaint");
        // Clear the duplicate redraw before recording the final frame.
        GUI.commands.Clear();hud.Draw(l,sticks,magazine,true,.85f,vitality);
        GUI.Save(Path.Combine(output,name+".json"));GUI.Record=false;hud.Dispose();
    }
}
