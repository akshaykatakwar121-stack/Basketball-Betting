using System;
using System.IO;
using UnityEngine;

namespace BasketballBetting
{
    [Serializable]
    public sealed class AnimationWeight
    {
        public string id;
        public float weight = 1f;
    }

    [Serializable]
    public sealed class TimingOddsModifiers
    {
        public double early;
        public double good;
        public double perfect;
        public double late;
    }

    [Serializable]
    public sealed class CharacterOddsModifiers
    {
        public double shootingPerPoint;
        public double accuracyPerPoint;
        public double threePointPerPoint;
        public double maxAbsoluteModifier;
    }

    [Serializable]
    public sealed class DoubleOrNothingConfig
    {
        public bool enabled = true;
        public double makeProbability = 0.45;
        public double payoutMultiplier = 2.0;
        public string label = "DOUBLE OR NOTHING";
    }

    [Serializable]
    public sealed class GameModeOdds
    {
        public string displayName;
        public string tagline;
        public double makeProbability = 0.48;
        public double payoutMultiplier = 2.0;
        public double blockShareOfMisses;
        public double deflectionShareOfMisses;
        public AnimationWeight[] winAnimations;
        public AnimationWeight[] loseAnimations;
    }

    [Serializable]
    public sealed class BettingConfigData
    {
        public string gameTitle = "HOOPS";
        public string gameSubtitle = "LIVE COURT BETTING";
        public string disclaimer = "This is a game of chance. Shot timing and player ratings are for presentation.";
        public string responsiblePlayText = "Play responsibly.";
        public string currencyCode = "USD";
        public string currencySymbol = "$";
        public int decimalPlaces = 2;
        public double startingBalance = 10000;
        public double minBet = 1;
        public double maxBet = 500;
        public double maxPayout = 5000;
        public double maxMultiplier = 10;
        public double rtp = 0.96;
        public double houseEdge = 0.04;
        public string oddsFormat = "decimal";
        public bool showRtpToPlayer;
        public bool showHouseEdgeToPlayer;
        public bool timingMeterAffectsOdds;
        public bool characterStatsAffectOdds;
        public int rngSeed;
        public float meterDurationSeconds = 0.85f;
        public int meterAutoReleaseCycles = 3;
        public float slowMotionScale = 0.28f;
        public float slowMotionDuration = 0.55f;
        public double[] betPresets = { 1, 5, 10, 25, 50, 100 };
        public TimingOddsModifiers timingModifiers = new TimingOddsModifiers();
        public CharacterOddsModifiers characterModifiers = new CharacterOddsModifiers();
        public DoubleOrNothingConfig doubleOrNothing = new DoubleOrNothingConfig();
        public GameModeOdds freeThrow;
        public GameModeOdds threePoint;

        public GameModeOdds GetMode(GameModeType mode)
        {
            return mode == GameModeType.ThreePoint ? threePoint : freeThrow;
        }

        public string FormatMoney(double amount)
        {
            string number = amount.ToString("N" + Mathf.Clamp(decimalPlaces, 0, 4));
            return string.IsNullOrEmpty(currencySymbol) ? number : currencySymbol + number;
        }

        public string FormatOdds(double multiplier)
        {
            if (string.Equals(oddsFormat, "american", StringComparison.OrdinalIgnoreCase))
            {
                if (multiplier >= 2.0)
                    return "+" + Mathf.RoundToInt((float)((multiplier - 1.0) * 100.0));
                if (multiplier > 1.0)
                    return (-100.0 / (multiplier - 1.0)).ToString("0");
                return "+0";
            }

            return multiplier.ToString("0.00") + "x";
        }

        public double ClampStake(double stake)
        {
            if (double.IsNaN(stake) || double.IsInfinity(stake))
                return minBet;
            return Math.Max(minBet, Math.Min(maxBet, stake));
        }

        public double ClampPayout(double payout, double multiplier)
        {
            double cappedMultiplier = Math.Min(multiplier, maxMultiplier);
            double value = payout;
            if (cappedMultiplier < multiplier && multiplier > 0)
                value = payout * (cappedMultiplier / multiplier);
            return Math.Min(value, maxPayout);
        }
    }

    public static class BettingConfigLoader
    {
        public const string FileName = "BettingConfig.json";
        static BettingConfigData _cached;

        public static BettingConfigData Current => _cached ?? Load();

        public static BettingConfigData Load()
        {
            string json = null;
            string streaming = Path.Combine(Application.streamingAssetsPath, FileName);
            if (File.Exists(streaming))
                json = File.ReadAllText(streaming);

            if (string.IsNullOrEmpty(json))
            {
                TextAsset resource = Resources.Load<TextAsset>("BettingConfig");
                if (resource != null)
                    json = resource.text;
            }

            _cached = Parse(json);
            Validate(_cached);
            return _cached;
        }

        public static BettingConfigData Parse(string json)
        {
            BettingConfigData data = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    data = JsonUtility.FromJson<BettingConfigData>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[BettingConfig] Failed to parse JSON: " + ex.Message);
                }
            }

