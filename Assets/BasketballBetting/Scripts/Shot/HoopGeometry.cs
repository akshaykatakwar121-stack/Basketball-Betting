using System;
using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>
    /// Single source of truth for the goal's geometry. Built once by the Hoop component
    /// from its real colliders and handed to the planner and detector as a value.
    /// </summary>
    [Serializable]
    public struct HoopGeometry
    {
        /// <summary>Centre of the rim opening, on the rim plane.</summary>
        public Vector3 RimCenter;
        /// <summary>Rim plane normal (world up).</summary>
        public Vector3 Up;
        /// <summary>Horizontal unit direction from the rim centre toward the court (away from the glass).</summary>
        public Vector3 TowardCourt;
        /// <summary>Radius to the inner edge of the iron (regulation 0.2286 m).</summary>
        public float InnerRadius;
        /// <summary>Radius of the iron tube itself (regulation ~0.009 m).</summary>
        public float IronRadius;
        /// <summary>A point on the court-facing glass surface.</summary>
        public Vector3 GlassPoint;
        /// <summary>Glass surface normal pointing toward the court.</summary>
        public Vector3 GlassNormal;
        public float GlassHalfWidth;
        public float GlassHalfHeight;
        /// <summary>Effective restitution of a ball/glass contact (after PhysX material combine).</summary>
        public float GlassRestitution;
        /// <summary>Effective restitution of a ball/iron contact.</summary>
        public float IronRestitution;

        public float RingRadius => InnerRadius + IronRadius;
        public float OuterRadius => InnerRadius + IronRadius * 2f;

        /// <summary>Distance from the rim centre to the glass surface along the glass normal.</summary>
        public float RimToGlass => Vector3.Dot(RimCenter - GlassPoint, GlassNormal);

        /// <summary>Signed height of a point above the rim plane.</summary>
        public float Along(Vector3 p) => Vector3.Dot(p - RimCenter, Up);

        /// <summary>Horizontal distance of a point from the rim axis.</summary>
        public float Radial(Vector3 p)
        {
            Vector3 d = p - RimCenter;
            d -= Up * Vector3.Dot(d, Up);
            return d.magnitude;
        }

        /// <summary>Horizontal offset of a point from the rim axis.</summary>
        public Vector3 RadialOffset(Vector3 p)
        {
            Vector3 d = p - RimCenter;
            return d - Up * Vector3.Dot(d, Up);
        }

        /// <summary>
        /// Largest horizontal distance from the axis at which a ball centre can cross the plane
        /// at the given descent angle without the ball touching the iron.
        /// </summary>
        public float EffectiveOpening(float ballRadius, float entryAngleDeg)
        {
            float s = Mathf.Sin(Mathf.Clamp(entryAngleDeg, 5f, 90f) * Mathf.Deg2Rad);
            return InnerRadius - ballRadius / s;
        }

        /// <summary>
        /// If the segment prev→now crosses the rim plane downward, returns the crossing point.
        /// </summary>
        public bool TryCrossingDown(Vector3 prev, Vector3 now, out Vector3 at)
        {
            float a0 = Along(prev);
            float a1 = Along(now);
            at = now;
            if (a0 <= 0f || a1 > 0f)
                return false;
            float span = a0 - a1;
            if (span < 1e-6f)
                return false;
            at = Vector3.Lerp(prev, now, a0 / span);
            return true;
        }

        /// <summary>Regulation goal at the court's rim position, glass behind it toward +Z.</summary>
        public static HoopGeometry Regulation(Vector3 rimCenter, float glassFaceZ, float ballRestitution, float glassRestitution, float ironRestitution)
        {
            return new HoopGeometry
            {
                RimCenter = rimCenter,
                Up = Vector3.up,
                TowardCourt = Vector3.back,
                InnerRadius = CourtMetrics.RimRadius,
                IronRadius = 0.009f,
                GlassPoint = new Vector3(rimCenter.x, rimCenter.y + 0.385f, glassFaceZ),
                GlassNormal = Vector3.back,
                GlassHalfWidth = CourtMetrics.BackboardWidth * 0.5f,
                GlassHalfHeight = CourtMetrics.BackboardHeight * 0.5f,
                GlassRestitution = 0.5f * (ballRestitution + glassRestitution),
                IronRestitution = 0.5f * (ballRestitution + ironRestitution)
            };
        }

        public static HoopGeometry Default => Regulation(CourtMetrics.RimCenter, CourtMetrics.PlayGlassCourtFaceZ, 0.6f, 0.72f, 0.6f);
    }
}
