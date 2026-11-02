using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace IntercomMvp
{
    /// <summary>Measured boot support, rigid single-link leg correction and independent torso suspension.</summary>
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed partial class IntercomGroundFeedback : MonoBehaviour
    {
        public IntercomPlayerMotor motor;
        [HideInInspector] public IntercomPlayerAudio audioSource; // Kept only for old scene serialization.
        public IntercomSimpleShooter shooter;
        public Animator animator;
        public Transform leftFoot;
        public Transform rightFoot;
        public Transform torso;
        public Transform body;
        public Transform leftCalf, rightCalf, leftToe, rightToe;
        public Transform leftUpperArm, rightUpperArm;
        public event Action<Collider, Vector3, float, bool> Footstep;
        public bool ProvidesFootContacts => isActiveAndEnabled && HasCurrentSoleCalibration
            && animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null
            && leftFoot != null && rightFoot != null && leftCalf != null && rightCalf != null
            && leftFoot.parent == leftCalf && rightFoot.parent == rightCalf;
        [Header("Foot support (world metres)")]
        public bool plantFeet = true;
        [Range(0f, 0.05f)] public float bodyHeightCompliance = 0.03f;
        [Range(0f, 0.2f)] public float maximumPlantCorrection = 0.10f;
        [Range(0f, 45f)] public float maximumCalfCorrection = 28f;
        [Range(0f, 25f)] public float maximumGroundTilt = 16f;
        [Header("Torso suspension")]
        [Range(0f, 1f)] public float accelerationLean = 0.38f;
        [Range(0f, 0.06f)] public float turnLagSeconds = 0.025f;
        [Range(1f, 10f)] public float torsoSpringFrequency = 4.5f;
        [Range(0f, 0.04f)] public float torsoTravelLag = 0.018f;
        [Range(0.01f, 0.3f)] public float aimStabilizationSeconds = 0.1f;
        [Tooltip("Included radial material. Its shader is retained by the build through this reference.")]
        public Material contactMaterial;
        [Tooltip("Sole centres in the corresponding foot bone's local space.")]
        public Vector3 leftSoleOffset;
        public Vector3 rightSoleOffset;
        public bool soleOffsetsCalibrated;
        [HideInInspector] public Vector3 leftHeelOffset, rightHeelOffset, leftToeOffset, rightToeOffset;
        [HideInInspector] public bool contactMarkersCalibrated;
        // The ankle pivot relocation changes foot-local marker coordinates.
        // Leave the serialized field uninitialized so old saved scenes read as version 0.
        private const int CurrentSoleCalibrationVersion = 1;
        [SerializeField, HideInInspector] private int soleCalibrationVersion;
        private bool HasCurrentSoleCalibration => soleOffsetsCalibrated && contactMarkersCalibrated
            && soleCalibrationVersion == CurrentSoleCalibrationVersion;
        public bool NeedsSoleCalibration => !HasCurrentSoleCalibration;
        private bool calibrationAttempted;
        private RuntimeAnimatorController calibrationController;
        public LayerMask groundMask = ~0;
        [Range(0f, 0.6f)] public float shadowOpacity = 0.32f;
        public Vector2 shadowSize = new Vector2(0.34f, 0.46f);
        [Min(0.05f)] public float shadowFadeHeight = 0.32f;
        [Min(0.001f)] public float surfaceOffset = 0.009f;
        [Range(0f, 10f)] public float runLeanDegrees = 5.5f;
        [Range(0f, 4f)] public float runSwayDegrees = 1.25f;
        [Range(0f, 8f)] public float recoilDegrees = 4f;
        [Range(0f, 0.025f)] public float contactCompression = 0.008f;
        public Color dustColor = new Color(0.76f, 0.68f, 0.47f, 0.26f);

        private struct Contact
        {
            internal bool valid;
            internal Vector3 point, normal;
            internal float height;
            internal Collider collider;
        }
        private struct Puff
        {
            internal Transform transform;
            internal MeshRenderer renderer;
            internal Vector3 origin, drift;
            internal float age;
            internal int bornFrame;
            internal bool active;
        }
        private const int DustCapacity = 12;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private sealed class FootRig
        {
            internal Transform foot, calf, toe, anchorMarker;
            internal bool left, anchored, inWindow;
            internal readonly IntercomFootPlantGate gate = new IntercomFootPlantGate();
            internal Collider surface;
            internal Vector3 anchorLocal, anchorMarkerOffset, anchorFacing, anchorTravel, targetAnkle, authoredAnkle, sole;
            internal int soleKind;
            internal SoleSample[] samples;
            internal int sampleIndex = -1, anchorSampleIndex = -1;
            internal Vector3 neutralLink, clearanceDirection;
            internal bool neutralLinkReady, clearanceActive;
            internal Quaternion authoredRotation, targetRotation, anchorRotation;
            internal float weight, length;
            internal Contact contact;
        }
        private struct SavedPose
        {
            internal Transform bone;
            internal Vector3 position;
            internal Quaternion rotation;
        }
        private MaterialPropertyBlock properties;
        private IntercomSimpleShooter subscribedShooter;
        private IntercomWeaponPose weaponPose;
        private GameObject effectsRoot;
        private Mesh discMesh;
        private Transform[] shadows;
        private MeshRenderer[] shadowRenderers;
        private Puff[] dust;
        private readonly SavedPose[] savedPose = new SavedPose[8];
        private readonly FootRig leftRig = new FootRig { left = true };
        private readonly FootRig rightRig = new FootRig { left = false };
        private Contact leftContact, rightContact;
        private int nextPuff, savedPoseCount;
        private float compression, recoil, aimStabilization;
        private Vector3 previousVelocity, previousPosition, previousFacing;
        private bool motionObserved;
        private uint resetVersion;
        private int previousAnimatorState;
        private float previousPhase;
        private float pitch, pitchVelocity, roll, rollVelocity, yaw, yawVelocity;
        private float lagX, lagXVelocity, lagZ, lagZVelocity, settle, settleVelocity;
        private bool focused = true;
        private bool paused;

        private bool Active => isActiveAndEnabled && focused && !paused && Time.deltaTime > 0f;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            if (motor == null) motor = GetComponent<IntercomPlayerMotor>();
            if (shooter == null) shooter = GetComponent<IntercomSimpleShooter>();
            weaponPose = GetComponent<IntercomWeaponPose>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
                {
                    if (leftFoot == null && bone.name == "L_Foot") leftFoot = bone;
                    if (rightFoot == null && bone.name == "R_Foot") rightFoot = bone;
                    if (torso == null && bone.name == "Torso") torso = bone;
                    if (body == null && bone.name == "Body") body = bone;
                    if (leftCalf == null && bone.name == "L_Calf") leftCalf = bone;
                    if (rightCalf == null && bone.name == "R_Calf") rightCalf = bone;
                    if (leftToe == null && bone.name == "L_Toe") leftToe = bone;
                    if (rightToe == null && bone.name == "R_Toe") rightToe = bone;
                    if (leftUpperArm == null && bone.name == "L_Upperarm") leftUpperArm = bone;
                    if (rightUpperArm == null && bone.name == "R_Upperarm") rightUpperArm = bone;
                }
                // Re-measure old saved offsets after a model-space calibration change.
                if (!HasCurrentSoleCalibration || calibratedSoleProfile != soleProfile) CalibrateSoles();
            }
            BindRig();
            if (contactMaterial != null) CreateEffects();
        }

        private void OnEnable()
        {
            Subscribe();
            if (!HasCurrentSoleCalibration) calibrationAttempted = false;
        }
        private void Update()
        {
            // Restore the exact previous Animator pose before its next evaluation.
            // Never multiply an inverse onto an already modified bone each frame.
            RestorePose();
            Subscribe();
            if (!Active) ClearTransientState();
        }

        private void LateUpdate()
        {
            bool animated = animator != null && animator.isActiveAndEnabled
                && animator.runtimeAnimatorController != null && animator.speed > 0f;
            // Awake can run before a controller is assigned. Retry once when it
            // becomes available (or is replaced), never every rendered frame.
            if (Active && animated && ((!HasCurrentSoleCalibration
                && (!calibrationAttempted || calibrationController != animator.runtimeAnimatorController))
                || calibratedSoleProfile != soleProfile))
                CalibrateSoles();
            if (!Active || motor == null || !motor.isActiveAndEnabled || !animated
                || !HasCurrentSoleCalibration)
            {
                RestorePose(); ClearTransientState(); HideEffects(); return;
            }
            float dt = Time.deltaTime;
            Vector3 facing = motor.FacingDirection;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            float phase = state.normalizedTime;
            bool spatialDiscontinuity = !motionObserved || resetVersion != motor.MotionResetVersion
                || (transform.position - previousPosition).sqrMagnitude > Mathf.Pow(Mathf.Max(0.5f, motor.moveSpeed * dt * 3f), 2f);
            bool discontinuity = spatialDiscontinuity || state.fullPathHash != previousAnimatorState
                || phase < previousPhase - 0.001f || phase - previousPhase > 1f;
            if (discontinuity)
            {
                ClearTransientState(spatialDiscontinuity);
                previousVelocity = motor.ActualPlanarVelocity;
                previousFacing = facing;
                motionObserved = true;
            }
            resetVersion = motor.MotionResetVersion;
            previousPosition = transform.position;
            bool grounded = motor.IsGrounded;
            bool moving = grounded && motor.ActualPlanarSpeed > 0.08f;
            float blend = Mathf.Clamp01(animator.GetFloat("Speed"));
            SavePose();
            bool turnOwnsLegs = ApplyTurnFootwork(dt, blend);
            bool wasRunning = sampleRunningSoles;
            sampleRunningSoles = moving && !turnOwnsLegs;
            if (wasRunning && !sampleRunningSoles) ResetRunningClearance();
            if (sampleRunningSoles)
            {
                AlignRunningGround(blend);
                RestoreDiagonalStride(phase, blend);
            }
            PrepareFoot(leftRig, phase, blend, moving, grounded);
            PrepareFoot(rightRig, phase, blend, moving, grounded);
            if (!turnOwnsLegs && plantFeet && body != null && grounded && moving) SolveRunningSupport();
            else if (!turnOwnsLegs && plantFeet && body != null && grounded)
            {
                float weightedHeight = 0f, weight = 0f;
                AccumulateSupport(leftRig, ref weightedHeight, ref weight);
                AccumulateSupport(rightRig, ref weightedHeight, ref weight);
                if (weight > 0.001f)
                    body.position += Vector3.up * Mathf.Clamp(weightedHeight / weight,
                        -bodyHeightCompliance, bodyHeightCompliance) * Mathf.Min(1f, weight);
                SolveFoot(leftRig); SolveFoot(rightRig);
            }
            if (sampleRunningSoles && plantFeet) PreserveRunningClearance(phase, dt);
            leftContact = ProbeFoot(leftRig);
            rightContact = ProbeFoot(rightRig);
            if (turnOwnsLegs)
            {
                leftRig.gate.Reset(); rightRig.gate.Reset();
                PublishTurnLanding();
            }
            else
            {
                PublishContact(leftRig, leftContact, moving, discontinuity);
                PublishContact(rightRig, rightContact, moving, discontinuity);
            }
            if (shadows != null)
            {
                UpdateShadow(0, leftContact, grounded);
                UpdateShadow(1, rightContact, grounded);
            }
            ApplyTorso(phase, moving, dt, discontinuity);
            CacheTurnPose();
            UpdateDust(dt);
            compression = Mathf.MoveTowards(compression, 0f, dt * 10f);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 12f);
            previousVelocity = motor.ActualPlanarVelocity;
            previousFacing = facing;
            previousAnimatorState = state.fullPathHash;
            previousPhase = phase;
        }

        private void BindRig()
        {
            leftRig.foot = leftFoot; leftRig.calf = leftCalf; leftRig.toe = leftToe;
            rightRig.foot = rightFoot; rightRig.calf = rightCalf; rightRig.toe = rightToe;
        }

        private void PrepareFoot(FootRig rig, float phase, float blend, bool moving, bool grounded)
        {
            if (sampleRunningSoles) { PrepareRunningFoot(rig, phase, blend, grounded); return; }
            rig.weight = 0f;
            if (rig.foot == null) return;
            rig.authoredAnkle = rig.foot.position;
            rig.authoredRotation = rig.foot.rotation;
            rig.contact = ProbeFoot(rig);
            bool supportWindow = moving && IntercomGroundMath.StanceWeight(phase, rig.left) > 0f;
            if (!supportWindow || !grounded || !rig.contact.valid)
            {
                rig.anchored = false;
                if (!supportWindow) rig.inWindow = false;
            }
            if (!grounded || !rig.contact.valid || rig.calf == null || rig.foot.parent != rig.calf) return;
            rig.length = Vector3.Distance(rig.calf.position, rig.foot.position);
            if (rig.length < 0.01f) return;
            float support = moving ? IntercomGroundMath.StanceWeight(phase, rig.left) * blend : 1f;
            if (rig.contact.height > (moving ? 0.10f : 0.055f)) support = 0f;
            if (support <= 0f) return;
            float friction = moving ? IntercomGroundMath.PlantLockWeight(phase, rig.left) : 0f;
            if (supportWindow) rig.inWindow = true;
            if (friction <= 0f) rig.anchored = false;
            if (supportWindow && friction > 0f)
            {
                if (!rig.anchored || rig.surface != rig.contact.collider)
                {
                    rig.surface = rig.contact.collider;
                    rig.anchorMarker = rig.soleKind == 2 && rig.toe != null ? rig.toe : rig.foot;
                    rig.anchorMarkerOffset = rig.soleKind == 2 ? (rig.left ? leftToeOffset : rightToeOffset)
                        : rig.soleKind == 1 ? (rig.left ? leftHeelOffset : rightHeelOffset)
                        : (rig.left ? leftSoleOffset : rightSoleOffset);
                    // Lock a boot material point, not its ankle. The ankle must
                    // still travel naturally as the boot rolls around its sole.
                    rig.anchorSampleIndex = rig.sampleIndex;
                    rig.anchorLocal = rig.surface.transform.InverseTransformPoint(AnchorMaterialPoint(rig));
                    rig.anchorFacing = motor.FacingDirection;
                    rig.anchorTravel = motor.ActualPlanarVelocity;
                    rig.anchored = true;
                }
            }
            Vector3 desired = rig.authoredAnkle;
            if (rig.anchored && rig.surface != null)
            {
                Vector3 anchor = rig.surface.transform.TransformPoint(rig.anchorLocal);
                Vector3 marker = AnchorMaterialPoint(rig);
                Vector3 offset = Vector3.ProjectOnPlane(anchor - marker, Vector3.up);
                float limit = Mathf.Min(maximumPlantCorrection, rig.length * 0.7f);
                // A one-link robot leg cannot reach an arbitrarily old foot anchor.
                // Release gracefully rather than stretching or detaching the boot.
                float reach = offset.magnitude;
                if (reach > limit && limit > 0.001f)
                    friction *= Mathf.Clamp01(1f - (reach - limit) / (limit * 0.5f));
                float turn = Vector3.Angle(rig.anchorFacing, motor.FacingDirection);
                friction *= Mathf.Clamp01(1f - Mathf.Max(0f, turn - 20f) / 35f);
                float travelChange = Vector3.Angle(rig.anchorTravel, motor.ActualPlanarVelocity);
                friction *= Mathf.Clamp01(1f - Mathf.Max(0f, travelChange - 30f) / 45f);
                desired += Vector3.ClampMagnitude(offset, limit) * friction;
            }
            float gap = Vector3.Dot(rig.sole - rig.contact.point, rig.contact.normal);
            desired -= rig.contact.normal * Mathf.Clamp(gap - 0.003f, -0.04f, 0.10f);
            rig.targetAnkle = desired;
            rig.weight = support;
        }

        private void AccumulateSupport(FootRig rig, ref float height, ref float weight)
        {
            if (rig.weight <= 0f || rig.calf == null) return;
            Vector3 delta = rig.targetAnkle - rig.calf.position;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            if (!IntercomGroundMath.TryHipHeight(rig.targetAnkle.y, horizontal, rig.length, out float hipY))
            { rig.weight = 0f; rig.anchored = false; return; }
            height += (hipY - rig.calf.position.y) * rig.weight;
            weight += rig.weight;
        }

        private void SolveFoot(FootRig rig)
        {
            if (rig.weight <= 0f || rig.foot == null || rig.calf == null) return;
            Vector3 source = rig.foot.position - rig.calf.position;
            Vector3 target = rig.targetAnkle - rig.calf.position;
            if (target.sqrMagnitude < 0.00001f) return;
            Quaternion correction = Quaternion.FromToRotation(source, target);
            correction = Quaternion.RotateTowards(Quaternion.identity, correction, maximumCalfCorrection);
            rig.calf.rotation = Quaternion.Slerp(Quaternion.identity, correction, rig.weight) * rig.calf.rotation;
            Quaternion slope = Quaternion.FromToRotation(Vector3.up, rig.contact.normal);
            slope = Quaternion.RotateTowards(Quaternion.identity, slope, maximumGroundTilt);
            // Keep authored heel/toe roll and all toe keys after the calf changes.
            rig.foot.rotation = Quaternion.Slerp(Quaternion.identity, slope, rig.weight) * rig.authoredRotation;
        }

        private void PublishContact(FootRig rig, Contact contact, bool moving, bool discontinuity)
        {
            bool touching = rig.weight >= 0.08f && contact.valid && contact.height <= 0.008f;
            if (!rig.gate.Observe(rig.inWindow, moving, touching, discontinuity)) return;
            compression = 1f;
            float strength = Mathf.Clamp01(0.35f + motor.ActualPlanarSpeed / Mathf.Max(0.1f, motor.moveSpeed) * 0.65f);
            Footstep?.Invoke(contact.collider, contact.point, strength, rig.left);
            EmitDust(contact);
        }

        private Contact ProbeFoot(FootRig rig)
        {
            if (rig.foot == null) return default;
            if (ProbeSoleCloud(rig, out Contact cloudContact)) return cloudContact;
            Vector3 centre = rig.foot.TransformPoint(rig.left ? leftSoleOffset : rightSoleOffset);
            if (!contactMarkersCalibrated) { rig.sole = centre; rig.soleKind = 0; return ProbePoint(centre); }
            Vector3 heel = rig.foot.TransformPoint(rig.left ? leftHeelOffset : rightHeelOffset);
            Transform toe = rig.toe != null ? rig.toe : rig.foot;
            Vector3 tip = toe.TransformPoint(rig.left ? leftToeOffset : rightToeOffset);
            // Use the actual articulated heel/toe, not one centre point which
            // floats as the boot rolls. The lower contact is the support point.
            rig.sole = heel.y < tip.y ? heel : tip;
            rig.soleKind = heel.y < tip.y ? 1 : 2;
            if (centre.y < rig.sole.y) { rig.sole = centre; rig.soleKind = 0; }
            return ProbePoint(rig.sole);
        }

        private void ApplyTorso(float phase, bool moving, float dt, bool discontinuity)
        {
            if (torso == null) return;
            // Body owns the slower lower-body heading. The torso and authored
            // weapon pose keep the responsive wrapper heading before feedback.
            torso.rotation = Quaternion.AngleAxis(-turnAppliedYaw, Vector3.up) * torso.rotation;
            Vector3 forward = motor.FacingDirection;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 velocity = motor.ActualPlanarVelocity;
            Vector3 acceleration = discontinuity ? Vector3.zero
                : Vector3.ClampMagnitude((velocity - previousVelocity) / Mathf.Max(0.001f, dt), 16f);
            float speedWeight = moving ? Mathf.Clamp01(motor.ActualPlanarSpeed / Mathf.Max(0.1f, motor.moveSpeed)) : 0f;
            float forwardSpeed = Vector3.Dot(velocity, forward) / Mathf.Max(0.1f, motor.moveSpeed);
            float sideSpeed = Vector3.Dot(velocity, right) / Mathf.Max(0.1f, motor.moveSpeed);
            float cycle = phase * Mathf.PI * 2f;
            float pitchTarget = forwardSpeed * runLeanDegrees + Vector3.Dot(acceleration, forward) * accelerationLean;
            float rollTarget = -sideSpeed * runLeanDegrees - Vector3.Dot(acceleration, right) * accelerationLean
                + Mathf.Sin(cycle) * runSwayDegrees * speedWeight;
            // The cuffs sit close to the lower body. Keep translational lean
            // and sway inside that clearance; yaw, recoil and standing turns
            // retain their existing response.
            pitchTarget *= Mathf.Clamp01(runningTiltWeight);
            rollTarget *= Mathf.Clamp01(runningTiltWeight);
            float turnRate = discontinuity ? 0f : Vector3.SignedAngle(previousFacing, forward, Vector3.up) / Mathf.Max(0.001f, dt);
            float yawTarget = Mathf.Clamp(-turnRate * turnLagSeconds, -8f, 8f) * (motor.IsAiming ? 0.35f : 1f);
            float lagTargetX = Mathf.Clamp(-acceleration.x / 12f, -1f, 1f) * torsoTravelLag;
            float lagTargetZ = Mathf.Clamp(-acceleration.z / 12f, -1f, 1f) * torsoTravelLag;
            IntercomGroundMath.Spring(ref pitch, ref pitchVelocity, pitchTarget, torsoSpringFrequency, dt);
            IntercomGroundMath.Spring(ref roll, ref rollVelocity, rollTarget, torsoSpringFrequency, dt);
            IntercomGroundMath.Spring(ref yaw, ref yawVelocity, yawTarget, torsoSpringFrequency, dt);
            IntercomGroundMath.Spring(ref lagX, ref lagXVelocity, lagTargetX, torsoSpringFrequency, dt);
            IntercomGroundMath.Spring(ref lagZ, ref lagZVelocity, lagTargetZ, torsoSpringFrequency, dt);
            IntercomGroundMath.Spring(ref settle, ref settleVelocity, -compression * contactCompression,
                torsoSpringFrequency + 2f, dt);
            bool armed = motor.IsAiming || (weaponPose != null && weaponPose.Posture != IntercomWeaponPosture.Relaxed);
            if (discontinuity) aimStabilization = armed ? 1f : 0f;
            else aimStabilization = Mathf.MoveTowards(aimStabilization, armed ? 1f : 0f,
                dt / Mathf.Max(0.01f, aimStabilizationSeconds));
            Quaternion leftAuthored = leftUpperArm != null ? leftUpperArm.rotation : Quaternion.identity;
            Quaternion rightAuthored = rightUpperArm != null ? rightUpperArm.rotation : Quaternion.identity;
            float visibleYaw = yaw;
            if (turnHasPose && !turnExiting)
            {
                float limit = Mathf.Clamp(maximumTurnTorsoYaw, 15f, 50f);
                if (turnSteps.Active) limit = Mathf.Max(limit, turnCommittedTwistLimit);
                visibleYaw = Mathf.Clamp(yaw, turnAppliedYaw - limit, turnAppliedYaw + limit);
            }
            torso.rotation = Quaternion.AngleAxis(pitch - recoil * recoilDegrees, right)
                * Quaternion.AngleAxis(roll, forward) * Quaternion.AngleAxis(visibleYaw, Vector3.up) * torso.rotation;
            torso.position += new Vector3(lagX, settle, lagZ) - forward * (recoil * 0.006f);
            // Stabilize only our added torso motion. Source raise/recoil keys
            // remain in the captured arm rotations; muzzle.forward stays valid.
            if (leftUpperArm != null)
                leftUpperArm.rotation = Quaternion.Slerp(leftUpperArm.rotation, leftAuthored, aimStabilization);
            if (rightUpperArm != null)
                rightUpperArm.rotation = Quaternion.Slerp(rightUpperArm.rotation, rightAuthored, aimStabilization);
        }

        private void SavePose()
        {
            SaveBone(body); SaveBone(leftCalf); SaveBone(rightCalf);
            SaveBone(leftFoot); SaveBone(rightFoot); SaveBone(torso);
            SaveBone(leftUpperArm); SaveBone(rightUpperArm);
        }
        private void SaveBone(Transform bone)
        {
            if (bone == null) return;
            savedPose[savedPoseCount++] = new SavedPose { bone = bone, position = bone.localPosition, rotation = bone.localRotation };
        }
        private void RestorePose()
        {
            for (int i = 0; i < savedPoseCount; i++)
            {
                SavedPose pose = savedPose[i];
                if (pose.bone != null) { pose.bone.localPosition = pose.position; pose.bone.localRotation = pose.rotation; }
            }
            savedPoseCount = 0;
        }

        private Contact ProbePoint(Vector3 sole)
        {
            Contact result = default;
            int count = Physics.RaycastNonAlloc(sole + Vector3.up * 0.3f, Vector3.down,
                hits, 1f, groundMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return result;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.normal.y < 0.45f || hit.distance >= nearest) continue;
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                float signedHeight = Vector3.Dot(sole - hit.point, hit.normal);
                // Tolerate the capsule skin (about 2.7cm at current model scale),
                // but do not project a boot shadow onto a nearby raised box top.
                if (signedHeight < -0.04f) continue;
                nearest = hit.distance;
                result.valid = true;
                result.collider = hit.collider;
                result.point = hit.point;
                result.normal = hit.normal;
                result.height = Mathf.Max(0f, signedHeight);
            }
            return result;
        }

        private void UpdateShadow(int index, Contact contact, bool grounded)
        {
            float alpha = IntercomGroundMath.ContactAlpha(contact.height, shadowFadeHeight,
                shadowOpacity, grounded && contact.valid);
            MeshRenderer renderer = shadowRenderers[index];
            renderer.enabled = alpha > 0.002f;
            if (!renderer.enabled) return;
            Transform item = shadows[index];
            Vector3 tangent = Vector3.ProjectOnPlane(motor.FacingDirection, contact.normal).normalized;
            if (tangent.sqrMagnitude < 0.1f) tangent = Vector3.ProjectOnPlane(Vector3.forward, contact.normal).normalized;
            item.SetPositionAndRotation(contact.point + contact.normal * Mathf.Max(0.002f, surfaceOffset),
                Quaternion.LookRotation(tangent, contact.normal));
            float softness = 1f + Mathf.Min(contact.height, shadowFadeHeight) * 0.8f;
            item.localScale = new Vector3(Mathf.Max(0.02f, shadowSize.x) * softness, 1f,
                Mathf.Max(0.02f, shadowSize.y) * softness);
            SetTint(renderer, new Color(0.08f, 0.105f, 0.12f, alpha));
        }

        private void EmitDust(Contact contact)
        {
            if (dust == null) return;
            for (int i = 0; i < 3; i++)
            {
                int slot = nextPuff++ % dust.Length;
                Vector3 drift = Quaternion.AngleAxis(i * 120f + nextPuff * 37f, contact.normal)
                    * Vector3.ProjectOnPlane(motor.FacingDirection, contact.normal).normalized;
                dust[slot].origin = contact.point + contact.normal * 0.02f;
                dust[slot].drift = drift * (0.15f + i * 0.035f) + contact.normal * 0.04f;
                dust[slot].age = 0f;
                dust[slot].bornFrame = Time.frameCount;
                dust[slot].transform.position = dust[slot].origin;
                dust[slot].transform.localScale = Vector3.one * 0.055f;
                SetTint(dust[slot].renderer, dustColor);
                dust[slot].active = true;
                dust[slot].renderer.enabled = true;
                dust[slot].transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(motor.FacingDirection, contact.normal), contact.normal);
            }
        }

        private void UpdateDust(float dt)
        {
            if (dust == null) return;
            for (int i = 0; i < dust.Length; i++)
            {
                if (!dust[i].active) continue;
                if (dust[i].bornFrame == Time.frameCount) continue;
                dust[i].age += dt;
                float life = dust[i].age / 0.23f;
                if (life >= 1f)
                {
                    dust[i].active = false;
                    dust[i].renderer.enabled = false;
                    continue;
                }
                dust[i].transform.position = dust[i].origin + dust[i].drift * life;
                dust[i].transform.localScale = Vector3.one * Mathf.Lerp(0.055f, 0.19f, life);
                Color color = dustColor; color.a *= (1f - life) * (1f - life);
                SetTint(dust[i].renderer, color);
            }
        }

        private void SetTint(Renderer renderer, Color color)
        {
            properties.Clear();
            properties.SetColor(TintId, color);
            renderer.SetPropertyBlock(properties);
        }

        private void CreateEffects()
        {
            effectsRoot = new GameObject("Intercom Ground Feedback Effects");
            effectsRoot.AddComponent<IntercomOccluder>().hideWhenBlocking = false;
            discMesh = MakeQuad();
            shadows = new Transform[2];
            shadowRenderers = new MeshRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                shadowRenderers[i] = CreateDisc("Boot Contact " + i);
                shadows[i] = shadowRenderers[i].transform;
            }
            dust = new Puff[DustCapacity];
            for (int i = 0; i < dust.Length; i++)
            {
                MeshRenderer renderer = CreateDisc("Contact Dust " + i);
                dust[i] = new Puff { renderer = renderer, transform = renderer.transform };
            }
        }

        private MeshRenderer CreateDisc(string label)
        {
            GameObject item = new GameObject(label);
            item.transform.SetParent(effectsRoot.transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = discMesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = contactMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return renderer;
        }

        private static Mesh MakeQuad()
        {
            Mesh mesh = new Mesh { name = "Intercom Radial Contact Quad" };
            mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>Measure actual heel/toe sole positions in a known Idle pose, once per model.</summary>
        public void CalibrateSoles()
        {
            calibrationAttempted = true;
            calibratedSoleProfile = soleProfile;
            leftRig.samples = rightRig.samples = null;
            calibrationController = animator != null ? animator.runtimeAnimatorController : null;
            soleOffsetsCalibrated = contactMarkersCalibrated = false;
            soleCalibrationVersion = 0;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            AnimationClip idle = null;
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == "Idle") { idle = clip; break; }
            // Without the reference pose, leave contact feedback unavailable.
            // PlayerAudio can use its existing physical-ground fallback instead.
            if (idle == null) return;
            Transform[] bones = animator.GetComponentsInChildren<Transform>(true);
            Vector3[] positions = new Vector3[bones.Length];
            Quaternion[] rotations = new Quaternion[bones.Length];
            Vector3[] scales = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                positions[i] = bones[i].localPosition;
                rotations[i] = bones[i].localRotation;
                scales[i] = bones[i].localScale;
            }
            try
            {
                // A saved scene can override the old bone transforms before the
                // Animator's first evaluation. Measure the current model's Idle
                // clip, then restore the user's pose without changing its state.
                idle.SampleAnimation(animator.gameObject, 0f);
                bool left = FindSole("tripo_part_3_fixed", leftFoot, leftToe,
                    out Vector3 leftCentre, out Vector3 leftHeel, out Vector3 leftTip);
                bool right = FindSole("tripo_part_2_fixed", rightFoot, rightToe,
                    out Vector3 rightCentre, out Vector3 rightHeel, out Vector3 rightTip);
                if (left) { leftSoleOffset = leftCentre; leftHeelOffset = leftHeel; leftToeOffset = leftTip; }
                if (right) { rightSoleOffset = rightCentre; rightHeelOffset = rightHeel; rightToeOffset = rightTip; }
                soleOffsetsCalibrated = contactMarkersCalibrated = left && right;
                soleCalibrationVersion = left && right ? CurrentSoleCalibrationVersion : 0;
                if (left && right) CalibrateSoleClouds();
            }
            finally
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i].localPosition = positions[i];
                    bones[i].localRotation = rotations[i];
                    bones[i].localScale = scales[i];
                }
            }
            BindRig();
        }

        private bool FindSole(string meshName, Transform foot, Transform toe,
            out Vector3 centreOffset, out Vector3 heelOffset, out Vector3 toeOffset)
        {
            centreOffset = heelOffset = toeOffset = Vector3.zero;
            if (foot == null) return false;
            foreach (SkinnedMeshRenderer renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != meshName) continue;
                Mesh mesh = new Mesh();
                renderer.BakeMesh(mesh);
                Vector3[] vertices = mesh.vertices;
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
                if (vertices.Length == 0) return false;
                Vector3 min = Vector3.one * float.PositiveInfinity;
                Vector3 max = Vector3.one * float.NegativeInfinity;
                Vector3 forward = motor != null ? motor.FacingDirection : -animator.transform.forward;
                forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
                float back = float.PositiveInfinity, front = float.NegativeInfinity;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = renderer.transform.TransformPoint(vertices[i]);
                    Vector3 point = vertices[i];
                    min = Vector3.Min(min, point); max = Vector3.Max(max, point);
                    float along = Vector3.Dot(point, forward);
                    back = Mathf.Min(back, along); front = Mathf.Max(front, along);
                }
                Vector3 centre = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
                Vector3 heel = Vector3.zero, tip = Vector3.zero;
                float heelY = float.PositiveInfinity, tipY = float.PositiveInfinity;
                int heels = 0, tips = 0;
                foreach (Vector3 point in vertices)
                {
                    if (point.y > min.y + 0.025f) continue;
                    float along = Vector3.Dot(point, forward);
                    if (along <= Mathf.Lerp(back, front, 0.28f))
                    { heel += point; heelY = Mathf.Min(heelY, point.y); heels++; }
                    if (along >= Mathf.Lerp(back, front, 0.72f))
                    { tip += point; tipY = Mathf.Min(tipY, point.y); tips++; }
                }
                if (heels > 0) { heel /= heels; heel.y = heelY; }
                else heel = centre - forward * ((front - back) * 0.25f);
                if (tips > 0) { tip /= tips; tip.y = tipY; }
                else tip = centre + forward * ((front - back) * 0.25f);
                centreOffset = foot.InverseTransformPoint(centre);
                heelOffset = foot.InverseTransformPoint(heel);
                toeOffset = (toe != null ? toe : foot).InverseTransformPoint(tip);
                return true;
            }
            return false;
        }

        private void Subscribe()
        {
            if (subscribedShooter != shooter)
            {
                if (subscribedShooter != null) subscribedShooter.VolleyFired -= OnVolley;
                subscribedShooter = shooter;
                if (subscribedShooter != null) subscribedShooter.VolleyFired += OnVolley;
            }
        }
        private void OnVolley(Vector3 position, int barrels) { if (Active && barrels > 0) recoil = 1f; }
        private void ClearTransientState(bool resetTurning = true)
        {
            if (resetTurning) ResetTurnFootwork();
            sampleRunningSoles = false;
            leftRig.clearanceDirection = rightRig.clearanceDirection = Vector3.zero;
            leftRig.clearanceActive = rightRig.clearanceActive = false;
            compression = recoil = aimStabilization = 0f;
            leftRig.anchored = leftRig.inWindow = false;
            leftRig.gate.Reset();
            rightRig.anchored = rightRig.inWindow = false;
            rightRig.gate.Reset();
            leftRig.surface = rightRig.surface = null;
            pitch = pitchVelocity = roll = rollVelocity = yaw = yawVelocity = 0f;
            lagX = lagXVelocity = lagZ = lagZVelocity = settle = settleVelocity = 0f;
            previousVelocity = Vector3.zero;
            motionObserved = false;
        }
        private void HideEffects()
        {
            if (shadowRenderers != null) foreach (MeshRenderer renderer in shadowRenderers) renderer.enabled = false;
            if (dust != null) for (int i = 0; i < dust.Length; i++)
            { dust[i].active = false; dust[i].renderer.enabled = false; }
        }
        private void OnDisable()
        {
            RestorePose(); ClearTransientState(); HideEffects();
            if (subscribedShooter != null) subscribedShooter.VolleyFired -= OnVolley;
            subscribedShooter = null;
        }
        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value) { RestorePose(); ClearTransientState(); HideEffects(); }
        }
        private void OnApplicationPause(bool value)
        {
            paused = value;
            if (value) { RestorePose(); ClearTransientState(); HideEffects(); }
        }
        private void OnDestroy()
        {
            RestorePose();
            if (effectsRoot != null) Destroy(effectsRoot);
            if (discMesh != null) Destroy(discMesh);
        }
    }
}
