using System;

namespace IntercomMvp
{
    /// <summary>Ground-plane camera tracking, independent of Unity so the actual production motion can be tested.</summary>
    public sealed class IntercomCameraTracking
    {
        public struct Projection
        {
            public double rightX, rightY, rightZ, upX, upY, upZ, forwardX, forwardY, forwardZ;
            public double halfHeight, aspect, distance;
            public bool orthographic;
        }

        public struct Settings
        {
            public bool freeMovement, recenterWhenIdle;
            // Fractions of the full frame. A width of .18 means +/-9% of the viewport.
            public double zoneWidth, zoneHeight, safetyWidth, safetyHeight;
            public double followSeconds, recenterDelay, recenterSeconds, idleSpeed;
            public double anticipationDistance, anticipationSeconds, teleportDistance;
            public bool adaptiveMotion;
            public double velocityFilterSeconds, velocityLeadSeconds, maximumLeadDistance;
            public double runningSpeed, runningFollowSeconds, reversalBrakeSeconds;
        }

        public double FocusX { get; private set; }
        public double FocusZ { get; private set; }
        public double AnchorX { get; private set; }
        public double AnchorZ { get; private set; }
        public double IdleSeconds { get; private set; }
        public bool Recentering { get; private set; }
        private double velocityX, velocityZ, lookX, lookZ, lookVelocityX, lookVelocityZ;
        private double lastX, lastZ;
        private double measuredVelocityX, measuredVelocityZ, lookTargetX, lookTargetZ;
        private bool initialized;

        public void Reset(double x, double z)
        {
            FocusX = AnchorX = lastX = Finite(x, 0);
            FocusZ = AnchorZ = lastZ = Finite(z, 0);
            velocityX = velocityZ = lookX = lookZ = lookVelocityX = lookVelocityZ = 0;
            measuredVelocityX = measuredVelocityZ = lookTargetX = lookTargetZ = 0;
            IdleSeconds = 0;
            Recentering = false;
            initialized = true;
        }

