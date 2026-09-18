using System;
using System.Collections.Generic;
using BasketballBetting.Shooting;
using NUnit.Framework;
using UnityEngine;

namespace BasketballBetting.Tests
{
    public class ShotPlannerTests
    {
        const float G = 9.81f;
        const float Dt = 0.01f;
        const float BallR = 0.12f;

        static readonly HoopGeometry Hoop = HoopGeometry.Default;

        public static readonly Vector3[] ReleasePoints =
        {
            new Vector3(0f, 2.2f, CourtMetrics.FreeThrowZ + 0.35f),                                   // free throw
            new Vector3(0f, 2.25f, CourtMetrics.RimCenter.z - CourtMetrics.ThreePointRadius + 0.4f),  // top of the key
            new Vector3(-6.45f, 2.25f, CourtMetrics.RimCenter.z - CourtMetrics.ThreePointCorner + 0.15f), // left corner
            new Vector3(4.9f, 2.3f, CourtMetrics.RimCenter.z - 5.2f)                                  // right wing
        };

        static ShotPlanner NewPlanner() => new ShotPlanner(Hoop, BallR, ShotTuningData.Default, G, Dt);

        static IEnumerable<ShotStyle> Styles(bool make)
        {
            foreach (ShotStyle s in Enum.GetValues(typeof(ShotStyle)))
                if (ShotIntent.IsMakeStyle(s) == make)
                    yield return s;
        }

        static ShotIntent Intent(ShotStyle style, int seed)
        {
            return new ShotIntent(ShotIntent.IsMakeStyle(style) ? ShotOutcome.Make : ShotOutcome.Miss, style, seed, "test-" + style + "-" + seed);
        }

        [Test]
        public void SwishAndHighArc_PredictCrossingInsideEffectiveOpening()
        {
            var planner = NewPlanner();
            foreach (ShotStyle style in new[] { ShotStyle.Swish, ShotStyle.HighArc })
                foreach (Vector3 release in ReleasePoints)
                    for (int seed = 0; seed < 30; seed++)
                    {
                        ShotPlan plan = planner.Plan(Intent(style, seed), release);
                        Assert.IsTrue(plan.CrossingInsideOpening, plan.Describe());
                        Assert.That(plan.FirstExpectedContact, Is.EqualTo(ShotContactKind.None), plan.Describe());
                        float opening = Hoop.EffectiveOpening(BallR, plan.EntryAngleDeg);
                        Assert.That(plan.PlaneCrossingRadial, Is.LessThan(opening - 0.015f), "margin to the iron too thin: " + plan.Describe());
                    }
        }

        [Test]
        public void RimInAndRattleIn_AimInsideTheHoleButOnTheBackIron()
        {
            var planner = NewPlanner();
            float R = Hoop.InnerRadius;
            foreach (ShotStyle style in new[] { ShotStyle.RimIn, ShotStyle.RattleIn })
                foreach (Vector3 release in ReleasePoints)
                    for (int seed = 0; seed < 20; seed++)
                    {
                        ShotPlan plan = planner.Plan(Intent(style, seed), release);
                        Assert.That(plan.FirstExpectedContact, Is.EqualTo(ShotContactKind.Rim), plan.Describe());
                        Assert.That(plan.PlaneCrossingRadial, Is.GreaterThan(R - BallR - 0.01f).And.LessThan(R), plan.Describe());
                    }
        }

