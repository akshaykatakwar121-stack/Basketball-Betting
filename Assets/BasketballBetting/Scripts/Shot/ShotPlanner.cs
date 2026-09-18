using System;
using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>
    /// Turns a ShotIntent into a ShotPlan: picks an aim point and entry angle for the style,
    /// solves the launch velocity, and predicts where the ball will cross the rim plane and
    /// what it will touch first. Pure: same inputs → same plan.
    /// </summary>
    public sealed class ShotPlanner
    {
        readonly HoopGeometry _hoop;
        readonly float _r;
        readonly ShotTuningData _t;
        readonly float _g;
        readonly float _dt;

        public ShotPlanner(HoopGeometry hoop, float ballRadius, ShotTuningData tuning, float gravity, float fixedStep)
        {
            _hoop = hoop;
            _r = ballRadius;
            _t = tuning ?? ShotTuningData.Default;
            _g = gravity > 0.1f ? gravity : ShotSolver.DefaultGravity;
            _dt = Mathf.Max(0f, fixedStep);
        }

        public HoopGeometry Hoop => _hoop;

        public ShotPlan Plan(ShotIntent intent, Vector3 release)
        {
            var rng = new System.Random(intent.Seed);
            Frame f = BuildFrame(release);
            float R = _hoop.InnerRadius;

            var plan = new ShotPlan
            {
                Intent = intent,
                ReleasePoint = release,
                Gravity = _g,
                FixedStep = _dt,
                FirstExpectedContact = ShotContactKind.None
            };

            switch (intent.Style)
            {
                case ShotStyle.Swish:
                    Ballistic(plan, f, OnPlane(f, Range(rng, -_t.SwishAlong, _t.SwishAlong), Range(rng, -_t.SwishLateral, _t.SwishLateral)), Range(rng, _t.SwishEntry), rng);
                    break;
                case ShotStyle.HighArc:
                    Ballistic(plan, f, OnPlane(f, Range(rng, -_t.SwishAlong, _t.SwishAlong), Range(rng, -_t.SwishLateral, _t.SwishLateral)), Range(rng, _t.HighArcEntry), rng);
                    break;
                case ShotStyle.RimIn:
                    Ballistic(plan, f, OnPlane(f, R - _r + Range(rng, _t.RimInDepth), Range(rng, -0.03f, 0.03f)), Range(rng, _t.RimInEntry), rng);
                    plan.FirstExpectedContact = ShotContactKind.Rim;
                    break;
                case ShotStyle.RattleIn:
                    Ballistic(plan, f, OnPlane(f, R - _r + 0.03f, Sign(rng) * Range(rng, _t.RattleLateral)), Range(rng, _t.RattleEntry), rng);
                    plan.FirstExpectedContact = ShotContactKind.Rim;
                    break;
                case ShotStyle.BankIn:
                    PlanBank(plan, f, rng, true);
                    break;

                case ShotStyle.Short:
                    Ballistic(plan, f, OnPlane(f, -(R + Range(rng, _t.ShortOutset)), Range(rng, -0.04f, 0.04f)), Range(rng, _t.ShortEntry), rng);
                    plan.FirstExpectedContact = ShotContactKind.Rim;
                    plan.ExitDirection = -f.Lane;
                    break;
                case ShotStyle.Long:
                case ShotStyle.BackRim:
                    Ballistic(plan, f, OnPlane(f, R + Range(rng, _t.LongOutset), Range(rng, -0.04f, 0.04f)), Range(rng, _t.LongEntry), rng);
                    plan.FirstExpectedContact = ShotContactKind.Rim;
                    plan.ExitDirection = -f.Lane;
                    break;
                case ShotStyle.Left:
                case ShotStyle.Right:
                    {
                        float s = intent.Style == ShotStyle.Left ? -1f : 1f;
                        Ballistic(plan, f, OnPlane(f, Range(rng, -0.05f, 0.05f), s * (R + Range(rng, _t.SideOutset))), Range(rng, _t.SideEntry), rng);
                        plan.FirstExpectedContact = ShotContactKind.Rim;
                        plan.ExitDirection = f.Side * s;
                        break;
                    }
                case ShotStyle.RimOut:
                    {
                        // Lands on the top of the back iron a touch off-centre: pops up and rolls off the side.
                        float s = Sign(rng);
                        Ballistic(plan, f, OnPlane(f, R + Range(rng, _t.RimOutDepth), s * Range(rng, 0.03f, 0.08f)), Range(rng, _t.RimOutEntry), rng);
                        plan.FirstExpectedContact = ShotContactKind.Rim;
                        plan.ExitDirection = (-f.Lane + f.Side * s * 0.8f).normalized;
                        break;
                    }
                case ShotStyle.InAndOut:
                    {
                        // Catches the lip of the back iron steeply, hops toward the front iron and out.
                        float s = Sign(rng);
                        Ballistic(plan, f, OnPlane(f, R + Range(rng, _t.InAndOutDepth), s * Range(rng, _t.InAndOutLateral)), Range(rng, _t.InAndOutEntry), rng);
                        plan.AngularVelocity *= 1.25f;
                        plan.FirstExpectedContact = ShotContactKind.Rim;
                        plan.ExitDirection = (f.Side * s + -f.Lane * 0.5f).normalized;
                        break;
                    }
                case ShotStyle.BankMiss:
                    PlanBank(plan, f, rng, false);
                    break;
                case ShotStyle.AirBall:
                    {
                        // Always short of the iron (a long air ball would be a glass hit), with some lateral spread.
                        float miss = R + _r + Range(rng, _t.AirBallMiss);
                        float lateral = Range(rng, -0.35f, 0.35f);
                        Vector3 aim = OnPlane(f, -miss, lateral);
                        Ballistic(plan, f, aim, Range(rng, _t.AirBallEntry), rng);
                        plan.ExitDirection = (-f.Lane + f.Side * Mathf.Sign(lateral) * 0.3f).normalized;
                        break;
                    }
                case ShotStyle.Blocked:
                    {
                        Ballistic(plan, f, OnPlane(f, 0f, 0f), Range(rng, _t.SwishEntry), rng);
                        Vector3 kick = f.Side * _t.BlockKick.x * Sign(rng) + Vector3.up * _t.BlockKick.y + f.Lane * _t.BlockKick.z;
                        AddContact(plan, _t.BlockTime, ShotContactKind.Defender, true, kick);
                        PredictCrossing(plan);
                        plan.FirstExpectedContact = ShotContactKind.Defender;
                        plan.FirstContactTime = _t.BlockTime;
                        plan.ExitDirection = -f.Lane;
                        break;
                    }
                case ShotStyle.Deflected:
                    {
                        Ballistic(plan, f, OnPlane(f, 0f, 0f), Range(rng, _t.SwishEntry), rng);
                        float s = Sign(rng);
                        Vector3 tip = f.Side * (_t.DeflectTip.x * s) + Vector3.up * _t.DeflectTip.y + f.Lane * _t.DeflectTip.z;
                        // Make the tip strong enough that the deflected ball lands clear of the hoop.
                        for (int i = 0; i < 6; i++)
                        {
                            plan.Segments.Clear();
                            plan.Segments.Add(new PlanSegment { StartTime = 0f, StartPosition = release, StartVelocity = plan.LaunchVelocity });
                            AddContact(plan, _t.DeflectTime, ShotContactKind.Defender, false, tip);
                            PredictCrossing(plan);
                            if (plan.PlaneCrossingRadial > R + _r + 0.1f)
                                break;
                            tip += f.Side * (0.6f * s);
                        }
                        plan.FirstExpectedContact = ShotContactKind.Defender;
                        plan.FirstContactTime = _t.DeflectTime;
                        plan.ExitDirection = f.Side * s;
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(intent.Style), intent.Style, "Unknown shot style");
            }

            if (plan.Segments.Count == 0)
                plan.Segments.Add(new PlanSegment { StartTime = 0f, StartPosition = release, StartVelocity = plan.LaunchVelocity });
            if (plan.Defender == null)
                PredictCrossing(plan);
            return plan;
        }

        // ---------------------------------------------------------------- helpers

        struct Frame
        {
            public Vector3 Release;
            public Vector3 Lane;   // horizontal unit vector from the shooter toward the rim
            public Vector3 Side;   // shooter's right
        }

        Frame BuildFrame(Vector3 release)
        {
            Vector3 lane = _hoop.RimCenter - release;
            lane.y = 0f;
            if (lane.sqrMagnitude < 1e-4f)
                lane = -_hoop.TowardCourt;
            lane.Normalize();
            return new Frame { Release = release, Lane = lane, Side = Vector3.Cross(Vector3.up, lane).normalized };
        }

        Vector3 OnPlane(Frame f, float along, float lateral)
        {
            return _hoop.RimCenter + f.Lane * along + f.Side * lateral;
        }

        void Ballistic(ShotPlan plan, Frame f, Vector3 aim, float entryDeg, System.Random rng)
        {
            float T = ShotSolver.FlightTimeForEntryAngle(f.Release, aim, entryDeg, _g, _dt);
            T = Mathf.Clamp(T, _t.MinFlightTime, _t.MaxFlightTime);
            Vector3 v0 = ShotSolver.LaunchVelocity(f.Release, aim, T, _g, _dt);
            plan.AimPoint = aim;
            plan.FlightTime = T;
            plan.LaunchVelocity = v0;
            plan.EntryAngleDeg = ShotSolver.EntryAngleDeg(ShotSolver.VelocityAt(v0, T, _g));
            plan.AngularVelocity = -f.Side * Range(rng, _t.BackspinMin, _t.BackspinMax);
            plan.Segments.Clear();
            plan.Segments.Add(new PlanSegment { StartTime = 0f, StartPosition = f.Release, StartVelocity = v0 });
        }

        void AddContact(ShotPlan plan, float time, ShotContactKind kind, bool overrideVelocity, Vector3 velocity)
        {
            Vector3 p = plan.PositionAt(time);
            Vector3 v = plan.VelocityAt(time);
            Vector3 after = overrideVelocity ? velocity : v + velocity;
            plan.Defender = new PlannedContact { Time = time, Kind = kind, Override = overrideVelocity, Velocity = velocity };
            plan.Segments.Add(new PlanSegment { StartTime = time, StartPosition = p, StartVelocity = after });
        }

        /// <summary>Bank shots: search glass contact candidates (both sides of the board) so the rebound lands where the style wants.</summary>
        void PlanBank(ShotPlan plan, Frame f, System.Random rng, bool make)
        {
            float R = _hoop.InnerRadius;
            Vector3 n = _hoop.GlassNormal;
            float d = _hoop.RimToGlass;
            // Lateral offsets run along the board itself, whatever direction the shooter is coming from.
            Vector3 boardSide = Vector3.Cross(Vector3.up, -n).normalized;
            Vector2 heights = make ? _t.BankHeight : _t.BankMissHeight;
            Vector2 laterals = make ? _t.BankLateral : _t.BankMissLateral;
            Vector2 entries = make ? _t.BankEntry : _t.BankMissEntry;

            float bestScore = float.MaxValue;
            bool found = false;
            Vector3 bestG = Vector3.zero, bestV0 = Vector3.zero, bestVOut = Vector3.zero;
            float bestT = 0f, bestT2 = 0f, bestSide = 1f;
            Vector3 bestCross = Vector3.zero;
            float bestRadial = 0f;
            float bestEntry2 = 0f;

            float h0 = Range(rng, heights);
            float s0 = Range(rng, laterals);
            float e0 = Range(rng, entries);
            float firstSide = Sign(rng);
            const int hSteps = 9, sSteps = 9, eSteps = 3;
            for (int sideIdx = 0; sideIdx < 2; sideIdx++)
            {
                float side = sideIdx == 0 ? firstSide : -firstSide;
                for (int hi = 0; hi < hSteps; hi++)
                {
                    float h = Mathf.Lerp(heights.x, heights.y, hi / (float)(hSteps - 1));
                    for (int si = 0; si < sSteps; si++)
                    {
                        float s = Mathf.Lerp(laterals.x, laterals.y, si / (float)(sSteps - 1));
                        for (int ei = 0; ei < eSteps; ei++)
                        {
                            float e = Mathf.Clamp(e0 + (ei - 1) * 4f, entries.x, entries.y);
                            Vector3 gc = _hoop.RimCenter - n * (d - _r) + Vector3.up * h + boardSide * (s * side);
                            float T = ShotSolver.FlightTimeForEntryAngle(f.Release, gc, e, _g, _dt);
                            T = Mathf.Clamp(T, _t.MinFlightTime, _t.MaxFlightTime);
                            Vector3 v0 = ShotSolver.LaunchVelocity(f.Release, gc, T, _g, _dt);
                            Vector3 vIn = ShotSolver.VelocityAt(v0, T, _g);
                            if (Vector3.Dot(vIn, n) >= -0.2f)
                                continue;
                            Vector3 vOut = ShotSolver.Reflect(vIn, n, _hoop.GlassRestitution, 0.9f);
                            if (!ShotSolver.TimeToHeightDescending(gc, vOut, _hoop.RimCenter.y, _g, _dt, out float t2))
                                continue;
                            Vector3 cross = ShotSolver.PositionAt(gc, vOut, t2, _g, _dt);
                            float radial = _hoop.Radial(cross);
                            float entry2 = ShotSolver.EntryAngleDeg(ShotSolver.VelocityAt(vOut, t2, _g));
                            float score;
                            if (make)
                            {
                                // Ball mostly over the hole; a light kiss of the back iron is expected and handled by the funnel.
                                if (radial > R - 0.035f)
                                    continue;
                                score = radial;
                            }
                            else
                            {
                                float clearance = radial - (R + _r + 0.05f);
                                if (clearance < 0f)
                                    continue;
                                score = -Mathf.Min(clearance, 0.35f);
                            }
                            // Prefer candidates near the seed's nominal point so different seeds look different.
                            score += 0.12f * Mathf.Abs(h - h0) + 0.12f * Mathf.Abs(s - s0) + (side == firstSide ? 0f : 0.02f);
                            if (score < bestScore)
                            {
                                bestScore = score;
                                found = true;
                                bestG = gc; bestV0 = v0; bestVOut = vOut; bestT = T; bestT2 = t2; bestSide = side;
                                bestCross = cross; bestRadial = radial; bestEntry2 = entry2;
                            }
                        }
                    }
                }
            }

            if (!found)
            {
                Vector3 gc = _hoop.RimCenter - n * (d - _r) + Vector3.up * h0 + boardSide * (s0 * firstSide);
                float T = Mathf.Clamp(ShotSolver.FlightTimeForEntryAngle(f.Release, gc, e0, _g, _dt), _t.MinFlightTime, _t.MaxFlightTime);
                bestV0 = ShotSolver.LaunchVelocity(f.Release, gc, T, _g, _dt);
                bestVOut = ShotSolver.Reflect(ShotSolver.VelocityAt(bestV0, T, _g), n, _hoop.GlassRestitution, 0.9f);
                bestG = gc; bestT = T; bestSide = firstSide;
                ShotSolver.TimeToHeightDescending(gc, bestVOut, _hoop.RimCenter.y, _g, _dt, out bestT2);
                bestCross = ShotSolver.PositionAt(gc, bestVOut, bestT2, _g, _dt);
                bestRadial = _hoop.Radial(bestCross);
                bestEntry2 = ShotSolver.EntryAngleDeg(ShotSolver.VelocityAt(bestVOut, bestT2, _g));
                plan.Notes = "bank: no candidate satisfied the geometric constraint; using nominal";
            }

            plan.AimPoint = bestG;
            plan.FlightTime = bestT;
            plan.LaunchVelocity = bestV0;
            plan.EntryAngleDeg = ShotSolver.EntryAngleDeg(ShotSolver.VelocityAt(bestV0, bestT, _g));
            // Less backspin on banks: spin against the glass would steepen the rebound unpredictably.
            plan.AngularVelocity = -f.Side * Range(rng, _t.BackspinMin, _t.BackspinMax) * 0.5f;
            plan.FirstExpectedContact = ShotContactKind.Backboard;
            plan.FirstContactTime = bestT;
            plan.Segments.Clear();
            plan.Segments.Add(new PlanSegment { StartTime = 0f, StartPosition = f.Release, StartVelocity = bestV0 });
            plan.Segments.Add(new PlanSegment { StartTime = bestT, StartPosition = bestG, StartVelocity = bestVOut });
            plan.PlaneCrossing = bestCross;
            plan.PlaneCrossingTime = bestT + bestT2;
            plan.PlaneCrossingRadial = bestRadial;
            plan.CrossingInsideOpening = bestRadial <= _hoop.EffectiveOpening(_r, bestEntry2);
            // Misses leave on the side they banked from; makes need no exit.
            Vector3 exit = _hoop.RadialOffset(bestCross);
            plan.ExitDirection = make ? Vector3.zero : (exit.sqrMagnitude > 1e-4f ? exit.normalized : boardSide * bestSide);
            plan.Defender = null;
            _bankPredicted = true;
        }

        bool _bankPredicted;

        /// <summary>Predicts the descending rim-plane crossing along the plan's segments and checks it against the iron.</summary>
        void PredictCrossing(ShotPlan plan)
        {
            if (_bankPredicted)
            {
                _bankPredicted = false;
                return;
            }
            float R = _hoop.InnerRadius;
            for (int i = plan.Segments.Count - 1; i >= 0; i--)
            {
                PlanSegment s = plan.Segments[i];
                if (!ShotSolver.TimeToHeightDescending(s.StartPosition, s.StartVelocity, _hoop.RimCenter.y, _g, _dt, out float t))
                    continue;
                float absT = s.StartTime + t;
                // The crossing must belong to this segment (before the next contact).
                if (i + 1 < plan.Segments.Count && absT > plan.Segments[i + 1].StartTime)
                    continue;
                Vector3 cross = ShotSolver.PositionAt(s.StartPosition, s.StartVelocity, t, _g, _dt);
                float entry = ShotSolver.EntryAngleDeg(ShotSolver.VelocityAt(s.StartVelocity, t, _g));
                plan.PlaneCrossing = cross;
                plan.PlaneCrossingTime = absT;
                plan.PlaneCrossingRadial = _hoop.Radial(cross);
                plan.CrossingInsideOpening = plan.PlaneCrossingRadial <= _hoop.EffectiveOpening(_r, entry);
                if (plan.FirstExpectedContact == ShotContactKind.None && !plan.CrossingInsideOpening
                    && plan.PlaneCrossingRadial < R + _r)
                {
                    plan.FirstExpectedContact = ShotContactKind.Rim;
                }
                if (plan.FirstExpectedContact == ShotContactKind.Rim && plan.FirstContactTime <= 0f)
                    plan.FirstContactTime = Mathf.Max(0f, absT - 0.05f);
                return;
            }
            plan.PlaneCrossingRadial = float.PositiveInfinity;
            plan.CrossingInsideOpening = false;
        }

        static float Range(System.Random rng, float min, float max)
        {
            if (max < min) { float t = min; min = max; max = t; }
            return min + (float)rng.NextDouble() * (max - min);
        }

        static float Range(System.Random rng, Vector2 range) => Range(rng, range.x, range.y);

        static float Sign(System.Random rng) => rng.NextDouble() < 0.5 ? -1f : 1f;
    }
}
