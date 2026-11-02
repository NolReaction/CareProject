using System;
using UnityEngine;

namespace IntercomMvp
{
    public sealed partial class IntercomGroundFeedback
    {
        [Header("Running support")]
        [Range(0f, 0.06f)] public float runBodyCorrection = 0.035f;
        [Tooltip("Keep the body clear of the short leg cuffs during running lean and sway.")]
        [Range(0f, 1f)] public float runningTiltWeight = 0.75f;
        private CharacterController runningController;

        private void AlignRunningGround(float blend)
        {
            if (body == null || !plantFeet) return;
            if (runningController == null) runningController = GetComponent<CharacterController>();
            if (runningController == null) return;
            // The capsule was fitted to the Idle soles. Account for its actual
            // ground/skin displacement once for the whole gait, including the
            // short flight phase, rather than pumping this offset per foot.
            Vector3 reference = runningController.transform.TransformPoint(
                runningController.center - Vector3.up * (runningController.height * 0.5f));
            Contact floor = ProbePoint(reference);
            if (!floor.valid) return;
            float correction = Mathf.Clamp(floor.point.y + 0.003f - reference.y,
                -bodyHeightCompliance, bodyHeightCompliance);
            body.position += Vector3.up * correction * blend;
        }

        private void RestoreDiagonalStride(float phase, float blend)
        {
            if (!plantFeet || body == null) return;
            float x = animator.GetFloat("MoveX"), y = animator.GetFloat("MoveY");
            float length = (float)Math.Sqrt(x * x + y * y);
            if (length < 0.01f) return;
            // Four cardinal clips form a diamond. Their diagonal blend loses
            // about 29% of the travel stroke; increasing playback instead would
            // make these short legs chatter. Restore the stroke on the rigid
            // reach sphere, including the heel landing and swing transitions.
            float gain = Mathf.Clamp((Math.Abs(x) + Math.Abs(y)) / length, 1f, 1.414214f);
            if (gain < 1.001f) return;
            Vector3 forward = motor.FacingDirection;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 travel = (right * (x * IntercomLocomotionCadence.SafeStrafeRatio(motor.strafeSpeedRatio)) + forward * y).normalized;
            RestoreDiagonalFoot(leftRig, gain, travel, blend);
            RestoreDiagonalFoot(rightRig, gain, travel, blend);
        }

        private void RestoreDiagonalFoot(FootRig rig, float gain, Vector3 travel, float weight)
        {
            if (weight <= 0f || !rig.neutralLinkReady || rig.foot == null || rig.calf == null) return;
            Vector3 link = rig.foot.position - rig.calf.position;
            float length = link.magnitude;
            if (length < 0.01f) return;
            Vector3 neutral = body.rotation * rig.neutralLink;
            float stroke = Vector3.Dot(link - neutral, travel);
            Vector3 horizontal = Vector3.ProjectOnPlane(link + travel * (stroke * (gain - 1f) * weight), Vector3.up);
            horizontal = Vector3.ClampMagnitude(horizontal, length * 0.95f);
            float vertical = (float)Math.Sqrt(Math.Max(0f, length * length - horizontal.sqrMagnitude));
            Vector3 target = horizontal + Vector3.up * (link.y <= 0f ? -vertical : vertical);
            Quaternion authored = rig.foot.rotation;
            rig.calf.rotation = Quaternion.FromToRotation(link, target) * rig.calf.rotation;
            rig.foot.rotation = authored;
        }

