using System;

namespace BasketballBetting
{
    public interface IRandomSource
    {
        double NextDouble();
        int Next(int minInclusive, int maxExclusive);
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        readonly Random _random;

        public SystemRandomSource(int seed)
        {
            _random = seed == 0 ? new Random() : new Random(seed);
        }

        public double NextDouble() => _random.NextDouble();
        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }

    /// <summary>
    /// Math layer only. Decides win/lose and a compatible animation id.
    /// Presentation must never override this result.
    /// </summary>
    public sealed class BettingEngine
    {
        readonly BettingConfigData _config;
        readonly IRandomSource _rng;

        public BettingEngine(BettingConfigData config, IRandomSource rng = null)
        {
            _config = config ?? BettingConfigLoader.CreateDefaults();
            _rng = rng ?? new SystemRandomSource(_config.rngSeed);
        }

        public RoundMathResult Resolve(RoundRequest request, TimingZone? timing = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            GameModeOdds mode = request.IsDoubleOrNothing
                ? new GameModeOdds
                {
                    makeProbability = _config.doubleOrNothing.makeProbability,
                    payoutMultiplier = _config.doubleOrNothing.payoutMultiplier,
                    winAnimations = _config.GetMode(request.Mode).winAnimations,
                    loseAnimations = _config.GetMode(request.Mode).loseAnimations,
                    blockShareOfMisses = request.Mode == GameModeType.ThreePoint ? 0.18 : 0,
                    deflectionShareOfMisses = request.Mode == GameModeType.ThreePoint ? 0.14 : 0
                }
                : _config.GetMode(request.Mode);

            double makeProb = Clamp01(mode.makeProbability);
            makeProb = ApplyOptionalModifiers(makeProb, request, timing);

            bool won = _rng.NextDouble() < makeProb;
            ShotAnimationId animation = PickAnimation(mode, won, request.Mode);
            DefenderScenario defender = PickDefenderScenario(request.Mode, won, animation);

            double multiplier = Math.Min(mode.payoutMultiplier, _config.maxMultiplier);
            double payout = 0;
            if (won)
            {
                double baseAmount = request.IsDoubleOrNothing ? request.DoubleOrNothingBaseWin : request.Stake;
                payout = _config.ClampPayout(baseAmount * multiplier, multiplier);
            }

            return new RoundMathResult
            {
                RoundId = string.IsNullOrEmpty(request.RoundId) ? Guid.NewGuid().ToString("N") : request.RoundId,
                Mode = request.Mode,
                Stake = request.Stake,
                Won = won,
                Payout = payout,
                Multiplier = multiplier,
                MakeProbabilityUsed = makeProb,
                Animation = animation,
                DefenderScenario = defender,
                IsDoubleOrNothing = request.IsDoubleOrNothing,
                CharacterId = request.Character != null ? request.Character.Id : "unknown",
                SettledAtUtc = DateTimeOffset.UtcNow
            };
        }

        double ApplyOptionalModifiers(double makeProb, RoundRequest request, TimingZone? timing)
        {
            double modifier = 0;

            if (_config.timingMeterAffectsOdds && timing.HasValue && _config.timingModifiers != null)
            {
                switch (timing.Value)
                {
                    case TimingZone.Early: modifier += _config.timingModifiers.early; break;
                    case TimingZone.Good: modifier += _config.timingModifiers.good; break;
                    case TimingZone.Perfect: modifier += _config.timingModifiers.perfect; break;
                    case TimingZone.Late: modifier += _config.timingModifiers.late; break;
                }
            }

            if (_config.characterStatsAffectOdds && request.Character != null && _config.characterModifiers != null)
            {
                CharacterAttributes a = request.Character.Attributes;
                int baseline = 80;
                modifier += (a.shooting - baseline) * _config.characterModifiers.shootingPerPoint;
                modifier += (a.accuracy - baseline) * _config.characterModifiers.accuracyPerPoint;
                if (request.Mode == GameModeType.ThreePoint)
                    modifier += (a.threePoint - baseline) * _config.characterModifiers.threePointPerPoint;

                double cap = _config.characterModifiers.maxAbsoluteModifier;
                if (cap > 0)
                    modifier = Math.Max(-cap, Math.Min(cap, modifier));
            }

            return Clamp01(makeProb + modifier);
        }

