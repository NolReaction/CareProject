using System;
using UnityEngine;

namespace IntercomMvp
{
    public sealed partial class IntercomGroundFeedback
    {
        [Header("Stationary turn steps")]
        public bool turnSteppingEnabled = true;
        [Range(15f, 50f)] public float maximumTurnTorsoYaw = 35f;
        [Tooltip("Small aim changes turn the upper chassis first. A planted step begins only beyond this free waist angle.")]
        [Range(8f, 35f)] public float stationaryWaistFreedom = 28f;
        [Range(0.12f, 0.35f)] public float turnStepSeconds = 0.13f;
        [Range(15f, 20f)] public float turnStepDegrees = 20f;
        [Range(35f, 75f)] public float turnLiftDegrees = 75f;
        [Range(0.08f, 0.25f)] public float turnExitSeconds = 0.14f;
        // True only while an actual stationary landing event is being dispatched.
        // Audio may accept that contact at zero travel speed, but never an idle tick.
        public bool IsTurnFootstep { get; private set; }
        public bool IsTurnStepping => turnHasPose && (turnSteps.Active || turnSteps.LandedThisTick);

        private readonly IntercomTurnStepState turnSteps = new IntercomTurnStepState();
        private bool turnReady, turnHasPose, turnExiting;
        private float turnAppliedYaw, turnExitWeight;
        private int turnPublishedStep;
        private Vector3 turnBaseBodyPosition, turnBodyOffset, turnStepOffset;
        private Quaternion turnBaseBodyRotation, turnBaseLeftCalf, turnBaseRightCalf;
        private Quaternion turnBaseLeftFoot, turnBaseRightFoot;
        private Vector3 turnLeftAnkle, turnRightAnkle, turnStanceAnkle, turnSwingStartAnkle;
        private Vector3 turnLeftLink, turnRightLink, turnSwingDirection;
        private Quaternion turnLeftRotation, turnRightRotation, turnStanceRotation, turnSwingStartRotation;
        private Quaternion turnSwingStartCalf;
        private Vector3 turnCachedBodyPosition;
        private Quaternion turnCachedBodyRotation, turnCachedLeftCalf, turnCachedRightCalf;
        private Quaternion turnCachedLeftFoot, turnCachedRightFoot;
        private float turnExitYaw, turnSwingFloorY, turnSwingStartSoleY, turnCommittedTwistLimit;

        /// <summary>Bound the visible upper/lower twist before the motor rotates.</summary>
        public bool TryConstrainStationaryHeading(Vector3 desired, float anticipatedSpeed, out Vector3 limited)
        {
            limited = desired;
            if (!turnSteppingEnabled || !turnReady || !turnHasPose || turnExiting
                || !ProvidesFootContacts || motor == null || !motor.IsGrounded
                || anticipatedSpeed >= turnSteps.stationaryExitSpeed || motor.WantsToMove) return false;
            float target = TurnYaw(desired);
            float error = IntercomTurnStepState.DeltaAngle(turnSteps.LowerYaw, target);
            float limit = Mathf.Clamp(maximumTurnTorsoYaw, 15f, 50f);
            if (turnSteps.Active) limit = Mathf.Max(limit, turnCommittedTwistLimit);
            // A reversal must also respect the already committed step endpoint;
            // otherwise the lower body can continue turning away this frame.
            float committed = IntercomTurnStepState.DeltaAngle(turnSteps.LowerYaw, turnSteps.DestinationYaw);
            float minimum = Mathf.Max(-limit, committed - limit);
            float maximum = Mathf.Min(limit, committed + limit);
            limited = Quaternion.AngleAxis(turnSteps.LowerYaw + Mathf.Clamp(error, minimum, maximum), Vector3.up)
                * Vector3.forward;
            return true;
        }