        private void PrepareRunningFoot(FootRig rig, float phase, float blend, bool grounded)
        {
            rig.weight = 0f;
            if (rig.foot == null) return;
            rig.authoredAnkle = rig.foot.position;
            rig.authoredRotation = rig.targetRotation = rig.foot.rotation;
            rig.contact = ProbeFoot(rig);
            float support = IntercomGroundMath.StanceWeight(phase, rig.left) * blend;
            rig.inWindow = support > 0f;
            if (!grounded || support <= 0f || !rig.contact.valid || rig.calf == null || rig.foot.parent != rig.calf)
            { rig.anchored = false; return; }
            rig.length = Vector3.Distance(rig.calf.position, rig.foot.position);
            if (rig.length < 0.01f || rig.contact.height > 0.08f) { rig.anchored = false; return; }
            rig.weight = support;
            float friction = IntercomGroundMath.PlantLockWeight(phase, rig.left);
            if (friction <= 0f) rig.anchored = false;
            Quaternion slope = Quaternion.RotateTowards(Quaternion.identity,
                Quaternion.FromToRotation(Vector3.up, rig.contact.normal), maximumGroundTilt);
            Quaternion onGround = Quaternion.Slerp(Quaternion.identity, slope, support) * rig.authoredRotation;
            if (friction > 0f && (!rig.anchored || rig.surface != rig.contact.collider))
            {
                rig.surface = rig.contact.collider;
                rig.anchorSampleIndex = rig.sampleIndex;
                rig.anchorMarker = rig.soleKind == 2 && rig.toe != null ? rig.toe : rig.foot;
                rig.anchorMarkerOffset = rig.soleKind == 2 ? (rig.left ? leftToeOffset : rightToeOffset)
                    : rig.soleKind == 1 ? (rig.left ? leftHeelOffset : rightHeelOffset)
                    : (rig.left ? leftSoleOffset : rightSoleOffset);
                rig.anchorLocal = rig.surface.transform.InverseTransformPoint(AnchorMaterialPoint(rig));
                rig.anchorRotation = onGround;
                rig.anchorFacing = motor.FacingDirection;
                rig.anchorTravel = motor.ActualPlanarVelocity;
                rig.anchored = true;
            }
            if (rig.anchored)
            {
                float turn = Vector3.Angle(rig.anchorFacing, motor.FacingDirection);
                float travelTurn = Vector3.Angle(rig.anchorTravel, motor.ActualPlanarVelocity);
                friction *= Mathf.Clamp01(1f - Mathf.Max(0f, turn - 15f) / 30f);
                friction *= Mathf.Clamp01(1f - Mathf.Max(0f, travelTurn - 25f) / 35f);
            }
            rig.targetRotation = rig.anchored ? Quaternion.Slerp(onGround, rig.anchorRotation, friction) : onGround;
            // Measure the chosen material point in the final boot orientation.
            // Both Foot and Toe skin contributions follow this rigid change.
            rig.foot.rotation = rig.targetRotation;
            Contact posed = ProbeFoot(rig);
            if (posed.valid) rig.contact = posed;
            Vector3 target = rig.authoredAnkle;
            if (rig.anchored && rig.surface != null)
            {
                Vector3 anchor = rig.surface.transform.TransformPoint(rig.anchorLocal);
                Vector3 offset = Vector3.ProjectOnPlane(anchor - AnchorMaterialPoint(rig), Vector3.up);
                float limit = Mathf.Min(maximumPlantCorrection, rig.length * 0.7f);
                float excess = offset.magnitude - limit;
                if (excess > 0f && limit > 0.001f) friction *= Mathf.Clamp01(1f - excess / (limit * 0.5f));
                target += Vector3.ClampMagnitude(offset, limit) * friction;
            }
            float gap = Vector3.Dot(rig.sole - rig.contact.point, rig.contact.normal);
            target -= rig.contact.normal * Mathf.Clamp(gap - 0.003f, -0.04f, 0.08f);
            rig.targetAnkle = target;
            rig.foot.rotation = rig.authoredRotation;
        }

        private void SolveRunningSupport()
        {
            // Authored support windows do not overlap. Retain a single contact
            // authority if a custom phase profile ever produces a tiny overlap.
            FootRig rig = leftRig.weight >= rightRig.weight ? leftRig : rightRig;
            if (rig.weight <= 0f || rig.calf == null || rig.foot == null || body == null) return;
            Vector3 delta = rig.targetAnkle - rig.authoredAnkle;
            Vector3 normal = rig.contact.normal;
            float gap = Vector3.Dot(rig.sole - rig.contact.point, normal);
            float verticalWeight = gap < 0f ? 1f : rig.weight;
            Vector3 desired = rig.authoredAnkle + Vector3.ProjectOnPlane(delta, normal) * rig.weight
                + normal * Vector3.Dot(delta, normal) * verticalWeight;
            Vector3 source = rig.foot.position - rig.calf.position;
            Vector3 target = desired - rig.calf.position;
            if (source.sqrMagnitude < 0.00001f || target.sqrMagnitude < 0.00001f) return;
            Quaternion correction = Quaternion.RotateTowards(Quaternion.identity,
                Quaternion.FromToRotation(source, target), maximumCalfCorrection);
            rig.calf.rotation = correction * rig.calf.rotation;
            rig.foot.rotation = rig.targetRotation;
            // Finish the rigid-link solve with its bounded residual. The old
            // solver stopped after a weighted 28-degree rotation, leaving the
            // sole hovering/slipping even while it was declared planted.
            body.position += Vector3.ClampMagnitude(desired - rig.foot.position, runBodyCorrection);
        }
        private void ResetRunningClearance()
        {
            leftRig.clearanceActive = rightRig.clearanceActive = false;
            leftRig.clearanceDirection = rightRig.clearanceDirection = Vector3.zero;
        }

