# HOOPS — Money App Integration

The game boots itself when you press Play. Math and presentation are separate:

`Bet -> Wallet debit -> RNG result -> compatible shot animation -> wallet credit`

## Wallet

Implement `IWalletProvider` and register it before the first round:

```csharp
MoneyAppBridge.RegisterWallet(new YourAppWalletProvider());
```

If you do not register a wallet, the game uses `LocalWalletProvider` (PlayerPrefs demo balance).

Listen for settlement:

```csharp
MoneyAppBridge.BetPlaced += evt => { /* stake reserved */ };
MoneyAppBridge.RoundSettled += evt => { /* payout / loss */ };
MoneyAppBridge.BalanceChanged += (balance, ctx) => { /* refresh host UI */ };
```

Each round has a `RoundId`. Debit and payout events include that id for reconciliation.

## Configurable math

Edit `Assets/StreamingAssets/BettingConfig.json` without a code change.

Important flags:

- `timingMeterAffectsOdds` — default `false`. Meter is skill-feel only.
- `characterStatsAffectOdds` — default `false`. Ratings are style only.
- `freeThrow` / `threePoint` — make probability, payout multiplier, animation weights, block/deflection shares
- `betPresets`, `minBet`, `maxBet`, `maxPayout`, `maxMultiplier`
- `doubleOrNothing`
- `oddsFormat` — `decimal` or `american`
- `currencySymbol`, `currencyCode`, `startingBalance`

A host app can push live config:

```csharp
MoneyAppBridge.ApplyRemoteConfig(jsonFromServer);
```

Default RTP is 96%:

- Free Throw: 48% make × 2.00x
- Three-Point: 32% make × 3.00x

## Replace placeholder characters

1. Unity menu: `Basketball Betting / Create Character Asset Folder`
2. Open `Assets/BasketballBetting/Resources/Characters/`
3. Assign your model to `Custom Visual Prefab`
4. The prefab must contain a child named `RightHand` (ball attach)

Until a prefab is assigned, the built-in placeholder body is used.

## Play

Open `Assets/Scenes/SampleScene` and press Play.

- Click / tap / Space / Enter to shoot
- `Basketball Betting / Reset Demo Wallet` clears the local balance