        private bool ApplyTurnFootwork(float dt, float runBlend)
        {
            turnAppliedYaw = 0f;
            bool canStand = turnSteppingEnabled && ProvidesFootContacts && body != null && torso != null
                && motor.IsGrounded && !motor.WantsToMove && motor.ActualPlanarSpeed < 0.12f && runBlend < 0.1f;
            float rootYaw = TurnYaw(motor.FacingDirection);
            if (!canStand || turnExiting)
            {
                if (!turnHasPose) { turnSteps.Reset(rootYaw); return false; }
                // Preserve the last rendered pose on the first travelling frame,
                // then return to the live Animator pose. No contact on cancellation.
                if (!turnExiting) { turnExiting = true; turnExitWeight = 1f; turnExitYaw = IntercomTurnStepState.DeltaAngle(rootYaw, turnSteps.LowerYaw); }
                turnSteps.Advance(rootYaw, 1f, motor.IsGrounded, dt);
                turnExitWeight = Mathf.MoveTowards(turnExitWeight, 0f, dt / Mathf.Max(0.08f, turnExitSeconds));
                float weight = turnExitWeight * turnExitWeight * (3f - 2f * turnExitWeight);
                if (weight <= 0f) { ResetTurnFootwork(); return false; }
                body.localPosition += (turnCachedBodyPosition - body.localPosition) * weight;
                body.localRotation = Quaternion.Slerp(body.localRotation, turnCachedBodyRotation, weight);
                leftCalf.localRotation = Quaternion.Slerp(leftCalf.localRotation, turnCachedLeftCalf, weight);
                rightCalf.localRotation = Quaternion.Slerp(rightCalf.localRotation, turnCachedRightCalf, weight);
                leftFoot.localRotation = Quaternion.Slerp(leftFoot.localRotation, turnCachedLeftFoot, weight);
                rightFoot.localRotation = Quaternion.Slerp(rightFoot.localRotation, turnCachedRightFoot, weight);
                turnAppliedYaw = turnExitYaw * weight;
                return true;
            }
            if (!turnHasPose || turnExiting)
            {
                // Keep the independent moving pelvis heading on the first
                // stopped frame. Reinitializing at the aimed wrapper would
                // snap both boots through the waist angle on stick release.
                turnSteps.Reset(runningLowerReady ? runningLowerYaw : rootYaw);
                turnBaseBodyPosition = body.localPosition;
                turnBaseBodyRotation = body.localRotation;
                turnBaseLeftCalf = leftCalf.localRotation; turnBaseRightCalf = rightCalf.localRotation;
                turnBaseLeftFoot = leftFoot.localRotation; turnBaseRightFoot = rightFoot.localRotation;
                turnBodyOffset = turnStepOffset = Vector3.zero;
                float seedOffset = IntercomTurnStepState.DeltaAngle(rootYaw, turnSteps.LowerYaw);
                body.rotation = Quaternion.AngleAxis(seedOffset, Vector3.up) * body.rotation;
                turnLeftAnkle = leftFoot.position; turnRightAnkle = rightFoot.position;
                turnLeftRotation = leftFoot.rotation; turnRightRotation = rightFoot.rotation;
                turnHasPose = turnReady = true; turnExiting = false;
                turnPublishedStep = 0;
            }
            turnSteps.stepDuration = Mathf.Clamp(turnStepSeconds, 0.12f, 0.35f);
            turnSteps.startDegrees = Mathf.Clamp(stationaryWaistFreedom, 8f,
                Mathf.Max(8f, Mathf.Clamp(maximumTurnTorsoYaw, 15f, 50f) - 3f));
            turnSteps.maxStepDegrees = Mathf.Min(Mathf.Clamp(turnStepDegrees, 15f, 20f),
                Mathf.Clamp(maximumTurnTorsoYaw, 15f, 50f));
            turnSteps.Advance(rootYaw, motor.ActualPlanarSpeed, true, dt);
            if (turnSteps.StartedThisTick)
            {
                turnCommittedTwistLimit = Mathf.Max(Mathf.Clamp(maximumTurnTorsoYaw, 15f, 50f),
                    Mathf.Abs(IntercomTurnStepState.DeltaAngle(rootYaw, turnSteps.LowerYaw)));
                turnStanceAnkle = turnSteps.SwingLeft ? turnRightAnkle : turnLeftAnkle;
                turnSwingStartAnkle = turnSteps.SwingLeft ? turnLeftAnkle : turnRightAnkle;
                turnSwingDirection = Vector3.ProjectOnPlane(turnSteps.SwingLeft ? turnLeftLink : turnRightLink, Vector3.up).normalized;
                turnStanceRotation = turnSteps.SwingLeft ? turnRightRotation : turnLeftRotation;
                turnSwingStartRotation = turnSteps.SwingLeft ? turnLeftRotation : turnRightRotation;
                turnSwingStartCalf = turnSteps.SwingLeft ? turnCachedLeftCalf : turnCachedRightCalf;
                turnStepOffset = turnBodyOffset;
                Contact floor = turnSteps.SwingLeft ? leftContact : rightContact;
                turnSwingStartSoleY = TurnSole(turnSteps.SwingLeft).y;
                turnSwingFloorY = floor.valid ? floor.point.y : turnSwingStartSoleY;
            }
            // Freeze only the lower-body Idle baseline. The torso/arms retain
            // authored Idle, aim and posture keys and the suspension below.
            body.localPosition = turnBaseBodyPosition;
            body.localRotation = turnBaseBodyRotation;
            leftCalf.localRotation = turnBaseLeftCalf; rightCalf.localRotation = turnBaseRightCalf;
            leftFoot.localRotation = turnBaseLeftFoot; rightFoot.localRotation = turnBaseRightFoot;
            turnAppliedYaw = IntercomTurnStepState.DeltaAngle(rootYaw, turnSteps.LowerYaw);
            body.rotation = Quaternion.AngleAxis(turnAppliedYaw, Vector3.up) * body.rotation;
            Vector3 neutralBody = body.position;
            bool stepping = turnSteps.Active || turnSteps.LandedThisTick;
            if (!stepping)
            {
                body.position += turnBodyOffset;
                // Preserve the final stance calf as well as boot orientation.
                if (turnSteps.StepId > 0)
                {
                    leftCalf.localRotation = turnCachedLeftCalf; rightCalf.localRotation = turnCachedRightCalf;
                    leftFoot.rotation = turnLeftRotation; rightFoot.rotation = turnRightRotation;
                }
                return true;
            }
            float phase = turnSteps.Phase;
            float transfer = TurnSmooth((phase - 0.10f) / 0.78f);
            body.position += turnStepOffset * (1f - transfer);
            Transform stanceCalf = turnSteps.SwingLeft ? rightCalf : leftCalf;
            Transform stanceFoot = turnSteps.SwingLeft ? rightFoot : leftFoot;
            Transform swingCalf = turnSteps.SwingLeft ? leftCalf : rightCalf;
            Transform swingFoot = turnSteps.SwingLeft ? leftFoot : rightFoot;
            Quaternion neutralSwingFoot = swingFoot.rotation;
            Vector3 neutralSwingAnkle = swingFoot.position - turnStepOffset * (1f - transfer);
            // A single rigid link: rotate toward support, then translate Body
            // by the residual. Never change Foot.localPosition or bone scale.
            Quaternion support = Quaternion.FromToRotation(stanceFoot.position - stanceCalf.position,
                turnStanceAnkle - stanceCalf.position);
            stanceCalf.rotation = Quaternion.RotateTowards(Quaternion.identity, support, 65f) * stanceCalf.rotation;
            body.position += turnStanceAnkle - stanceFoot.position;
            stanceFoot.rotation = turnStanceRotation;
            turnBodyOffset = body.position - neutralBody;
            Quaternion swingBase = turnSteps.SwingLeft ? turnBaseLeftCalf : turnBaseRightCalf;
            swingCalf.localRotation = Quaternion.Slerp(turnSwingStartCalf, swingBase, transfer);
            Vector3 right = Quaternion.AngleAxis(turnSteps.LowerYaw, Vector3.up) * new Vector3(1f, 0f, 0f);
            float lift = Mathf.Sin(phase * Mathf.PI);
            float roll = lift * lift * Mathf.Lerp(6f, -3f, TurnSmooth((phase - 0.42f) / 0.36f));
            Quaternion footRotation = Quaternion.AngleAxis(roll, right)
                * Quaternion.Slerp(turnSwingStartRotation, neutralSwingFoot, transfer);
            swingFoot.rotation = footRotation;
            float soleBelowAnkle = TurnSole(turnSteps.SwingLeft).y - swingFoot.position.y;
            float requiredAnkleY = Mathf.Lerp(turnSwingStartSoleY, turnSwingFloorY + 0.003f, transfer)
                + 0.02f * lift * lift - soleBelowAnkle;
            Vector3 landing = turnSwingStartAnkle + (neutralSwingAnkle - turnSwingStartAnkle) * transfer;
            // These broad boots need room at their inner heels during a turn.
            // Bias the reachable landing outwards instead of crossing the
            // support boot while the two feet have different headings.
            Vector3 leftOutward = Vector3.ProjectOnPlane(leftCalf.position - rightCalf.position, Vector3.up).normalized;
            landing += leftOutward * (turnSteps.SwingLeft ? 0.025f : -0.025f) * transfer;
            PlaceTurnSwing(swingCalf, swingFoot, landing, requiredAnkleY, dt);
            swingFoot.rotation = footRotation;
            return true;
        }