        /// <summary>
        /// Presentation only. Never changes Won, Stake, Payout, or Multiplier.
        /// Early/late misses are more often wide/short so routine coverage can actually appear.
        /// </summary>
        public void BindPresentation(RoundMathResult result, TimingZone timing)
        {
            if (result == null || result.Won)
                return;

            if (result.Animation == ShotAnimationId.AirBall)
            {
                if (result.Mode == GameModeType.ThreePoint && result.DefenderScenario != DefenderScenario.Block)
                    result.DefenderScenario = DefenderScenario.CleanShot;
                return;
            }

            if (result.Animation == ShotAnimationId.DefenderBlock
                || result.Animation == ShotAnimationId.DefenderDeflection
                || result.Animation == ShotAnimationId.InAndOut)
                return;

            if (result.Animation != ShotAnimationId.RimOut
                && result.Animation != ShotAnimationId.BackRimMiss
                && result.Animation != ShotAnimationId.BackboardMiss)
                return;

            float p = 0f;
            switch (timing)
            {
                case TimingZone.Early: p = 0.78f; break;
                case TimingZone.Late: p = 0.58f; break;
                case TimingZone.Good: p = 0.28f; break;
                case TimingZone.Perfect: p = 0.08f; break;
            }

            if (_rng.NextDouble() >= p)
                return;

            result.Animation = ShotAnimationId.AirBall;
            if (result.Mode == GameModeType.ThreePoint && result.DefenderScenario != DefenderScenario.Block)
                result.DefenderScenario = DefenderScenario.CleanShot;
        }

        ShotAnimationId PickAnimation(GameModeOdds mode, bool won, GameModeType gameMode)
        {
            AnimationWeight[] table = won ? mode.winAnimations : mode.loseAnimations;
            ShotAnimationId picked = WeightedPick(table, won ? ShotAnimationId.Swish : ShotAnimationId.RimOut);

            if (gameMode == GameModeType.FreeThrow)
            {
                if (picked == ShotAnimationId.DefenderBlock || picked == ShotAnimationId.DefenderDeflection)
                    picked = won ? ShotAnimationId.RimIn : ShotAnimationId.RimOut;
            }

            if (won && (picked == ShotAnimationId.DefenderBlock || picked == ShotAnimationId.DefenderDeflection))
                picked = ShotAnimationId.ContestedMake;

            if (!won && gameMode == GameModeType.ThreePoint)
            {
                double roll = _rng.NextDouble();
                double blockShare = Clamp01(mode.blockShareOfMisses);
                double deflShare = Clamp01(mode.deflectionShareOfMisses);
                if (roll < blockShare)
                    return ShotAnimationId.DefenderBlock;
                if (roll < blockShare + deflShare)
                    return ShotAnimationId.DefenderDeflection;
                if (picked == ShotAnimationId.DefenderBlock || picked == ShotAnimationId.DefenderDeflection)
                    picked = WeightedPick(FilterNonDefender(mode.loseAnimations), ShotAnimationId.RimOut);
            }

            return picked;
        }

        DefenderScenario PickDefenderScenario(GameModeType mode, bool won, ShotAnimationId animation)
        {
            if (mode != GameModeType.ThreePoint)
                return DefenderScenario.None;

            switch (animation)
            {
                case ShotAnimationId.DefenderBlock:
                    return DefenderScenario.Block;
                case ShotAnimationId.DefenderDeflection:
                    return DefenderScenario.SlightDeflection;
                case ShotAnimationId.ContestedMake:
                    return _rng.NextDouble() < 0.45 ? DefenderScenario.VeryCloseContest : DefenderScenario.ContestedShot;
                case ShotAnimationId.AirBall:
                    return DefenderScenario.CleanShot;
                default:
                    if (won)
                        return _rng.NextDouble() < 0.35 ? DefenderScenario.ContestedShot : DefenderScenario.CleanShot;
                    return _rng.NextDouble() < 0.4 ? DefenderScenario.ContestedShot : DefenderScenario.CleanShot;
            }
        }

        ShotAnimationId WeightedPick(AnimationWeight[] table, ShotAnimationId fallback)
        {
            if (table == null || table.Length == 0)
                return fallback;

            float total = 0f;
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] != null && table[i].weight > 0)
                    total += table[i].weight;
            }

            if (total <= 0f)
                return fallback;

            float pick = (float)_rng.NextDouble() * total;
            float cumulative = 0f;
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] == null || table[i].weight <= 0)
                    continue;
                cumulative += table[i].weight;
                if (pick <= cumulative)
                    return ParseAnimation(table[i].id, fallback);
            }

            return fallback;
        }

        static AnimationWeight[] FilterNonDefender(AnimationWeight[] source)
        {
            if (source == null)
                return Array.Empty<AnimationWeight>();
            var list = new System.Collections.Generic.List<AnimationWeight>();
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null)
                    continue;
                ShotAnimationId id = ParseAnimation(source[i].id, ShotAnimationId.RimOut);
                if (id != ShotAnimationId.DefenderBlock && id != ShotAnimationId.DefenderDeflection)
                    list.Add(source[i]);
            }
            return list.ToArray();
        }

        static ShotAnimationId ParseAnimation(string id, ShotAnimationId fallback)
        {
            if (string.IsNullOrEmpty(id))
                return fallback;
            return Enum.TryParse(id, true, out ShotAnimationId parsed) ? parsed : fallback;
        }

        static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }
    }
}
