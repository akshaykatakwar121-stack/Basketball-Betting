using System;
using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>
    /// The one and only physical basketball. Owns the Rigidbody configuration and reports
    /// contacts by layer. Contains no scoring logic and no knowledge of the bet.
    ///
    /// Only ShotController drives it (Launch / Hold / Freeze). Replay never touches it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    [DefaultExecutionOrder(150)]
    public sealed class Basketball : MonoBehaviour
    {
        public enum BallState
        {
            Held = 0,
            InFlight = 1,
            Free = 2,
            Frozen = 3
        }

        public const float ContactDebounce = 0.06f;

        [SerializeField] BallSpec _spec = BallSpec.Regulation;
        [SerializeField] Transform _visual;

        Rigidbody _rb;
        SphereCollider _col;
        Transform _hand;
        Vector3 _handOffset;
        Vector3 _prevPos;
        bool _hasPrev;
        float _lastRimContact = -10f, _lastGlassContact = -10f, _lastFloorContact = -10f;
        bool _contactThisStep;
        float _clock;

        public BallState State { get; private set; } = BallState.Free;
        public Rigidbody Body => _rb;
        public SphereCollider Collider => _col;
        public BallSpec Spec => _spec;
        public float Radius => _spec.Radius;
        public Transform Visual => _visual;
        /// <summary>Number of times the sweep guard had to correct a missed collision. Must stay 0.</summary>
        public int TunnelGuardCount { get; private set; }
        public bool HadRimContact { get; private set; }
        public bool HadGlassContact { get; private set; }
        public bool HadFloorContact { get; private set; }

        /// <summary>Raised for every debounced physical contact.</summary>
        public event Action<ShotContactKind, Vector3> OnContact;

        void Awake()
        {
            Configure();
        }

        void Reset()
        {
            Configure();
        }

        /// <summary>Applies the canonical rigidbody/collider setup. Safe to call repeatedly.</summary>
        public void Configure()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<SphereCollider>();
            gameObject.layer = GameLayers.Ball;
            transform.localScale = Vector3.one;

            _col.radius = _spec.Radius;
            _col.center = Vector3.zero;
            _col.isTrigger = false;
            _col.sharedMaterial = GamePhysicsMaterials.Ball;
            _col.contactOffset = 0.004f;

            _rb.mass = _spec.Mass;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0.05f;
            _rb.useGravity = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.constraints = RigidbodyConstraints.None;
            _rb.maxAngularVelocity = 60f;
            _rb.maxDepenetrationVelocity = 5f;
            _rb.solverIterations = PhysicsSetup.SolverIterations;
            _rb.solverVelocityIterations = PhysicsSetup.SolverVelocityIterations;
            _rb.sleepThreshold = 0.05f;
            _rb.centerOfMass = Vector3.zero;

            if (_visual != null)
            {
                _visual.localPosition = Vector3.zero;
                _visual.localScale = Vector3.one * (_spec.Radius * 2f);
            }
        }

        public void SetVisual(Transform visual)
        {
            _visual = visual;
            Configure();
        }

        /// <summary>Parents the ball to a hand transform (kinematic, follows every LateUpdate).</summary>
        public void Hold(Transform hand, Vector3 localOffset)
        {
            _hand = hand;
            _handOffset = localOffset;
            State = BallState.Held;
            StopMotion();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.None;
            _hasPrev = false;
            SnapToHand();
        }

        /// <summary>Releases from the hand with an explicit launch state. Position is the current (hand) position.</summary>
        public void Launch(Vector3 velocity, Vector3 angularVelocity)
        {
            Vector3 p = transform.position;
            _hand = null;
            State = BallState.InFlight;
            ResetContactFlags();
            _rb.isKinematic = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.linearDamping = 0f;
            _rb.detectCollisions = true;
            _rb.position = p;
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = angularVelocity;
            _rb.WakeUp();
            _prevPos = p;
            _hasPrev = true;
        }

        /// <summary>Leaves the ball to plain physics (after a result is locked).</summary>
        public void SetFree()
        {
            if (State == BallState.Held)
                return;
            State = BallState.Free;
            _rb.linearDamping = 0.15f;
        }

        /// <summary>Stops the ball where it is (used while a replay plays and on reset).</summary>
        public void Freeze()
        {
            _hand = null;
            State = BallState.Frozen;
            StopMotion();
            _rb.isKinematic = true;
        }

        /// <summary>Zeroes velocities only while the body is dynamic (PhysX warns otherwise).</summary>
        void StopMotion()
        {
            if (_rb == null || _rb.isKinematic)
                return;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        public void Teleport(Vector3 position)
        {
            bool kin = _rb.isKinematic;
            _rb.isKinematic = true;
            transform.position = position;
            _rb.position = position;
            _rb.isKinematic = kin;
            _prevPos = position;
        }

        public bool IsResting
        {
            get
            {
                if (_rb == null || _rb.isKinematic)
                    return true;
                return _rb.linearVelocity.sqrMagnitude < 0.04f && _rb.angularVelocity.sqrMagnitude < 0.5f;
            }
        }

        public Vector3 Velocity => _rb != null && !_rb.isKinematic ? _rb.linearVelocity : Vector3.zero;
        public Vector3 Position => _rb != null ? _rb.position : transform.position;

        void ResetContactFlags()
        {
            HadRimContact = false;
            HadGlassContact = false;
            HadFloorContact = false;
            _lastRimContact = _lastGlassContact = _lastFloorContact = -10f;
            _contactThisStep = false;
            _clock = 0f;
        }

        void LateUpdate()
        {
            if (State == BallState.Held)
                SnapToHand();
        }

        /// <summary>Puts the held ball exactly on the hand right now (call before launching from a release event).</summary>
        public void SyncToHand()
        {
            if (State == BallState.Held)
                SnapToHand();
        }

        void SnapToHand()
        {
            if (_hand == null)
                return;
            transform.position = _hand.TransformPoint(_handOffset);
            transform.rotation = _hand.rotation;
        }

        /// <summary>
        /// Called by the ShotController once per fixed step before the simulation advances.
        /// Runs the sweep guard against the goal so a fast ball can never end up past the iron or glass.
        /// </summary>
        public void PreStep(float dt)
        {
            _clock += Mathf.Max(0f, dt);
            if (State != BallState.InFlight || _rb == null || _rb.isKinematic)
                return;

            Vector3 now = _rb.position;
            if (_hasPrev && !_contactThisStep)
            {
                Vector3 delta = now - _prevPos;
                float dist = delta.magnitude;
                if (dist > _spec.Radius * 0.5f)
                {
                    Vector3 dir = delta / dist;
                    if (Physics.SphereCast(_prevPos, _spec.Radius * 0.9f, dir, out RaycastHit hit, dist, GameLayers.GoalMask, QueryTriggerInteraction.Ignore)
                        && Vector3.Dot(now - hit.point, hit.normal) < -0.01f)
                    {
                        // The centre ended up on the far side of a surface the sweep crossed: PhysX missed this contact.
                        TunnelGuardCount++;
                        Vector3 place = _prevPos + dir * Mathf.Max(0f, hit.distance - 0.002f);
                        _rb.position = place;
                        transform.position = place;
                        Vector3 v = _rb.linearVelocity;
                        float e = hit.collider.gameObject.layer == GameLayers.Backboard ? GamePhysicsMaterials.GlassBounce : GamePhysicsMaterials.IronBounce;
                        _rb.linearVelocity = ShotSolver.Reflect(v, hit.normal, 0.5f * (e + GamePhysicsMaterials.BallBounce), 0.9f);
                        Report(hit.collider.gameObject.layer == GameLayers.Backboard ? ShotContactKind.Backboard : ShotContactKind.Rim, hit.point);
                        now = place;
                    }
                }
            }
            _prevPos = now;
            _hasPrev = true;
            _contactThisStep = false;
        }

        void OnCollisionEnter(Collision collision)
        {
            HandleCollision(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            HandleCollision(collision);
        }

        void HandleCollision(Collision collision)
        {
            if (State == BallState.Held || State == BallState.Frozen || collision == null || collision.collider == null)
                return;
            int layer = collision.collider.gameObject.layer;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            _contactThisStep = true;
            if (layer == GameLayers.Hoop)
                Report(ShotContactKind.Rim, point);
            else if (layer == GameLayers.Backboard)
                Report(ShotContactKind.Backboard, point);
            else if (layer == GameLayers.Court)
                Report(ShotContactKind.Floor, point);
        }

        void Report(ShotContactKind kind, Vector3 point)
        {
            float now = _clock;
            switch (kind)
            {
                case ShotContactKind.Rim:
                    HadRimContact = true;
                    if (now - _lastRimContact < ContactDebounce) return;
                    _lastRimContact = now;
                    break;
                case ShotContactKind.Backboard:
                    HadGlassContact = true;
                    if (now - _lastGlassContact < ContactDebounce) return;
                    _lastGlassContact = now;
                    break;
                case ShotContactKind.Floor:
                    HadFloorContact = true;
                    if (now - _lastFloorContact < ContactDebounce) return;
                    _lastFloorContact = now;
                    break;
            }
            OnContact?.Invoke(kind, point);
        }

        /// <summary>Creates a fully configured ball (sphere collider + rigidbody + visual child).</summary>
        public static Basketball Create(Transform parent, Material visualMaterial = null)
        {
            var go = new GameObject("Basketball");
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.layer = GameLayers.Ball;
            go.AddComponent<Rigidbody>();
            go.AddComponent<SphereCollider>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "BallVisual";
            visual.layer = GameLayers.Ball;
            var vc = visual.GetComponent<Collider>();
            if (vc != null)
                UnityUtil.SafeDestroy(vc);
            visual.transform.SetParent(go.transform, false);
            if (visualMaterial != null)
                visual.GetComponent<MeshRenderer>().sharedMaterial = visualMaterial;

            var ball = go.AddComponent<Basketball>();
            ball._visual = visual.transform;
            ball.Configure();
            ball.Freeze();
            return ball;
        }
    }
}
