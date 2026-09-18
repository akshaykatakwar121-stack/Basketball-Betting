using BasketballBetting.Shooting;
using NUnit.Framework;
using UnityEngine;

namespace BasketballBetting.Tests
{
    public class ShotSolverTests
    {
        const float G = 9.81f;

        static System.Random Rng(int seed) => new System.Random(seed);

        static Vector3 RandomPoint(System.Random r, float yMin, float yMax)
        {
            return new Vector3((float)(r.NextDouble() * 14 - 7), yMin + (float)r.NextDouble() * (yMax - yMin), (float)(r.NextDouble() * 20 - 10));
        }

        [Test]
        public void LaunchVelocity_LandsOnTarget_Continuous()
        {
            var r = Rng(1);
            for (int i = 0; i < 200; i++)
            {
                Vector3 from = RandomPoint(r, 1.5f, 2.5f);
                Vector3 to = RandomPoint(r, 2.5f, 3.5f);
                float T = 0.6f + (float)r.NextDouble() * 1.2f;
                Vector3 v0 = ShotSolver.LaunchVelocity(from, to, T, G, 0f);
                Vector3 p = ShotSolver.PositionAt(from, v0, T, G, 0f);
                Assert.That(Vector3.Distance(p, to), Is.LessThan(1e-3f), $"case {i}: {p} vs {to}");
            }
        }

        [Test]
        public void LaunchVelocity_WithStepCorrection_MatchesSemiImplicitEulerSimulation()
        {
            const float dt = 0.01f;
            var r = Rng(2);
            for (int i = 0; i < 100; i++)
            {
                Vector3 from = RandomPoint(r, 1.5f, 2.5f);
                Vector3 to = RandomPoint(r, 2.5f, 3.5f);
                int n = 60 + r.Next(0, 140);
                float T = n * dt;
                Vector3 v0 = ShotSolver.LaunchVelocity(from, to, T, G, dt);
                Vector3 simulated = ShotSolver.SimulateDiscrete(from, v0, G, dt, n);
                Assert.That(Vector3.Distance(simulated, to), Is.LessThan(2e-3f), $"case {i}: sim {simulated} vs {to}");
                // And the closed form must agree with the step simulation everywhere along the flight.
                int k = r.Next(1, n);
                Vector3 mid = ShotSolver.SimulateDiscrete(from, v0, G, dt, k);
                Vector3 closed = ShotSolver.PositionAt(from, v0, k * dt, G, dt);
                Assert.That(Vector3.Distance(mid, closed), Is.LessThan(2e-3f), $"case {i} step {k}");
            }
        }

        [Test]
        public void ContinuousLaunch_DriftsUnderDiscreteIntegration_WhichIsWhyWeCorrect()
        {
            // Documents the error the correction removes: ~0.5·g·dt·T.
            const float dt = 0.01f;
            Vector3 from = new Vector3(0f, 2.2f, 8.9f);
            Vector3 to = CourtMetrics.RimCenter;
            float T = 1.1f;
            Vector3 v0 = ShotSolver.LaunchVelocity(from, to, T, G, 0f);
            Vector3 simulated = ShotSolver.SimulateDiscrete(from, v0, G, dt, Mathf.RoundToInt(T / dt));
            float drift = Vector3.Distance(simulated, to);
            Assert.That(drift, Is.GreaterThan(0.03f).And.LessThan(0.08f), $"drift {drift}");
        }

