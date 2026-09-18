using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>
    /// Closed-form ballistics. Pure functions, no scene access, fully unit-tested.
    ///
    /// All formulas take the physics fixed step <c>dt</c> because PhysX integrates
    /// semi-implicitly (v += g·dt, then p += v·dt). After n steps of that scheme
    /// the position is p0 + v0·t + g·t·(t + dt)/2, not the textbook p0 + v0·t + g·t²/2.
    /// Passing dt = 0 gives the continuous result.
    /// </summary>
    public static class ShotSolver
    {
        public const float DefaultGravity = 9.81f;

        /// <summary>Position after time t for a body launched with v0 under gravity g (magnitude, acting on -Y).</summary>
        public static Vector3 PositionAt(Vector3 from, Vector3 v0, float t, float g, float dt)
        {
            return from + v0 * t + Vector3.down * (0.5f * g * t * (t + dt));
        }

        /// <summary>Velocity after time t (at a step boundary).</summary>
        public static Vector3 VelocityAt(Vector3 v0, float t, float g)
        {
            return v0 + Vector3.down * (g * t);
        }

        /// <summary>Launch velocity that lands exactly on <paramref name="to"/> after <paramref name="flightTime"/>.</summary>
        public static Vector3 LaunchVelocity(Vector3 from, Vector3 to, float flightTime, float g, float dt)
        {
            float T = Mathf.Max(0.05f, flightTime);
            Vector3 delta = to - from;
            Vector3 v = delta / T;
            v.y += 0.5f * g * (T + dt);
            return v;
        }

        /// <summary>
        /// Flight time so that the descent angle at the target (measured from the horizontal)
        /// equals <paramref name="entryAngleDeg"/>.
        /// </summary>
        public static float FlightTimeForEntryAngle(Vector3 from, Vector3 to, float entryAngleDeg, float g, float dt)
        {
            float D = HorizontalDistance(from, to);
            float dy = to.y - from.y;
            float tan = Mathf.Tan(Mathf.Clamp(entryAngleDeg, 10f, 85f) * Mathf.Deg2Rad);
            float k = D * tan + dy;
            if (k < 0.02f)
                k = 0.02f;
            // 0.5 g T² − 0.5 g dt T − k = 0
            float disc = 0.25f * g * g * dt * dt + 2f * g * k;
            return (0.5f * g * dt + Mathf.Sqrt(disc)) / g;
        }

        /// <summary>Descent angle (degrees from horizontal, positive when moving down) of a velocity.</summary>
        public static float EntryAngleDeg(Vector3 v)
        {
            float h = new Vector2(v.x, v.z).magnitude;
            return Mathf.Atan2(-v.y, Mathf.Max(1e-5f, h)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Later (descending) time at which the trajectory reaches height y. False if it never gets there.
        /// </summary>
        public static bool TimeToHeightDescending(Vector3 from, Vector3 v0, float y, float g, float dt, out float t)
        {
            // from.y + v0.y t − 0.5 g t (t + dt) = y
            float b = v0.y - 0.5f * g * dt;
            float c = from.y - y;
            float disc = b * b + 2f * g * c;
            if (disc < 0f)
            {
                t = 0f;
                return false;
            }
            t = (b + Mathf.Sqrt(disc)) / g;
            return t >= 0f;
        }

        /// <summary>Highest point of the trajectory.</summary>
        public static float ApexHeight(Vector3 from, Vector3 v0, float g)
        {
            if (v0.y <= 0f)
                return from.y;
            return from.y + v0.y * v0.y / (2f * g);
        }

        /// <summary>
        /// Reflects a velocity off a surface. The normal component is scaled by the restitution,
        /// the tangential component by <paramref name="tangentialKeep"/> (friction/spin loss).
        /// </summary>
        public static Vector3 Reflect(Vector3 v, Vector3 normal, float restitution, float tangentialKeep)
        {
            normal.Normalize();
            float vn = Vector3.Dot(v, normal);
            Vector3 normalPart = normal * vn;
            Vector3 tangent = v - normalPart;
            if (vn >= 0f)
                return v; // moving away already
            return tangent * tangentialKeep - normalPart * restitution;
        }

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Step-by-step semi-implicit Euler, used by tests to prove the closed forms match PhysX's scheme.
        /// </summary>
        public static Vector3 SimulateDiscrete(Vector3 from, Vector3 v0, float g, float dt, int steps)
        {
            Vector3 p = from;
            Vector3 v = v0;
            for (int i = 0; i < steps; i++)
            {
                v += Vector3.down * (g * dt);
                p += v * dt;
            }
            return p;
        }
    }
}
