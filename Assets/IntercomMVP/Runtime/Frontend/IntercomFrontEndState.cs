namespace IntercomMvp
{
    public enum IntercomFrontEndSection { Home, Play, Robot, Armory }

    /// <summary>Navigation state only. Mission, inventory and economy keep their own state owners.</summary>
    public sealed class IntercomFrontEndState
    {
        public IntercomFrontEndSection CurrentSection { get; private set; } = IntercomFrontEndSection.Home;
        public uint Revision { get; private set; }

        public bool Select(IntercomFrontEndSection section)
        {
            if (!Valid(section) || CurrentSection == section) return false;
            CurrentSection = section;
            unchecked { Revision++; }
            return true;
        }

        public bool Back() => Select(IntercomFrontEndSection.Home);
        public static bool Valid(IntercomFrontEndSection section) => (uint)section <= (uint)IntercomFrontEndSection.Armory;
    }
}