        // Intersect the rigid leg sphere with the required sole-height plane.
        // Choose the reachable point closest to the neutral landing position,
        // so alternating steps recenter instead of ratcheting backwards.
        private void PlaceTurnSwing(Transform calf, Transform foot, Vector3 landing, float targetY, float dt)
        {
            Vector3 link = foot.position - calf.position;
            float length = link.magnitude;
            if (length < 0.01f) return;
            float maximumY = -length * (float)Math.Cos(Mathf.Clamp(turnLiftDegrees, 35f, 75f) * Math.PI / 180.0);
            float vertical = Mathf.Clamp(targetY - calf.position.y, -length, maximumY);
            float radius = (float)Math.Sqrt(Math.Max(0.0, length * length - vertical * vertical));
            Vector3 horizontal = Vector3.ProjectOnPlane(landing - calf.position, Vector3.up);
            if (horizontal.sqrMagnitude < 0.00000001f) horizontal = Vector3.ProjectOnPlane(link, Vector3.up);
            if (horizontal.sqrMagnitude < 0.00000001f)
                horizontal = Quaternion.AngleAxis(turnSteps.LowerYaw, Vector3.up) * Vector3.forward;
            horizontal = horizontal.normalized;
            // The closest point on a circle can jump to its opposite side when
            // its target crosses the centre. Follow that azimuth at a bounded
            // rate, retaining the exact height and link length throughout.
            if (turnSwingDirection.sqrMagnitude < 0.1f) turnSwingDirection = horizontal;
            Quaternion azimuth = Quaternion.FromToRotation(turnSwingDirection, horizontal);
            float swingSpeed = 720f * (0.18f / Mathf.Clamp(turnStepSeconds, 0.12f, 0.35f));
            turnSwingDirection = Quaternion.RotateTowards(Quaternion.identity, azimuth, swingSpeed * dt) * turnSwingDirection;
            turnSwingDirection = Vector3.ProjectOnPlane(turnSwingDirection, Vector3.up).normalized;
            Vector3 target = turnSwingDirection * radius + Vector3.up * vertical;
            calf.rotation = Quaternion.FromToRotation(link, target) * calf.rotation;
        }

