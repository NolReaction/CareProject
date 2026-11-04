using System;
using UnityEngine;

namespace IntercomMvp
{
    public sealed partial class IntercomGroundFeedback
    {
        [Tooltip("Real sole vertices and skin weights. Setup Robot rebuilds this profile after a model change.")]
        public IntercomGroundFeedbackSoleProfile soleProfile;
        private IntercomGroundFeedbackSoleProfile calibratedSoleProfile;
        private bool sampleRunningSoles;

        private struct SoleSample
        {
            internal Vector3 footOffset, toeOffset;
            internal float toeWeight;
            internal Vector3 Position(FootRig rig)
            {
                Vector3 footPoint = rig.foot.TransformPoint(footOffset);
                if (rig.toe == null || toeWeight <= 0f) return footPoint;
                return footPoint * (1f - toeWeight) + rig.toe.TransformPoint(toeOffset) * toeWeight;
            }
        }

        private void CalibrateSoleClouds()
        {
            leftRig.samples = rightRig.samples = null;
            calibratedSoleProfile = soleProfile;
            if (body != null && leftFoot != null && rightFoot != null && leftCalf != null && rightCalf != null)
            {
                leftRig.neutralLink = Quaternion.Inverse(body.rotation) * (leftFoot.position - leftCalf.position);
                rightRig.neutralLink = Quaternion.Inverse(body.rotation) * (rightFoot.position - rightCalf.position);
                leftRig.neutralLinkReady = rightRig.neutralLinkReady = true;
            }
            if (soleProfile == null || leftFoot == null || rightFoot == null) return;
            Vector3 lateral = Vector3.ProjectOnPlane(leftFoot.position - rightFoot.position, Vector3.up).normalized;
            if (lateral.sqrMagnitude < 0.5f) return;
            leftRig.samples = MakeSoleCloud(soleProfile.left, leftFoot, leftCalf, leftToe, lateral);
            rightRig.samples = MakeSoleCloud(soleProfile.right, rightFoot, rightCalf, rightToe, lateral);
        }

        private static SoleSample[] MakeSoleCloud(IntercomGroundFeedbackSoleProfile.Point[] points,
            Transform foot, Transform calf, Transform toe, Vector3 lateral)
        {
            if (points == null || points.Length == 0 || foot == null || calf == null || toe == null) return null;
            float length = Vector3.Distance(foot.position, calf.position);
            if (length < 0.01f) return null;
            Vector3 forward = Vector3.ProjectOnPlane(toe.position - foot.position, Vector3.up);
            forward = Vector3.ProjectOnPlane(forward, lateral).normalized;
            if (forward.sqrMagnitude < 0.5f) return null;
            SoleSample[] result = new SoleSample[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 point = points[i].footPosition;
                Vector3 toePoint = points[i].toePosition;
                if (!FiniteSole(point.x) || !FiniteSole(point.y) || !FiniteSole(point.z)
                    || !FiniteSole(toePoint.x) || !FiniteSole(toePoint.y) || !FiniteSole(toePoint.z)
                    || !FiniteSole(points[i].toeWeight)) return null;
                Vector3 world = foot.position + (lateral * point.x + Vector3.up * point.y + forward * point.z) * length;
                Vector3 toeWorld = foot.position + (lateral * toePoint.x + Vector3.up * toePoint.y + forward * toePoint.z) * length;
                result[i] = new SoleSample { footOffset = foot.InverseTransformPoint(world),
                    toeOffset = toe.InverseTransformPoint(toeWorld), toeWeight = Mathf.Clamp01(points[i].toeWeight) };
            }
            return result;
        }
        private static bool FiniteSole(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private bool ProbeSoleCloud(FootRig rig, out Contact contact)
        {
            contact = default;
            rig.sampleIndex = -1;
            if (!sampleRunningSoles || rig.samples == null || rig.samples.Length == 0) return false;
            Matrix4x4 footMatrix = rig.foot.localToWorldMatrix;
            Matrix4x4 toeMatrix = rig.toe != null ? rig.toe.localToWorldMatrix : footMatrix;
            Vector3 lowest = SamplePosition(rig.samples[0], footMatrix, toeMatrix);
            int selected = 0;
            for (int i = 1; i < rig.samples.Length; i++)
            {
                Vector3 point = SamplePosition(rig.samples[i], footMatrix, toeMatrix);
                if (point.y < lowest.y) { lowest = point; selected = i; }
            }
            rig.sampleIndex = selected;
            rig.sole = lowest;
            contact = ProbePoint(lowest);
            return true;
        }

        private static Vector3 SamplePosition(SoleSample point, Matrix4x4 foot, Matrix4x4 toe)
            => foot.MultiplyPoint3x4(point.footOffset) * (1f - point.toeWeight)
                + toe.MultiplyPoint3x4(point.toeOffset) * point.toeWeight;

        private static Vector3 AnchorMaterialPoint(FootRig rig)
        {
            if (rig.anchorSampleIndex >= 0 && rig.samples != null && rig.anchorSampleIndex < rig.samples.Length)
                return rig.samples[rig.anchorSampleIndex].Position(rig);
            return rig.anchorMarker.TransformPoint(rig.anchorMarkerOffset);
        }
    }
}
