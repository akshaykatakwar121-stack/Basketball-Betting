using BasketballBetting.Shooting;
using UnityEngine;

namespace BasketballBetting
{
    /// <summary>
    /// The authored arena's wiring. The scene builder fills these references; the game binds to them at
    /// Play instead of searching by name.
    /// </summary>
    public sealed class ArenaSceneRoot : MonoBehaviour
    {
        public Hoop PlayGoal;
        public HoopNet PlayNet;
        public Basketball Ball;
        public Transform CourtRoot;
        public Transform CrowdRoot;
        public Transform LightingRoot;
        public Transform ActorsRoot;

        public Vector3 RimCenter => PlayGoal != null ? PlayGoal.RimCenter : CourtMetrics.RimCenter;
        public Vector3 TowardCourt => PlayGoal != null ? PlayGoal.Geometry.TowardCourt : Vector3.back;
    }
}
