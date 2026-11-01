namespace IntercomMvp
{
    /// <summary>Tracks cancellation epochs separately from ordinary button/finger release.</summary>
    public sealed class IntercomInputCancellation
    {
        private object observedSource;
        private uint observedVersion;
        private bool hasSnapshot;

        /// <summary>The first observation establishes a baseline. A later source change
        /// or cancellation-version change invalidates work queued by the old input.</summary>
        public bool Observe(object source, uint version)
        {
            bool cancelled = hasSnapshot
                && (!ReferenceEquals(observedSource, source) || observedVersion != version);
            observedSource = source;
            observedVersion = version;
            hasSnapshot = true;
            return cancelled;
        }
    }
}
