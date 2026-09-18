using System;
using System.Collections;
using BasketballBetting.Ui;
using UnityEngine;
using UnityEngine.UI;
using UiImage = UnityEngine.UI.Image;

namespace BasketballBetting
{
    /// <summary>
    /// Main menu screen: broadcast dark frame, LIVE badge, bank display,
    /// HOOPS title block, PLAY button, mode tiles, HOW TO PLAY info.
    /// </summary>
    public sealed class MainMenuView : UiView
    {
        Text _balanceText;
        Button _playBtn;
        RectTransform _titleBlock, _tilesBlock;
        GameObject _howPanel;
        bool _howOpen;

        public event Action OnPlay;
        public event Action OnSettings;
        public event Action<GameModeType> OnQuickMode;

        public MainMenuView(Transform parent)
        {
            Init(parent, "MainMenu");

            // Background scrim
            var bg = Mk(Root, "Scrim", UiTokens.Scrim);
            bg.raycastTarget = true;

            // Floor / ceiling gradients
            var floor = Mk(Root, "Floor", new Color(0f, 0f, 0f, 0.80f), UiSprites.GradientDown());
            Widgets.Bar((RectTransform)floor.transform, false, 1100f);
            var ceil = Mk(Root, "Ceiling", new Color(0f, 0f, 0f, 0.65f), UiSprites.GradientUp());
            Widgets.Bar((RectTransform)ceil.transform, true, 600f);

            // Corner brackets
            Widgets.CornerBrackets(Root, "Brackets", 44f, 64f, 3f, new Color(1f, 1f, 1f, 0.30f));

            // Bank card — top left
            var bankPlate = Widgets.Card(Root, "Bank",
                UiTokens.Alpha(UiTokens.Carbon, 0.88f), UiTokens.Gold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, 108f), new Vector2(40f, -44f), 12, false, false);
            Widgets.Caption(bankPlate.transform, "BankLbl", "BANK", UiTokens.TextDim, UiTokens.Micro, TextAnchor.UpperLeft);
            _balanceText = Widgets.Label(bankPlate.transform, "Balance", "$0", UiTokens.H2, UiTokens.Gold, TextAnchor.LowerLeft);
            Widgets.Stretch(_balanceText.rectTransform, 12f, 36f, 12f, 16f);

            // LIVE badge — top right
            var livePlate = Widgets.Card(Root, "Live",
                UiTokens.Alpha(UiTokens.Carbon, 0.82f), UiTokens.Loss,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(360f, 76f), new Vector2(-40f, -60f), 12, false, false);
            // pulsing red dot
            var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(UiImage));
            dotGo.GetComponent<RectTransform>().SetParent(livePlate.transform, false);
            Widgets.Place(dotGo.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(16f, 16f), new Vector2(20f, 0f));
            dotGo.GetComponent<UiImage>().sprite = UiSprites.Dot;
            dotGo.GetComponent<UiImage>().color = UiTokens.Loss;
            Widgets.Caption(livePlate.transform, "LiveTxt", "Live  ·  Court Night", UiTokens.TextDim, UiTokens.Micro, TextAnchor.MiddleLeft);
            var liveLblRt = livePlate.transform.Find("LiveTxt").GetComponent<RectTransform>();
            Widgets.Stretch(liveLblRt, 44f, 8f, 8f, 8f);
            UiTween.Pulse(dotGo.GetComponent<UiImage>(), UiTokens.Loss, 0.4f, 1f, 0.8f, () => IsVisible);

