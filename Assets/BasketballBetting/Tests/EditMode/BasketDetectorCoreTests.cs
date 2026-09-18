using System.Collections.Generic;
using BasketballBetting.Shooting;
using NUnit.Framework;
using UnityEngine;

namespace BasketballBetting.Tests
{
    public class BasketDetectorCoreTests
    {
        const float Dt = 0.01f;
        static readonly HoopGeometry Hoop = HoopGeometry.Default;
        static readonly Vector3 Rim = Hoop.RimCenter;

        static BasketDetectorCore NewCore()
        {
            var core = new BasketDetectorCore(Hoop, 0.12f, ShotTuningData.Default);
            core.Arm(1, 0f, Rim + new Vector3(0f, 1.5f, -3f));
            return core;
        }

        /// <summary>Feeds a straight-line path (constant velocity) and collects the events.</summary>
        static List<DetectorEvent> Run(BasketDetectorCore core, Vector3 from, Vector3 to, float seconds, float startTime = 0f)
        {
            var events = new List<DetectorEvent>();
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds / Dt));
            Vector3 vel = (to - from) / seconds;
            for (int i = 1; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)n);
                DetectorEvent e = core.Step(p, vel, startTime + i * Dt);
                if (e != DetectorEvent.None)
                    events.Add(e);
            }
            return events;
        }

        [Test]
        public void ArmedBelowTheRim_DoesNotLockAMissUntilTheBallHasBeenAboveThePlane()
        {
            var core = new BasketDetectorCore(Hoop, 0.12f, ShotTuningData.Default);
            core.Arm(1, 0f, Rim + new Vector3(0f, -1.5f, -3f)); // a normal release point: below the plane, far from the axis
            // Rising phase, still below the plane and far from the axis: must stay undecided.
            var events = Run(core, Rim + new Vector3(0f, -1.5f, -3f), Rim + new Vector3(0f, 1.2f, -1.2f), 0.6f);
            Assert.That(events, Is.Empty);
            Assert.That(core.State, Is.EqualTo(DetectorState.Armed).Or.EqualTo(DetectorState.AboveRim));
            // Descends through the centre.
            events = Run(core, Rim + new Vector3(0f, 1.2f, -1.2f), Rim + new Vector3(0f, 1.0f, -0.3f), 0.2f, 0.6f);
            events.AddRange(Run(core, Rim + new Vector3(0f, 1.0f, -0.3f), Rim + new Vector3(0f, -0.6f, 0.05f), 0.5f, 0.8f));
            Assert.That(events, Is.EqualTo(new[] { DetectorEvent.Confirmed }));
        }

        [Test]
        public void RisesThenFallsWideOfTheRim_IsAMissOnlyOnTheWayDown()
        {
            var core = new BasketDetectorCore(Hoop, 0.12f, ShotTuningData.Default);
            core.Arm(1, 0f, Rim + new Vector3(0f, -1.5f, -3f));
            var events = Run(core, Rim + new Vector3(0f, -1.5f, -3f), Rim + new Vector3(0.6f, 1.0f, -0.8f), 0.6f);
            Assert.That(events, Is.Empty, "still rising / above the plane");
            events = Run(core, Rim + new Vector3(0.6f, 1.0f, -0.8f), Rim + new Vector3(0.7f, -1.2f, -0.6f), 0.7f, 0.6f);
            Assert.That(events, Is.EqualTo(new[] { DetectorEvent.Missed }));
        }

        [Test]
        public void CleanDropThroughTheCentre_ConfirmsExactlyOnce()
        {
            var core = NewCore();
            var events = Run(core, Rim + new Vector3(0f, 0.6f, -0.2f), Rim + new Vector3(0f, -0.6f, 0f), 0.4f);
            Assert.That(events, Is.EqualTo(new[] { DetectorEvent.Confirmed }));
            Assert.That(core.State, Is.EqualTo(DetectorState.Confirmed));
            Assert.That(core.ConfirmCount, Is.EqualTo(1));
            Assert.That(core.CrossingPoint.y, Is.EqualTo(Rim.y).Within(1e-4f));
        }

        [Test]
        public void AfterConfirmation_NothingChangesTheResult()
        {
            var core = NewCore();
            Run(core, Rim + new Vector3(0f, 0.6f, 0f), Rim + new Vector3(0f, -0.6f, 0f), 0.4f);
            Assert.That(core.State, Is.EqualTo(DetectorState.Confirmed));
            // Ball comes back up through the hoop and drops again (impossible, but the detector must not care).
            var later = Run(core, Rim + new Vector3(0f, -0.6f, 0f), Rim + new Vector3(0f, 0.6f, 0f), 0.4f, 0.4f);
            later.AddRange(Run(core, Rim + new Vector3(0f, 0.6f, 0f), Rim + new Vector3(0f, -0.9f, 0f), 0.5f, 0.8f));
            Assert.That(later, Is.Empty);
            Assert.That(core.NotifyFloor(2f), Is.EqualTo(DetectorEvent.None));
            Assert.That(core.NotifyExitGate(2f), Is.EqualTo(DetectorEvent.None));
            Assert.That(core.ConfirmCount, Is.EqualTo(1));
            Assert.That(core.MissCount, Is.EqualTo(0));
        }

        [Test]
        public void RimOut_DipsBelowThePlaneThenPopsOut_IsAMissNotABasket()
        {
            var core = NewCore();
            // Enters the cylinder and dips 8 cm below the plane...
            var events = Run(core, Rim + new Vector3(0.05f, 0.5f, 0f), Rim + new Vector3(0.05f, -0.08f, 0f), 0.3f);
            Assert.That(events, Is.Empty);
            Assert.That(core.State, Is.EqualTo(DetectorState.Through));
            // ...bounces back up and out over the front iron...
            events = Run(core, Rim + new Vector3(0.05f, -0.08f, 0f), Rim + new Vector3(0.1f, 0.35f, -0.4f), 0.3f, 0.3f);
            Assert.That(events, Is.Empty);
            Assert.That(core.State, Is.EqualTo(DetectorState.AboveRim));
            // ...and falls to the floor in front of the rim.
            events = Run(core, Rim + new Vector3(0.1f, 0.35f, -0.4f), Rim + new Vector3(0.2f, -1.2f, -1.2f), 0.6f, 0.6f);
            Assert.That(events, Is.EqualTo(new[] { DetectorEvent.Missed }));
            Assert.That(core.ConfirmCount, Is.EqualTo(0));
            Assert.That(core.MissCount, Is.EqualTo(1));
        }

        [Test]
        public void CrossingOutsideTheRim_IsAMiss()
        {
            var core = NewCore();
            var events = Run(core, Rim + new Vector3(0.4f, 0.6f, -0.2f), Rim + new Vector3(0.4f, -0.9f, -0.2f), 0.5f);
            Assert.That(events, Is.EqualTo(new[] { DetectorEvent.Missed }));
            Assert.That(core.ConfirmCount, Is.EqualTo(0));
        }

        [Test]
        public void CrossingWithTheCentreOverlappingTheIron_IsTunnellingEvidenceAndNeverScores()
        {
            var core = NewCore();
            float r = Hoop.InnerRadius - 0.02f; // centre 2 cm inside the iron edge: physically impossible for a 12 cm ball
            var events = Run(core, Rim + new Vector3(r, 0.4f, 0f), Rim + new Vector3(r, -0.9f, 0f), 0.5f);
            Assert.That(core.SuspiciousCrossings, Is.EqualTo(1));
            Assert.That(core.ConfirmCount, Is.EqualTo(0));
            Assert.That(events.Contains(DetectorEvent.Confirmed), Is.False);
        }

        [Test]
        public void Timeout_LocksAMiss()
        {
            var core = NewCore();
            DetectorEvent e = DetectorEvent.None;
            for (int i = 1; i <= 700 && e == DetectorEvent.None; i++)
                e = core.Step(Rim + new Vector3(0f, 1f, -3f), Vector3.zero, i * Dt);
            Assert.That(e, Is.EqualTo(DetectorEvent.Missed));
            Assert.That(core.State, Is.EqualTo(DetectorState.Missed));
        }

        [Test]
        public void FloorContact_BeforeAnyCrossing_IsAMiss_ButNotAfterConfirmation()
        {
            var core = NewCore();
            Assert.That(core.NotifyFloor(1f), Is.EqualTo(DetectorEvent.Missed));
            Assert.That(core.NotifyFloor(1.1f), Is.EqualTo(DetectorEvent.None), "locked");

            var made = NewCore();
            Run(made, Rim + new Vector3(0f, 0.6f, 0f), Rim + new Vector3(0f, -0.6f, 0f), 0.4f);
            Assert.That(made.NotifyFloor(1f), Is.EqualTo(DetectorEvent.None));
            Assert.That(made.State, Is.EqualTo(DetectorState.Confirmed));
        }

        [Test]
        public void ExitGate_OnlyConfirmsWhenTheBallWentThroughThePlane()
        {
            var core = NewCore();
            Assert.That(core.NotifyExitGate(0.1f), Is.EqualTo(DetectorEvent.None), "not through yet");
            Run(core, Rim + new Vector3(0f, 0.5f, 0f), Rim + new Vector3(0f, -0.05f, 0f), 0.2f);
            Assert.That(core.State, Is.EqualTo(DetectorState.Through));
            Assert.That(core.NotifyExitGate(0.25f), Is.EqualTo(DetectorEvent.Confirmed));
            Assert.That(core.NotifyExitGate(0.26f), Is.EqualTo(DetectorEvent.None), "only once");
        }

        [Test]
        public void EntryGate_MovesArmedToAboveRim_AndIsIgnoredOtherwise()
        {
            var core = NewCore();
            core.NotifyEntryGate();
            Assert.That(core.State, Is.EqualTo(DetectorState.AboveRim));
            Run(core, Rim + new Vector3(0f, 0.5f, 0f), Rim + new Vector3(0f, -0.6f, 0f), 0.4f);
            core.NotifyEntryGate();
            Assert.That(core.State, Is.EqualTo(DetectorState.Confirmed));
        }

        [Test]
        public void ReArming_StartsAFreshShot()
        {
            var core = NewCore();
            Run(core, Rim + new Vector3(0f, 0.6f, 0f), Rim + new Vector3(0f, -0.6f, 0f), 0.4f);
            Assert.That(core.State, Is.EqualTo(DetectorState.Confirmed));
            core.Arm(2, 5f, Rim + new Vector3(0f, 1f, -3f));
            Assert.That(core.State, Is.EqualTo(DetectorState.Armed));
            Assert.That(core.ShotId, Is.EqualTo(2));
            var events = Run(core, Rim + new Vector3(0f, 0.6f, 0f), Rim + new Vector3(0f, -0.6f, 0f), 0.4f, 5f);
            Assert.That(events, Is.EqualTo(new[] { DetectorEvent.Confirmed }));
            Assert.That(core.ConfirmCount, Is.EqualTo(2));
        }

        [Test]
        public void ResultState_LocksOnceAndFlagsMismatch()
        {
            var intent = new ShotIntent(ShotOutcome.Make, ShotStyle.Swish, 1, "r1");
            var state = new ShotResultState(1, intent);
            int locks = 0;
            state.OnLocked += _ => locks++;
            Assert.IsTrue(state.TryLock(ShotObservation.Make, 1f, "ok"));
            Assert.IsFalse(state.TryLock(ShotObservation.Miss, 2f, "late physics event"));
            Assert.That(state.Observed, Is.EqualTo(ShotObservation.Make));
            Assert.That(state.Final, Is.EqualTo(ShotOutcome.Make));
            Assert.IsFalse(state.Mismatch);
            Assert.That(locks, Is.EqualTo(1));

            var bad = new ShotResultState(2, intent);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("MISMATCH"));
            bad.TryLock(ShotObservation.Miss, 1f, "physics disagreed");
            Assert.IsTrue(bad.Mismatch);
            Assert.That(bad.Final, Is.EqualTo(ShotOutcome.Make), "the betting outcome stays authoritative");
        }
    }
}
