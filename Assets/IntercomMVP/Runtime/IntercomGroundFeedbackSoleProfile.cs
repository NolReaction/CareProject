using System;
using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Small exact skinning stencil for the soles, authored from real mesh vertices.</summary>
    [CreateAssetMenu(menuName = "Intercom MVP/Sole Contact Profile")]
    public sealed class IntercomGroundFeedbackSoleProfile : ScriptableObject
    {
        [Serializable]
        public struct Point
        {
            // In the sampled Idle anatomical frame: +X toward the left boot,
            // +Y up, +Z toward the toes; units are the calf-to-ankle length.
            public Vector3 footPosition;
            public Vector3 toePosition;
            [Range(0f, 1f)] public float toeWeight;
        }
        [HideInInspector] public string sourceGeometry;
        public Point[] left = new Point[0];
        public Point[] right = new Point[0];
    }
}
