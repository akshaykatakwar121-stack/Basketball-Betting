using System;
using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>
    /// Every tunable number of the shot pipeline in one place. Plain serializable data so the
    /// planner and guidance can be unit-tested without a scene; wrapped by <see cref="ShotTuning"/>
    /// for the inspector.
    /// </summary>
    [Serializable]
    public sealed class ShotTuningData
    {
        [Header("Flight")]
        public float MinFlightTime = 0.55f;
        public float MaxFlightTime = 2.2f;
        public float BackspinMin = 12f;   // rad/s
        public float BackspinMax = 18f;

        [Header("Make — entry angles (deg from horizontal)")]
        public Vector2 SwishEntry = new Vector2(49f, 56f);
        public Vector2 HighArcEntry = new Vector2(56f, 60f);
        public Vector2 RimInEntry = new Vector2(55f, 62f);
        public Vector2 RattleEntry = new Vector2(54f, 60f);
        public Vector2 BankEntry = new Vector2(42f, 52f);

        [Header("Make — aim variation (m)")]
        public float SwishAlong = 0.025f;
        public float SwishLateral = 0.035f;
        public Vector2 RimInDepth = new Vector2(0.02f, 0.05f);     // beyond (R − r)
        public Vector2 RattleLateral = new Vector2(0.06f, 0.09f);
        public Vector2 BankLateral = new Vector2(0.04f, 0.42f);
        public Vector2 BankHeight = new Vector2(0.22f, 0.68f);      // above the rim plane

        [Header("Miss — entry angles")]
        public Vector2 ShortEntry = new Vector2(44f, 52f);
        public Vector2 LongEntry = new Vector2(36f, 44f);
        public Vector2 SideEntry = new Vector2(44f, 52f);
        public Vector2 RimOutEntry = new Vector2(40f, 46f);
        public Vector2 InAndOutEntry = new Vector2(50f, 56f);
        public Vector2 BankMissEntry = new Vector2(44f, 52f);
        public Vector2 AirBallEntry = new Vector2(40f, 50f);

        [Header("Miss — aim variation (m)")]
        public Vector2 ShortOutset = new Vector2(0.01f, 0.06f);     // beyond R, in front
        public Vector2 LongOutset = new Vector2(0.0f, 0.05f);       // beyond R, behind
        public Vector2 SideOutset = new Vector2(0.02f, 0.09f);      // beyond R, lateral
        public Vector2 RimOutDepth = new Vector2(0.00f, 0.04f);     // beyond R, behind
        public Vector2 InAndOutDepth = new Vector2(-0.02f, 0.02f);
        public Vector2 InAndOutLateral = new Vector2(0.05f, 0.09f);
        public Vector2 BankMissHeight = new Vector2(0.45f, 0.85f);
        public Vector2 BankMissLateral = new Vector2(0.25f, 0.60f);
        public Vector2 AirBallMiss = new Vector2(0.25f, 0.45f);      // beyond R + r

        [Header("Defender contact")]
        public float BlockTime = 0.11f;
        public Vector3 BlockKick = new Vector3(1.25f, 1.6f, -4.1f); // side, up, lane
        public float DeflectTime = 0.18f;
        public Vector3 DeflectTip = new Vector3(2.6f, 0.5f, 0f);

        [Header("Guidance — flight tracking (pre-contact)")]
        public float TrackP = 18f;
        public float TrackD = 3.5f;
        public float TrackMaxAccel = 2.5f;

        [Header("Guidance — funnel (make, after contact)")]
        public float FunnelAccel = 7f;
        public float FunnelHorizontalDamp = 0.12f;
        public float FunnelRadiusPad = 0.12f;
        public float FunnelBelowPlane = 0.05f;
        public float FunnelAbovePlane = 0.45f;

        [Header("Guidance — guard (miss)")]
        public float GuardAccel = 14f;
        public float GuardMinExitSpeed = 1.6f;

        [Header("Detector")]
        public float ConfirmDepth = 0.25f;
        public float MissDepth = 0.5f;
        public float ThroughRevertTime = 0.5f;
        public float ShotTimeout = 6f;

        public static ShotTuningData Default => new ShotTuningData();
    }

    /// <summary>Inspector/asset wrapper for <see cref="ShotTuningData"/>.</summary>
    [CreateAssetMenu(menuName = "Basketball Betting/Shot Tuning", fileName = "ShotTuning")]
    public sealed class ShotTuning : ScriptableObject
    {
        public ShotTuningData Data = new ShotTuningData();

        public static ShotTuning CreateDefault()
        {
            var t = CreateInstance<ShotTuning>();
            t.name = "ShotTuning (runtime default)";
            return t;
        }
    }
}
