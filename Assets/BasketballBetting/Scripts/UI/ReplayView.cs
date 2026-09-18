using System;
using BasketballBetting.Presentation;
using BasketballBetting.Ui;
using UnityEngine;
using UnityEngine.UI;
using UiImage = UnityEngine.UI.Image;

namespace BasketballBetting
{
    /// <summary>
    /// Broadcast replay chrome. Shows on top of the game during a replay:
    ///   * Corner viewfinder brackets (white, 45% opacity)
    ///   * REPLAY bug — dark pill, pulsing red dot, gold "REPLAY" text (top-left)
    ///   * Camera caption — slides in from left on every cut (bottom-left)
    ///   * Cut wipe — brief white flash across the screen on each cut
    ///   * Skip button — ghost pill button with a chevron icon (bottom-right)
    ///
    /// Call Show()/Hide() to fade it in/out. Wire OnSkip to ReplayDirector.Skip().
    /// </summary>
    public sealed class ReplayView : UiView
    {
        public event Action OnSkip;

        readonly Text _camLabel;
        readonly RectTransform _camRoot;
        readonly CanvasGroup _camGroup;
        readonly UiImage _wipe;
        readonly UiImage _dot;

        public ReplayView(Transform parent)
        {
            Init(parent, "ReplayView");

            // ── full-screen white wipe (transparent at rest, flashes on cut) ──
            {
                var go = new GameObject("Wipe", typeof(RectTransform), typeof(UiImage));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(Root, false);
                Widgets.Stretch(rt, Vector2.zero, Vector2.one);
                _wipe = go.GetComponent<UiImage>();
                _wipe.color = new Color(1f, 1f, 1f, 0f);
                _wipe.raycastTarget = false;
                _wipe.sprite = UiSprites.Round12;
                _wipe.type = UiImage.Type.Sliced;
            }

            // ── corner brackets ──
            Widgets.CornerBrackets(Root, "Brackets", 36f, 56f, 4f, UiTokens.Alpha(Color.white, 0.45f));

            // ── REPLAY bug (top-left) ──
            {
                var bugGo = new GameObject("Bug", typeof(RectTransform), typeof(UiImage));
                var bugRt = bugGo.GetComponent<RectTransform>();
                bugRt.SetParent(Root, false);
                bugRt.anchorMin = bugRt.anchorMax = new Vector2(0f, 1f);
                bugRt.pivot = new Vector2(0f, 1f);
                bugRt.anchoredPosition = new Vector2(28f, -32f);
                bugRt.sizeDelta = new Vector2(220f, 52f);
                var bugImg = bugGo.GetComponent<UiImage>();
                bugImg.sprite = UiSprites.Round12;
                bugImg.type = UiImage.Type.Sliced;
                bugImg.color = UiTokens.Alpha(UiTokens.Ink, 0.90f);
                bugImg.raycastTarget = false;

                // top accent strip (red)
                var accent = new GameObject("Accent", typeof(RectTransform), typeof(UiImage));
                var acRt = accent.GetComponent<RectTransform>();
                acRt.SetParent(bugGo.transform, false);
                acRt.anchorMin = new Vector2(0f, 1f);
                acRt.anchorMax = new Vector2(1f, 1f);
                acRt.pivot = new Vector2(0.5f, 1f);
                acRt.anchoredPosition = Vector2.zero;
                acRt.sizeDelta = new Vector2(0f, 3f);
                accent.GetComponent<UiImage>().color = UiTokens.Loss;
                accent.GetComponent<UiImage>().raycastTarget = false;

                // pulsing red dot
                var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(UiImage));
                var dotRt = dotGo.GetComponent<RectTransform>();
                dotRt.SetParent(bugGo.transform, false);
                dotRt.anchorMin = dotRt.anchorMax = new Vector2(0f, 0.5f);
                dotRt.pivot = new Vector2(0.5f, 0.5f);
                dotRt.anchoredPosition = new Vector2(24f, 0f);
                dotRt.sizeDelta = new Vector2(13f, 13f);
                _dot = dotGo.GetComponent<UiImage>();
                _dot.sprite = UiSprites.Dot;
                _dot.color = UiTokens.Loss;
                _dot.raycastTarget = false;
                _dot.preserveAspect = true;

                // REPLAY label
                Widgets.Label(bugGo.transform, "Label", "REPLAY", UiTokens.Small, UiTokens.Gold,
                    TextAnchor.MiddleLeft, new Vector2(0.22f, 0.08f), new Vector2(0.97f, 0.92f), FontStyle.Bold);
            }

