using System.Collections;
using System.Text;
using BasketballBetting.Shooting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BasketballBetting.Tests
{
    /// <summary>Prints what the ball actually does step by step. Never asserts on physics; used to debug the harness.</summary>
    public class ShotDiagnosticsTests
    {
        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator LogSwishTrajectory()
        {
            var sb = new StringBuilder();
            using (var rig = ShotTestRig.Create(guidance: false))
            {
                var intent = new ShotIntent(ShotOutcome.Make, ShotStyle.Swish, 0, "diag");
                rig.PlaceBall(ShotTestRig.ReleasePoints[0]);
                sb.AppendLine($"simMode={Physics.simulationMode} gravity={Physics.gravity} fixedDt={Time.fixedDeltaTime} ballLayer={rig.Ball.gameObject.layer} kinematicBefore={rig.Ball.Body.isKinematic}");
                sb.AppendLine($"hoop rim={rig.Hoop.Geometry.RimCenter} R={rig.Hoop.Geometry.InnerRadius} glassPoint={rig.Hoop.Geometry.GlassPoint} rimColliderEnabled={rig.Hoop.RimCollider.enabled} rimColliderBounds={rig.Hoop.RimCollider.bounds}");
                ShotPlan plan = rig.Controller.Fire(intent);
                sb.AppendLine("plan: " + plan.Describe());
                sb.AppendLine($"after Fire: state={rig.Ball.State} kinematic={rig.Ball.Body.isKinematic} pos={rig.Ball.Position} vel={rig.Ball.Body.linearVelocity} phase={rig.Controller.Phase} det={rig.Controller.DetectorState}");
                for (int i = 1; i <= 400; i++)
                {
                    rig.Step();
                    if (i <= 5 || i % 10 == 0 || rig.Controller.Phase == ShotPhase.ResultLocked)
                    {
                        Vector3 p = rig.Ball.Position;
                        sb.AppendLine($"step {i,3} t={i * rig.Dt:F2} pos={p:F3} vel={rig.Ball.Body.linearVelocity:F2} predicted={plan.PositionAt(i * rig.Dt):F3} det={rig.Controller.DetectorState} phase={rig.Controller.Phase} along={rig.Hoop.Geometry.Along(p):F3} radial={rig.Hoop.Geometry.Radial(p):F3} rim={rig.Ball.HadRimContact} floor={rig.Ball.HadFloorContact}");
                    }
                    if (rig.Controller.Phase == ShotPhase.ResultLocked || rig.Controller.Phase == ShotPhase.Finished)
                    {
                        sb.AppendLine("LOCKED: " + rig.Controller.Result);
                        break;
                    }
                }
            }
            Debug.Log("[Diag]\n" + sb);
            yield return null;
        }
    }
}
