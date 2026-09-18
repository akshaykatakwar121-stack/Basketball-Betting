using System;
using System.Collections.Generic;
using BasketballBetting.Ui;
using UnityEngine;
using UnityEngine.UI;
using UiImage = UnityEngine.UI.Image;

namespace BasketballBetting
{
    /// <summary>Bet lobby bottom sheet: grabber, shooter card, bet chips, mode switcher, lock-in.</summary>
    public sealed class BetPanelView : UiView
    {
        Text _balanceText, _stakeLabel, _payoutLabel, _oddsLabel, _shooterNameText;
        List<Button> _chipBtns = new List<Button>();
        Widgets.Segmented _modeSeg;
        BettingConfigData _cfg;
        double _stake;
        GameModeType _selectedMode;

        public event Action OnLockIn;
        public event Action OnBack;
        public double Stake => _stake;
        public GameModeType SelectedMode => _selectedMode;

        public BetPanelView(Transform parent)
        {
            Init(parent, "BetPanel");

            // Background scrim
            var bg = new GameObject("Scrim", typeof(RectTransform), typeof(UiImage));
            bg.GetComponent<RectTransform>().SetParent(Root, false);
            Widgets.Stretch(bg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            bg.GetComponent<UiImage>().color = new Color(0f, 0f, 0f, 0.60f);
            bg.GetComponent<UiImage>().raycastTarget = true;

            // Bottom sheet panel
            var sheet = Widgets.Card(Root, "Sheet",
                UiTokens.Alpha(UiTokens.Carbon, 0.97f), UiTokens.Gold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1080f, 660f), new Vector2(0f, 0f), 22, true, true);

            // Grabber pill
            var grab = new GameObject("Grabber", typeof(RectTransform), typeof(UiImage));
            grab.GetComponent<RectTransform>().SetParent(sheet.transform, false);
            Widgets.Place(grab.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(60f, 6f), new Vector2(0f, -14f));
            grab.GetComponent<UiImage>().sprite = UiSprites.Pill;
            grab.GetComponent<UiImage>().color = new Color(1f, 1f, 1f, 0.20f);
            grab.GetComponent<UiImage>().raycastTarget = false;

            // Title
            Widgets.Caption(sheet.transform, "Title", "Place Your Stake", UiTokens.Gold, UiTokens.H3, TextAnchor.UpperCenter);
            sheet.transform.Find("Title").GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1f);
            sheet.transform.Find("Title").GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
            sheet.transform.Find("Title").GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            sheet.transform.Find("Title").GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -44f);
            sheet.transform.Find("Title").GetComponent<RectTransform>().sizeDelta = new Vector2(1000f, 44f);

            // Shooter card button
            var shooterCard = Widgets.Card(sheet.transform, "ShooterCard",
                UiTokens.Alpha(UiTokens.Slate, 0.90f), UiTokens.Accent,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(960f, 80f), new Vector2(0f, -104f), 12, false, false);
            _shooterNameText = Widgets.Label(shooterCard.transform, "Name", "Select Player", UiTokens.Body, UiTokens.Ice, TextAnchor.MiddleLeft);
            Widgets.Stretch(_shooterNameText.rectTransform, 16f, 8f, 120f, 8f);
            var changeBtn = Widgets.Ghost(shooterCard.transform, "Change", "CHANGE", UiTokens.Micro, () => OnBack?.Invoke(), 8);
            Widgets.Place((RectTransform)changeBtn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(120f, 48f), new Vector2(-8f, 0f));

            // Mode segmented control
            _modeSeg = new Widgets.Segmented(sheet.transform, "ModeSeg",
                new[] { "FREE THROW", "THREE-POINT" }, UiTokens.Small, OnModeSelect, new Vector2(960f, 64f));
            Widgets.Place(_modeSeg.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(960f, 64f), new Vector2(0f, -200f));

