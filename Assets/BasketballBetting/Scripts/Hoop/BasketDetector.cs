using System;
using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>
    /// Scene wrapper around <see cref="BasketDetectorCore"/>: wires the hoop gates and the ball,
    /// and is stepped by the ShotController each fixed step. Raises exactly one terminal event per shot.
    /// </summary>
    public sealed class BasketDetector
    {
        readonly Hoop _hoop;
        readonly Basketball _ball;
        readonly BasketDetectorCore _core;
        float _now;

        public event Action<int> BasketConfirmed;
        public event Action<int> Missed;

        public BasketDetectorCore Core => _core;
        public DetectorState State => _core.State;

        public BasketDetector(Hoop hoop, Basketball ball, ShotTuningData tuning)
        {
            _hoop = hoop;
            _ball = ball;
            _core = new BasketDetectorCore(hoop.Geometry, ball.Radius, tuning);
            _hoop.EntryGateEntered += OnEntryGate;
            _hoop.ExitGateEntered += OnExitGate;
            _ball.OnContact += OnBallContact;
        }

        public void Dispose()
        {
            _hoop.EntryGateEntered -= OnEntryGate;
            _hoop.ExitGateEntered -= OnExitGate;
            _ball.OnContact -= OnBallContact;
        }

        public void Arm(int shotId, float now)
        {
            _now = now;
            _core.Arm(shotId, now, _ball.Position);
        }

        public void Reset()
        {
            _core.Reset();
        }

        public void Step(float now)
        {
            _now = now;
            Dispatch(_core.Step(_ball.Position, _ball.Velocity, now));
        }

        void OnEntryGate(Collider other)
        {
            _core.NotifyEntryGate();
        }

        void OnExitGate(Collider other)
        {
            Dispatch(_core.NotifyExitGate(_now));
        }

        void OnBallContact(ShotContactKind kind, Vector3 point)
        {
            if (kind == ShotContactKind.Floor)
                Dispatch(_core.NotifyFloor(_now));
        }

        void Dispatch(DetectorEvent evt)
        {
            switch (evt)
            {
                case DetectorEvent.Confirmed:
                    BasketConfirmed?.Invoke(_core.ShotId);
                    break;
                case DetectorEvent.Missed:
                    Missed?.Invoke(_core.ShotId);
                    break;
            }
        }
    }
}