        public void Advance(double x, double z, double directionX, double directionZ,
            double dt, Settings settings, Projection projection)
        {
            if (!IsFinite(x) || !IsFinite(z) || !IsFinite(dt) || dt <= 0) return;
            if (!initialized) { Reset(x, z); return; }
            double dx = x - lastX, dz = z - lastZ;
            double travelled = Math.Sqrt(dx * dx + dz * dz);
            if (travelled > Math.Max(.1, Finite(settings.teleportDistance, 8))) { Reset(x, z); return; }
            lastX = x; lastZ = z;
            bool moving = travelled / dt > Math.Max(0, Finite(settings.idleSpeed, .04));
            // Measure the collision-resolved anchor, never the stick or the visual
            // skeleton. A held stick against a wall cannot keep pulling the view.
            double sampleX = moving ? dx / dt : 0, sampleZ = moving ? dz / dt : 0;
            double blend = 1 - Math.Exp(-dt / Math.Max(.01, Finite(settings.velocityFilterSeconds, .09)));
            measuredVelocityX += (sampleX - measuredVelocityX) * blend;
            measuredVelocityZ += (sampleZ - measuredVelocityZ) * blend;
            IdleSeconds = moving ? 0 : IdleSeconds + dt;
            bool recenter = settings.freeMovement && settings.recenterWhenIdle && !moving
                && IdleSeconds >= Math.Max(0, Finite(settings.recenterDelay, .85));
            // Starting to walk during a recenter opens a fresh free zone at the current camera.
            if (Recentering && moving) { AnchorX = FocusX - lookX; AnchorZ = FocusZ - lookZ; }
            Recentering = recenter;
            double ax = AnchorX, az = AnchorZ;
            double width = Clamp(Finite(settings.zoneWidth, .18), 0, .8);
            double height = Clamp(Finite(settings.zoneHeight, .14), 0, .8);
            if (!settings.freeMovement || recenter) { ax = x; az = z; }
            else Constrain(x, z, ref ax, ref az, projection, width, height);
            bool following = Math.Abs(ax - AnchorX) + Math.Abs(az - AnchorZ) > .000001;
            AnchorX = ax; AnchorZ = az;
            double directionLength = Math.Sqrt(directionX * directionX + directionZ * directionZ);
            if (!IsFinite(directionLength)) { directionX = directionZ = 0; }
            else if (directionLength > 1) { directionX /= directionLength; directionZ /= directionLength; }
            double lead = moving && (!settings.freeMovement || following)
                ? Math.Max(0, Finite(settings.anticipationDistance, 0)) : 0;
            if (settings.adaptiveMotion)
            {
                if (recenter || (!settings.freeMovement && !moving)) lookTargetX = lookTargetZ = 0;
                else if (moving && (!settings.freeMovement || following))
                {
                    double speed = Math.Sqrt(measuredVelocityX * measuredVelocityX + measuredVelocityZ * measuredVelocityZ);
                    // The existing fixed lead remains a useful minimum at full
                    // speed, but very small movements must not make the camera lurch.
                    double runSpeed = Math.Max(.1, Finite(settings.runningSpeed, 3.3));
                    double distance = Math.Max(Math.Max(0, Finite(settings.anticipationDistance, 0)) * Clamp(speed / runSpeed, 0, 1),
                        speed * Math.Max(0, Finite(settings.velocityLeadSeconds, .16)));
                    distance = Math.Min(distance, Math.Max(0, Finite(settings.maximumLeadDistance, .65)));
                    lookTargetX = speed > .0001 ? measuredVelocityX / speed * distance : 0;
                    lookTargetZ = speed > .0001 ? measuredVelocityZ / speed * distance : 0;
                }
                // Inside the free zone retain the last framing goal. Cancelling
                // anticipation immediately on stop used to make the camera recoil.
                // Idle recentering, when enabled, removes it gently after its delay.
            }
            else { lookTargetX = directionX * lead; lookTargetZ = directionZ * lead; }
            Damp(ref lookX, ref lookVelocityX, lookTargetX, settings.anticipationSeconds, dt);
            Damp(ref lookZ, ref lookVelocityZ, lookTargetZ, settings.anticipationSeconds, dt);
            double fx = FocusX, fz = FocusZ;
            double seconds = recenter ? settings.recenterSeconds : settings.followSeconds;
            bool coastAgainstStaleGoal = false;
            double travelX = 0, travelZ = 0, coastPosition = 0, coastVelocity = 0;
            if (settings.adaptiveMotion && !recenter)
            {
                double speed = Math.Sqrt(measuredVelocityX * measuredVelocityX + measuredVelocityZ * measuredVelocityZ);
                double amount = Clamp(speed / Math.Max(.1, Finite(settings.runningSpeed, 3.3)), 0, 1);
                double slow = Math.Max(.01, Finite(settings.followSeconds, .18));
                double fast = Math.Min(slow, Math.Max(.01, Finite(settings.runningFollowSeconds, .12)));
                seconds = slow + (fast - slow) * amount;
                // Brake the existing camera travel quickly when the hero reverses;
                // the screen-space dead zone still determines when chasing resumes.
                if (moving && velocityX * sampleX + velocityZ * sampleZ < 0)
                    seconds = Math.Min(seconds, Math.Max(.01, Finite(settings.reversalBrakeSeconds, .09)));
                if (moving && (ax + lookX - fx) * sampleX + (az + lookZ - fz) * sampleZ < 0)
                {
                    // During a reversal the free-zone anchor can still be on the
                    // old side. A faster spring toward that stale goal would speed
                    // the camera UP in the wrong direction. Coast to rest along the
                    // travel axis instead; lateral catch-up may continue normally.
                    double sampleSpeed = Math.Sqrt(sampleX * sampleX + sampleZ * sampleZ);
                    travelX = sampleX / sampleSpeed; travelZ = sampleZ / sampleSpeed;
                    double brakingSeconds = Math.Max(.01, Finite(settings.reversalBrakeSeconds, .09));
                    double decay = Math.Exp(-dt / brakingSeconds);
                    double previousVelocity = velocityX * travelX + velocityZ * travelZ;
                    coastPosition = fx * travelX + fz * travelZ + previousVelocity * brakingSeconds * (1 - decay);
                    coastVelocity = previousVelocity * decay;
                    coastAgainstStaleGoal = true;
                }
            }
            Damp(ref fx, ref velocityX, ax + lookX, seconds, dt);
            Damp(ref fz, ref velocityZ, az + lookZ, seconds, dt);
            if (coastAgainstStaleGoal)
            {
                double correction = coastPosition - (fx * travelX + fz * travelZ);
                fx += travelX * correction; fz += travelZ * correction;
                correction = coastVelocity - (velocityX * travelX + velocityZ * travelZ);
                velocityX += travelX * correction; velocityZ += travelZ * correction;
            }
            if (settings.freeMovement)
            {
                double unclampedX = fx, unclampedZ = fz;
                Constrain(x, z, ref fx, ref fz, projection,
                    Clamp(Finite(settings.safetyWidth, .65), width, .95),
                    Clamp(Finite(settings.safetyHeight, .55), height, .95));
                if (Math.Abs(fx - unclampedX) > .000001) velocityX = 0;
                if (Math.Abs(fz - unclampedZ) > .000001) velocityZ = 0;
            }
            FocusX = fx; FocusZ = fz;
        }

