using System;

namespace IntercomMvp
{
    /// <summary>Pointer ownership and radial dead zones, independent of Unity and frame rate.</summary>
    public sealed class IntercomTwinStickState
    {
        public sealed class Stick
        {
            public bool Captured { get; private set; }
            public int PointerId { get; private set; }
            public float RawX { get; private set; }
            public float RawY { get; private set; }
            public float X { get; private set; }
            public float Y { get; private set; }
            public bool Active => X != 0f || Y != 0f;
            internal float CenterX, CenterY, Radius, DeadZone;
            internal bool Seen;

            internal void Capture(int id, float x, float y)
            {
                Captured = true;
                PointerId = id;
                Move(x, y);
            }

            internal void Move(float x, float y)
            {
                Seen = true;
                float dx = (x - CenterX) / Radius;
                float dy = (y - CenterY) / Radius;
                float length = (float)Math.Sqrt(dx * dx + dy * dy);
                float clamped = Math.Min(1f, length);
                if (length > 0f)
                {
                    RawX = dx / length * clamped;
                    RawY = dy / length * clamped;
                }
                else RawX = RawY = 0f;
                if (clamped <= DeadZone || length <= 0f) { X = Y = 0f; return; }
                float strength = (clamped - DeadZone) / (1f - DeadZone);
                X = dx / length * strength;
                Y = dy / length * strength;
            }

            internal void Clear()
            {
                Captured = Seen = false;
                PointerId = 0;
                RawX = RawY = X = Y = 0f;
            }

            internal float DistanceSquared(float x, float y)
            {
                float dx = x - CenterX, dy = y - CenterY;
                return dx * dx + dy * dy;
            }
        }

        public Stick Left { get; } = new Stick();
        public Stick Right { get; } = new Stick();
        public bool AnyCaptured => Left.Captured || Right.Captured;
        private bool configured, invalidContact;

        /// <summary>Screen coordinates use a bottom-left origin, matching Unity touch positions.</summary>
        public void Configure(float leftX, float leftY, float rightX, float rightY, float radius,
            float movementDeadZone, float aimDeadZone)
        {
            if (!Finite(leftX) || !Finite(leftY) || !Finite(rightX) || !Finite(rightY)
                || !Finite(radius) || radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius), "Stick layout must be finite with a positive radius.");
            movementDeadZone = ClampDeadZone(movementDeadZone);
            aimDeadZone = ClampDeadZone(aimDeadZone);
            bool moved = !configured || Left.CenterX != leftX || Left.CenterY != leftY
                || Right.CenterX != rightX || Right.CenterY != rightY || Left.Radius != radius
                || Left.DeadZone != movementDeadZone || Right.DeadZone != aimDeadZone;
            Left.CenterX = leftX; Left.CenterY = leftY;
            Right.CenterX = rightX; Right.CenterY = rightY;
            Left.Radius = Right.Radius = radius;
            Left.DeadZone = movementDeadZone; Right.DeadZone = aimDeadZone;
            configured = true;
            // Rotation/safe-area changes invalidate old screen-space contacts.
            if (moved) Reset();
        }

        public void BeginFrame() { Left.Seen = Right.Seen = invalidContact = false; }

        /// <summary>Only a new contact can claim a stick; crossing the screen never switches ownership.</summary>
        public bool PointerDown(int id, float x, float y)
        {
            if (!configured || !Finite(x) || !Finite(y)) return false;
            if (Owns(id)) { PointerMove(id, x, y); return true; }
            float hitRadius = Left.Radius * 1.3f;
            float leftDistance = Left.DistanceSquared(x, y), rightDistance = Right.DistanceSquared(x, y);
            bool canLeft = !Left.Captured && leftDistance <= hitRadius * hitRadius;
            bool canRight = !Right.Captured && rightDistance <= hitRadius * hitRadius;
            if (canLeft && (!canRight || leftDistance <= rightDistance)) Left.Capture(id, x, y);
            else if (canRight) Right.Capture(id, x, y);
            else return false;
            return true;
        }

        public void PointerMove(int id, float x, float y)
        {
            if (!Finite(x) || !Finite(y)) { invalidContact |= Owns(id); PointerUp(id); return; }
            if (Left.Captured && Left.PointerId == id) Left.Move(x, y);
            else if (Right.Captured && Right.PointerId == id) Right.Move(x, y);
        }

        public void PointerUp(int id)
        {
            if (Left.Captured && Left.PointerId == id) Left.Clear();
            if (Right.Captured && Right.PointerId == id) Right.Clear();
        }

        public bool Owns(int id) => (Left.Captured && Left.PointerId == id) || (Right.Captured && Right.PointerId == id);

        /// <summary>Returns true for lost/invalid OS contacts, distinct from a normal user release.</summary>
        public bool EndFrame()
        {
            bool cancelled = invalidContact;
            if (Left.Captured && !Left.Seen) { Left.Clear(); cancelled = true; }
            if (Right.Captured && !Right.Seen) { Right.Clear(); cancelled = true; }
            return cancelled;
        }

        public void Reset() { Left.Clear(); Right.Clear(); invalidContact = false; }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static float ClampDeadZone(float value) => Finite(value) ? Math.Max(0f, Math.Min(0.8f, value)) : 0.2f;
    }
}