        private void PreserveRunningClearance(float phase, float dt)
        {
            FootRig support = leftRig.weight >= rightRig.weight ? leftRig : rightRig;
            KeepSwingAboveGround(leftRig, support.weight > 0f && support == leftRig, phase, dt);
            KeepSwingAboveGround(rightRig, support.weight > 0f && support == rightRig, phase, dt);
        }

        private void KeepSwingAboveGround(FootRig rig, bool supportsBody, float phase, float dt)
        {
            if (rig.foot == null || rig.calf == null) return;
            Vector3 link = rig.foot.position - rig.calf.position;
            Vector3 lateral = Vector3.ProjectOnPlane(leftCalf.position - rightCalf.position, Vector3.up).normalized;
            Vector3 outward = lateral * (rig.left ? 1f : -1f);
            if (supportsBody && (!rig.clearanceActive || IntercomGroundMath.PlantLockWeight(phase, rig.left) > 0f))
            {
                rig.clearanceActive = false;
                rig.clearanceDirection = Vector3.ProjectOnPlane(link, Vector3.up).normalized;
                return;
            }
            Contact floor = ProbeFoot(rig);
            if (!floor.valid) floor = ProbePoint(rig.sole + Vector3.up * 0.08f);
            if (!floor.valid) return;
            float localPhase = phase - (float)Math.Floor(phase) - (rig.left ? 0f : 0.5f);
            if (localPhase < 0f) localPhase += 1f;
            float flight = Mathf.Clamp01((localPhase - 0.4f) / 0.6f);
            float arc = Mathf.Sin(flight * Mathf.PI);
            float minimum = 0.003f + 0.008f * arc * arc;
            float gap = Vector3.Dot(rig.sole - floor.point, floor.normal);
            Vector3 tangent = Vector3.ProjectOnPlane(link, floor.normal);
            bool needsClearance = gap < minimum;
            if (!needsClearance && !rig.clearanceActive)
            {
                rig.clearanceDirection = tangent.normalized;
                return;
            }
            rig.clearanceActive = true;
            float length = link.magnitude;
            if (length < 0.01f) return;
            float vertical = Vector3.Dot(link, floor.normal) + Mathf.Max(0f, minimum - gap);
            // This is a reachable-height correction, never a translated boot.
            // Keep the same conservative horizontal reach used by the gait.
            float maximumVertical = -length * (float)Math.Sqrt(1f - 0.95f * 0.95f);
            vertical = needsClearance ? Mathf.Clamp(vertical, -length, maximumVertical) : vertical;
            float radius = (float)Math.Sqrt(Math.Max(0f, length * length - vertical * vertical));
            tangent = tangent.sqrMagnitude > 0.0000001f ? tangent.normalized : outward;
            float inward = Vector3.Dot(tangent, outward);
            if (needsClearance && inward < 0.25f)
            {
                float steer = Mathf.Clamp01((minimum - gap) / 0.01f);
                tangent = (tangent + outward * ((0.25f - inward) * steer)).normalized;
            }
            Vector3 desiredTangent = tangent;
            if (rig.clearanceDirection.sqrMagnitude < 0.1f) rig.clearanceDirection = tangent;
            Quaternion azimuth = Quaternion.FromToRotation(rig.clearanceDirection, tangent);
            // Unlike a stationary turn, this arc must finish within a running
            // half-cycle. Scale its angular budget with the actual clip rate;
            // carrying a fixed 720-degree budget into flat support caused a snap.
            float angularSpeed = Mathf.Max(720f, (1800f / 2.1f) * animator.GetFloat("RunRate"));
            tangent = Quaternion.RotateTowards(Quaternion.identity, azimuth, angularSpeed * dt) * rig.clearanceDirection;
            tangent = Vector3.ProjectOnPlane(tangent, floor.normal).normalized;
            Vector3 target = tangent * radius + floor.normal * vertical;
            Quaternion authored = rig.foot.rotation;
            rig.calf.rotation = Quaternion.FromToRotation(link, target) * rig.calf.rotation;
            rig.foot.rotation = authored;
            rig.clearanceDirection = tangent;
            if (!needsClearance && Vector3.Angle(tangent, desiredTangent) < 0.5f) rig.clearanceActive = false;
        }
    }
}