        [Test]
        public void FlightTimeForEntryAngle_ProducesRequestedDescentAngle()
        {
            var r = Rng(3);
            foreach (float dt in new[] { 0f, 0.01f, 0.02f })
            {
                for (int i = 0; i < 100; i++)
                {
                    Vector3 from = RandomPoint(r, 1.8f, 2.4f);
                    Vector3 to = RandomPoint(r, 2.9f, 3.2f);
                    if (ShotSolver.HorizontalDistance(from, to) < 1.5f)
                        continue;
                    float angle = 35f + (float)r.NextDouble() * 30f;
                    float T = ShotSolver.FlightTimeForEntryAngle(from, to, angle, G, dt);
                    Vector3 v0 = ShotSolver.LaunchVelocity(from, to, T, G, dt);
                    float measured = ShotSolver.EntryAngleDeg(ShotSolver.VelocityAt(v0, T, G));
                    Assert.That(measured, Is.EqualTo(angle).Within(0.25f), $"dt={dt} case {i}");
                }
            }
        }

        [Test]
        public void TimeToHeightDescending_FindsTheDescendingCrossing()
        {
            var r = Rng(4);
            for (int i = 0; i < 100; i++)
            {
                Vector3 from = RandomPoint(r, 1.8f, 2.4f);
                Vector3 to = RandomPoint(r, 2.9f, 3.2f);
                float T = ShotSolver.FlightTimeForEntryAngle(from, to, 50f, G, 0.01f);
                Vector3 v0 = ShotSolver.LaunchVelocity(from, to, T, G, 0.01f);
                Assert.IsTrue(ShotSolver.TimeToHeightDescending(from, v0, to.y, G, 0.01f, out float t));
                Assert.That(t, Is.EqualTo(T).Within(1e-3f));
                Assert.That(ShotSolver.VelocityAt(v0, t, G).y, Is.LessThan(0f));
                Assert.IsFalse(ShotSolver.TimeToHeightDescending(from, v0, ShotSolver.ApexHeight(from, v0, G) + 0.5f, G, 0.01f, out _));
            }
        }

        [Test]
        public void Reflect_ScalesNormalComponentAndKeepsTangent()
        {
            Vector3 v = new Vector3(1f, -2f, 3f);
            Vector3 n = Vector3.back; // glass facing the court, ball moving +z into it
            Vector3 outV = ShotSolver.Reflect(v, n, 0.5f, 0.9f);
            Assert.That(outV.z, Is.EqualTo(-1.5f).Within(1e-4f));
            Assert.That(outV.x, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(outV.y, Is.EqualTo(-1.8f).Within(1e-4f));
            // Already moving away: untouched.
            Assert.That(ShotSolver.Reflect(new Vector3(0f, 0f, -1f), n, 0.5f, 0.9f), Is.EqualTo(new Vector3(0f, 0f, -1f)));
        }

        [Test]
        public void EffectiveOpening_ShrinksWithFlatterEntry()
        {
            HoopGeometry h = HoopGeometry.Default;
            float steep = h.EffectiveOpening(0.12f, 60f);
            float mid = h.EffectiveOpening(0.12f, 50f);
            float flat = h.EffectiveOpening(0.12f, 40f);
            Assert.That(steep, Is.GreaterThan(mid));
            Assert.That(mid, Is.GreaterThan(flat));
            Assert.That(mid, Is.EqualTo(0.2286f - 0.12f / Mathf.Sin(50f * Mathf.Deg2Rad)).Within(1e-4f));
        }

        [Test]
        public void HoopGeometry_CrossingDown_InterpolatesThePlanePoint()
        {
            HoopGeometry h = HoopGeometry.Default;
            Vector3 above = h.RimCenter + new Vector3(0.05f, 0.1f, 0f);
            Vector3 below = h.RimCenter + new Vector3(0.05f, -0.1f, 0f);
            Assert.IsTrue(h.TryCrossingDown(above, below, out Vector3 at));
            Assert.That(at.y, Is.EqualTo(h.RimCenter.y).Within(1e-5f));
            Assert.That(at.x, Is.EqualTo(h.RimCenter.x + 0.05f).Within(1e-5f));
            Assert.IsFalse(h.TryCrossingDown(below, above, out _), "upward crossings are not scoring crossings");
            Assert.IsFalse(h.TryCrossingDown(above, above + Vector3.down * 0.05f, out _));
        }
    }
}
