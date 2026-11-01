namespace IntercomMvp
{
    /// <summary>Select a closest valid hit from an unordered, complete physics result.</summary>
    public struct IntercomProjectileHitChoice
    {
        private float nearest;
        private readonly float maximum;
        public int Index { get; private set; }

        public IntercomProjectileHitChoice(float maximumDistance)
        {
            nearest = float.PositiveInfinity;
            maximum = maximumDistance;
            Index = -1;
        }

        public void Consider(int index, float distance, bool owner)
        {
            if (owner || index < 0 || float.IsNaN(distance) || float.IsInfinity(distance)
                || distance < 0f || distance > maximum || distance >= nearest) return;
            nearest = distance;
            Index = index;
        }

        public static bool IsComplete(int hitCount, int capacity) => capacity > 0 && hitCount >= 0 && hitCount < capacity;
    }
}
