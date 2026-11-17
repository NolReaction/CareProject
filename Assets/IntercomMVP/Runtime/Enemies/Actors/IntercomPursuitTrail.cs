using System;

namespace IntercomMvp
{
    /// <summary>Bounded observations of a target's travelled route. Only the straight tail may change; old bends are consumed in order.</summary>
    internal sealed class IntercomPursuitTrail
    {
        internal const int Capacity = 512;
        internal const float ArrivalRadius = .08f;
        private const double TeleportSquared = 64d;
        private const double RejoinSquared = .0625d;
        private readonly Point[] points = new Point[Capacity];
        private int head, count;
        private Point previousObservation, tailAnchor;
        private bool hasObservation, hasTailAnchor;

        private readonly struct Point
        {
            internal readonly float X, Z;
            internal Point(float x, float z) { X=x; Z=z; }
        }

        public int Count => count;
        public bool HasGap { get; private set; }
        public bool HasPendingCorners => count>1;
        public bool HasBacklog => HasPendingCorners || HasGap;

        public void Clear()
        { head=count=0; hasObservation=hasTailAnchor=HasGap=false; previousObservation=tailAnchor=default; }

        public void ResetTo(float x, float z)
        { Clear(); Observe(x,z); }

        /// <returns>True only when an observed teleport replaced the previous history.</returns>
        public bool Observe(float x, float z)
        {
            if(!Finite(x) || !Finite(z)) return false;
            Point next=new Point(x,z);
            if(!hasObservation)
            {
                points[0]=next; head=0; count=1; previousObservation=next; hasObservation=true;
                return false;
            }
            double movement=DistanceSquared(previousObservation,next);
            if(movement>TeleportSquared)
            { ResetTo(x,z); return true; }
            previousObservation=next;
            Point tail=At(count-1);
            if(HasGap)
            {
                // Never drop the oldest bend or bridge an unrecorded section on overflow.
                // The adapter may reacquire after physically reaching the saved tail;
                // an earlier target return can also reconnect this recorded route.
                if(DistanceSquared(tail,next)>RejoinSquared) return false;
                HasGap=false;
            }
            if(DistanceSquared(tail,next)<.00000001d) return false;
            Point anchor=count>1?At(count-2):tailAnchor;
            if((count>1 || hasTailAnchor) && Straight(anchor,tail,next))
            {
                points[(head+count-1)%Capacity]=next;
                return false;
            }
            if(count==Capacity) { HasGap=true; return false; }
            points[(head+count)%Capacity]=next; count++;
            return false;
        }

        public bool TryGetGoal(out float x, out float z)
        {
            x=z=0f; if(count==0) return false;
            Point value=At(0); x=value.X; z=value.Z; return true;
        }

        public bool Advance(float x, float z, float radius=ArrivalRadius)
        {
            if(!Finite(x) || !Finite(z) || !Finite(radius) || radius<0f) return false;
            bool advanced=false;
            while(count>1 && DistanceSquared(At(0),new Point(x,z))<=(double)radius*radius)
            { ConsumeHead(); advanced=true; }
            return advanced;
        }

        /// <summary>The adapter may reach a safely projected version of an otherwise inaccessible target-floor point.</summary>
        public bool ConsumeHead()
        {
            if(count<=1) return false;
            Point consumed=At(0); head=(head+1)%Capacity; count--;
            if(count==1) { tailAnchor=consumed; hasTailAnchor=true; }
            return true;
        }

        private Point At(int index) => points[(head+index)%Capacity];
        private static double DistanceSquared(Point a, Point b)
        { double x=(double)a.X-b.X,z=(double)a.Z-b.Z; return x*x+z*z; }
        private static bool Straight(Point a, Point b, Point c)
        {
            double ax=(double)b.X-a.X,az=(double)b.Z-a.Z,bx=(double)c.X-b.X,bz=(double)c.Z-b.Z;
            double a2=ax*ax+az*az,b2=bx*bx+bz*bz,dot=ax*bx+az*bz,cross=ax*bz-az*bx;
            // This is numerical collinearity, not a smoothing angle. Even a gradual
            // curved path retains its observed bends instead of becoming a chord.
            return a2>0d && b2>0d && dot>0d && cross*cross<=a2*b2*1e-10d;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
