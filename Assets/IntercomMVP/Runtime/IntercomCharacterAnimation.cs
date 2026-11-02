using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Small arm gestures layered after grounding and before actual muzzle sampling.</summary>
    [DefaultExecutionOrder(175)]
    [DisallowMultipleComponent]
    public sealed class IntercomCharacterAnimation : MonoBehaviour
    {
        public Animator animator;
        public IntercomPlayerMotor motor;
        public IntercomWeaponPose weaponPose;
        public IntercomWeaponMagazine magazine;
        public IntercomPlayerInput inputSource;
        public Transform leftUpperArm, rightUpperArm, leftHand, rightHand;
        public Transform leftMuzzle, rightMuzzle;

        [Header("Aim — align wrists to the common dual-pistol heading")]
        [Range(0f, 40f)] public float maximumAimYawCorrection = 25f;
        [Min(0.01f)] public float aimAlignmentSeconds = 0.16f;

        [Header("Reload gesture — rotation only")]
        [Range(0f, 55f)] public float reloadArmLift = 38f;
        [Range(0f, 15f)] public float reloadArmSpread = 8f;
        [Range(0f, 30f)] public float reloadWristLift = 18f;
        [Range(0f, 15f)] public float reloadWristRoll = 8f;

        [Header("Near a wall — relaxed / lowering posture only")]
        public LayerMask wallMask = ~0;
        [Min(0.1f)] public float wallProbeHeight = 1.02f;
        [Min(0.01f)] public float wallProbeRadius = 0.12f;
        [Min(0.1f)] public float wallNearDistance = 0.35f;
        [Min(0.1f)] public float wallFarDistance = 0.78f;
        [Range(0f, 30f)] public float wallArmDrawback = 18f;
        [Range(0f, 15f)] public float wallArmInward = 5f;
        [Min(0.01f)] public float wallBlendIn = 0.16f;
        [Min(0.01f)] public float wallBlendOut = 0.10f;

        [Header("Quiet idle gesture")]
        [Range(0f, 8f)] public float idleInspectionDegrees = 3f;

        public float WallCompression => wallWeight;

        private struct Snapshot
        {
            internal Transform bone;
            internal Quaternion original, applied;
        }
        private readonly Snapshot[] saved = new Snapshot[4];
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private int savedCount;
        private int updatedFrame = -1;
        private float wallWeight, idleSeconds, aimWeight;
        private bool focused = true, paused;
        private uint observedReset;
        private Animator boundAnimator;

        private void Awake() { ResolveReferences(); }
        private void OnEnable() { ResolveReferences(); ClearState(); }
        private void Update()
        {
            // GroundFeedback(150) restores its authored upper-arm snapshot first.
            // Never write our old snapshot over a pose already restored by it.
            RestoreOwnedPose();
            if (boundAnimator != animator) ResolveReferences();
            if (!Active) ClearState();
        }

        private bool Active => isActiveAndEnabled && focused && !paused && Time.deltaTime > 0f
            && animator != null && animator.isActiveAndEnabled && animator.speed > 0f
            && animator.runtimeAnimatorController != null && motor != null && motor.isActiveAndEnabled;

        public void ResolveReferences()
        {
            RestoreOwnedPose();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (motor == null) motor = GetComponent<IntercomPlayerMotor>();
            if (weaponPose == null) weaponPose = GetComponent<IntercomWeaponPose>();
            if (magazine == null) magazine = GetComponent<IntercomWeaponMagazine>();
            if (inputSource == null) inputSource = GetComponent<IntercomPlayerInput>();
            if (animator != null)
            {
                bool replaced = boundAnimator != null && boundAnimator != animator;
                if (replaced) leftUpperArm = rightUpperArm = leftHand = rightHand = leftMuzzle = rightMuzzle = null;
                foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
                {
                    if (leftUpperArm == null && bone.name == "L_Upperarm") leftUpperArm = bone;
                    if (rightUpperArm == null && bone.name == "R_Upperarm") rightUpperArm = bone;
                    if (leftHand == null && bone.name == "L_Hand") leftHand = bone;
                    if (rightHand == null && bone.name == "R_Hand") rightHand = bone;
                    if (leftMuzzle == null && bone.name == "IntercomMuzzle_L") leftMuzzle = bone;
                    if (rightMuzzle == null && bone.name == "IntercomMuzzle_R") rightMuzzle = bone;
                }
            }
            boundAnimator = animator;
        }

        private void LateUpdate()
        {
            if (!Active) { RestoreOwnedPose(); ClearState(); return; }
            if (updatedFrame == Time.frameCount) return;
            if (observedReset != motor.MotionResetVersion)
            {
                ClearState(); observedReset = motor.MotionResetVersion;
            }
            updatedFrame = Time.frameCount;
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingDirection, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.5f) return;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            bool reloading = magazine != null && magazine.IsReloading;
            bool firing = inputSource != null && (inputSource.FireHeld || inputSource.FirePressed);
            bool relaxed = weaponPose == null || weaponPose.Posture == IntercomWeaponPosture.Relaxed
                || weaponPose.Posture == IntercomWeaponPosture.Lowering;
            float targetWall = !reloading && !firing && relaxed ? ProbeWall(forward) : 0f;
            wallWeight = IntercomCharacterAnimationMath.Follow(wallWeight, targetWall,
                targetWall > wallWeight ? wallBlendIn : wallBlendOut, Time.deltaTime);
            // The existing raise clip is .33 s. Fully release the relaxed wall
            // gesture before its first shot, and never alter live firing poses.
            if (weaponPose != null && weaponPose.Posture == IntercomWeaponPosture.Combat && !reloading)
                wallWeight = 0f;
            bool quiet = relaxed && !reloading && !firing && motor.ActualPlanarSpeed < 0.08f && wallWeight < 0.01f;
            idleSeconds = quiet ? idleSeconds + Time.deltaTime : 0f;
            float inspect = IntercomCharacterAnimationMath.IdleInspection(idleSeconds);
            float reload = reloading ? IntercomCharacterAnimationMath.ReloadWeight(magazine.ReloadProgress) : 0f;
            float latch = reloading ? IntercomCharacterAnimationMath.ReloadLatch(magazine.ReloadProgress) : 0f;
            bool aiming = weaponPose != null && (weaponPose.Posture == IntercomWeaponPosture.Raising
                || weaponPose.Posture == IntercomWeaponPosture.Combat);
            aimWeight = IntercomCharacterAnimationMath.Follow(aimWeight, aiming ? 1f : 0f,
                aimAlignmentSeconds, Time.deltaTime);
            if (weaponPose != null && weaponPose.Posture == IntercomWeaponPosture.Combat) aimWeight = 1f;
            float alignment = aimWeight * (1f - reload) * (1f - wallWeight);
            if (reload <= 0f && wallWeight <= 0f && inspect <= 0f && alignment <= 0f) return;

            Save(leftUpperArm); Save(rightUpperArm); Save(leftHand); Save(rightHand);
            Vector3 centre = leftUpperArm != null && rightUpperArm != null
                ? (leftUpperArm.position + rightUpperArm.position) * 0.5f : transform.position;
            ApplyArm(leftUpperArm, leftHand, centre, forward, right, reload, latch, inspect);
            ApplyArm(rightUpperArm, rightHand, centre, forward, right, reload, latch, inspect);
            AlignMuzzles(forward, alignment);
            for (int i = 0; i < savedCount; i++)
            {
                Snapshot value = saved[i]; value.applied = value.bone.localRotation; saved[i] = value;
            }
        }

        private void AlignMuzzles(Vector3 forward, float weight)
        {
            if (weight <= 0f || leftHand == null || rightHand == null || leftMuzzle == null || rightMuzzle == null) return;
            // Snapshot both before rotating either hand. Both visual barrels and
            // Shooter(200) use the same shared mean heading, never a close target.
            Vector3 left = Vector3.ProjectOnPlane(leftMuzzle.forward, Vector3.up);
            Vector3 right = Vector3.ProjectOnPlane(rightMuzzle.forward, Vector3.up);
            if (left.sqrMagnitude < 0.000001f || right.sqrMagnitude < 0.000001f) return;
            Vector3 common = IntercomProjectileDirection.Parallel(right, left, forward, true);
            float limit = Mathf.Clamp(maximumAimYawCorrection, 0f, 40f);
            float leftYaw = Mathf.Clamp(Vector3.SignedAngle(left, common, Vector3.up), -limit, limit) * weight;
            float rightYaw = Mathf.Clamp(Vector3.SignedAngle(right, common, Vector3.up), -limit, limit) * weight;
            leftHand.rotation = Quaternion.AngleAxis(leftYaw, Vector3.up) * leftHand.rotation;
            rightHand.rotation = Quaternion.AngleAxis(rightYaw, Vector3.up) * rightHand.rotation;
        }

        private void ApplyArm(Transform arm, Transform hand, Vector3 centre, Vector3 forward,
            Vector3 right, float reload, float latch, float inspect)
        {
            if (arm == null) return;
            float side = Vector3.Dot(arm.position - centre, right) >= 0f ? 1f : -1f;
            float pitch = -reloadArmLift * reload + wallArmDrawback * wallWeight;
            float inward = side * (reloadArmSpread * reload - wallArmInward * wallWeight);
            arm.rotation = Quaternion.AngleAxis(inward, Vector3.up)
                * Quaternion.AngleAxis(pitch, right) * arm.rotation;
            if (hand == null) return;
            float handPitch = -reloadWristLift * reload + 5f * latch + idleInspectionDegrees * inspect;
            float handRoll = side * (reloadWristRoll * reload - 6f * latch + idleInspectionDegrees * 0.5f * inspect);
            hand.rotation = Quaternion.AngleAxis(handRoll, forward)
                * Quaternion.AngleAxis(handPitch, right) * hand.rotation;
        }

        private float ProbeWall(Vector3 forward)
        {
            float far = Mathf.Max(wallNearDistance + 0.05f, wallFarDistance);
            int count = Physics.SphereCastNonAlloc(transform.position + Vector3.up * wallProbeHeight,
                Mathf.Max(0.01f, wallProbeRadius), forward, hits, far, wallMask, QueryTriggerInteraction.Ignore);
            // A full unsorted buffer cannot reliably identify the closest wall.
            if (count == hits.Length) return 0f;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || Mathf.Abs(hit.normal.y) > 0.55f || hit.distance < 0f) continue;
                Transform item = hit.collider.transform;
                if (item == transform || item.IsChildOf(transform)) continue;
                if (hit.distance < nearest) nearest = hit.distance;
            }
            return IntercomCharacterAnimationMath.WallWeight(nearest, wallNearDistance, far);
        }

        private void Save(Transform bone)
        {
            if (bone == null) return;
            saved[savedCount++] = new Snapshot { bone = bone, original = bone.localRotation, applied = bone.localRotation };
        }

        private void RestoreOwnedPose()
        {
            for (int i = 0; i < savedCount; i++)
            {
                Snapshot value = saved[i];
                if (value.bone != null && SameRotation(value.bone.localRotation, value.applied))
                    value.bone.localRotation = value.original;
            }
            savedCount = 0;
        }

        private static bool SameRotation(Quaternion a, Quaternion b)
        {
            // Component comparison preserves even a small authoritative change
            // by GroundFeedback/Animator; a dot epsilon is too broad near 1.
            float sign = Quaternion.Dot(a, b) < 0f ? -1f : 1f;
            return Mathf.Abs(a.x - b.x * sign) <= 0.000001f
                && Mathf.Abs(a.y - b.y * sign) <= 0.000001f
                && Mathf.Abs(a.z - b.z * sign) <= 0.000001f
                && Mathf.Abs(a.w - b.w * sign) <= 0.000001f;
        }

        private void ClearState() { wallWeight = idleSeconds = aimWeight = 0f; updatedFrame = -1; }
        private void OnDisable() { RestoreOwnedPose(); ClearState(); }
        private void OnApplicationFocus(bool value)
        { focused = value; if (!value) { RestoreOwnedPose(); ClearState(); } }
        private void OnApplicationPause(bool value)
        { paused = value; if (value) { RestoreOwnedPose(); ClearState(); } }
    }
}
