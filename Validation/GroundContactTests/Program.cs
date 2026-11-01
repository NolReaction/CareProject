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
        Console.WriteLine("Ground contact opacity: "+checks+" assertions passed. Pure math; not Unity physics/rendering.");
    }
}
