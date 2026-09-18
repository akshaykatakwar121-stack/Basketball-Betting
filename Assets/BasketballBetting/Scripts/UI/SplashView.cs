using System;
using System.Collections;
using BasketballBetting.Ui;
using UnityEngine;
using UnityEngine.UI;
using UiImage = UnityEngine.UI.Image;

namespace BasketballBetting
{
    /// <summary>
    /// Full-bleed splash / intro screen with broadcast chrome.
    /// Dark plate + corner brackets + HOOPS title + glow + TAP TO PLAY card.
    /// </summary>
    public sealed class SplashView : UiView
    {
        Graphic _tapGraphic;
        RectTransform _block;
        float _shownAt;
        bool _dismissRequested;
        bool _closed;
        Coroutine _clock;

        public bool IsDone { get; private set; }
        const float MinSeconds = 1.2f;
        const float MaxSeconds = 9f;
        float Elapsed => Time.unscaledTime - _shownAt;

        public SplashView(Transform parent)
        {
            Init(parent, "Splash");

            // Own canvas at sortingOrder=100 so splash is always on top
            var canvas = Root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            Root.gameObject.AddComponent<GraphicRaycaster>();

            // Background
            Mk(Root, "Plate", UiTokens.Ink);

            // Floor / ceiling scrims
            var floor = Mk(Root, "Floor", new Color(0.02f, 0.025f, 0.04f, 0.96f), UiSprites.GradientDown());
            Widgets.Bar((RectTransform)floor.transform, false, 900f);
            var ceil = Mk(Root, "Ceiling", new Color(0.02f, 0.025f, 0.04f, 0.82f), UiSprites.GradientUp());
            Widgets.Bar((RectTransform)ceil.transform, true, 500f);

            // Corner brackets
            Widgets.CornerBrackets(Root, "Brackets", 44f, 70f, 4f, new Color(1f, 1f, 1f, 0.35f));

            // Content block centred slightly above midpoint
            _block = new GameObject("Block", typeof(RectTransform)).GetComponent<RectTransform>();
            _block.SetParent(Root, false);
            Widgets.Place(_block, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1000f, 700f), new Vector2(0f, 60f));

            // Title glow
            var glowImg = Mk(_block, "Glow", UiTokens.Alpha(UiTokens.Accent, 0.20f), UiSprites.Glow);
            Widgets.Place((RectTransform)glowImg.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 460f), new Vector2(0f, -120f));
            glowImg.transform.SetAsFirstSibling();

            // HOOPS title
            var titleT = Widgets.Display(_block, "Title", "HOOPS", UiTokens.DisplayXL, UiTokens.Ice, TextAnchor.MiddleCenter);
            Widgets.Place(titleT.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000f, 160f), new Vector2(0f, -140f));

            // Gold rule
            var rule = Mk(_block, "Rule", UiTokens.Gold);
            Widgets.Place((RectTransform)rule.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(72f, 3f), new Vector2(0f, -308f));

            // Tagline
            var tagT = Widgets.Caption(_block, "Tag", "Broadcast Edition  ·  Free Throw & Three-Point",
                UiTokens.TextDim, UiTokens.Small, TextAnchor.MiddleCenter);
            Widgets.Place(tagT.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000f, 32f), new Vector2(0f, -330f));

            // TAP TO PLAY card
            var tapPlate = Widgets.Card(_block, "TapCard",
                UiTokens.Alpha(UiTokens.Carbon, 0.88f), UiTokens.Gold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(520f, 78f), new Vector2(0f, -420f), 14, false, false);
            var tapLbl = Widgets.Label(tapPlate.transform, "Tap", "TAP  TO  PLAY", UiTokens.Small + 2, UiTokens.Gold, TextAnchor.MiddleCenter);
            Widgets.Stretch(tapLbl.rectTransform, Vector2.zero, Vector2.one);
            _tapGraphic = tapLbl;

            // Legal
            var legal = Widgets.Label(Root, "Legal", "18+  ·  Play money only  ·  No real cash value",
                UiTokens.Micro, UiTokens.TextFaint, TextAnchor.MiddleCenter);
            Widgets.Place(legal.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1000f, 28f), new Vector2(0f, 80f));

            // Full-screen tap detector
            Widgets.InvisibleButton(Root, "TapArea", RequestDismiss);
        }

        static UiImage Mk(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UiImage));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            Widgets.Stretch(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            var img = go.GetComponent<UiImage>();
            img.sprite = sprite; img.color = color;
            img.type = sprite != null ? UiImage.Type.Simple : UiImage.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        public void Begin()
        {
            _shownAt = Time.unscaledTime;
            _dismissRequested = false; IsDone = false; _closed = false;
            Show(true);
            if (UiTween.Runner == null) return;
            if (_tapGraphic != null)
                UiTween.Pulse(_tapGraphic, UiTokens.Gold, 0.55f, 1f, 0.65f, () => IsVisible && !_closed);
            _clock = UiTween.Runner.StartCoroutine(Clock());
        }

        IEnumerator Clock()
        {
            while (!UnityEngine.Rendering.SplashScreen.isFinished && !_closed) yield return null;
            _shownAt = Time.unscaledTime;
            if (_block != null) UiTween.Stagger(_block, new Vector2(0f, -28f), 0.44f, 0.06f);
            while (!IsDone && !_closed)
            {
                bool tap = false;
                if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) tap = true;
                if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame) tap = true;
                if (UnityEngine.InputSystem.Keyboard.current != null && (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame || UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)) tap = true;
                if (tap) _dismissRequested = true;
                if ((_dismissRequested && Elapsed >= MinSeconds) || Elapsed >= MaxSeconds)
                    IsDone = true;
                yield return null;
            }
        }

        public void RequestDismiss()
        {
            _dismissRequested = true;
            if (Elapsed >= MinSeconds) IsDone = true;
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true; IsDone = true;
            if (_clock != null && UiTween.Runner != null) UiTween.Runner.StopCoroutine(_clock);
            Hide();
            if (UiTween.Runner != null) UiTween.Runner.StartCoroutine(DestroyAfter(0.3f));
            else if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
        }

        IEnumerator DestroyAfter(float t)
        {
            yield return new WaitForSecondsRealtime(t);
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
        }
    }
}
