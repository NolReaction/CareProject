using System.Collections.Generic;

namespace IntercomMvp
{
    /// <summary>Captures a reload-button pointer until release, preventing a UI tap from becoming fire.</summary>
    public sealed class IntercomReloadControl
    {
        private readonly HashSet<int> pointers = new HashSet<int>();
        private float minX, minY, maxX, maxY;
        public void Configure(float x, float y, float width, float height)
        { minX = x; minY = y; maxX = x + width; maxY = y + height; }
        public bool PointerDown(int id, float x, float y)
        {
            pointers.Remove(id);
            if (x < minX || x > maxX || y < minY || y > maxY) return false;
            pointers.Add(id); return true;
        }
        public bool Owns(int id) => pointers.Contains(id);
        public void PointerUp(int id) { pointers.Remove(id); }
        public void Reset() { pointers.Clear(); }
    }
}