        /// <summary>Keep the tracker consistent with the existing map bounds without resetting idle time.</summary>
        public void ApplyBounds(double focusX, double focusZ, double anchorX, double anchorZ)
        {
            if (Math.Abs(FocusX - focusX) > .000001) velocityX = 0;
            if (Math.Abs(FocusZ - focusZ) > .000001) velocityZ = 0;
            FocusX = focusX; FocusZ = focusZ; AnchorX = anchorX; AnchorZ = anchorZ;
        }

        /// <summary>Viewport displacement of the virtual framing anchor. +/-1 is a screen edge.</summary>
        public static bool Project(double dx, double dz, Projection p, out double nx, out double ny)
        {
            nx = ny = 0;
            double depth = p.distance + dx * p.forwardX + dz * p.forwardZ;
            double scale = p.orthographic ? 1 : depth / Math.Max(.1, p.distance);
            double hh = Math.Max(.01, p.halfHeight) * scale;
            if (hh <= .00001 || !IsFinite(hh)) return false;
            nx = (dx * p.rightX + dz * p.rightZ) / (hh * Math.Max(.1, p.aspect));
            ny = (dx * p.upX + dz * p.upZ) / hh;
            return IsFinite(nx) && IsFinite(ny);
        }

        /// <summary>
        /// Move a ground-plane camera only far enough to keep its virtual target in a viewport rectangle.
        /// Perspective uses a ray/plane intersection, so zoom, pitch, yaw, roll and aspect remain correct.
        /// Near-horizontal views safely fall back to centered tracking; their ground plane is singular.
        /// </summary>
        public static void Constrain(double targetX, double targetZ, ref double focusX, ref double focusZ,
            Projection p, double width, double height)
        {
            if (Math.Abs(p.forwardY) < .01
                || !Project(targetX - focusX, targetZ - focusZ, p, out double nx, out double ny))
            { focusX = targetX; focusZ = targetZ; return; }
            width = Clamp(Finite(width, 0), 0, .95);
            height = Clamp(Finite(height, 0), 0, .95);
            double bx = Clamp(nx, -width, width), by = Clamp(ny, -height, height);
            if (nx == bx && ny == by) return;
            double sx = bx * Math.Max(.01, p.halfHeight) * Math.Max(.1, p.aspect);
            double sy = by * Math.Max(.01, p.halfHeight);
            double gx, gz;
            if (p.orthographic)
            {
                double y = p.rightY * sx + p.upY * sy;
                gx = p.rightX * sx + p.upX * sy - p.forwardX * y / p.forwardY;
                gz = p.rightZ * sx + p.upZ * sy - p.forwardZ * y / p.forwardY;
            }
            else
            {
                double d = Math.Max(.1, p.distance);
                double rx = p.forwardX + (p.rightX * sx + p.upX * sy) / d;
                double ry = p.forwardY + (p.rightY * sx + p.upY * sy) / d;
                double rz = p.forwardZ + (p.rightZ * sx + p.upZ * sy) / d;
                double length = Math.Abs(ry) < .00001 ? -1 : d * p.forwardY / ry;
                if (length <= 0 || !IsFinite(length)) { focusX = targetX; focusZ = targetZ; return; }
                gx = -d * p.forwardX + length * rx;
                gz = -d * p.forwardZ + length * rz;
            }
            focusX = targetX - gx; focusZ = targetZ - gz;
        }

        private static void Damp(ref double value, ref double velocity, double target, double seconds, double dt)
        {
            double omega = 2 / Math.Max(.01, Finite(seconds, .3));
            double error = value - target, impulse = velocity + omega * error;
            double decay = Math.Exp(-omega * dt);
            value = target + (error + impulse * dt) * decay;
            velocity = (velocity - omega * impulse * dt) * decay;
        }
        private static double Clamp(double x, double lo, double hi) => Math.Max(lo, Math.Min(x, hi));
        private static bool IsFinite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static double Finite(double x, double fallback) => IsFinite(x) ? x : fallback;
    }
}
