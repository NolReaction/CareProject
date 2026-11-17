using System;
using IntercomMvp;
using Box = IntercomMvp.IntercomHudLayout.Box;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Inside(Box small, Box large) => small.X >= large.X-.01f && small.Y >= large.Y-.01f
        && small.X+small.Width <= large.X+large.Width+.01f && small.Y+small.Height <= large.Y+large.Height+.01f;
    private static bool Overlap(Box a, Box b) => a.X < b.X+b.Width-.001f && b.X < a.X+a.Width-.001f
        && a.Y < b.Y+b.Height-.001f && b.Y < a.Y+a.Height-.001f;
    private static void Hits(IntercomFrontEndLayout l, IntercomFrontEndSection section, Box b, IntercomFrontEndAction expected)
    { Check(l.Hit(section,b.X+b.Width*.5f,b.Y+b.Height*.5f)==expected,"drawn control centre routes wrong action: "+expected); }

    private static void Main()
    {
        Navigation();
        Feedback();
        foreach (var screen in new[] {new Box(0,0,1280,720),new Box(98,34,2152,1012),new Box(18,14,600,330),
            new Box(0,0,320,180),new Box(0,0,480,900),new Box(0,0,900,900),new Box(10,20,3440,1440)})
            Layout(screen);
        var random = new Random(7401);
        for (int i=0;i<80;i++) Layout(new Box(random.Next(0,160),random.Next(0,100),random.Next(240,3900),random.Next(180,2200)));
        InvalidAreas();
        Console.WriteLine("Front end models: "+checks+" assertions PASS; navigation, safe areas and shared pointer bounds. Pure production models, no Unity execution.");
    }

    private static void Navigation()
    {
        var state = new IntercomFrontEndState();
        Check(state.CurrentSection==IntercomFrontEndSection.Home&&state.Revision==0,"initial entry is home");
        Check(!state.Back()&&state.Revision==0,"home Back is inert");
        foreach (var page in new[] {IntercomFrontEndSection.Play,IntercomFrontEndSection.Robot,IntercomFrontEndSection.Armory})
        {
            uint before=state.Revision;
            Check(state.Select(page)&&state.CurrentSection==page&&state.Revision==before+1,"explicit navigation updates once");
            Check(!state.Select(page)&&state.Revision==before+1,"repeat tab press is inert");
            Check(!state.Select((IntercomFrontEndSection)(-1))&&!state.Select((IntercomFrontEndSection)99),"invalid page rejected");
            Check(state.CurrentSection==page&&state.Revision==before+1,"invalid page did not corrupt current room");
            Check(state.Back()&&state.CurrentSection==IntercomFrontEndSection.Home&&state.Revision==before+2,"Back returns home");
        }
    }

    private static void Feedback()
    {
        var state = new IntercomFrontEndFeedbackState();
        state.Sample(10f,0,IntercomFrontEndAction.Play,true);
        Check(state.ContentOpacity>=.8f&&state.ContentOpacity<=1f,"first reveal keeps content readable and clickable");
        state.Sample(10.08f,0,IntercomFrontEndAction.Play,true);
        float hovered=state.Hover(IntercomFrontEndAction.Play),reveal=state.Reveal;
        Check(hovered>.5f&&hovered<1f,"pointer hover eases toward selected control");
        for(int i=0;i<5;i++)state.Sample(10.08f,0,IntercomFrontEndAction.Play,true);
        Check(state.Hover(IntercomFrontEndAction.Play)==hovered&&state.Reveal==reveal,"same-time repaint cannot advance menu animation");
        state.Press(IntercomFrontEndAction.Play);
        Check(state.PressAmount(IntercomFrontEndAction.Play)==1f&&state.PressAmount(IntercomFrontEndAction.Robot)==0f,"accepted press affects only its control");
        state.Sample(10.12f,1,IntercomFrontEndAction.None,true);
        Check(state.Reveal==0f&&state.Hover(IntercomFrontEndAction.Play)==0f,"new page resets reveal and prior hover");
        Check(state.PressAmount(IntercomFrontEndAction.Play)>0f,"tab press remains visible during page change");
        state.Sample(10.4f,1,IntercomFrontEndAction.None,true);
        Check(state.Reveal==1f&&state.ContentOpacity==1f&&state.PressAmount(IntercomFrontEndAction.Play)==0f,"short animations settle without affecting input");
        state.Sample(10.5f,1,IntercomFrontEndAction.Robot,true);
        float before=state.Hover(IntercomFrontEndAction.Robot);
        state.Sample(float.NaN,1,IntercomFrontEndAction.Play,true);
        Check(state.Hover(IntercomFrontEndAction.Robot)==before,"invalid time cannot poison presentation");
        state.Press((IntercomFrontEndAction)99);
        Check(state.PressedAction==IntercomFrontEndAction.Play,"invalid enum press ignored");
        Check(state.Hover((IntercomFrontEndAction)99)==0f&&state.PressAmount((IntercomFrontEndAction)(-1))==0f,"invalid enum feedback bounded");
        state.Sample(10.6f,1,IntercomFrontEndAction.Robot,false);
        Check(state.Hover(IntercomFrontEndAction.Robot)==0f&&state.PressedAction==IntercomFrontEndAction.None,"hidden menu clears hover and press");
        state.Sample(12f,3,IntercomFrontEndAction.Armory,true);
        Check(state.Reveal==0f,"reopened menu has fresh presentation clock");
        state.Sample(11f,3,IntercomFrontEndAction.Armory,true);
        Check(state.Reveal==0f,"clock rewind resets cleanly");
    }

    private static void Layout(Box safe)
    {
        var l=IntercomFrontEndLayout.Calculate(safe.X,safe.Y,safe.Width,safe.Height);
        var all=new[] {l.Panel,l.Header,l.Footer,l.Tabs,l.TabHome,l.TabPlay,l.TabRobot,l.TabArmory,l.Body,l.HeroPreview,l.Content,
            l.ActionPrimary,l.ActionSecondary,l.ModeFirst,l.ModeSecond,l.ModeThird};
        foreach (var b in all)
        {
            Check(Finite(b.X)&&Finite(b.Y)&&Finite(b.Width)&&Finite(b.Height)&&b.Width>0&&b.Height>0,"invalid control geometry");
            Check(Inside(b,safe),"visible or interactive control outside safe area");
        }
        Check(!Overlap(l.HeroPreview,l.Content),"illustration covers interactive detail area");
        Check(!Overlap(l.Header,l.Tabs)&&!Overlap(l.Tabs,l.Body)&&!Overlap(l.Footer,l.Body),"major UI rows overlap");
        Check(Inside(l.ActionPrimary,l.Content)&&Inside(l.ActionSecondary,l.Content),"actions escaped detail pane");
        Check(Inside(l.ModeFirst,l.Content)&&Inside(l.ModeSecond,l.Content)&&Inside(l.ModeThird,l.Content),"modes escaped detail pane");
        var buttons=new[]{l.TabHome,l.TabPlay,l.TabRobot,l.TabArmory,l.ModeFirst,l.ModeSecond,l.ModeThird};
        for(int i=0;i<buttons.Length;i++)for(int j=i+1;j<buttons.Length;j++)Check(!Overlap(buttons[i],buttons[j]),"simultaneous Play controls overlap");
        Check(!Overlap(l.ActionPrimary,l.ActionSecondary),"primary/secondary overlap");
        foreach(var section in new[]{IntercomFrontEndSection.Home,IntercomFrontEndSection.Play,IntercomFrontEndSection.Robot,IntercomFrontEndSection.Armory})
        {
            Hits(l,section,l.TabHome,IntercomFrontEndAction.Home);Hits(l,section,l.TabPlay,IntercomFrontEndAction.Play);
            Hits(l,section,l.TabRobot,IntercomFrontEndAction.Robot);Hits(l,section,l.TabArmory,IntercomFrontEndAction.Armory);
            Check(l.Hit(section,safe.X-10,safe.Y-10)==IntercomFrontEndAction.None,"outside click routed to menu");
            Check(l.Hit(section,float.NaN,50)==IntercomFrontEndAction.None&&l.Hit(section,50,float.PositiveInfinity)==IntercomFrontEndAction.None,"invalid pointer routed");
        }
        Hits(l,IntercomFrontEndSection.Play,l.ModeFirst,IntercomFrontEndAction.SelectElimination);
        Hits(l,IntercomFrontEndSection.Play,l.ModeSecond,IntercomFrontEndAction.SelectRescue);
        Hits(l,IntercomFrontEndSection.Play,l.ModeThird,IntercomFrontEndAction.SelectFreePlay);
        foreach(var section in new[]{IntercomFrontEndSection.Home,IntercomFrontEndSection.Robot,IntercomFrontEndSection.Armory})
        {
            Hits(l,section,l.ActionPrimary,IntercomFrontEndAction.Primary);Hits(l,section,l.ActionSecondary,IntercomFrontEndAction.Secondary);
            Hits(l,section,l.ModeFirst,IntercomFrontEndAction.None);
        }
        Check(l.Hit((IntercomFrontEndSection)99,l.TabPlay.X+1,l.TabPlay.Y+1)==IntercomFrontEndAction.None,"invalid section still accepted action");
    }

    private static void InvalidAreas()
    {
        foreach(var b in new[]{new Box(0,0,0,720),new Box(0,0,1280,-1),new Box(float.NaN,0,1280,720),
            new Box(0,float.PositiveInfinity,1280,720),new Box(0,0,float.NaN,720),new Box(float.MaxValue,0,float.MaxValue,720),
            new Box(float.MaxValue,0,1,720)})
        {
            bool rejected=false;try{IntercomFrontEndLayout.Calculate(b.X,b.Y,b.Width,b.Height);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"invalid/nonrepresentable safe area accepted");
        }
        Layout(new Box(0,0,.001f,.002f));
    }
}
