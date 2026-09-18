using System;
using UnityEngine;

namespace BasketballBetting
{
    public enum GameModeType
    {
        FreeThrow = 0,
        ThreePoint = 1
    }

    public enum TimingZone
    {
        Early = 0,
        Good = 1,
        Perfect = 2,
        Late = 3
    }

    public enum ShotAnimationId
    {
        Swish,
        RimIn,
        BackboardIn,
        MultiRimIn,
        HighArc,
        ContestedMake,
        BuzzerStyle,
        RimOut,
        BackRimMiss,
        InAndOut,
        BackboardMiss,
        AirBall,
        DefenderBlock,
        DefenderDeflection
    }

    public enum DefenderScenario
    {
        None,
        CleanShot,
        ContestedShot,
        Block,
        SlightDeflection,
        VeryCloseContest
    }

    public enum ThreePointSpot
    {
        Top,
        LeftWing,
        RightWing,
        LeftCorner,
        RightCorner
    }

    public enum BroadcastTier
    {
        Threat,
        Routine
    }

    public static class BroadcastCoverage
    {
        public static BroadcastTier Classify(RoundMathResult math)
        {
            if (math == null)
                return BroadcastTier.Routine;
            if (math.Won)
                return BroadcastTier.Threat;
            switch (math.Animation)
            {
                case ShotAnimationId.AirBall:
                    return BroadcastTier.Routine;
                default:
                    return BroadcastTier.Threat;
            }
        }
    }

    public enum WalletTxReason
    {
        Stake,
        Payout,
        Refund,
        DoubleOrNothingStake,
        DoubleOrNothingPayout
    }

    [Serializable]
    public struct CharacterAttributes
    {
        public int shooting;
        public int accuracy;
        public int releaseSpeed;
        public int power;
        public int threePoint;
    }

    public sealed class RoundRequest
    {
        public string RoundId;
        public GameModeType Mode;
        public double Stake;
        public CharacterDefinition Character;
        public ThreePointSpot Spot;
        public bool IsDoubleOrNothing;
        public double DoubleOrNothingBaseWin;
    }

    public sealed class RoundMathResult
    {
        public string RoundId;
        public GameModeType Mode;
        public double Stake;
        public bool Won;
        public double Payout;
        public double Multiplier;
        public double MakeProbabilityUsed;
        public ShotAnimationId Animation;
        public DefenderScenario DefenderScenario;
        public bool IsDoubleOrNothing;
        public string CharacterId;
        public DateTimeOffset SettledAtUtc;
    }

    public sealed class PresentedShot
    {
        public RoundMathResult Math;
        public TimingZone Timing;
        public float TimingNormalized;
        public ThreePointSpot Spot;
        public Vector3 ShootFrom;
        public Vector3 RimCenter;
    }

    public static class CourtMetrics
    {
        public const float Length = 28.65f;
        public const float Width = 15.24f;
        public const float HalfLength = Length * 0.5f;
        public const float HalfWidth = Width * 0.5f;
        public const float RimHeight = 3.05f;
        public const float RimRadius = 0.2286f;
        public const float BackboardWidth = 1.83f;
        public const float BackboardHeight = 1.07f;
        public const float BackboardFromBaseline = 1.22f;
        public const float RimFromBackboard = 0.40f;
        public const float FreeThrowFromBackboard = 4.57f;
        public const float ThreePointRadius = 7.24f;
        public const float ThreePointCorner = 6.70f;

        public static float BaselineZ => HalfLength;
        public static float BackboardZ => HalfLength - BackboardFromBaseline;
        public static Vector3 RimCenter => new Vector3(0f, RimHeight, BackboardZ - RimFromBackboard);
        public static float FreeThrowZ => BackboardZ - FreeThrowFromBackboard;
        public const float BackboardThickness = 0.05f;
        public static float PlayGlassCourtFaceZ => BackboardZ - BackboardThickness * 0.5f;

        public static float GlassClearance(float radius = 0.12f) => PlayGlassCourtFaceZ - radius - 0.03f;

        /// <summary>
        /// Keeps the ball on the court side of the play-end glass. Overshoot reflects
        /// so a rim deflection never interpolates through the backboard.
        /// </summary>
        public static Vector3 KeepInFrontOfGlass(Vector3 point, float radius = 0.12f)
        {
            float limit = GlassClearance(radius);
            if (point.z > limit)
            {
                point.z = limit - (point.z - limit);
                if (point.z > limit)
                    point.z = limit;
            }
            return point;
        }

        public static Vector3 FreeThrowSpot => new Vector3(0f, 0f, FreeThrowZ);

        public static Vector3 GetThreePointSpot(ThreePointSpot spot)
        {
            Vector3 rim = RimCenter;
            rim.y = 0f;
            switch (spot)
            {
                case ThreePointSpot.LeftCorner:
                    return new Vector3(-HalfWidth + 0.91f, 0f, rim.z - ThreePointCorner);
                case ThreePointSpot.RightCorner:
                    return new Vector3(HalfWidth - 0.91f, 0f, rim.z - ThreePointCorner);
                case ThreePointSpot.LeftWing:
                    return PolarFromRim(135f);
                case ThreePointSpot.RightWing:
                    return PolarFromRim(45f);
                default:
                    return new Vector3(0f, 0f, rim.z - ThreePointRadius);
            }
        }

        static Vector3 PolarFromRim(float angleDegFromPositiveX)
        {
            Vector3 rim = RimCenter;
            float rad = angleDegFromPositiveX * Mathf.Deg2Rad;
            return new Vector3(
                rim.x + Mathf.Cos(rad) * ThreePointRadius,
                0f,
                rim.z - Mathf.Abs(Mathf.Sin(rad) * ThreePointRadius));
        }
    }
}
