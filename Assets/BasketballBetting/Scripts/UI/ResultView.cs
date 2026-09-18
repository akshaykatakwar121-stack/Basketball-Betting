using System;
using System.Collections;
using BasketballBetting.Ui;
using UnityEngine;
using UnityEngine.UI;
using UiImage = UnityEngine.UI.Image;

namespace BasketballBetting
{
    /// <summary>In-game HUD top bar: bank, mode chip, hamburger menu.</summary>
    public sealed class HudView : UiView
    {
        Text _bankText, _modeText;

        public event Action OnMenu;

        public HudView(Transform parent)
        {
            Init(parent, "HudBar");

            // Top bar plate
            var bar = Widgets.Card(Root, "Bar",
                UiTokens.Alpha(UiTokens.Carbon, 0.92f), UiTokens.Accent,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1040f, 88f), new Vector2(0f, -12f), 14, false, false);

            // Bank section (left)
            var bankLbl = Widgets.Caption(bar.transform, "BankLbl", "BANK", UiTokens.TextFaint, UiTokens.Micro, TextAnchor.UpperLeft);
            Widgets.Place(bankLbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180f, 22f), new Vector2(20f, -10f));
            _bankText = Widgets.Label(bar.transform, "BankVal", "$0", UiTokens.H3, UiTokens.Gold, TextAnchor.LowerLeft);
            Widgets.Place(_bankText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(300f, 40f), new Vector2(20f, 10f));

            // Mode chip (centre)
            var modeChipGo = new GameObject("ModeChip", typeof(RectTransform), typeof(UiImage));
            modeChipGo.GetComponent<RectTransform>().SetParent(bar.transform, false);
            Widgets.Place(modeChipGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(280f, 52f), new Vector2(0f, 0f));
            modeChipGo.GetComponent<UiImage>().sprite = UiSprites.Chamfer(10);
            modeChipGo.GetComponent<UiImage>().type = UiImage.Type.Sliced;
            modeChipGo.GetComponent<UiImage>().color = UiTokens.Alpha(UiTokens.Slate, 0.90f);
            _modeText = Widgets.Caption(modeChipGo.transform, "Mode", "FREE THROW", UiTokens.TextDim, UiTokens.Micro, TextAnchor.MiddleCenter);
            Widgets.Stretch(_modeText.rectTransform, Vector2.zero, Vector2.one);

