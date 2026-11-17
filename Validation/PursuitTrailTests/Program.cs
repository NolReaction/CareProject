using System;
using IntercomMvp;

static class Program
{
    static int checks;
    static void Check(bool ok,string label) { if(!ok)throw new Exception(label); checks++; }
    static void Goal(IntercomPursuitTrail trail,float x,float z,string label)
    { Check(trail.TryGetGoal(out float actualX,out float actualZ)&&Math.Abs(actualX-x)<.00001f&&Math.Abs(actualZ-z)<.00001f,label); }
    static void Main()
    {
        var trail=new IntercomPursuitTrail();
        Check(!trail.TryGetGoal(out _,out _)&&!trail.HasBacklog,"empty history has no invented goal");
        trail.Observe(0,0);
        for(int i=1;i<=100;i++)trail.Observe(i*.1f,0);
        Check(trail.Count==2,"exact straight continuation keeps only entry and mutable tail");
        Goal(trail,0,0,"oldest observed entry stays committed while target moves");
        Check(trail.Advance(0,0)&&trail.Count==1,"arrival consumes entry before following straight tail");
        Goal(trail,10,0,"caught-up history follows its latest straight endpoint");
        trail.Observe(11,0);Check(trail.Count==1,"consumed straight anchor still supports safe tail updates");
        trail.Observe(11,2);Check(trail.Count==2,"new turn freezes the previously mutable tail");
        Goal(trail,11,0,"open visibility cannot replace the newly committed elbow");
        Check(!trail.Advance(10.90f,0),"corner is not consumed from a distance");
        trail.Advance(11,0);Goal(trail,11,2,"turn is consumed only after physical arrival");
        trail.Observe(11,1);Check(trail.Count==2,"reversing direction preserves the apex instead of flattening it");

        trail.Clear();
        for(int i=0;i<80;i++)
        { double a=i*Math.PI/90;trail.Observe((float)(Math.Cos(a)*5),(float)(Math.Sin(a)*5)); }
        Check(trail.Count==80,"gradual curved observations retain every bend rather than becoming a long chord");
        Goal(trail,5,0,"curved route oldest point remains first");
        while(trail.Count>1)Check(trail.ConsumeHead(),"curved history consumed in bounded order");
        Check(trail.Count==1&&!trail.HasBacklog,"stopped target can eventually be caught up completely");

        trail.Clear();
        for(int i=0;i<IntercomPursuitTrail.Capacity;i++)trail.Observe(i%2*.2f,i*.01f);
        Check(trail.Count==IntercomPursuitTrail.Capacity,"history is capped at its fixed point budget");
        trail.Observe(0,IntercomPursuitTrail.Capacity*.01f);
        Check(trail.HasGap&&trail.Count==IntercomPursuitTrail.Capacity,"overflow records a gap without deleting an old corner");
        Goal(trail,0,0,"overflow cannot advance to an intercept ahead of the existing history");
        while(trail.ConsumeHead()){}
        float tailX=(IntercomPursuitTrail.Capacity-1)%2*.2f,tailZ=(IntercomPursuitTrail.Capacity-1)*.01f;
        Goal(trail,tailX,tailZ,"consumed overflow history stops at its last genuinely recorded point");
        trail.Observe(tailX+5,tailZ);Check(trail.HasGap,"visibility or free capacity cannot bridge missing turns");
        trail.Observe(tailX,tailZ);Check(!trail.HasGap,"target physically returning to recorded tail recovers saturated history");
        trail.Observe(tailX+.2f,tailZ+.2f);Check(trail.Count==2,"recovered target travel is recorded again");

        trail.ResetTo(0,0);trail.Observe(1,0);trail.Observe(1,1);
        Check(trail.Observe(30,30)&&trail.Count==1&&!trail.HasBacklog,"observed teleport creates a clean history boundary");
        Goal(trail,30,30,"teleport does not run through unrelated stale locations");
        int count=trail.Count;trail.Observe(float.NaN,0);trail.Observe(0,float.PositiveInfinity);
        Check(trail.Count==count&&!trail.Advance(float.NaN,0)&&!trail.Advance(30,30,float.PositiveInfinity),"invalid points and radii do not poison history");
        trail.Clear();Check(trail.Count==0&&!trail.HasGap&&!trail.TryGetGoal(out _,out _),"life/target reset discards all history");

        trail.ResetTo(0,0);
        for(int i=0;i<100;i++) {trail.Observe(i*.01f,0);trail.Advance(i*.01f,0);}
        long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=100;i<10100;i++) {trail.Observe(i*.01f,0);trail.Advance(i*.01f,0);}
        Check(GC.GetAllocatedBytesForCurrentThread()==bytes,"steady-state observation, compaction and catch-up allocate no managed memory");
        Console.WriteLine("Pursuit trail: "+checks+" assertions PASS. Pure bounded observations; navigation and rendering require separate checks.");
    }
}
