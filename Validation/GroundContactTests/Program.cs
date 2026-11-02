using System;
using IntercomMvp;
class Program
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static void Main()
    {
        Check(IntercomGroundMath.ContactAlpha(0f,.32f,.32f,true)==.32f,"Grounded boot has intended contact opacity");
        Check(IntercomGroundMath.ContactAlpha(0f,.32f,.32f,false)==0f,"Airborne or unsupported boot has no stuck shadow");
        Check(IntercomGroundMath.ContactAlpha(.32f,.32f,.32f,true)==0f,"Shadow disappears beyond fade distance");
        Check(IntercomGroundMath.ContactAlpha(1f,.32f,.32f,true)==0f,"Faraway floor does not retain shadow");
        Check(IntercomGroundMath.ContactAlpha(-.02f,.32f,.32f,true)==.32f,"Small solver penetration remains bounded");
        float previous=.32f;
        for(int i=0;i<=100;i++)
        {
            float a=IntercomGroundMath.ContactAlpha(i*.004f,.32f,.32f,true);
            Check(a>=0f && a<=previous,"Rising boot fades monotonically without opacity overshoot");previous=a;
        }
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            Check(IntercomGroundMath.ContactAlpha(invalid,.32f,.32f,true)==0f,"Invalid height safe");
            Check(IntercomGroundMath.ContactAlpha(0f,invalid,.32f,true)==0f,"Invalid fade safe");
            Check(IntercomGroundMath.ContactAlpha(0f,.32f,invalid,true)==0f,"Invalid opacity safe");
        }
        Check(IntercomGroundMath.ContactAlpha(0f,-1f,4f,true)==.6f,"Malformed settings stay bounded");
        Check(IntercomGroundMath.ContactAlpha(.05f,0f,.32f,true)==0f,"Zero fade distance has safe minimum");
        for (int i=0;i<=1000;i++)
        {
            float phase=i/1000f;
            float l=IntercomGroundMath.StanceWeight(phase,true), r=IntercomGroundMath.StanceWeight(phase,false);
            Check(l>=0f&&l<=1f&&r>=0f&&r<=1f,"Support weights bounded");
            Check(l==0f||r==0f,"Only one foot owns running support at a time");
            Check(Math.Abs(l-IntercomGroundMath.StanceWeight(phase+.5f,false))<.00001f,"Left/right stance symmetric");
            if(phase>=.28f&&phase<=.5f)Check(l<.000001f,"Left releases before swing");
            if(phase>=.78f)Check(r<.000001f,"Right releases before swing");
            float friction=IntercomGroundMath.PlantLockWeight(phase,true);
            Check(friction>=0f&&friction<=1f,"Friction lock bounded");
            if(phase<=.035f||phase>=.17f)Check(friction<.000001f,"Heel strike and toe-off retain source roll");
            if(phase>=.06f&&phase<=.14f)Check(friction>.999f,"Flat support owns planted sole marker");
        }
        for(int i=0;i<985;i++)
        {
            float horizontal=.2f*i/1000f;
            Check(IntercomGroundMath.TryHipHeight(.1f,horizontal,.2f,out float hip),"Reachable ankle accepted");
            Check(Math.Abs(Math.Sqrt(horizontal*horizontal+(hip-.1f)*(hip-.1f))-.2f)<.000001f,
                "Rigid leg length preserved by solved hip height");
        }
        Check(!IntercomGroundMath.TryHipHeight(0f,.2f,.2f,out _),"Horizontal singularity releases anchor");
        Check(!IntercomGroundMath.TryHipHeight(0f,.3f,.2f,out _),"Unreachable ankle rejected without stretch");
        Check(!IntercomGroundMath.TryHipHeight(0f,0f,0f,out _),"Degenerate link rejected");
        float reference=0f;
        foreach(int fps in new[]{15,30,60,120})
        {
            float position=0f, velocity=0f, last=0f;
            for(int frame=0;frame<fps;frame++)
            {
                IntercomGroundMath.Spring(ref position,ref velocity,6f,4.5f,1f/fps);
                Check(position>=last-.00001f&&position<=6.00001f,"Critical spring settles without overshoot");
                last=position;
            }
            Check(Math.Abs(position-6f)<.0001f,"Torso settles to target at each frame rate");
            if(fps==15)reference=position;
            else Check(Math.Abs(position-reference)<.00001f,"Spring consistent at15/30/60/120FPS");
            var left=new IntercomFootPlantGate();var right=new IntercomFootPlantGate();int contacts=0;
            for(int frame=0;frame<fps*10;frame++)
            {
                float phase=frame/(float)fps*2.25f;
                if(left.Observe(IntercomGroundMath.StanceWeight(phase,true)>.001f,true,true,frame==0))contacts++;
                if(right.Observe(IntercomGroundMath.StanceWeight(phase,false)>.001f,true,true,frame==0))contacts++;
            }
            Check(contacts==45,"Exactly45 verified footsteps in10s at1.5run rate across frame rates");
        }
        var gate=new IntercomFootPlantGate();
        Check(!gate.Observe(true,true,true,true),"Teleport/resync does not invent an impact");
        Check(!gate.Observe(true,true,true,false),"Resync window stays silent");
        Check(!gate.Observe(false,true,false,false),"Airborne swing silent");
        Check(!gate.Observe(true,true,false,false),"Gait phase alone cannot emit without contact");
        Check(gate.Observe(true,true,true,false),"Verified landing emits once");
        Check(!gate.Observe(true,true,true,false),"Planted foot cannot repeat impact");
        Check(!gate.Observe(true,false,true,false),"Idle/falling cannot emit contact");
        gate.Reset();Check(!gate.Observe(true,true,true,true),"Reset plus rebaseline remains silent");
        float p=1f,v=2f;IntercomGroundMath.Spring(ref p,ref v,4f,4.5f,0f);
        Check(p==1f&&v==2f,"Paused spring does not advance");
        IntercomGroundMath.Spring(ref p,ref v,4f,4.5f,2f);
        Check(!float.IsNaN(p)&&!float.IsInfinity(p)&&Math.Abs(p)<5f,"Long frame remains bounded");
        Console.WriteLine("Ground contact / rigid support / torso spring / impact gate: "+checks+" assertions passed. Production pure math; not Unity physics/rendering.");
    }
}