            // Chip row (presets built in Present)
            var chipRow = new GameObject("ChipRow", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            chipRow.SetParent(sheet.transform, false);
            Widgets.Place(chipRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(960f, 68f), new Vector2(0f, -286f));
            var hlg = chipRow.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            // Stake stepper row
            var stepRow = new GameObject("StepRow", typeof(RectTransform)).GetComponent<RectTransform>();
            stepRow.SetParent(sheet.transform, false);
            Widgets.Place(stepRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(960f, 80f), new Vector2(0f, -372f));
            var minusBtn = Widgets.Ghost(stepRow, "Minus", "−", UiTokens.H1, () => AdjustStake(-1), 12);
            Widgets.Place((RectTransform)minusBtn.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, 72f), new Vector2(0f, 0f));
            _stakeLabel = Widgets.Display(stepRow, "Stake", "$10", UiTokens.H1, UiTokens.Gold, TextAnchor.MiddleCenter);
            Widgets.Place(_stakeLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600f, 72f), new Vector2(0f, 0f));
            var plusBtn = Widgets.Ghost(stepRow, "Plus", "+", UiTokens.H1, () => AdjustStake(1), 12);
            Widgets.Place((RectTransform)plusBtn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(100f, 72f), new Vector2(0f, 0f));

            // Payout / odds row
            _payoutLabel = Widgets.Label(sheet.transform, "Payout", "Payout: $20  ·  2x odds", UiTokens.Small, UiTokens.TextDim, TextAnchor.MiddleCenter);
            Widgets.Place(_payoutLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(960f, 32f), new Vector2(0f, -464f));

            // LOCK IN button
            var lockBtn = Widgets.Primary(sheet.transform, "LockIn", "LOCK IN", UiTokens.H2, () => OnLockIn?.Invoke(), 18);
            Widgets.Place((RectTransform)lockBtn.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(800f, 96f), new Vector2(0f, 48f));

            // Balance label
            _balanceText = Widgets.Label(sheet.transform, "Balance", "BANK: $0", UiTokens.Small, UiTokens.TextFaint, TextAnchor.UpperRight);
            Widgets.Place(_balanceText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(300f, 32f), new Vector2(-32f, -8f));
        }

        void OnModeSelect(int i)
        {
            _selectedMode = i == 0 ? GameModeType.FreeThrow : GameModeType.ThreePoint;
            _modeSeg?.Select(i);
            RefreshPayout();
        }

        void AdjustStake(int dir)
        {
            if (_cfg == null || _chipBtns.Count == 0) return;
            // find current preset index
            double[] presets = _cfg.betPresets;
            if (presets == null || presets.Length == 0) return;
            int ci = 0;
            for (int i = 0; i < presets.Length; i++) if (presets[i] <= _stake) ci = i;
            ci = Mathf.Clamp(ci + dir, 0, presets.Length - 1);
            SetStake(presets[ci]);
        }

        void SetStake(double v)
        {
            _stake = v;
            if (_stakeLabel != null) _stakeLabel.text = "$" + v.ToString("N0");
            RefreshChipSelection();
            RefreshPayout();
        }

        void RefreshChipSelection()
        {
            if (_cfg == null) return;
            double[] presets = _cfg.betPresets;
            for (int i = 0; i < _chipBtns.Count && i < presets.Length; i++)
                Widgets.SetChipSelected(_chipBtns[i], Math.Abs(presets[i] - _stake) < 0.01);
        }

        void RefreshPayout()
        {
            if (_payoutLabel == null || _cfg == null) return;
            var mode = _cfg.GetMode(_selectedMode);
            if (mode == null) return;
            double payout = _stake * mode.payoutMultiplier;
            _payoutLabel.text = "Payout: $" + payout.ToString("N0") + "  ·  " + mode.payoutMultiplier.ToString("F1") + "x";
        }

        public void Present(BettingConfigData cfg, double balance, int modeIndex, double stake)
        {
            _cfg = cfg;
            _stake = stake;
            _selectedMode = modeIndex == 0 ? GameModeType.FreeThrow : GameModeType.ThreePoint;

            // Build chips if needed
            if (_chipBtns.Count == 0 && cfg != null && cfg.betPresets != null)
            {
                var chipRow = Root.GetComponentInChildren<HorizontalLayoutGroup>();
                if (chipRow != null)
                {
                    foreach (var preset in cfg.betPresets)
                    {
                        double p = preset;
                        var chip = Widgets.Chip(chipRow.transform, "Chip" + p, "$" + p.ToString("N0"), UiTokens.Small, () => SetStake(p));
                        Widgets.Size(chip, 160f, 60f);
                        _chipBtns.Add(chip);
                    }
                }
            }

            if (_balanceText != null) _balanceText.text = "BANK: $" + balance.ToString("N0");
            SetStake(stake);
            _modeSeg?.Select(modeIndex);
            Show();
        }

        public void SetShooterName(string name, string nickname)
        {
            if (_shooterNameText != null)
                _shooterNameText.text = (name ?? "Player") + (nickname != null ? "  ·  " + nickname : "");
        }
    }
}
