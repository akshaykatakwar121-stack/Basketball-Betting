using UnityEngine;

namespace BasketballBetting.Shooting
{
    public enum DetectorState
    {
        Idle = 0,
        Armed = 1,
        AboveRim = 2,
        Through = 3,
        Confirmed = 4,
        Missed = 5
    }

    public enum DetectorEvent
    {
        None = 0,
        Confirmed = 1,
        Missed = 2
    }

    /// <summary>
    /// Pure basket-detection state machine (no MonoBehaviour, no colliders). The scene wrapper
    /// feeds it rigidbody positions every fixed step plus optional gate-trigger notifications.
    ///
    /// SHOT_ACTIVE (Armed) → BALL_APPROACHING (AboveRim) → BALL_IN_SCORING_ZONE (Through)
    /// → BASKET_CONFIRMED (Confirmed, locked)   |   → BALL_MISSED (Missed, locked)
    ///
    /// Exactly one terminal event per armed shot. Once locked nothing changes the state
    /// until the next Arm().
    /// </summary>
    public sealed class BasketDetectorCore
    {
        readonly HoopGeometry _hoop;
        readonly float _ballRadius;
        readonly ShotTuningData _t;

        public DetectorState State { get; private set; } = DetectorState.Idle;
        public int ShotId { get; private set; } = -1;
        public float ArmedAt { get; private set; }
        public int ConfirmCount { get; private set; }
        public int MissCount { get; private set; }
        public Vector3 CrossingPoint { get; private set; }
        public float CrossingTime { get; private set; }
        /// <summary>Crossings that were geometrically impossible without overlapping the iron (tunnel evidence).</summary>
        public int SuspiciousCrossings { get; private set; }

        float _throughAt;
        Vector3 _prev;
        bool _hasPrev;
        bool _wasAbovePlane;

        public BasketDetectorCore(HoopGeometry hoop, float ballRadius, ShotTuningData tuning)
        {
            _hoop = hoop;
            _ballRadius = ballRadius;
            _t = tuning ?? ShotTuningData.Default;
        }

        public bool IsLocked => State == DetectorState.Confirmed || State == DetectorState.Missed;
        public bool IsActive => State == DetectorState.Armed || State == DetectorState.AboveRim || State == DetectorState.Through;

        /// <summary>Starts watching a new shot. Any previous shot is discarded.</summary>
        public void Arm(int shotId, float now, Vector3 ballPosition)
        {
            ShotId = shotId;
            ArmedAt = now;
            State = DetectorState.Armed;
            _prev = ballPosition;
            _hasPrev = true;
            _throughAt = 0f;
            _wasAbovePlane = _hoop.Along(ballPosition) > 0f;
            CrossingPoint = Vector3.zero;
            CrossingTime = 0f;
        }

        public void Reset()
        {
            State = DetectorState.Idle;
            ShotId = -1;
            _hasPrev = false;
        }

        /// <summary>Gate trigger from the scene: the ball entered the volume just above the rim.</summary>
        public void NotifyEntryGate()
        {
            if (State == DetectorState.Armed)
                State = DetectorState.AboveRim;
        }

        /// <summary>Gate trigger from the scene: the ball entered the volume below the rim.</summary>
        public DetectorEvent NotifyExitGate(float now)
        {
            if (State == DetectorState.Through)
                return Lock(DetectorEvent.Confirmed, now);
            return DetectorEvent.None;
        }

        /// <summary>The ball touched the floor.</summary>
        public DetectorEvent NotifyFloor(float now)
        {
            if (State == DetectorState.Armed || State == DetectorState.AboveRim)
                return Lock(DetectorEvent.Missed, now);
            return DetectorEvent.None;
        }

        /// <summary>Fixed-step update with the rigidbody's position and velocity.</summary>
        public DetectorEvent Step(Vector3 pos, Vector3 vel, float now)
        {
            if (!IsActive)
                return DetectorEvent.None;

            if (!_hasPrev)
            {
                _prev = pos;
                _hasPrev = true;
            }

            float R = _hoop.InnerRadius;
            float along = _hoop.Along(pos);
            float radial = _hoop.Radial(pos);
            DetectorEvent result = DetectorEvent.None;
            if (along > 0f)
                _wasAbovePlane = true;

            if (now - ArmedAt > _t.ShotTimeout)
            {
                result = Lock(DetectorEvent.Missed, now);
                _prev = pos;
                return result;
            }

            switch (State)
            {
                case DetectorState.Armed:
                case DetectorState.AboveRim:
                    if (State == DetectorState.Armed && along > 0.02f && radial < R + 0.2f)
                        State = DetectorState.AboveRim;

                    if (_hoop.TryCrossingDown(_prev, pos, out Vector3 at))
                    {
                        float rAt = _hoop.Radial(at);
                        // A real ball cannot cross the plane with its centre closer than ~half a radius to the iron;
                        // such a crossing is tunnelling evidence and is never scored.
                        float maxCentre = R - _ballRadius * 0.45f;
                        if (rAt <= maxCentre)
                        {
                            State = DetectorState.Through;
                            _throughAt = now;
                            CrossingPoint = at;
                            CrossingTime = now;
                        }
                        else if (rAt <= R)
                        {
                            SuspiciousCrossings++;
                        }
                    }

                    // "Fell past the rim outside the hoop" only means something once the ball has been above the plane;
                    // the release point itself is below the rim and far from the axis.
                    if (State != DetectorState.Through && _wasAbovePlane && along < -_t.MissDepth && radial > R
                        && Vector3.Dot(vel, _hoop.Up) < 0f)
                        result = Lock(DetectorEvent.Missed, now);
                    break;

                case DetectorState.Through:
                    if (along <= -_t.ConfirmDepth && radial <= R + 0.05f)
                    {
                        result = Lock(DetectorEvent.Confirmed, now);
                    }
                    else if (along > 0.005f)
                    {
                        // Popped back out above the plane: rim-out. Keep watching.
                        State = DetectorState.AboveRim;
                    }
                    else if (now - _throughAt > _t.ThroughRevertTime)
                    {
                        // Sat on the rim without dropping; treat as still undecided.
                        State = DetectorState.AboveRim;
                    }
                    break;
            }

            _prev = pos;
            return result;
        }

        DetectorEvent Lock(DetectorEvent evt, float now)
        {
            if (IsLocked)
                return DetectorEvent.None;
            if (evt == DetectorEvent.Confirmed)
            {
                State = DetectorState.Confirmed;
                ConfirmCount++;
            }
            else
            {
                State = DetectorState.Missed;
                MissCount++;
            }
            return evt;
        }
    }
}
