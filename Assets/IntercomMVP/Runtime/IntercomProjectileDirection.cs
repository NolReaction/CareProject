using UnityEngine;
namespace IntercomMvp
{
    public static class IntercomProjectileDirection
    {
        public static Vector3 Resolve(Vector3 muzzleForward, Vector3 ownerForward, bool planar)
        {
            Vector3 direction=planar ? Vector3.ProjectOnPlane(muzzleForward,Vector3.up) : muzzleForward;
            if(direction.sqrMagnitude<0.000001f)
                direction=planar ? Vector3.ProjectOnPlane(ownerForward,Vector3.up) : ownerForward;
            return direction.sqrMagnitude<0.000001f ? Vector3.forward : direction.normalized;
        }
    }
}
