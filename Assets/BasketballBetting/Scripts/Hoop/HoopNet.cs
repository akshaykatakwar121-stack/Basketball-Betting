using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>
    /// Procedural net: a small Verlet lattice hung from the rim, rendered as line strands.
    /// Visual only. It never collides with the ball; it reads a ball transform (live or replay ghost)
    /// and keeps its cords outside the ball, and it takes impulses on a confirmed basket.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public sealed class HoopNet : MonoBehaviour
    {
        const int Strands = 12;
        const int Rings = 8;

        [SerializeField] float _topRadius = CourtMetrics.RimRadius * 0.985f;
        [SerializeField] float _bottomRadius = 0.13f;
        [SerializeField] float _hang = 0.40f;
        [SerializeField] float _cordWidth = 0.007f;
        [SerializeField] Material _cordMaterial;

        Vector3[] _pos;
        Vector3[] _prev;
        Vector3[] _rest;
        LineRenderer[] _strandLines;
        LineRenderer[] _ringLines;
        Transform _ballSource;
        float _ballRadius = 0.12f;
        float _impulseTimer;
        bool _built;

        public Material CordMaterial { get => _cordMaterial; set { _cordMaterial = value; ApplyMaterial(); } }

        void Awake()
        {
            Build();
        }

        void OnEnable()
        {
            if (_built)
                ResetRest();
        }

        /// <summary>Creates the lattice and its renderers (idempotent).</summary>
        public void Build()
        {
            int n = Strands * Rings;
            _pos = new Vector3[n];
            _prev = new Vector3[n];
            _rest = new Vector3[n];
            for (int r = 0; r < Rings; r++)
            {
                float t = r / (float)(Rings - 1);
                float radius = Mathf.Lerp(_topRadius, _bottomRadius, Mathf.Pow(t, 0.85f));
                float y = -_hang * t;
                for (int s = 0; s < Strands; s++)
                {
                    float a = (s + (r % 2) * 0.5f) / Strands * Mathf.PI * 2f;
                    _rest[r * Strands + s] = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
                }
            }
            for (int i = 0; i < n; i++)
                _pos[i] = _prev[i] = _rest[i];

            Transform lines = transform.Find("Cords");
            if (lines == null)
            {
                lines = new GameObject("Cords").transform;
                lines.SetParent(transform, false);
            }
            for (int i = lines.childCount - 1; i >= 0; i--)
                UnityUtil.SafeDestroy(lines.GetChild(i).gameObject);

            _strandLines = new LineRenderer[Strands];
            for (int s = 0; s < Strands; s++)
                _strandLines[s] = MakeLine(lines, "Strand" + s, Rings * 2 - 1);
            _ringLines = new LineRenderer[Rings];
            for (int r = 0; r < Rings; r++)
                _ringLines[r] = (r == 0 || r == Rings - 1) ? MakeLine(lines, "Ring" + r, Strands + 1) : null;
            ApplyMaterial();
            _built = true;
            Render();
        }

        LineRenderer MakeLine(Transform parent, string name, int points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = gameObject.layer;
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = points;
            lr.startWidth = _cordWidth;
            lr.endWidth = _cordWidth;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.generateLightingData = true;
            return lr;
        }

        void ApplyMaterial()
        {
            if (_cordMaterial == null)
            {
                _cordMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "NetCord" };
                _cordMaterial.SetColor("_BaseColor", new Color(0.96f, 0.96f, 0.94f));
                _cordMaterial.SetFloat("_Smoothness", 0.25f);
            }
            if (_strandLines != null)
                foreach (var l in _strandLines) if (l != null) l.sharedMaterial = _cordMaterial;
            if (_ringLines != null)
                foreach (var l in _ringLines) if (l != null) l.sharedMaterial = _cordMaterial;
        }

        /// <summary>Which transform the net should wrap around (live ball, or the replay ghost).</summary>
        public void SetBallSource(Transform ball, float radius)
        {
            _ballSource = ball;
            _ballRadius = radius;
        }

        public void ResetRest()
        {
            if (_pos == null)
                return;
            for (int i = 0; i < _pos.Length; i++)
                _pos[i] = _prev[i] = _rest[i];
            Render();
        }

        /// <summary>The ball went through: whip the lower rings down and out.</summary>
        public void Swish(float strength = 1f)
        {
            if (_pos == null)
                return;
            for (int r = 1; r < Rings; r++)
            {
                float t = r / (float)(Rings - 1);
                for (int s = 0; s < Strands; s++)
                {
                    int i = r * Strands + s;
                    Vector3 radial = new Vector3(_pos[i].x, 0f, _pos[i].z).normalized;
                    _prev[i] -= (Vector3.down * (0.10f * t) + radial * (0.05f * t)) * strength * 0.5f;
                }
            }
            _impulseTimer = 0.4f;
        }

        /// <summary>Rim contact: a light shiver.</summary>
        public void Rattle(Vector3 worldDirection, float strength = 0.5f)
        {
            if (_pos == null)
                return;
            Vector3 local = transform.InverseTransformDirection(worldDirection.normalized);
            for (int r = 1; r < Rings; r++)
            {
                float t = r / (float)(Rings - 1);
                for (int s = 0; s < Strands; s++)
                    _prev[r * Strands + s] -= local * (0.03f * t * strength);
            }
        }

        void LateUpdate()
        {
            if (_pos == null)
                return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            if (dt <= 0f)
                return;
            Simulate(dt);
            Render();
        }

        void Simulate(float dt)
        {
            int n = _pos.Length;
            float damping = 0.94f;
            Vector3 gravity = Vector3.down * (2.2f * dt * dt);
            float restPull = 0.06f;

            // Verlet integrate (top ring pinned).
            for (int i = Strands; i < n; i++)
            {
                Vector3 vel = (_pos[i] - _prev[i]) * damping;
                _prev[i] = _pos[i];
                _pos[i] += vel + gravity + (_rest[i] - _pos[i]) * restPull;
            }

            // Constraints: strand segments and ring segments keep their rest lengths.
            // We intertwine ball keep-out so the net stays tight but doesn't clip.
            for (int pass = 0; pass < 8; pass++)
            {
                for (int r = 1; r < Rings; r++)
                {
                    for (int s = 0; s < Strands; s++)
                    {
                        int i = r * Strands + s;
                        // Diamond lattice: each node hangs from two nodes of the ring above.
                        int upA = (r - 1) * Strands + s;
                        int upB = (r - 1) * Strands + ((r % 2 == 1) ? (s + 1) % Strands : (s + Strands - 1) % Strands);
                        Constrain(i, upA, Vector3.Distance(_rest[i], _rest[upA]), r - 1 == 0, 1f);
                        Constrain(i, upB, Vector3.Distance(_rest[i], _rest[upB]), r - 1 == 0, 1f);
                        int side = r * Strands + (s + 1) % Strands;
                        Constrain(i, side, Vector3.Distance(_rest[i], _rest[side]), false, 0.15f);
                    }
                }

                // Ball keep-out (enforced after constraints in each pass to guarantee no clipping)
                if (_ballSource != null)
                {
                    Vector3 ball = transform.InverseTransformPoint(_ballSource.position);
                    float keep = _ballRadius + 0.012f;
                    if (ball.y < 0.35f && ball.y > -_hang - 0.35f && new Vector2(ball.x, ball.z).magnitude < _topRadius + 0.25f)
                    {
                        for (int i = Strands; i < n; i++)
                        {
                            float dy = _pos[i].y - ball.y;
                            if (Mathf.Abs(dy) < keep)
                            {
                                float hr = Mathf.Sqrt(keep * keep - dy * dy);
                                float dx = _pos[i].x - ball.x;
                                float dz = _pos[i].z - ball.z;
                                float hdist = Mathf.Sqrt(dx * dx + dz * dz);
                                if (hdist < hr)
                                {
                                    if (hdist < 1e-5f)
                                    {
                                        dx = 1e-5f;
                                        hdist = dx;
                                    }
                                    _pos[i].x = ball.x + dx / hdist * hr;
                                    _pos[i].z = ball.z + dz / hdist * hr;
                                }
                            }
                        }
                    }
                }
            }

            if (_impulseTimer > 0f)
                _impulseTimer -= dt;
        }

        void Constrain(int a, int b, float rest, bool bPinned, float stiffness = 1f)
        {
            Vector3 d = _pos[b] - _pos[a];
            float dist = d.magnitude;
            if (dist < 1e-6f)
                return;
            float diff = (dist - rest) / dist * stiffness;
            if (bPinned)
            {
                _pos[a] += d * diff;
            }
            else
            {
                _pos[a] += d * (diff * 0.5f);
                _pos[b] -= d * (diff * 0.5f);
            }
        }

        void Render()
        {
            if (_strandLines == null)
                return;
            for (int s = 0; s < Strands; s++)
            {
                LineRenderer lr = _strandLines[s];
                if (lr == null)
                    continue;
                int k = 0;
                for (int r = 0; r < Rings; r++)
                {
                    lr.SetPosition(k++, _pos[r * Strands + s]);
                    if (r < Rings - 1)
                    {
                        // zig to the neighbour on the next ring to draw the diamond lattice
                        int next = (r + 1) * Strands + ((r + 1) % 2 == 1 ? (s + 1) % Strands : s);
                        lr.SetPosition(k++, Vector3.Lerp(_pos[r * Strands + s], _pos[next], 0.5f));
                    }
                }
            }
            for (int r = 0; r < Rings; r++)
            {
                LineRenderer lr = _ringLines[r];
                if (lr == null)
                    continue;
                for (int s = 0; s <= Strands; s++)
                    lr.SetPosition(s, _pos[r * Strands + (s % Strands)]);
            }
        }
    }
}