        [Test]
        public void BankIn_FindsAGlassContactWhoseReboundDropsThroughTheOpening()
        {
            var planner = NewPlanner();
            int fallbacks = 0, total = 0;
            foreach (Vector3 release in ReleasePoints)
                for (int seed = 0; seed < 20; seed++)
                {
                    ShotPlan plan = planner.Plan(Intent(ShotStyle.BankIn, seed), release);
                    total++;
                    Assert.That(plan.FirstExpectedContact, Is.EqualTo(ShotContactKind.Backboard), plan.Describe());
                    Assert.That(plan.Segments.Count, Is.EqualTo(2), plan.Describe());
                    if (plan.Notes.Contains("no candidate"))
                    {
                        fallbacks++;
                        continue;
                    }
                    // A bank drops mostly into the cylinder; kissing the back iron on the way down is expected.
                    Assert.That(plan.PlaneCrossingRadial, Is.LessThan(Hoop.InnerRadius - 0.03f), plan.Describe());
                    // The glass contact must be on the glass, above the rim and inside the board.
                    Assert.That(Hoop.Along(plan.AimPoint), Is.GreaterThan(0.2f), plan.Describe());
                    Assert.That(Mathf.Abs(plan.AimPoint.x - Hoop.RimCenter.x), Is.LessThan(Hoop.GlassHalfWidth - BallR), plan.Describe());
                }
            Assert.That(fallbacks, Is.EqualTo(0), $"{fallbacks}/{total} bank plans fell back to the nominal candidate");
        }

        [Test]
        public void MissStyles_NeverPredictACleanCrossing()
        {
            var planner = NewPlanner();
            float R = Hoop.InnerRadius;
            foreach (ShotStyle style in Styles(false))
                foreach (Vector3 release in ReleasePoints)
                    for (int seed = 0; seed < 20; seed++)
                    {
                        ShotPlan plan = planner.Plan(Intent(style, seed), release);
                        Assert.IsFalse(plan.CrossingInsideOpening, plan.Describe());
                        switch (style)
                        {
                            case ShotStyle.AirBall:
                                Assert.That(plan.PlaneCrossingRadial, Is.GreaterThan(R + BallR + 0.15f), plan.Describe());
                                Assert.That(plan.FirstExpectedContact, Is.EqualTo(ShotContactKind.None), plan.Describe());
                                break;
                            case ShotStyle.Short:
                            case ShotStyle.Long:
                            case ShotStyle.BackRim:
                            case ShotStyle.Left:
                            case ShotStyle.Right:
                                Assert.That(plan.FirstExpectedContact, Is.EqualTo(ShotContactKind.Rim), plan.Describe());
                                Assert.That(plan.PlaneCrossingRadial, Is.GreaterThan(R - 0.005f), plan.Describe());
                                break;
                            case ShotStyle.RimOut:
                            case ShotStyle.InAndOut:
                                Assert.That(plan.FirstExpectedContact, Is.EqualTo(ShotContactKind.Rim), plan.Describe());
                                Assert.That(plan.PlaneCrossingRadial, Is.GreaterThan(R - BallR - 0.005f), plan.Describe());
                                break;
                            case ShotStyle.BankMiss:
                                Assert.That(plan.FirstExpectedContact, Is.EqualTo(ShotContactKind.Backboard), plan.Describe());
                                Assert.That(plan.PlaneCrossingRadial, Is.GreaterThan(R + BallR + 0.04f), plan.Describe());
                                break;
                            case ShotStyle.Blocked:
                            case ShotStyle.Deflected:
                                Assert.That(plan.FirstExpectedContact, Is.EqualTo(ShotContactKind.Defender), plan.Describe());
                                Assert.IsTrue(plan.Defender.HasValue, plan.Describe());
                                Assert.That(plan.PlaneCrossingRadial, Is.GreaterThan(R + BallR + 0.05f), plan.Describe());
                                break;
                        }
                        Assert.That(plan.ExitDirection.sqrMagnitude, Is.GreaterThan(0.5f), "miss plans need an exit direction: " + plan.Describe());
                    }
        }

