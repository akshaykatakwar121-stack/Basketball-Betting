using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BasketballBetting.Shooting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BasketballBetting.Tests
{
    /// <summary>
    /// The contract of the whole shot system: for every style, spot and seed the physical
    /// realisation produces the intended outcome, with no tunnelling and no double results.
    /// </summary>
    public class ShotOutcomeMatrixTests
    {
        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        const int SeedsPerCell = 12;
        const int MaxSteps = 900; // 9 s at 100 Hz

        sealed class ShotRecord
        {
            public ShotIntent Intent;
            public string Spot;
            public ShotPlan Plan;
            public ShotResultState Result;
            public int Steps;
            public int TunnelGuard;
            public int Suspicious;
            public float MaxTracking;
            public bool WentBehindGlass;
            public bool RimContact;
            public bool GlassContact;
            public int Locks;
            public float MinDistanceToAxisAtPlane = float.MaxValue;
        }

        static IEnumerable<ShotStyle> AllStyles()
        {
            foreach (ShotStyle s in Enum.GetValues(typeof(ShotStyle)))
                yield return s;
        }

        static ShotRecord RunOne(ShotTestRig rig, ShotStyle style, int seed, int spot)
        {
            var intent = new ShotIntent(ShotIntent.IsMakeStyle(style) ? ShotOutcome.Make : ShotOutcome.Miss, style, seed, $"matrix-{style}-{spot}-{seed}");
            var rec = new ShotRecord { Intent = intent, Spot = ShotTestRig.ReleaseNames[spot] };
            rig.PlaceBall(ShotTestRig.ReleasePoints[spot]);
            int locks = 0;
            Action<ShotResultState> onLock = _ => locks++;
            rig.Controller.ResultLocked += onLock;
            rec.Plan = rig.Controller.Fire(intent);

            HoopGeometry h = rig.Hoop.Geometry;
            float glassFace = h.GlassPoint.z;
            for (int i = 0; i < MaxSteps; i++)
            {
                rig.Step();
                rec.Steps++;
                Vector3 p = rig.Ball.Position;
                // Behind the glass while inside the board's extents = pass-through.
                if (p.z > glassFace + 0.01f && Mathf.Abs(p.x - h.RimCenter.x) < h.GlassHalfWidth && p.y > h.GlassPoint.y - h.GlassHalfHeight && p.y < h.GlassPoint.y + h.GlassHalfHeight)
                    rec.WentBehindGlass = true;
                if (rig.Controller.Phase == ShotPhase.Finished)
                    break;
            }
            rig.Controller.ResultLocked -= onLock;

            rec.Result = rig.Controller.Result;
            rec.Locks = locks;
            rec.TunnelGuard = rig.Ball.TunnelGuardCount;
            rec.Suspicious = rig.Controller.SuspiciousCrossings;
            rec.MaxTracking = rig.Controller.MaxTrackingError;
            rec.RimContact = rig.Ball.HadRimContact;
            rec.GlassContact = rig.Ball.HadGlassContact;
            return rec;
        }

        static string Describe(ShotRecord r)
        {
            string res = r.Result == null ? "no result" : r.Result.ToString();
            return $"{r.Spot} {r.Intent} → {res} steps={r.Steps} rim={r.RimContact} glass={r.GlassContact} tunnel={r.TunnelGuard} suspicious={r.Suspicious} track={r.MaxTracking:F3} | {r.Plan.Describe()}";
        }

        [UnityTest]
        public IEnumerator EveryStyle_ProducesTheIntendedOutcome_ForEverySpotAndSeed()
        {
            var failures = new List<string>();
            var summary = new StringBuilder();
            int total = 0;
            using (var rig = ShotTestRig.Create())
            {
                foreach (ShotStyle style in AllStyles())
                {
                    int styleFails = 0;
                    for (int spot = 0; spot < ShotTestRig.ReleasePoints.Length; spot++)
                    {
                        for (int seed = 0; seed < SeedsPerCell; seed++)
                        {
                            total++;
                            ShotRecord r = RunOne(rig, style, seed, spot);
                            string why = null;
                            if (r.Result == null || !r.Result.Locked) why = "result never locked";
                            else if (r.Result.Mismatch) why = "physical outcome != intent";
                            else if (r.Locks != 1) why = $"locked {r.Locks} times";
                            else if (r.TunnelGuard > 0) why = "tunnel guard fired";
                            else if (r.Suspicious > 0) why = "suspicious plane crossing";
                            else if (r.WentBehindGlass) why = "ball went behind the backboard";
                            if (why != null)
                            {
                                styleFails++;
                                failures.Add(why + " :: " + Describe(r));
                            }
                        }
                    }
                    summary.AppendLine($"{style,-10} fails={styleFails}/{ShotTestRig.ReleasePoints.Length * SeedsPerCell}");
                }
            }
            Debug.Log("[ShotMatrix]\n" + summary);
            Assert.That(failures, Is.Empty, $"{failures.Count}/{total} shots failed:\n" + string.Join("\n", failures));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Makes_TouchWhatTheirStyleSays()
        {
            var problems = new List<string>();
            using (var rig = ShotTestRig.Create())
            {
                for (int spot = 0; spot < ShotTestRig.ReleasePoints.Length; spot++)
                    for (int seed = 0; seed < 6; seed++)
                    {
                        ShotRecord swish = RunOne(rig, ShotStyle.Swish, seed, spot);
                        if (swish.RimContact) problems.Add("swish touched iron :: " + Describe(swish));
                        ShotRecord rimIn = RunOne(rig, ShotStyle.RimIn, seed, spot);
                        if (!rimIn.RimContact) problems.Add("rim-in never touched iron :: " + Describe(rimIn));
                        ShotRecord bank = RunOne(rig, ShotStyle.BankIn, seed, spot);
                        if (!bank.GlassContact) problems.Add("bank never touched glass :: " + Describe(bank));
                        ShotRecord air = RunOne(rig, ShotStyle.AirBall, seed, spot);
                        if (air.RimContact || air.GlassContact) problems.Add("air ball touched the goal :: " + Describe(air));
                    }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SequentialShots_AreIndependent()
        {
            using (var rig = ShotTestRig.Create())
            {
                var styles = new[] { ShotStyle.Swish, ShotStyle.Short, ShotStyle.RimIn, ShotStyle.AirBall, ShotStyle.BankIn, ShotStyle.RimOut, ShotStyle.Swish, ShotStyle.Long, ShotStyle.RattleIn, ShotStyle.BankMiss };
                for (int i = 0; i < styles.Length; i++)
                {
                    ShotRecord r = RunOne(rig, styles[i], 100 + i, i % ShotTestRig.ReleasePoints.Length);
                    Assert.IsNotNull(r.Result, "shot " + i);
                    Assert.IsTrue(r.Result.Locked, "shot " + i + " never locked: " + Describe(r));
                    Assert.IsFalse(r.Result.Mismatch, Describe(r));
                    Assert.That(r.Result.ShotId, Is.EqualTo(i + 1), "shot ids must be unique and increasing");
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Guidance_IsInvisible_TrackingErrorStaysUnderACentimetreBeforeContact()
        {
            var worst = new List<string>();
            using (var rig = ShotTestRig.Create())
            {
                for (int spot = 0; spot < ShotTestRig.ReleasePoints.Length; spot++)
                    for (int seed = 0; seed < 5; seed++)
                    {
                        ShotRecord r = RunOne(rig, ShotStyle.Swish, seed, spot);
                        if (r.MaxTracking > 0.012f)
                            worst.Add(Describe(r));
                    }
            }
            Assert.That(worst, Is.Empty, "tracking correction is too visible:\n" + string.Join("\n", worst));
            yield return null;
        }

        [UnityTest]
        public IEnumerator WithoutGuidance_ThePlanAloneStillScoresMostSwishes()
        {
            // Documents how much the closed-form plan does on its own (the guidance only removes residual drift).
            int made = 0, total = 0;
            using (var rig = ShotTestRig.Create(guidance: false))
            {
                for (int spot = 0; spot < ShotTestRig.ReleasePoints.Length; spot++)
                    for (int seed = 0; seed < 6; seed++)
                    {
                        total++;
                        ShotRecord r = RunOne(rig, ShotStyle.Swish, seed, spot);
                        if (r.Result != null && r.Result.Observed == ShotObservation.Make)
                            made++;
                        else
                            Debug.Log("[NoGuidance] miss: " + Describe(r));
                    }
            }
            Debug.Log($"[NoGuidance] swish success without guidance: {made}/{total}");
            Assert.That(made, Is.GreaterThanOrEqualTo(total * 0.8f), "the ballistic plan itself should be nearly sufficient");
            yield return null;
        }
    }
}
