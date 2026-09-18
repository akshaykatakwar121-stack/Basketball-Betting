using UnityEngine;

namespace BasketballBetting.Shooting
{
    public enum GuidanceMode
    {
        Off = 0,
        Tracking = 1,   // free flight: hold the rigidbody on the planned trajectory
        Funnel = 2,     // make, after a goal contact: guide the ball down through the opening
        Guard = 3       // miss, near the opening: make sure it leaves
    }

    /// <summary>
    /// Bounded corrective forces that make the physical ball realise the planned outcome
    /// without visible faking. Pure logic; the controller decides the mode and calls Apply
    /// once per fixed step before the simulation advances.
    /// </summary>
    public sealed class ShotGuidance
    {
        readonly ShotTuningData _t;
        readonly HoopGeometry _hoop;
        readonly float _r;

        public GuidanceMode Mode { get; private set; } = GuidanceMode.Off;
        public float MaxTrackingError { get; private set; }
        public float LastTrackingError { get; private set; }

        public ShotGuidance(HoopGeometry hoop, float ballRadius, ShotTuningData tuning)
        {
            _hoop = hoop;
            _r = ballRadius;
            _t = tuning ?? ShotTuningData.Default;
        }

        public void SetMode(GuidanceMode mode)
        {
            Mode = mode;
        }

        public void ResetStats()
        {
            MaxTrackingError = 0f;
            LastTrackingError = 0f;
        }

        public void Apply(Rigidbody rb, ShotPlan plan, float flightTime, float dt)
        {
            if (rb == null || rb.isKinematic || plan == null)
                return;
            switch (Mode)
            {
                case GuidanceMode.Tracking:
                    Track(rb, plan, flightTime);
                    break;
                case GuidanceMode.Funnel:
                    Funnel(rb);
                    break;
                case GuidanceMode.Guard:
                    Guard(rb, plan);
                    break;
            }
        }

        void Track(Rigidbody rb, ShotPlan plan, float t)
        {
            Vector3 targetP = plan.PositionAt(t);
            Vector3 targetV = plan.VelocityAt(t);
            Vector3 ep = targetP - rb.position;
            Vector3 ev = targetV - rb.linearVelocity;
            LastTrackingError = ep.magnitude;
            if (LastTrackingError > MaxTrackingError)
                MaxTrackingError = LastTrackingError;
            Vector3 a = ep * _t.TrackP + ev * _t.TrackD;
            float m = a.magnitude;
            if (m > _t.TrackMaxAccel)
                a *= _t.TrackMaxAccel / m;
            if (m > 1e-5f)
                rb.AddForce(a, ForceMode.Acceleration);
        }

        void Funnel(Rigidbody rb)
        {
            Vector3 p = rb.position;
            float along = _hoop.Along(p);
            Vector3 off = _hoop.RadialOffset(p);
            float radial = off.magnitude;
            float R = _hoop.InnerRadius;
            if (along < -_t.FunnelBelowPlane || along > _t.FunnelAbovePlane || radial > R + _t.FunnelRadiusPad)
                return;

            if (radial > 1e-4f)
            {
                float strength = _t.FunnelAccel * Mathf.Clamp01(radial / R + 0.35f);
                rb.AddForce(-off / radial * strength, ForceMode.Acceleration);
            }
            Vector3 v = rb.linearVelocity;
            Vector3 vh = v - _hoop.Up * Vector3.Dot(v, _hoop.Up);
            v -= vh * _t.FunnelHorizontalDamp;
            float vy = Vector3.Dot(v, _hoop.Up);
            if (vy > 0f)
                v -= _hoop.Up * (vy * 0.35f);   // take the pop out of rim bounces
            rb.linearVelocity = v;
        }

        void Guard(Rigidbody rb, ShotPlan plan)
        {
            Vector3 p = rb.position;
            float along = _hoop.Along(p);
            Vector3 off = _hoop.RadialOffset(p);
            float radial = off.magnitude;
            float R = _hoop.InnerRadius;
            if (along < -0.06f || along > 0.55f || radial > R + 0.06f)
                return;

            Vector3 exit = plan.ExitDirection;
            exit -= _hoop.Up * Vector3.Dot(exit, _hoop.Up);
            if (exit.sqrMagnitude < 0.01f)
                exit = radial > 1e-4f ? off / radial : _hoop.TowardCourt;
            exit.Normalize();

            Vector3 v = rb.linearVelocity;
            float vy = Vector3.Dot(v, _hoop.Up);
            // A ball dancing on the iron (little vertical speed) is where the rim's spin-out really happens.
            float dancing = 1f + 2f * (1f - Mathf.Clamp01(Mathf.Abs(vy) / 3f));
            rb.AddForce(exit * (_t.GuardAccel * dancing), ForceMode.Acceleration);

            float vExit = Vector3.Dot(v, exit);
            if (along < 0.2f && vy < 0f && vExit < _t.GuardMinExitSpeed)
            {
                v += exit * (_t.GuardMinExitSpeed - vExit);
                rb.linearVelocity = v;
            }
        }
    }
}