            // Hamburger menu button (right)
            var menuBtn = new GameObject("MenuBtn", typeof(RectTransform)).GetComponent<RectTransform>();
            menuBtn.SetParent(bar.transform, false);
            Widgets.Place(menuBtn, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(72f, 72f), new Vector2(-12f, 0f));
            // 3 horizontal lines
            for (int i = 0; i < 3; i++)
            {
                var lineGo = new GameObject("L" + i, typeof(RectTransform), typeof(UiImage));
                lineGo.GetComponent<RectTransform>().SetParent(menuBtn, false);
                var lrt = lineGo.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0.15f, 0.5f); lrt.anchorMax = new Vector2(0.85f, 0.5f);
                lrt.pivot = new Vector2(0.5f, 0.5f); lrt.anchoredPosition = new Vector2(0f, (i - 1) * 14f); lrt.sizeDelta = new Vector2(0f, 3f);
                lineGo.GetComponent<UiImage>().sprite = UiSprites.Pixel;
                lineGo.GetComponent<UiImage>().color = UiTokens.Ice;
                lineGo.GetComponent<UiImage>().raycastTarget = false;
            }
            var menuImg = menuBtn.gameObject.AddComponent<UiImage>();
            menuImg.color = new Color(0f, 0f, 0f, 0f);
            var menuBtnComp = menuBtn.gameObject.AddComponent<Button>();
            menuBtnComp.targetGraphic = menuImg;
            menuBtnComp.onClick.AddListener(() => OnMenu?.Invoke());
            menuBtn.gameObject.AddComponent<ButtonFx>();
        }

        public void Present(string modeName, double balance, double stake)
        {
            if (_bankText != null) _bankText.text = "$" + balance.ToString("N0");
            if (_modeText != null) _modeText.text = Widgets.Track(modeName);
            Show();
        }

        public void SetBalance(double balance)
        {
            if (_bankText != null) _bankText.text = "$" + balance.ToString("N0");
        }
    }

    /// <summary>
    /// Result lower-third: verdict banner (IT''S GOOD / MISSED), payout chip.
    /// </summary>
    public sealed class ResultView : UiView
    {
        Text _banner, _sub;
        UiImage _bannerGlow, _slashImg;
        RectTransform _lowerThird;

        public ResultView(Transform parent)
        {
            Init(parent, "ResultView");
            _lowerThird = new GameObject("LowerThird", typeof(RectTransform)).GetComponent<RectTransform>();
            _lowerThird.SetParent(Root, false);
            Widgets.Place(_lowerThird, new Vector2(0.5f, 0.27f), new Vector2(0.5f, 0.5f), new Vector2(1080f, 340f), Vector2.zero);

            // Dark backing
            var darkBg = new GameObject("Dark", typeof(RectTransform), typeof(UiImage));
            darkBg.GetComponent<RectTransform>().SetParent(_lowerThird, false);
            Widgets.Stretch(darkBg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            darkBg.GetComponent<UiImage>().color = new Color(0f, 0f, 0f, 0.72f);
            darkBg.GetComponent<UiImage>().raycastTarget = false;

            // Diagonal slash
            var slashGo = new GameObject("Slash", typeof(RectTransform), typeof(UiImage));
            slashGo.GetComponent<RectTransform>().SetParent(_lowerThird, false);
            Widgets.Stretch(slashGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            _slashImg = slashGo.GetComponent<UiImage>();
            _slashImg.sprite = UiSprites.Slash();
            _slashImg.color = new Color(1f, 1f, 1f, 0.10f);
            _slashImg.raycastTarget = false;

            // Left accent sidebar
            var sidebar = new GameObject("Sidebar", typeof(RectTransform), typeof(UiImage));
            sidebar.GetComponent<RectTransform>().SetParent(_lowerThird, false);
            sidebar.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            sidebar.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
            sidebar.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
            sidebar.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            sidebar.GetComponent<RectTransform>().sizeDelta = new Vector2(10f, 0f);
            sidebar.GetComponent<UiImage>().color = UiTokens.Gold;
            sidebar.GetComponent<UiImage>().raycastTarget = false;

            // Glow behind banner
            var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(UiImage));
            glowGo.GetComponent<RectTransform>().SetParent(_lowerThird, false);
            Widgets.Stretch(glowGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            _bannerGlow = glowGo.GetComponent<UiImage>();
            _bannerGlow.sprite = UiSprites.Glow;
            _bannerGlow.color = UiTokens.Alpha(UiTokens.Win, 0f);
            _bannerGlow.raycastTarget = false;

            // Banner text
            _banner = Widgets.Display(_lowerThird, "Banner", "IT'S GOOD", UiTokens.DisplayM, UiTokens.Win, TextAnchor.MiddleCenter);
            Widgets.Place(_banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1000f, 120f), new Vector2(0f, 30f));

            // Sub text
            _sub = Widgets.Caption(_lowerThird, "Sub", "SWISH  ·  PERFECT", UiTokens.TextDim, UiTokens.Small, TextAnchor.MiddleCenter);
            Widgets.Place(_sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1000f, 32f), new Vector2(0f, -40f));
        }

        public void Present(RoundMathResult result, TimingZone timing, bool isReplay = false)
        {
            bool win = result != null && result.Won;
            Color verdictColor = win ? UiTokens.Win : UiTokens.Loss;

            string title = win
                ? (result.IsDoubleOrNothing ? "CLUTCH" : ShotCallout.For(result))
                : MissTitle(result);
            if (_banner != null) { _banner.text = title; _banner.color = verdictColor; }
            if (_bannerGlow != null) _bannerGlow.color = UiTokens.Alpha(verdictColor, 0.32f);
            if (_sidebar != null) _sidebar.color = verdictColor;

            string subStr = timing.ToString().ToUpperInvariant();
            if (result != null)
            {
                string animName = AnimLabel(result.Animation);
                subStr = animName + "  ·  " + subStr;
            }
            if (_sub != null) _sub.text = subStr;

            Show();
            if (_banner != null) UiTween.Punch(_banner.rectTransform, 0.50f, 1.15f, 0.6f);
            UiTween.FlashGraphic(_bannerGlow, 0.85f, 0.55f);
        }

        UiImage _sidebar => _lowerThird?.Find("Sidebar")?.GetComponent<UiImage>();

        static string MissTitle(RoundMathResult result)
        {
            if (result == null) return "MISSED";
            switch (result.Animation)
            {
                case ShotAnimationId.DefenderBlock: return "BLOCKED";
                case ShotAnimationId.AirBall: return "AIR BALL";
                default: return "MISSED";
            }
        }

        static string AnimLabel(ShotAnimationId a)
        {
            switch (a)
            {
                case ShotAnimationId.Swish: return "SWISH";
                case ShotAnimationId.HighArc: return "HIGH ARC";
                case ShotAnimationId.BackboardIn: return "OFF THE GLASS";
                case ShotAnimationId.RimIn: return "RATTLES IN";
                case ShotAnimationId.MultiRimIn: return "DOUBLE RATTLE";
                default: return a.ToString().ToUpperInvariant();
            }
        }
    }

    public static class ShotCallout
    {
        public static string For(RoundMathResult result)
        {
            if (result == null) return "SWISH";
            if (result.IsDoubleOrNothing) return "CLUTCH";
            switch (result.Animation)
            {
                case ShotAnimationId.Swish: return "SWISH";
                case ShotAnimationId.HighArc: return "HIGH ARC";
                case ShotAnimationId.BackboardIn: return "BANK SHOT";
                case ShotAnimationId.RimIn: return "RATTLE IN";
                case ShotAnimationId.MultiRimIn: return "DOWN THE DRAIN";
                default: return "IT'S GOOD";
            }
        }
    }

    /// <summary>Post-shot buttons: SHOOT AGAIN + CHANGE SETUP + double-or-nothing.</summary>
    public sealed class PostShotView : UiView
    {
        Button _againBtn, _setupBtn, _donBtn;
        Text _againLabel;

        public event Action OnShootAgain;
        public event Action OnChangeSetup;
        public event Action OnDoubleOrNothing;

        public PostShotView(Transform parent)
        {
            Init(parent, "PostShot");

            var stack = new GameObject("Stack", typeof(RectTransform)).GetComponent<RectTransform>();
            stack.SetParent(Root, false);
            Widgets.Place(stack, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(720f, 340f), new Vector2(0f, 80f));

            _againBtn = Widgets.Primary(stack, "Again", "SHOOT AGAIN", UiTokens.H2, () => OnShootAgain?.Invoke(), 16);
            Widgets.Place((RectTransform)_againBtn.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(720f, 100f), new Vector2(0f, -8f));
            _againLabel = _againBtn.transform.Find("Label")?.GetComponent<Text>();

            _donBtn = Widgets.Primary(stack, "Don", "DOUBLE OR NOTHING", UiTokens.H3, () => OnDoubleOrNothing?.Invoke(), 16);
            Widgets.Place((RectTransform)_donBtn.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(720f, 84f), new Vector2(0f, -124f));
            Widgets.SetButtonLabel(_donBtn, "DOUBLE OR NOTHING  ·  2x");
            _donBtn.gameObject.SetActive(false);

            _setupBtn = Widgets.Secondary(stack, "Setup", "CHANGE SETUP", UiTokens.Body, () => OnChangeSetup?.Invoke(), 14);
            Widgets.Place((RectTransform)_setupBtn.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(720f, 80f), new Vector2(0f, 0f));
        }

        public void Present(double stake, double payout, bool canAffordAgain, bool offerDon, BettingConfigData cfg)
        {
            if (_againBtn != null)
            {
                _againBtn.interactable = canAffordAgain;
                if (_againLabel != null)
                    _againLabel.text = canAffordAgain ? "SHOOT AGAIN  ·  $" + stake.ToString("N0") : "INSUFFICIENT BALANCE";
            }
            if (_donBtn != null)
            {
                _donBtn.gameObject.SetActive(offerDon);
                Widgets.SetButtonLabel(_donBtn, "DOUBLE OR NOTHING  ·  $" + payout.ToString("N0"));
            }
            
            var stack = Root.GetChild(0) as RectTransform;
            if (stack != null) 
            {
                stack.sizeDelta = new Vector2(720f, offerDon ? 340f : 204f);
            }
            
            Show();
            if (stack != null) UiTween.Stagger(stack, new Vector2(0f, -32f), 0.32f, 0.06f, 0.10f);
        }
    }

    /// <summary>Double-or-nothing countdown: payout display + ring timer + COLLECT/RISK.</summary>
    public sealed class LadderView : UiView
    {
        Text _amountText, _nextAmountText, _secondsText;
        UiImage _ringFill;
        Button _collectBtn, _riskBtn;

        public event Action OnCollect;
        public event Action OnRisk;

        public LadderView(Transform parent)
        {
            Init(parent, "Ladder");

            var panel = Widgets.Card(Root, "Panel",
                UiTokens.Alpha(UiTokens.Carbon, 0.97f), UiTokens.Gold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(960f, 500f), new Vector2(0f, 80f), 22, true, true);

            // "YOU WON" caption
            Widgets.Caption(panel.transform, "Won", "You Won", UiTokens.TextDim, UiTokens.H3, TextAnchor.UpperCenter);
            panel.transform.Find("Won").GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1f);
            panel.transform.Find("Won").GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
            panel.transform.Find("Won").GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            panel.transform.Find("Won").GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -32f);
            panel.transform.Find("Won").GetComponent<RectTransform>().sizeDelta = new Vector2(900f, 48f);

            // Amount
            _amountText = Widgets.Display(panel.transform, "Amount", "$0", UiTokens.DisplayM, UiTokens.Gold, TextAnchor.MiddleCenter);
            Widgets.Place(_amountText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 100f), new Vector2(0f, -90f));

            // Countdown ring
            var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(UiImage));
            ringGo.GetComponent<RectTransform>().SetParent(panel.transform, false);
            Widgets.Place(ringGo.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(120f, 120f), new Vector2(0f, -210f));
            _ringFill = ringGo.GetComponent<UiImage>();
            _ringFill.sprite = UiSprites.RadialRing(0.18f);
            _ringFill.color = UiTokens.Accent;
            _ringFill.type = UiImage.Type.Filled;
            _ringFill.fillMethod = UiImage.FillMethod.Radial360;
            _ringFill.fillOrigin = (int)UiImage.Origin360.Top;
            _ringFill.fillClockwise = false;
            _ringFill.fillAmount = 1f;
            _secondsText = Widgets.Label(ringGo.transform, "Sec", "10", UiTokens.H2, UiTokens.Ice, TextAnchor.MiddleCenter);
            Widgets.Stretch(_secondsText.rectTransform, Vector2.zero, Vector2.one);

            // Next amount label
            _nextAmountText = Widgets.Label(panel.transform, "Next", "Risk for $0", UiTokens.Small, UiTokens.TextDim, TextAnchor.MiddleCenter);
            Widgets.Place(_nextAmountText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 32f), new Vector2(0f, -350f));

            // Buttons row
            _collectBtn = Widgets.Secondary(panel.transform, "Collect", "COLLECT", UiTokens.Body, () => OnCollect?.Invoke(), 14);
            Widgets.Place((RectTransform)_collectBtn.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(440f, 84f), new Vector2(24f, 40f));
            _riskBtn = Widgets.Primary(panel.transform, "Risk", "RISK IT", UiTokens.Body, () => OnRisk?.Invoke(), 14);
            Widgets.Place((RectTransform)_riskBtn.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(440f, 84f), new Vector2(-24f, 40f));
        }

        public void Present(double amount, double nextAmount, double multiplier, int step, int maxSteps)
        {
            if (_amountText != null) _amountText.text = "$" + amount.ToString("N0");
            if (_nextAmountText != null)
                _nextAmountText.text = "Risk for $" + nextAmount.ToString("N0") + "  (" + multiplier.ToString("F1") + "x)  —  " + step + " / " + maxSteps;
            Widgets.SetButtonLabel(_riskBtn, "RISK IT  ·  " + multiplier.ToString("F1") + "x");
            if (_ringFill != null) _ringFill.fillAmount = 1f;
            Show();
        }

        public void SetCountdown(float remaining01, int secondsLeft)
        {
            if (_ringFill != null) _ringFill.fillAmount = Mathf.Clamp01(remaining01);
            if (_secondsText != null) _secondsText.text = secondsLeft.ToString();
        }
    }
}
