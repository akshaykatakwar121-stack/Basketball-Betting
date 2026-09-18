using System;
using BasketballBetting.Shooting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BasketballBetting.Tests
{
    /// <summary>
    /// Minimal physical court for PlayMode tests: floor, regulation goal, ball, controller.
    /// Physics is stepped manually (SimulationMode.Script) so hundreds of shots run in seconds.
    /// </summary>
    public sealed class ShotTestRig : IDisposable
    {
        public GameObject Root { get; private set; }
        public Hoop Hoop { get; private set; }
        public Basketball Ball { get; private set; }
        public ShotController Controller { get; private set; }
        public float Dt => PhysicsSetup.FixedStep;

        SimulationMode _previousMode;
        float _previousFixedStep;
        float _previousBounceThreshold;

        public static readonly Vector3[] ReleasePoints =
        {
            new Vector3(0f, 2.2f, CourtMetrics.FreeThrowZ + 0.35f),
            new Vector3(0f, 2.25f, CourtMetrics.RimCenter.z - CourtMetrics.ThreePointRadius + 0.4f),
            new Vector3(-6.45f, 2.25f, CourtMetrics.RimCenter.z - CourtMetrics.ThreePointCorner + 0.15f),
            new Vector3(4.9f, 2.3f, CourtMetrics.RimCenter.z - 5.2f)
        };

        public static readonly string[] ReleaseNames = { "FT", "Top3", "LeftCorner3", "RightWing" };

        public static ShotTestRig Create(bool guidance = true)
        {
            var rig = new ShotTestRig();
            rig._previousMode = Physics.simulationMode;
            rig._previousFixedStep = Time.fixedDeltaTime;
            rig._previousBounceThreshold = Physics.bounceThreshold;
            PhysicsSetup.Apply();
            Physics.simulationMode = SimulationMode.Script;

            rig.Root = new GameObject("ShotTestRig");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(rig.Root.transform, false);
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(40f, 0.2f, 40f);
            floor.layer = GameLayers.Court;
            floor.GetComponent<Collider>().sharedMaterial = GamePhysicsMaterials.Floor;

            rig.Hoop = Hoop.CreateGoal(rig.Root.transform, CourtMetrics.RimCenter, Vector3.forward);
            rig.Ball = Basketball.Create(rig.Root.transform);
            rig.Controller = rig.Root.AddComponent<ShotController>();
            rig.Controller.GuidanceEnabled = guidance;
            rig.Controller.Bind(rig.Ball, rig.Hoop);
            return rig;
        }

        public void PlaceBall(Vector3 position)
        {
            Ball.Freeze();
            Ball.Teleport(position);
        }

        /// <summary>Advances physics + shot logic by one fixed step.</summary>
        public void Step()
        {
            Controller.Step(Dt);
            Physics.Simulate(Dt);
        }

        public void Dispose()
        {
            if (Root != null)
                Object.DestroyImmediate(Root);
            Physics.simulationMode = _previousMode;
            Time.fixedDeltaTime = _previousFixedStep;
            Physics.bounceThreshold = _previousBounceThreshold;
        }
    }
}