            // ── camera caption (bottom-left, hidden until first cut) ──
            {
                var camGo = new GameObject("CamCaption", typeof(RectTransform), typeof(UiImage));
                var camRt = camGo.GetComponent<RectTransform>();
                camRt.SetParent(Root, false);
                camRt.anchorMin = camRt.anchorMax = new Vector2(0f, 0f);
                camRt.pivot = new Vector2(0f, 0f);
                camRt.anchoredPosition = new Vector2(28f, 40f);
                camRt.sizeDelta = new Vector2(380f, 48f);
                var camImg = camGo.GetComponent<UiImage>();
                camImg.sprite = UiSprites.Round12;
                camImg.type = UiImage.Type.Sliced;
                camImg.color = UiTokens.Alpha(UiTokens.Ink, 0.85f);
                camImg.raycastTarget = false;

                // left accent strip (accent orange)
                var acGo = new GameObject("Accent", typeof(RectTransform), typeof(UiImage));
                var acRt = acGo.GetComponent<RectTransform>();
                acRt.SetParent(camGo.transform, false);
                acRt.anchorMin = new Vector2(0f, 0f);
                acRt.anchorMax = new Vector2(0f, 1f);
                acRt.pivot = new Vector2(0f, 0.5f);
                acRt.anchoredPosition = Vector2.zero;
                acRt.sizeDelta = new Vector2(3f, 0f);
                acGo.GetComponent<UiImage>().color = UiTokens.Accent;
                acGo.GetComponent<UiImage>().raycastTarget = false;

                _camLabel = Widgets.Label(camGo.transform, "Label", "", UiTokens.Micro, UiTokens.Muted,
                    TextAnchor.MiddleLeft, new Vector2(0.04f, 0f), new Vector2(0.97f, 1f), FontStyle.Normal);

                _camRoot = camRt;
                _camGroup = camGo.AddComponent<CanvasGroup>();
                _camGroup.alpha = 0f;
            }

            // ── skip button (bottom-right ghost pill with chevron) ──
            {
                var skipGo = new GameObject("Skip", typeof(RectTransform), typeof(UiImage));
                var skipRt = skipGo.GetComponent<RectTransform>();
                skipRt.SetParent(Root, false);
                skipRt.anchorMin = skipRt.anchorMax = new Vector2(1f, 0f);
                skipRt.pivot = new Vector2(1f, 0f);
                skipRt.anchoredPosition = new Vector2(-28f, 40f);
                skipRt.sizeDelta = new Vector2(196f, 60f);
                var skipBg = skipGo.GetComponent<UiImage>();
                skipBg.sprite = UiSprites.Round12;
                skipBg.type = UiImage.Type.Sliced;
                skipBg.color = UiTokens.Alpha(UiTokens.Slate, 0.92f);

                // outline edge
                var edgeGo = new GameObject("Edge", typeof(RectTransform), typeof(UiImage));
                var edgeRt = edgeGo.GetComponent<RectTransform>();
                edgeRt.SetParent(skipGo.transform, false);
                Widgets.Stretch(edgeRt, Vector2.zero, Vector2.one);
                var edgeImg = edgeGo.GetComponent<UiImage>();
                edgeImg.sprite = UiSprites.Outline20;
                edgeImg.type = UiImage.Type.Sliced;
                edgeImg.color = UiTokens.LineStrong;
                edgeImg.raycastTarget = false;

                // chevron icon (right side)
                var chevGo = new GameObject("Chev", typeof(RectTransform), typeof(UiImage));
                var chevRt = chevGo.GetComponent<RectTransform>();
                chevRt.SetParent(skipGo.transform, false);
                chevRt.anchorMin = chevRt.anchorMax = new Vector2(1f, 0.5f);
                chevRt.pivot = new Vector2(1f, 0.5f);
                chevRt.anchoredPosition = new Vector2(-14f, 0f);
                chevRt.sizeDelta = new Vector2(22f, 22f);
                var chevImg = chevGo.GetComponent<UiImage>();
                chevImg.sprite = UiSprites.Chevron;
                chevImg.type = UiImage.Type.Simple;
                chevImg.color = UiTokens.Muted;
                chevImg.raycastTarget = false;

                // "SKIP" label
                Widgets.Label(skipGo.transform, "Label", "SKIP", UiTokens.Small, UiTokens.Ice,
                    TextAnchor.MiddleCenter, new Vector2(0.06f, 0f), new Vector2(0.76f, 1f));

                // button component
                var btn = skipGo.AddComponent<Button>();
                btn.targetGraphic = skipBg;
                var sc = btn.colors;
                sc.normalColor = Color.white;
                sc.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
                sc.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
                sc.fadeDuration = 0.06f;
                btn.colors = sc;
                btn.onClick.AddListener(() => OnSkip?.Invoke());
                skipGo.AddComponent<ButtonFx>();
            }
        }

        /// <summary>
        /// Called whenever ReplayDirector starts a new cut/angle.
        /// Pass the shot name, cut index, and total cuts.
        /// </summary>
        public void NotifyCut(string shotName, int index, int total)
        {
            if (_camLabel != null)
                _camLabel.text = "CAM " + (index + 1) + "/" + Mathf.Max(1, total) + "  \u00b7  " + shotName.ToUpperInvariant();

            // white wipe flash
            if (_wipe != null)
                UiTween.FlashGraphic(_wipe, 0.40f, 0.25f);

            // camera caption slides in from left
            UiTween.Reveal(_camRoot, _camGroup, new Vector2(-44f, 0f), 0.24f);
        }

        public override void SetVisible(bool visible, bool instant = false)
        {
            base.SetVisible(visible, instant);
            if (visible)
            {
                // pulse red dot while replay is on
                UiTween.Pulse(_dot, UiTokens.Loss, 0.3f, 1f, 1.1f, () => IsVisible);
            }
            else
            {
                // hide cam caption immediately
                if (_camGroup != null) _camGroup.alpha = 0f;
            }
        }
    }
}
