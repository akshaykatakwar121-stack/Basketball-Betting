using System;
using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>
    /// The goal. Owns the rim/backboard colliders, the two score gates, and publishes the
    /// HoopGeometry every other system reads. Building is idempotent so the scene builder,
    /// Awake and the test harness all produce the same physical goal.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class Hoop : MonoBehaviour
    {
        [Header("Regulation geometry (m)")]
        [SerializeField] float _innerRadius = CourtMetrics.RimRadius;   // to the inner edge of the iron
        [SerializeField] float _ironRadius = 0.009f;                     // visual tube radius
        [SerializeField] float _physicsIronRadius = 0.011f;              // slightly fatter for collision
        [SerializeField] float _rimToGlass = CourtMetrics.RimFromBackboard - CourtMetrics.BackboardThickness * 0.5f;
        [SerializeField] float _glassThickness = 0.08f;
        [SerializeField] Vector2 _glassSize = new Vector2(CourtMetrics.BackboardWidth, CourtMetrics.BackboardHeight);
        [SerializeField] float _glassBottomBelowRim = 0.15f;

        [Header("Wiring (created by Build if missing)")]
        [SerializeField] Transform _rimAnchor;
        [SerializeField] MeshCollider _rimCollider;
        [SerializeField] BoxCollider _glassCollider;
        [SerializeField] BoxCollider _entryGate;
        [SerializeField] BoxCollider _exitGate;

        public HoopGeometry Geometry { get; private set; }
        public Transform RimAnchor => _rimAnchor;
        public MeshCollider RimCollider => _rimCollider;
        public BoxCollider GlassCollider => _glassCollider;
        public float InnerRadius => _innerRadius;
        public float IronRadius => _ironRadius;
        public float PhysicsIronRadius => _physicsIronRadius;

        /// <summary>Ball entered the volume just above the rim / just below the rim.</summary>
        public event Action<Collider> EntryGateEntered;
        public event Action<Collider> ExitGateEntered;

        void Awake()
        {
            Build();
        }

        void OnValidate()
        {
            _innerRadius = Mathf.Max(0.15f, _innerRadius);
            _ironRadius = Mathf.Clamp(_ironRadius, 0.004f, 0.02f);
            _physicsIronRadius = Mathf.Clamp(_physicsIronRadius, _ironRadius, 0.03f);
        }

        /// <summary>The rim plane centre in world space (rim anchor position).</summary>
        public Vector3 RimCenter => _rimAnchor != null ? _rimAnchor.position : transform.position;

        /// <summary>
        /// Creates or refreshes every collider and gate. The visual meshes are the scene builder's
        /// job; this only guarantees the physics is correct no matter how the goal was authored.
        /// </summary>
        public void Build()
        {
            if (_rimAnchor == null)
            {
                Transform existing = transform.Find("RimAnchor");
                _rimAnchor = existing != null ? existing : new GameObject("RimAnchor").transform;
                _rimAnchor.SetParent(transform, false);
                _rimAnchor.localPosition = Vector3.zero;
                _rimAnchor.localRotation = Quaternion.identity;
            }

            Vector3 rim = _rimAnchor.position;
            // Court is at lower z than the glass in this project's court frame.
            Vector3 towardCourt = -transform.forward; // the goal's +Z points at the glass
            Vector3 up = Vector3.up;

            // ---- rim iron (torus mesh collider) ----
            float ring = _innerRadius + _physicsIronRadius;
            Transform ironT = EnsureChild(_rimAnchor, "RimIronCollider");
            ironT.localPosition = Vector3.zero;
            ironT.localRotation = Quaternion.identity;
            ironT.gameObject.layer = GameLayers.Hoop;
            _rimCollider = ironT.GetComponent<MeshCollider>();
            if (_rimCollider == null)
                _rimCollider = ironT.gameObject.AddComponent<MeshCollider>();
            Mesh physMesh = _rimCollider.sharedMesh;
            if (physMesh == null || physMesh.name != PhysicsMeshName(ring))
            {
                physMesh = TorusMeshBuilder.Build(ring, _physicsIronRadius, 48, 16, PhysicsMeshName(ring));
                _rimCollider.sharedMesh = physMesh;
            }
            _rimCollider.convex = false;
            _rimCollider.isTrigger = false;
            _rimCollider.sharedMaterial = GamePhysicsMaterials.Iron;
            _rimCollider.contactOffset = 0.003f;
            _rimCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                                        | MeshColliderCookingOptions.EnableMeshCleaning
                                        | MeshColliderCookingOptions.WeldColocatedVertices
                                        | MeshColliderCookingOptions.UseFastMidphase;

            // ---- backboard ----
            Transform glassT = EnsureChild(_rimAnchor, "BackboardCollider");
            float glassCenterY = -_glassBottomBelowRim + _glassSize.y * 0.5f;
            // Glass front face is _rimToGlass behind the rim centre; the box extends backward from it.
            glassT.localPosition = new Vector3(0f, glassCenterY, _rimToGlass + _glassThickness * 0.5f);
            glassT.localRotation = Quaternion.identity;
            glassT.gameObject.layer = GameLayers.Backboard;
            _glassCollider = glassT.GetComponent<BoxCollider>();
            if (_glassCollider == null)
                _glassCollider = glassT.gameObject.AddComponent<BoxCollider>();
            _glassCollider.center = Vector3.zero;
            _glassCollider.size = new Vector3(_glassSize.x, _glassSize.y, _glassThickness);
            _glassCollider.isTrigger = false;
            _glassCollider.sharedMaterial = GamePhysicsMaterials.Glass;
            _glassCollider.contactOffset = 0.004f;

            // ---- score gates (triggers) ----
            _entryGate = EnsureGate("EntryGate", new Vector3(0f, 0.20f, 0f), new Vector3(0.62f, 0.16f, 0.62f), true);
            _exitGate = EnsureGate("ExitGate", new Vector3(0f, -0.32f, 0f), new Vector3(0.52f, 0.28f, 0.52f), false);

            Geometry = new HoopGeometry
            {
                RimCenter = rim,
                Up = up,
                TowardCourt = towardCourt,
                InnerRadius = _innerRadius,
                IronRadius = _physicsIronRadius,
                GlassPoint = rim + transform.forward * _rimToGlass + up * glassCenterY,
                GlassNormal = towardCourt,
                GlassHalfWidth = _glassSize.x * 0.5f,
                GlassHalfHeight = _glassSize.y * 0.5f,
                GlassRestitution = 0.5f * (GamePhysicsMaterials.BallBounce + GamePhysicsMaterials.GlassBounce),
                IronRestitution = 0.5f * (GamePhysicsMaterials.BallBounce + GamePhysicsMaterials.IronBounce)
            };
        }

        static string PhysicsMeshName(float ring) => "RimIronPhysics_" + ring.ToString("F4");

        BoxCollider EnsureGate(string name, Vector3 localCenter, Vector3 size, bool entry)
        {
            Transform t = EnsureChild(_rimAnchor, name);
            t.localPosition = localCenter;
            t.localRotation = Quaternion.identity;
            t.gameObject.layer = GameLayers.Hoop;
            var box = t.GetComponent<BoxCollider>();
            if (box == null)
                box = t.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = size;
            var relay = t.GetComponent<HoopGateRelay>();
            if (relay == null)
                relay = t.gameObject.AddComponent<HoopGateRelay>();
            relay.Bind(this, entry);
            return box;
        }

        static Transform EnsureChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }
            return t;
        }

        internal void RaiseGate(bool entry, Collider other)
        {
            if (other == null || other.gameObject.layer != GameLayers.Ball)
                return;
            if (entry)
                EntryGateEntered?.Invoke(other);
            else
                ExitGateEntered?.Invoke(other);
        }

        /// <summary>
        /// Creates a complete goal (component + colliders + gates) at a rim position. Visual dressing is added
        /// separately by the scene builder; this is what the test harness uses.
        /// </summary>
        public static Hoop CreateGoal(Transform parent, Vector3 rimCenter, Vector3 towardGlass)
        {
            var go = new GameObject("Goal");
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.position = rimCenter;
            go.transform.rotation = Quaternion.LookRotation(towardGlass.sqrMagnitude > 0.001f ? towardGlass.normalized : Vector3.forward, Vector3.up);
            var hoop = go.AddComponent<Hoop>();
            hoop.Build();
            return hoop;
        }

        void OnDrawGizmosSelected()
        {
            Vector3 c = RimCenter;
            Gizmos.color = Color.green;
            DrawCircle(c, _innerRadius);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            DrawCircle(c, _innerRadius + _physicsIronRadius * 2f);
        }

        static void DrawCircle(Vector3 c, float r)
        {
            const int n = 48;
            Vector3 prev = c + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
