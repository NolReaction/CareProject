using System;
using IntercomMvp;

static class Program
{
    static int assertions;
    static void Check(bool value, string name) { assertions++; if (!value) throw new Exception(name); }
    static void Near(float value,float wanted,string name) => Check(Math.Abs(value-wanted)<.001f,name);
    static void Main()
    {
        var state = new IntercomHealthFeedbackState();
        state.Sample(100,100,1,10,true);
        Near(state.Fill,1,"initial snapshot is immediate");Check(!state.PopupVisible(0),"initial HP cannot look like a pickup");
        state.Sample(62,100,1,10,true);
        Near(state.Fill,1,"damage begins from previous fill");Near(state.Trail,1,"damage ghost starts at previous HP");
        Check(state.GetPopup(state.LatestPopupIndex).Text=="-38 HP","actual damage popup");
        state.Sample(62,100,1,10.08f,true);
        Check(state.Fill<1&&state.Fill>.62f,"fill animates towards actual HP");Near(state.Trail,1,"ghost holds before catching up");
        float fill=state.Fill,pulse=state.DamagePulse,clock=state.Clock;
        for(int i=0;i<8;i++)state.Sample(62,100,1,10.08f,true);
        Near(state.Fill,fill,"multiple GUI passes cannot accelerate fill");Near(state.DamagePulse,pulse,"multiple GUI passes cannot consume pulse");
        state.Sample(62,100,1,11.08f,false);
        Near(state.Clock,clock,"pause freezes feedback time");Near(state.Fill,fill,"pause freezes bar");
        state.Sample(62,100,1,11.18f,true);
        Check(state.Clock<.19f,"pause interval cannot be consumed on resume");
        state.Sample(62,100,1,11.6f,true);
        Check(state.Trail<1&&state.Trail>state.Fill,"ghost visibly catches up after delay");
        state.Sample(62,100,1,14,true);Near(state.Fill,.62f,"fill settles exactly");Near(state.Trail,.62f,"ghost settles exactly");
        state.Sample(97,100,1,14,true);
        Near(state.HealingPulse,1,"repair starts green pulse");Near(state.DamagePulse,0,"repair cancels red hit flash");
        Check(state.GetPopup(state.LatestPopupIndex).Text=="+35 HP","repair popup uses applied amount");
        state.Sample(97,100,1,14.15f,true);Check(state.Fill>.62f&&state.Fill<.97f,"repair fills smoothly");
        state.Sample(22,100,1,14.16f,true);Check(state.Critical,"low HP enters critical");Near(state.HealingPulse,0,"new damage cancels green flash");
        for(int i=0;i<200;i++)
        {
            state.Sample(22,100,1,14.2f+i*.016f,true);
            Check(state.CriticalPulse>=0&&state.CriticalPulse<=1,"critical double beat stays bounded");
            Check(state.Fill>=0&&state.Fill<=1&&state.Trail>=0&&state.Trail<=1,"bars remain bounded");
        }
        state.Sample(100,100,2,18,true);
        Near(state.Fill,1,"retry snaps full health");Near(state.DamagePulse,0,"retry clears hit effect");Near(state.HealingPulse,0,"retry cannot look like healing");Check(!state.Critical&&!state.PopupVisible(state.LatestPopupIndex),"retry clears all popups and critical warning");
        state.Sample(75,150,2,18.01f,true);Near(state.Fill,.5f,"maximum-health change resynchronizes");
        state.Sample(float.NaN,150,2,19,true);Near(state.Fill,.5f,"invalid snapshot cannot poison state");
        state.Sample(75,150,2,float.PositiveInfinity,true);Near(state.Fill,.5f,"invalid clock cannot poison state");
        state.Sample(75,150,2,2,true);Near(state.Fill,.5f,"reversed clock resets cleanly");
        state.Sample(0,150,2,2.1f,true);Check(!state.Critical,"death cannot emit critical heartbeat");
        state.Sample(0,150,2,4,true);Near(state.Fill,0,"dead fill reaches zero");
        Console.WriteLine("PASS "+assertions+" health animation, trail, pause, popup and reset assertions. Pure presentation state; Unity rendering is separate.");
    }
}
