using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>A scheduled external contact during flight (the defender's hand).</summary>
    public struct PlannedContact
    {
        public float Time;
        public ShotContactKind Kind;
        /// <summary>If true the ball's velocity becomes <see cref="Velocity"/>; otherwise it is added.</summary>
        public bool Override;
        public Vector3 Velocity;
    }

    /// <summary>One ballistic segment of a plan (a new segment starts after every planned contact).</summary>
    public struct PlanSegment
    {
        public float StartTime;
        public Vector3 StartPosition;
        public Vector3 StartVelocity;
    }

    /// <summary>
    /// Everything decided before the ball leaves the hand. Immutable after planning; the
    /// controller, guidance, detector, debug overlay and tests all read the same object.
    /// </summary>
    public sealed class ShotPlan
    {
        public ShotIntent Intent;
        public Vector3 ReleasePoint;
        public Vector3 AimPoint;
        public Vector3 LaunchVelocity;
        public Vector3 AngularVelocity;
        public float FlightTime;
        public float EntryAngleDeg;
        public float Gravity;
        public float FixedStep;

        /// <summary>Predicted first contact with the goal (Rim / Backboard) or None for a clean crossing/air ball.</summary>
        public ShotContactKind FirstExpectedContact;
        public float FirstContactTime;

        /// <summary>Predicted point where the ball centre crosses the rim plane on the way down.</summary>
        public Vector3 PlaneCrossing;
        public float PlaneCrossingTime;
        /// <summary>Horizontal distance of the predicted crossing from the rim axis.</summary>
        public float PlaneCrossingRadial;
        /// <summary>True when the predicted crossing is inside the effective opening (a geometric swish).</summary>
        public bool CrossingInsideOpening;

        /// <summary>For misses: horizontal direction the ball should leave the rim region.</summary>
        public Vector3 ExitDirection;
        /// <summary>Defender contact, if any.</summary>
        public PlannedContact? Defender;

        /// <summary>Ballistic segments (first is the launch; more after planned contacts).</summary>
        public readonly List<PlanSegment> Segments = new List<PlanSegment>(2);

        /// <summary>Optional human-readable notes from the planner (candidate search results etc.).</summary>
        public string Notes = string.Empty;

        public bool IsMake => Intent.IsMake;

        /// <summary>Predicted centre position at time t after release (piecewise ballistic).</summary>
        public Vector3 PositionAt(float t)
        {
            PlanSegment s = SegmentAt(t);
            return ShotSolver.PositionAt(s.StartPosition, s.StartVelocity, t - s.StartTime, Gravity, FixedStep);
        }

        /// <summary>Predicted velocity at time t after release.</summary>
        public Vector3 VelocityAt(float t)
        {
            PlanSegment s = SegmentAt(t);
            return ShotSolver.VelocityAt(s.StartVelocity, t - s.StartTime, Gravity);
        }

        PlanSegment SegmentAt(float t)
        {
            PlanSegment best = Segments.Count > 0 ? Segments[0] : new PlanSegment { StartPosition = ReleasePoint, StartVelocity = LaunchVelocity };
            for (int i = 1; i < Segments.Count; i++)
            {
                if (t >= Segments[i].StartTime)
                    best = Segments[i];
            }
            return best;
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append(Intent).Append(" | v0=").Append(LaunchVelocity.ToString("F2"))
              .Append(" |v|=").Append(LaunchVelocity.magnitude.ToString("F2"))
              .Append(" T=").Append(FlightTime.ToString("F2"))
              .Append(" entry=").Append(EntryAngleDeg.ToString("F1"))
              .Append("° aim=").Append(AimPoint.ToString("F3"))
              .Append(" cross r=").Append(PlaneCrossingRadial.ToString("F3"))
              .Append(CrossingInsideOpening ? " (inside)" : " (outside)")
              .Append(" first=").Append(FirstExpectedContact);
            if (!string.IsNullOrEmpty(Notes))
                sb.Append(" | ").Append(Notes);
            return sb.ToString();
        }
    }
}