        private Vector3 TurnSole(bool left)
        {
            Transform foot = left ? leftFoot : rightFoot;
            Transform toe = left ? leftToe : rightToe;
            Vector3 result = foot.TransformPoint(left ? leftSoleOffset : rightSoleOffset);
            Vector3 heel = foot.TransformPoint(left ? leftHeelOffset : rightHeelOffset);
            Vector3 tip = (toe != null ? toe : foot).TransformPoint(left ? leftToeOffset : rightToeOffset);
            if (heel.y < result.y) result = heel;
            if (tip.y < result.y) result = tip;
            return result;
        }

        private void CacheTurnPose()
        {
            if (!turnHasPose || turnExiting) return;
            turnLeftAnkle = leftFoot.position; turnRightAnkle = rightFoot.position;
            turnLeftLink = leftFoot.position - leftCalf.position; turnRightLink = rightFoot.position - rightCalf.position;
            turnLeftRotation = leftFoot.rotation; turnRightRotation = rightFoot.rotation;
            turnCachedBodyPosition = body.localPosition; turnCachedBodyRotation = body.localRotation;
            turnCachedLeftCalf = leftCalf.localRotation; turnCachedRightCalf = rightCalf.localRotation;
            turnCachedLeftFoot = leftFoot.localRotation; turnCachedRightFoot = rightFoot.localRotation;
        }

        private void PublishTurnLanding()
        {
            if (!turnSteps.LandedThisTick || turnSteps.StepId == turnPublishedStep || turnExiting || !motor.IsGrounded) return;
            turnPublishedStep = turnSteps.StepId;
            Contact contact = turnSteps.SwingLeft ? leftContact : rightContact;
            if (!contact.valid || contact.height > 0.04f) return;
            compression = 0.45f;
            IsTurnFootstep = true;
            try { Footstep?.Invoke(contact.collider, contact.point, 0.42f, turnSteps.SwingLeft); }
            finally { IsTurnFootstep = false; }
        }

        private void ResetTurnFootwork()
        {
            turnSteps.Reset(motor != null ? TurnYaw(motor.FacingDirection) : 0f);
            turnReady = turnHasPose = turnExiting = IsTurnFootstep = false;
            turnAppliedYaw = turnExitWeight = turnCommittedTwistLimit = 0f;
            turnBodyOffset = turnStepOffset = Vector3.zero;
            turnPublishedStep = 0;
        }
        private static float TurnYaw(Vector3 direction) => (float)(Math.Atan2(direction.x, direction.z) * 180.0 / Math.PI);
        private static float TurnSmooth(float value) { float t = Mathf.Clamp01(value); return t * t * (3f - 2f * t); }
    }
}
