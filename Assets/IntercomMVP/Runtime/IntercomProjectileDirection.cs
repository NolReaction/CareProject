using UnityEngine;
namespace IntercomMvp
{
    public static class IntercomProjectileDirection
    {
        /// <summary>One shared heading, two real origins. Averaging mirrored barrel yaw removes crossed fire without aiming behind a turning actor.</summary>
        public static Vector3 Parallel(Vector3 rightForward, Vector3 leftForward, Vector3 fallback, bool planar)
        {
            Vector3 right = planar ? Vector3.ProjectOnPlane(rightForward,Vector3.up) : rightForward;
            Vector3 left = planar ? Vector3.ProjectOnPlane(leftForward,Vector3.up) : leftForward;
            if(right.sqrMagnitude>0.000001f) right=right.normalized;
            if(left.sqrMagnitude>0.000001f) left=left.normalized;
            return Resolve(right+left,fallback,planar);
        }
        public static Vector3 Resolve(Vector3 muzzleForward, Vector3 ownerForward, bool planar)
        {
            Vector3 direction=planar ? Vector3.ProjectOnPlane(muzzleForward,Vector3.up) : muzzleForward;
            if(direction.sqrMagnitude<0.000001f)
                direction=planar ? Vector3.ProjectOnPlane(ownerForward,Vector3.up) : ownerForward;
            return direction.sqrMagnitude<0.000001f ? Vector3.forward : direction.normalized;
        }
    }
}
