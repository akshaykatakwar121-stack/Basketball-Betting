using System;
using System.Collections;
using UnityEngine;

namespace BasketballBetting.Shooting
{
    public enum ShotPhase
    {
        Idle = 0,
        Targeted = 1,
        Released = 2,
        Simulating = 3,
        Interacting = 4,
        ResultLocked = 5,
        Finished = 6
    }

    /// <summary>
    /// The shot lifecycle owner:
    /// Shot Initiated → Target Determined → Trajectory Calculated → Ball Released → Ball Simulated
    /// → Rim/Backboard Interaction → Basket Detection → Result Confirmed → (presentation by listeners)
    ///
    /// One shot at a time. The only writer to the ball's rigidbody during a shot.
    /// Stepped from FixedUpdate in play, or manually by tests (Physics.simulationMode = Script).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class ShotController : MonoBehaviour
    {
        [SerializeField] ShotTuning _tuning;
        [SerializeField] Basketball _ball;
        [SerializeField] Hoop _hoop;
        [SerializeField] bool _guidanceEnabled = true;
        [Tooltip("Seconds after the result locks before the shot is considered finished (unless the ball rests earlier).")]
        [SerializeField] float _settleTimeout = 2.5f;

        ShotPlanner _planner;
        ShotGuidance _guidance;
        BasketDetector _detector;
        int _shotCounter;
        float _flightClock;
        float _lockClock;
        bool _defenderApplied;
        bool _hoopContacted;
        bool _stepping;

        public ShotPhase Phase { get; private set; } = ShotPhase.Idle;
        public ShotPlan CurrentPlan { get; private set; }
        public ShotResultState Result { get; private set; }
        public Basketball Ball => _ball;
        public Hoop Hoop => _hoop;
        public ShotTuningData Tuning => (_tuning != null ? _tuning : (_tuning = ShotTuning.CreateDefault())).Data;
        public float FlightClock => _flightClock;
        public GuidanceMode Guidance => _guidance != null ? _guidance.Mode : GuidanceMode.Off;
        public float MaxTrackingError => _guidance != null ? _guidance.MaxTrackingError : 0f;
        public DetectorState DetectorState => _detector != null ? _detector.State : DetectorState.Idle;
        public int SuspiciousCrossings => _detector != null ? _detector.Core.SuspiciousCrossings : 0;
        public bool GuidanceEnabled { get => _guidanceEnabled; set => _guidanceEnabled = value; }

        /// <summary>Ball left the hand with this plan.</summary>
        public event Action<ShotPlan> Released;
        /// <summary>Physical contact during the shot (rim, glass, floor, defender).</summary>
        public event Action<ShotContactKind, Vector3> Contact;
        /// <summary>The authoritative result is locked. Fired exactly once per shot.</summary>
        public event Action<ShotResultState> ResultLocked;
        /// <summary>The ball has come to rest (or the settle timeout elapsed) after the lock.</summary>
        public event Action<ShotResultState> Finished;

        public void Bind(Basketball ball, Hoop hoop, ShotTuning tuning = null)
        {
            Unbind();
            _ball = ball;
            _hoop = hoop;
            if (tuning != null)
                _tuning = tuning;
            _planner = new ShotPlanner(_hoop.Geometry, _ball.Radius, Tuning, -Physics.gravity.y, Time.fixedDeltaTime);
            _guidance = new ShotGuidance(_hoop.Geometry, _ball.Radius, Tuning);
            _detector = new BasketDetector(_hoop, _ball, Tuning);
            _detector.BasketConfirmed += OnBasketConfirmed;
            _detector.Missed += OnMissed;
            _ball.OnContact += OnBallContact;
        }

        void Unbind()
        {
            if (_detector != null)
            {
                _detector.BasketConfirmed -= OnBasketConfirmed;
                _detector.Missed -= OnMissed;
                _detector.Dispose();
                _detector = null;
            }
            if (_ball != null)
                _ball.OnContact -= OnBallContact;
        }

        void Awake()
        {
            if (_ball != null && _hoop != null && _planner == null)
                Bind(_ball, _hoop, _tuning);
        }

        void OnDestroy()
        {
            Unbind();
        }

        public bool IsBusy => Phase != ShotPhase.Idle && Phase != ShotPhase.Finished;

        /// <summary>Plans without launching (debug overlays, previews). Uses the current ball position as the release point.</summary>
        public ShotPlan Preview(ShotIntent intent)
        {
            EnsureBound();
            return _planner.Plan(intent, _ball.transform.position);
        }

        /// <summary>
        /// Shot Initiated → Targeted → Released, in one call, from wherever the ball currently is (the hand).
        /// Returns the plan so callers can log/inspect it.
        /// </summary>
        public ShotPlan Fire(ShotIntent intent)
        {
            EnsureBound();
            if (IsBusy)
                Abort();

            _shotCounter++;
            Vector3 release = _ball.transform.position;
            Phase = ShotPhase.Targeted;
            CurrentPlan = _planner.Plan(intent, release);
            Result = new ShotResultState(_shotCounter, intent);

            _flightClock = 0f;
            _lockClock = 0f;
            _defenderApplied = false;
            _hoopContacted = false;
            _guidance.ResetStats();
            _guidance.SetMode(_guidanceEnabled ? GuidanceMode.Tracking : GuidanceMode.Off);

            _ball.Launch(CurrentPlan.LaunchVelocity, CurrentPlan.AngularVelocity);
            _detector.Arm(_shotCounter, 0f);
            Phase = ShotPhase.Released;
            Released?.Invoke(CurrentPlan);
            Phase = ShotPhase.Simulating;
            return CurrentPlan;
        }

        /// <summary>Cancels the current shot without a result (scene reset, return to lobby).</summary>
        public void Abort()
        {
            if (_detector != null)
                _detector.Reset();
            if (_guidance != null)
                _guidance.SetMode(GuidanceMode.Off);
            if (Result != null && !Result.Locked)
                Result = null;
            CurrentPlan = null;
            Phase = ShotPhase.Idle;
        }

        /// <summary>Waits until the result is locked (coroutine helper for the round flow).</summary>
        public IEnumerator WaitForResult(float timeout = 8f)
        {
            float t = 0f;
            while (Phase != ShotPhase.ResultLocked && Phase != ShotPhase.Finished && Phase != ShotPhase.Idle && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Waits until the ball has settled after the lock.</summary>
        public IEnumerator WaitForFinish(float timeout = 6f)
        {
            float t = 0f;
            while (Phase != ShotPhase.Finished && Phase != ShotPhase.Idle && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void FixedUpdate()
        {
            if (Physics.simulationMode == SimulationMode.FixedUpdate)
                Step(Time.fixedDeltaTime);
        }

        /// <summary>One fixed step of shot logic. Call before Physics.Simulate when stepping manually.</summary>
        public void Step(float dt)
        {
            if (_stepping || Phase == ShotPhase.Idle || Phase == ShotPhase.Finished || CurrentPlan == null)
                return;
            _stepping = true;
            try
            {
                _ball.PreStep(dt);

                if (Phase == ShotPhase.Simulating || Phase == ShotPhase.Interacting)
                {
                    ApplyPlannedDefender();
                    if (_guidanceEnabled)
                        _guidance.Apply(_ball.Body, CurrentPlan, _flightClock, dt);
                    _detector.Step(_flightClock);
                    _flightClock += dt;
                }
                else if (Phase == ShotPhase.ResultLocked)
                {
                    _lockClock += dt;
                    _flightClock += dt;
                    if (_lockClock >= 0.35f && (_ball.IsResting || _lockClock >= _settleTimeout))
                        FinishShot();
                }
            }
            finally
            {
                _stepping = false;
            }
        }

        void ApplyPlannedDefender()
        {
            if (_defenderApplied || CurrentPlan.Defender == null)
                return;
            PlannedContact c = CurrentPlan.Defender.Value;
            if (_flightClock + 1e-5f < c.Time)
                return;
            _defenderApplied = true;
            Rigidbody rb = _ball.Body;
            rb.linearVelocity = c.Override ? c.Velocity : rb.linearVelocity + c.Velocity;
            Contact?.Invoke(ShotContactKind.Defender, rb.position);
        }

        void OnBallContact(ShotContactKind kind, Vector3 point)
        {
            if (Phase != ShotPhase.Simulating && Phase != ShotPhase.Interacting)
                return;
            Contact?.Invoke(kind, point);
            if (kind == ShotContactKind.Rim || kind == ShotContactKind.Backboard)
            {
                if (!_hoopContacted)
                {
                    _hoopContacted = true;
                    Phase = ShotPhase.Interacting;
                    if (_guidanceEnabled)
                        _guidance.SetMode(CurrentPlan.IsMake ? GuidanceMode.Funnel : GuidanceMode.Guard);
                }
            }
        }

        void OnBasketConfirmed(int shotId)
        {
            Lock(ShotObservation.Make, "detector confirmed basket");
        }

        void OnMissed(int shotId)
        {
            Lock(ShotObservation.Miss, "detector locked miss");
        }

        void Lock(ShotObservation observed, string reason)
        {
            if (Result == null || Result.Locked)
                return;
            Result.TryLock(observed, _flightClock, reason);
            _guidance.SetMode(GuidanceMode.Off);
            _ball.SetFree();
            Phase = ShotPhase.ResultLocked;
            ResultLocked?.Invoke(Result);
        }

        void FinishShot()
        {
            Phase = ShotPhase.Finished;
            Finished?.Invoke(Result);
        }

        void EnsureBound()
        {
            if (_planner == null)
            {
                if (_ball == null || _hoop == null)
                    throw new InvalidOperationException("ShotController needs a Basketball and a Hoop. Call Bind().");
                Bind(_ball, _hoop, _tuning);
            }
        }
    }
}