            // Title block
            _titleBlock = new GameObject("TitleBlock", typeof(RectTransform)).GetComponent<RectTransform>();
            _titleBlock.SetParent(Root, false);
            Widgets.Place(_titleBlock, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.5f), new Vector2(1000f, 580f), new Vector2(0f, 0f));

            // Title glow
            var glowImg = Mk(_titleBlock, "TitleGlow", UiTokens.Alpha(UiTokens.Accent, 0.18f), UiSprites.Glow);
            Widgets.Place((RectTransform)glowImg.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(960f, 500f), new Vector2(0f, -60f));
            glowImg.transform.SetAsFirstSibling();

            // Kicker
            var kickerT = Widgets.Caption(_titleBlock, "Kicker", "Broadcast Edition  ·  Free Throw & Three-Point",
                UiTokens.TextDim, UiTokens.Small, TextAnchor.MiddleCenter);
            Widgets.Place(kickerT.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000f, 28f), new Vector2(0f, -48f));

            // Title
            var titleT = Widgets.Display(_titleBlock, "Title", "HOOPS", UiTokens.TitleL + 56, UiTokens.Ice, TextAnchor.MiddleCenter);
            Widgets.Place(titleT.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000f, 140f), new Vector2(0f, -90f));

            // Gold rule
            var ruleImg = Mk(_titleBlock, "Rule", UiTokens.Gold);
            Widgets.Place((RectTransform)ruleImg.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(60f, 3f), new Vector2(0f, -242f));

            // Tagline
            var tagT = Widgets.Label(_titleBlock, "Tagline", "One shot. Everything on the line.",
                UiTokens.Body, UiTokens.TextDim, TextAnchor.MiddleCenter);
            Widgets.Place(tagT.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000f, 32f), new Vector2(0f, -262f));

            // Tiles block (buttons)
            _tilesBlock = new GameObject("TilesBlock", typeof(RectTransform)).GetComponent<RectTransform>();
            _tilesBlock.SetParent(Root, false);
            Widgets.Place(_tilesBlock, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1000f, 620f), new Vector2(0f, 80f));

            // PLAY primary
            _playBtn = Widgets.Primary(_tilesBlock, "PlayBtn", "PLAY", UiTokens.H2, () => OnPlay?.Invoke(), 18);
            Widgets.Place((RectTransform)_playBtn.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(800f, 110f), new Vector2(0f, -8f));

            // Mode tiles row
            var tileRow = Widgets.Row(_tilesBlock, "Tiles", 16f);
            Widgets.Place((RectTransform)tileRow.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(960f, 130f), new Vector2(0f, -134f));
            BuildModeTile(tileRow.transform, "Free Throw", "15 ft  ·  No defender", GameModeType.FreeThrow);
            BuildModeTile(tileRow.transform, "Three-Point", "23 ft 9 in  ·  Contested", GameModeType.ThreePoint);

            // Separator
            var sep = Mk(_tilesBlock, "Sep", new Color(1f, 1f, 1f, 0.10f));
            Widgets.Place((RectTransform)sep.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(960f, 1f), new Vector2(0f, -282f));

            // How to play + settings row
            var botRow = new GameObject("BotRow", typeof(RectTransform)).GetComponent<RectTransform>();
            botRow.SetParent(_tilesBlock, false);
            Widgets.Place(botRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(960f, 72f), new Vector2(0f, -300f));
            var howBtn = Widgets.Ghost(botRow, "HowBtn", "HOW TO PLAY", UiTokens.Small, ToggleHow, 10);
            Widgets.Place((RectTransform)howBtn.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(360f, 64f), new Vector2(0f, 0f));
            var setBtn = Widgets.Ghost(botRow, "SettBtn", "SETTINGS", UiTokens.Small, () => OnSettings?.Invoke(), 10);
            Widgets.Place((RectTransform)setBtn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(260f, 64f), new Vector2(0f, 0f));

            // HOW TO PLAY panel (hidden by default)
            _howPanel = new GameObject("HowPanel", typeof(RectTransform), typeof(UiImage));
            var howPanelRt = _howPanel.GetComponent<RectTransform>();
            howPanelRt.SetParent(Root, false);
            Widgets.Place(howPanelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(960f, 900f), new Vector2(0f, 0f));
            _howPanel.GetComponent<UiImage>().color = new Color(0f, 0f, 0f, 0.82f);
            _howPanel.GetComponent<UiImage>().raycastTarget = true;
            var howCard = Widgets.Layered(howPanelRt, "HowCard", UiTokens.Alpha(UiTokens.Slate, 0.97f), UiTokens.Gold, 18, true, true);
            Widgets.Stretch(howCard.transform.parent.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            Widgets.Caption(howCard.transform, "HowTitle", "HOW TO PLAY", UiTokens.Gold, UiTokens.H3, TextAnchor.UpperCenter);
            howCard.transform.Find("HowTitle").GetComponent<RectTransform>().anchorMin = new Vector2(0.5f,1f);
            howCard.transform.Find("HowTitle").GetComponent<RectTransform>().anchorMax = new Vector2(0.5f,1f);
            howCard.transform.Find("HowTitle").GetComponent<RectTransform>().pivot = new Vector2(0.5f,1f);
            howCard.transform.Find("HowTitle").GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -40f);
            howCard.transform.Find("HowTitle").GetComponent<RectTransform>().sizeDelta = new Vector2(900f, 48f);
            AddHowRow(howCard.transform, -120f, UiTokens.Win, "PERFECT", "Green zone: maximum chance. Meter moves 2x faster here.");
            AddHowRow(howCard.transform, -240f, UiTokens.Gold, "GOOD", "Yellow zone: solid chance of scoring. Focus your timing here.");
            AddHowRow(howCard.transform, -360f, UiTokens.Loss, "LATE / EARLY", "Red zone: very low chance - almost always a miss.");
            AddHowRow(howCard.transform, -480f, UiTokens.Muted, "BET", "Place a stake, choose a mode, then tap LOCK IN to shoot.");
            var closeBtn = Widgets.Primary(howCard.transform, "CloseHow", "GOT IT", UiTokens.Body, ToggleHow, 14);
            Widgets.Place((RectTransform)closeBtn.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(440f, 80f), new Vector2(0f, 48f));
            _howPanel.SetActive(false);
        }

        void BuildModeTile(Transform parent, string label, string sub, GameModeType mode)
        {
            var plate = Widgets.Card(parent, label + "Tile",
                UiTokens.Alpha(UiTokens.Slate, 0.88f), UiTokens.Accent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(456f, 120f), Vector2.zero, 14, true, true);
            Widgets.Size(plate.transform.parent.GetComponent<RectTransform>(), 456f, 120f);
            var lbl = Widgets.Display(plate.transform, "Label", label.ToUpper(), UiTokens.H3, UiTokens.Ice, TextAnchor.UpperCenter);
            Widgets.Place(lbl.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(440f, 48f), new Vector2(0f, -18f));
            var sublbl = Widgets.Caption(plate.transform, "Sub", sub, UiTokens.TextDim, UiTokens.Small, TextAnchor.LowerCenter);
            Widgets.Place(sublbl.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(440f, 32f), new Vector2(0f, 20f));
            Widgets.InvisibleButton(plate.transform, "Tap", () => OnQuickMode?.Invoke(mode));
        }

        static void AddHowRow(Transform parent, float y, Color color, string label, string desc)
        {
            var row = new GameObject("R" + label, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = new Vector2(0.5f, 1f); row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f); row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(860f, 90f);
            var dot = new GameObject("Dot", typeof(RectTransform), typeof(UiImage));
            dot.GetComponent<RectTransform>().SetParent(row, false);
            Widgets.Place(dot.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(14f, 14f), new Vector2(0f, 0f));
            dot.GetComponent<UiImage>().sprite = UiSprites.Dot;
            dot.GetComponent<UiImage>().color = color;
            var lbl = Widgets.Caption(row, "Lbl", label, color, UiTokens.H3, TextAnchor.UpperLeft);
            lbl.rectTransform.anchorMin = new Vector2(0f, 1f); lbl.rectTransform.anchorMax = new Vector2(1f, 1f);
            lbl.rectTransform.pivot = new Vector2(0.5f, 1f); lbl.rectTransform.anchoredPosition = new Vector2(28f, 0f); lbl.rectTransform.sizeDelta = new Vector2(-28f, 36f);
            var descT = Widgets.Label(row, "Desc", desc, UiTokens.Small, UiTokens.TextDim, TextAnchor.LowerLeft);
            descT.rectTransform.anchorMin = Vector2.zero; descT.rectTransform.anchorMax = Vector2.one;
            descT.rectTransform.offsetMin = new Vector2(28f, 0f); descT.rectTransform.offsetMax = new Vector2(0f, -36f);
        }

        static UiImage Mk(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UiImage));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            Widgets.Stretch(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            var img = go.GetComponent<UiImage>();
            img.sprite = sprite; img.color = color; img.type = UiImage.Type.Simple; img.raycastTarget = false;
            return img;
        }

        void ToggleHow()
        {
            if (_howPanel == null) return;
            _howOpen = !_howOpen;
            _howPanel.SetActive(_howOpen);
        }

        public void Present(double balance)
        {
            SetBalance(balance);
            Show();
        }

        public void SetBalance(double balance)
        {
            if (_balanceText != null) _balanceText.text = "$" + balance.ToString("N0");
        }

        public void PlayIntro()
        {
            if (_titleBlock != null) UiTween.Stagger(_titleBlock, new Vector2(0f, -28f), 0.44f, 0.06f);
            if (_tilesBlock != null) UiTween.Stagger(_tilesBlock, new Vector2(0f, -36f), 0.38f, 0.07f, 0.20f);
        }
    }
}
