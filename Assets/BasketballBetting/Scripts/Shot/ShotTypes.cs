using System;
using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>The only two gameplay outcomes. Betting decides which one; gameplay realises it.</summary>
    public enum ShotOutcome
    {
        Make = 0,
        Miss = 1
    }

    /// <summary>
    /// How the outcome is realised physically. A style never changes the outcome:
    /// every style is either a make style or a miss style.
    /// </summary>
    public enum ShotStyle
    {
        // Make styles
        Swish = 0,
        RimIn = 1,
        BankIn = 2,
        RattleIn = 3,
        HighArc = 4,

        // Miss styles
        Short = 10,
        Long = 11,
        Left = 12,
        Right = 13,
        RimOut = 14,
        InAndOut = 15,
        BackRim = 16,
        BankMiss = 17,
        AirBall = 18,
        Blocked = 19,
        Deflected = 20
    }

    /// <summary>What the ball touched. Reported by the ball, consumed by audio/camera/detector.</summary>
    public enum ShotContactKind
    {
        None = 0,
        Rim = 1,
        Backboard = 2,
        Floor = 3,
        Net = 4,
        Defender = 5
    }

    /// <summary>What the basket detector saw for the current shot.</summary>
    public enum ShotObservation
    {
        Pending = 0,
        Make = 1,
        Miss = 2
    }

    /// <summary>
    /// The betting → gameplay handoff. Built once per round from the settled RoundMathResult.
    /// Immutable; the gameplay never writes back to betting.
    /// </summary>
    [Serializable]
    public readonly struct ShotIntent
    {
        public readonly ShotOutcome Outcome;
        public readonly ShotStyle Style;
        public readonly int Seed;
        public readonly string RoundId;

        public ShotIntent(ShotOutcome outcome, ShotStyle style, int seed, string roundId)
        {
            if (!StyleMatches(outcome, style))
                throw new ArgumentException($"Style {style} is not a {outcome} style.");
            Outcome = outcome;
            Style = style;
            Seed = seed;
            RoundId = roundId ?? string.Empty;
        }

        public bool IsMake => Outcome == ShotOutcome.Make;

        public static bool IsMakeStyle(ShotStyle style) => (int)style < 10;

        public static bool StyleMatches(ShotOutcome outcome, ShotStyle style)
        {
            return (outcome == ShotOutcome.Make) == IsMakeStyle(style);
        }

        /// <summary>
        /// Maps the betting result to a gameplay intent. Won → a make style, lost → a miss style.
        /// The betting Animation id is only a hint for the style; the outcome is always Won.
        /// </summary>
        public static ShotIntent FromBet(RoundMathResult math, TimingZone timing)
        {
            if (math == null)
                throw new ArgumentNullException(nameof(math));

            int seed = SeedFrom(math.RoundId);
            ShotOutcome outcome = math.Won ? ShotOutcome.Make : ShotOutcome.Miss;
            ShotStyle style = StyleFromAnimation(math.Animation, outcome, timing, seed);
            return new ShotIntent(outcome, style, seed, math.RoundId);
        }

        public static ShotStyle StyleFromAnimation(ShotAnimationId anim, ShotOutcome outcome, TimingZone timing, int seed)
        {
            var rng = new System.Random(seed ^ 0x5bd1e995);
            if (outcome == ShotOutcome.Make)
            {
                switch (anim)
                {
                    case ShotAnimationId.RimIn: return ShotStyle.RimIn;
                    case ShotAnimationId.BackboardIn: return ShotStyle.BankIn;
                    case ShotAnimationId.MultiRimIn: return ShotStyle.RattleIn;
                    case ShotAnimationId.HighArc: return ShotStyle.HighArc;
                    case ShotAnimationId.ContestedMake:
                    case ShotAnimationId.BuzzerStyle:
                        return rng.NextDouble() < 0.6 ? ShotStyle.Swish : ShotStyle.RimIn;
                    default: return ShotStyle.Swish;
                }
            }

            switch (anim)
            {
                case ShotAnimationId.RimOut: return ShotStyle.RimOut;
                case ShotAnimationId.BackRimMiss: return ShotStyle.BackRim;
                case ShotAnimationId.InAndOut: return ShotStyle.InAndOut;
                case ShotAnimationId.BackboardMiss: return ShotStyle.BankMiss;
                case ShotAnimationId.DefenderBlock: return ShotStyle.Blocked;
                case ShotAnimationId.DefenderDeflection: return ShotStyle.Deflected;
                case ShotAnimationId.AirBall:
                    // Early releases fall short, late releases pull wide; otherwise pure air ball.
                    if (timing == TimingZone.Early) return ShotStyle.Short;
                    if (timing == TimingZone.Late) return rng.NextDouble() < 0.5 ? ShotStyle.Left : ShotStyle.Right;
                    return ShotStyle.AirBall;
                default:
                    {
                        double roll = rng.NextDouble();
                        if (roll < 0.35) return ShotStyle.Short;
                        if (roll < 0.6) return ShotStyle.Long;
                        if (roll < 0.8) return ShotStyle.Left;
                        return ShotStyle.Right;
                    }
            }
        }

        /// <summary>Stable FNV-1a hash so a round id always yields the same shot.</summary>
        public static int SeedFrom(string roundId)
        {
            if (string.IsNullOrEmpty(roundId))
                return 0x1234567;
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < roundId.Length; i++)
                {
                    h ^= roundId[i];
                    h *= 16777619;
                }
                return (int)h;
            }
        }

        public override string ToString() => $"{Outcome}/{Style} seed={Seed}";
    }

    /// <summary>Physical description of the ball used by planner, guidance and detector.</summary>
    [Serializable]
    public struct BallSpec
    {
        public float Radius;
        public float Mass;

        public static BallSpec Regulation => new BallSpec { Radius = 0.12f, Mass = 0.62f };
    }
}
