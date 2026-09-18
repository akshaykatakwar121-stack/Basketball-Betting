using UnityEngine;

namespace BasketballBetting
{
    public sealed class ShotMeter
    {
        public float Normalized { get; private set; }
        public TimingZone Zone { get; private set; } = TimingZone.Early;
        public bool Running { get; private set; }
        public int Cycles { get; private set; }

        float _duration;
        float _t;
        int _dir = 1;

        public void Begin(float durationSeconds)
        {
            _duration = Mathf.Max(0.35f, durationSeconds);
            _t = 0f;
            _dir = 1;
            Cycles = 0;
            Running = true;
            Normalized = 0f;
            Zone = TimingZone.Early;
        }

        public void Tick(float dt)
        {
            if (!Running)
                return;

            _t += dt * _dir;
            if (_t >= _duration)
            {
                _t = _duration;
                _dir = -1;
                Cycles++;
            }
            else if (_t <= 0f)
            {
                _t = 0f;
                _dir = 1;
                Cycles++;
            }

            Normalized = _t / _duration;
            Zone = Evaluate(Normalized);
        }

        public TimingZone Stop()
        {
            Running = false;
            Zone = Evaluate(Normalized);
            return Zone;
        }

        public static TimingZone Evaluate(float n)
        {
            if (n < 0.28f) return TimingZone.Early;
            if (n < 0.42f) return TimingZone.Good;
            if (n < 0.58f) return TimingZone.Perfect;
            if (n < 0.72f) return TimingZone.Good;
            return TimingZone.Late;
        }

        public static Color ZoneColor(TimingZone zone)
        {
            switch (zone)
            {
                case TimingZone.Perfect: return new Color(0.25f, 0.85f, 0.38f);
                case TimingZone.Good: return new Color(0.95f, 0.78f, 0.18f);
                default: return new Color(0.85f, 0.28f, 0.22f);
            }
        }
    }
}
