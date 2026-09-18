using System;
using UnityEngine;

namespace BasketballBetting
{
    public readonly struct WalletResult
    {
        public readonly bool Success;
        public readonly string TransactionId;
        public readonly string Error;
        public readonly double BalanceAfter;

        public WalletResult(bool success, string transactionId, string error, double balanceAfter)
        {
            Success = success;
            TransactionId = transactionId;
            Error = error;
            BalanceAfter = balanceAfter;
        }

        public static WalletResult Ok(string tx, double balance) => new WalletResult(true, tx, null, balance);
        public static WalletResult Fail(string error, double balance) => new WalletResult(false, null, error, balance);
    }

    public sealed class WalletTxContext
    {
        public string RoundId;
        public GameModeType Mode;
        public string CharacterId;
        public WalletTxReason Reason;
        public double Amount;
    }

    /// <summary>
    /// Host money-app integration point. Replace LocalWalletProvider before public cash play.
    /// </summary>
    public interface IWalletProvider
    {
        string CurrencyCode { get; }
        double GetBalance();
        WalletResult TryDebit(WalletTxContext context);
        WalletResult Credit(WalletTxContext context);
    }

    public sealed class LocalWalletProvider : IWalletProvider
    {
        const string PrefsKey = "BasketballBetting.LocalBalance";
        readonly BettingConfigData _config;
        double _balance;

        public LocalWalletProvider(BettingConfigData config)
        {
            _config = config;
            _balance = LoadBalance(config);
            if (_balance < config.minBet)
            {
                _balance = config.startingBalance;
                Persist();
            }
            CurrencyCode = config.currencyCode;
        }

        public string CurrencyCode { get; }

        double LoadBalance(BettingConfigData config)
        {
            if (!PlayerPrefs.HasKey(PrefsKey))
                return config.startingBalance;
            if (double.TryParse(PlayerPrefs.GetString(PrefsKey, config.startingBalance.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value))
                return value;
            return PlayerPrefs.GetFloat(PrefsKey, (float)config.startingBalance);
        }

        public double GetBalance() => _balance;

        public WalletResult TryDebit(WalletTxContext context)
        {
            if (context == null || context.Amount <= 0)
                return WalletResult.Fail("Invalid debit amount.", _balance);
            if (_balance + 0.0001 < context.Amount)
                return WalletResult.Fail("Insufficient balance.", _balance);

            _balance = Math.Round(_balance - context.Amount, _config.decimalPlaces);
            Persist();
            return WalletResult.Ok(Guid.NewGuid().ToString("N"), _balance);
        }

        public WalletResult Credit(WalletTxContext context)
        {
            if (context == null || context.Amount <= 0)
                return WalletResult.Fail("Invalid credit amount.", _balance);

            _balance = Math.Round(_balance + context.Amount, _config.decimalPlaces);
            Persist();
            return WalletResult.Ok(Guid.NewGuid().ToString("N"), _balance);
        }

        public void SetBalance(double amount)
        {
            _balance = Math.Max(0, amount);
            Persist();
        }

        public void ResetToStartingBalance()
        {
            _balance = _config.startingBalance;
            Persist();
        }

        void Persist()
        {
            PlayerPrefs.SetString(PrefsKey, _balance.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }
    }

    public static class WalletService
    {
        public static IWalletProvider Provider { get; private set; }
        public static event Action<double> BalanceChanged;

        public static void Initialize(IWalletProvider provider)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            BalanceChanged?.Invoke(Provider.GetBalance());
        }

        public static double Balance => Provider != null ? Provider.GetBalance() : 0;

        public static WalletResult Debit(WalletTxContext context)
        {
            Ensure();
            WalletResult result = Provider.TryDebit(context);
            if (result.Success)
            {
                BalanceChanged?.Invoke(result.BalanceAfter);
                MoneyAppBridge.RaiseBalanceChanged(result.BalanceAfter, context);
            }
            return result;
        }

        public static WalletResult Credit(WalletTxContext context)
        {
            Ensure();
            WalletResult result = Provider.Credit(context);
            if (result.Success)
            {
                BalanceChanged?.Invoke(result.BalanceAfter);
                MoneyAppBridge.RaiseBalanceChanged(result.BalanceAfter, context);
            }
            return result;
        }

        static void Ensure()
        {
            if (Provider == null)
                throw new InvalidOperationException("Wallet provider is not registered. Call WalletService.Initialize or MoneyAppBridge.RegisterWallet.");
        }
    }

    public sealed class BetPlacedEvent
    {
        public string RoundId;
        public GameModeType Mode;
        public string CharacterId;
        public double Stake;
        public string DebitTransactionId;
        public DateTimeOffset TimestampUtc;
    }

    public sealed class RoundSettledEvent
    {
        public RoundMathResult Result;
        public string PayoutTransactionId;
        public double BalanceAfter;
        public TimingZone Timing;
    }

    /// <summary>
    /// Public integration surface for host money apps.
    /// Call RegisterWallet before the first round when embedding this game.
    /// </summary>
    public static class MoneyAppBridge
    {
        public static event Action<BetPlacedEvent> BetPlaced;
        public static event Action<RoundSettledEvent> RoundSettled;
        public static event Action<double, WalletTxContext> BalanceChanged;
        public static event Action<BettingConfigData> ConfigLoaded;

        public static void RegisterWallet(IWalletProvider provider)
        {
            WalletService.Initialize(provider);
        }

        public static void ApplyRemoteConfig(string json)
        {
            BettingConfigLoader.Apply(BettingConfigLoader.Parse(json));
            ConfigLoaded?.Invoke(BettingConfigLoader.Current);
        }

        public static void ApplyRemoteConfig(BettingConfigData data)
        {
            BettingConfigLoader.Apply(data);
            ConfigLoaded?.Invoke(BettingConfigLoader.Current);
        }

        internal static void RaiseBetPlaced(BetPlacedEvent evt) => BetPlaced?.Invoke(evt);
        internal static void RaiseRoundSettled(RoundSettledEvent evt) => RoundSettled?.Invoke(evt);
        internal static void RaiseBalanceChanged(double balance, WalletTxContext ctx) => BalanceChanged?.Invoke(balance, ctx);
        internal static void RaiseConfigLoaded(BettingConfigData data) => ConfigLoaded?.Invoke(data);
    }
}