        [Test]
        public void EveryStyle_HasHumanLaunchSpeedAndArc()
        {
            var planner = NewPlanner();
            foreach (ShotStyle style in Enum.GetValues(typeof(ShotStyle)))
                foreach (Vector3 release in ReleasePoints)
                    for (int seed = 0; seed < 10; seed++)
                    {
                        ShotPlan plan = planner.Plan(Intent(style, seed), release);
                        float speed = plan.LaunchVelocity.magnitude;
                        Assert.That(speed, Is.GreaterThan(4.5f).And.LessThan(11.5f), plan.Describe());
                        Assert.That(plan.LaunchVelocity.y, Is.GreaterThan(0f), plan.Describe());
                        float apex = ShotSolver.ApexHeight(release, plan.LaunchVelocity, G);
                        Assert.That(apex, Is.GreaterThan(Hoop.RimCenter.y + 0.3f).And.LessThan(Hoop.RimCenter.y + 4.5f), plan.Describe());
                        Assert.That(plan.AngularVelocity.magnitude, Is.GreaterThan(5f), "needs spin: " + plan.Describe());
                    }
        }

        [Test]
        public void Planning_IsDeterministicForASeed_AndVariesAcrossSeeds()
        {
            var planner = NewPlanner();
            Vector3 release = ReleasePoints[0];
            foreach (ShotStyle style in Enum.GetValues(typeof(ShotStyle)))
            {
                ShotPlan a = planner.Plan(Intent(style, 7), release);
                ShotPlan b = new ShotPlanner(Hoop, BallR, ShotTuningData.Default, G, Dt).Plan(Intent(style, 7), release);
                Assert.That(a.LaunchVelocity, Is.EqualTo(b.LaunchVelocity), style.ToString());
                Assert.That(a.AimPoint, Is.EqualTo(b.AimPoint), style.ToString());

                var aims = new HashSet<string>();
                for (int seed = 0; seed < 12; seed++)
                    aims.Add(planner.Plan(Intent(style, seed), release).LaunchVelocity.ToString("F4"));
                Assert.That(aims.Count, Is.GreaterThanOrEqualTo(6), style + " shows too little variation");
            }
        }

        [Test]
        public void PlanPrediction_MatchesStepSimulationUntilTheFirstContact()
        {
            var planner = NewPlanner();
            ShotPlan plan = planner.Plan(Intent(ShotStyle.Swish, 3), ReleasePoints[1]);
            int steps = Mathf.RoundToInt(plan.PlaneCrossingTime / Dt);
            Vector3 simulated = ShotSolver.SimulateDiscrete(plan.ReleasePoint, plan.LaunchVelocity, G, Dt, steps);
            Vector3 predicted = plan.PositionAt(steps * Dt);
            Assert.That(Vector3.Distance(simulated, predicted), Is.LessThan(2e-3f));
            Assert.That(Mathf.Abs(simulated.y - Hoop.RimCenter.y), Is.LessThan(0.03f), "should reach the rim plane at the predicted time");
        }

        [Test]
        public void FromBet_MapsEveryAnimationToAStyleOfTheRightOutcome()
        {
            foreach (ShotAnimationId anim in Enum.GetValues(typeof(ShotAnimationId)))
                foreach (bool won in new[] { true, false })
                    foreach (TimingZone timing in Enum.GetValues(typeof(TimingZone)))
                    {
                        var math = new RoundMathResult { RoundId = "r-" + anim + "-" + won, Won = won, Animation = anim };
                        ShotIntent intent = ShotIntent.FromBet(math, timing);
                        Assert.That(intent.IsMake, Is.EqualTo(won), anim + "/" + won);
                        Assert.IsTrue(ShotIntent.StyleMatches(intent.Outcome, intent.Style));
                        Assert.That(intent.Seed, Is.EqualTo(ShotIntent.SeedFrom(math.RoundId)));
                    }
        }

        [Test]
        public void ShotIntent_RejectsMismatchedStyle()
        {
            Assert.Throws<ArgumentException>(() => new ShotIntent(ShotOutcome.Make, ShotStyle.Short, 1, "x"));
            Assert.Throws<ArgumentException>(() => new ShotIntent(ShotOutcome.Miss, ShotStyle.Swish, 1, "x"));
        }
    }
}