            if (data == null)
                data = CreateDefaults();

            if (data.freeThrow == null)
                data.freeThrow = CreateDefaults().freeThrow;
            if (data.threePoint == null)
                data.threePoint = CreateDefaults().threePoint;
            if (data.timingModifiers == null)
                data.timingModifiers = new TimingOddsModifiers();
            if (data.characterModifiers == null)
                data.characterModifiers = new CharacterOddsModifiers();
            if (data.doubleOrNothing == null)
                data.doubleOrNothing = new DoubleOrNothingConfig();
            if (data.betPresets == null || data.betPresets.Length == 0)
                data.betPresets = new[] { 1d, 5d, 10d, 25d, 50d, 100d };

            return data;
        }

        public static void Apply(BettingConfigData data)
        {
            _cached = data ?? CreateDefaults();
            Validate(_cached);
        }

        public static void Reload()
        {
            _cached = null;
            Load();
        }

        static void Validate(BettingConfigData cfg)
        {
            WarnProbability("FreeThrow.makeProbability", cfg.freeThrow.makeProbability);
            WarnProbability("ThreePoint.makeProbability", cfg.threePoint.makeProbability);
            WarnProbability("DoubleOrNothing.makeProbability", cfg.doubleOrNothing.makeProbability);

            double ftRtp = cfg.freeThrow.makeProbability * cfg.freeThrow.payoutMultiplier;
            double tpRtp = cfg.threePoint.makeProbability * cfg.threePoint.payoutMultiplier;
            if (Math.Abs(ftRtp - cfg.rtp) > 0.05)
                Debug.LogWarning($"[BettingConfig] Free Throw implied RTP {ftRtp:0.000} differs from configured RTP {cfg.rtp:0.000}.");
            if (Math.Abs(tpRtp - cfg.rtp) > 0.05)
                Debug.LogWarning($"[BettingConfig] Three-Point implied RTP {tpRtp:0.000} differs from configured RTP {cfg.rtp:0.000}.");
        }

        static void WarnProbability(string name, double value)
        {
            if (value < 0 || value > 1)
                Debug.LogError($"[BettingConfig] {name} must be between 0 and 1. Current: {value}");
        }

        public static BettingConfigData CreateDefaults()
        {
            return new BettingConfigData
            {
                freeThrow = new GameModeOdds
                {
                    displayName = "FREE THROW",
                    tagline = "No defender. From the line. Cleaner and faster.",
                    makeProbability = 0.48,
                    payoutMultiplier = 2.0,
                    winAnimations = DefaultWins(false),
                    loseAnimations = DefaultLosses(false)
                },
                threePoint = new GameModeOdds
                {
                    displayName = "THREE-POINT",
                    tagline = "Active defender. Five spots. Blocks and contests.",
                    makeProbability = 0.32,
                    payoutMultiplier = 3.0,
                    blockShareOfMisses = 0.22,
                    deflectionShareOfMisses = 0.16,
                    winAnimations = DefaultWins(true),
                    loseAnimations = DefaultLosses(true)
                }
            };
        }

        static AnimationWeight[] DefaultWins(bool threePoint)
        {
            if (!threePoint)
            {
                return new[]
                {
                    new AnimationWeight { id = "Swish", weight = 28 },
                    new AnimationWeight { id = "RimIn", weight = 24 },
                    new AnimationWeight { id = "BackboardIn", weight = 18 },
                    new AnimationWeight { id = "MultiRimIn", weight = 16 },
                    new AnimationWeight { id = "HighArc", weight = 14 }
                };
            }

            return new[]
            {
                new AnimationWeight { id = "Swish", weight = 18 },
                new AnimationWeight { id = "RimIn", weight = 18 },
                new AnimationWeight { id = "BackboardIn", weight = 12 },
                new AnimationWeight { id = "MultiRimIn", weight = 14 },
                new AnimationWeight { id = "HighArc", weight = 12 },
                new AnimationWeight { id = "ContestedMake", weight = 16 },
                new AnimationWeight { id = "BuzzerStyle", weight = 10 }
            };
        }

        static AnimationWeight[] DefaultLosses(bool threePoint)
        {
            if (!threePoint)
            {
                return new[]
                {
                    new AnimationWeight { id = "RimOut", weight = 20 },
                    new AnimationWeight { id = "BackRimMiss", weight = 14 },
                    new AnimationWeight { id = "InAndOut", weight = 20 },
                    new AnimationWeight { id = "BackboardMiss", weight = 14 },
                    new AnimationWeight { id = "AirBall", weight = 32 }
                };
            }

            return new[]
            {
                new AnimationWeight { id = "RimOut", weight = 14 },
                new AnimationWeight { id = "BackRimMiss", weight = 10 },
                new AnimationWeight { id = "InAndOut", weight = 14 },
                new AnimationWeight { id = "BackboardMiss", weight = 10 },
                new AnimationWeight { id = "AirBall", weight = 24 },
                new AnimationWeight { id = "DefenderBlock", weight = 18 },
                new AnimationWeight { id = "DefenderDeflection", weight = 14 }
            };
        }
    }
}
