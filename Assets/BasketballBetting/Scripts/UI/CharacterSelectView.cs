using System;
using System.Collections;
using BasketballBetting.Ui;
using UnityEngine;
using UnityEngine.UI;
using UiImage = UnityEngine.UI.Image;

namespace BasketballBetting
{
    /// <summary>
    /// Character selection screen: bottom dock with name, stats, dot pips, prev/next nav.
    /// </summary>
    public sealed class CharacterSelectView : UiView
    {
        Text _nameText, _nicknameText, _numText;
        RectTransform[] _statFills = new RectTransform[3];
        Text[] _statVals = new Text[3];
        UiImage[] _dots;
        Button _prevBtn, _nextBtn, _selectBtn;
        int _index;
        CharacterDefinition[] _all;
        RectTransform _dock;

        static readonly string[] StatLabels = { "SHOOT", "RANGE", "RELEASE" };

        public event Action<int> OnPreview;
        public event Action<int> OnConfirm;
        public event Action OnBack;
        public int SelectedIndex => _index;

        public CharacterSelectView(Transform parent)
        {
            Init(parent, "CharSelect");

            // Dark scrim
            var bg = new GameObject("Scrim", typeof(RectTransform), typeof(UiImage));
            bg.GetComponent<RectTransform>().SetParent(Root, false);
            Widgets.Stretch(bg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            bg.GetComponent<UiImage>().color = UiTokens.Scrim;
            bg.GetComponent<UiImage>().raycastTarget = true;

            // BACK button
            var backBtn = Widgets.Ghost(Root, "BackBtn", "< BACK", UiTokens.Small, () => OnBack?.Invoke(), 10);
            Widgets.Place((RectTransform)backBtn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(220f, 60f), new Vector2(32f, -52f));

            // "CHOOSE YOUR PLAYER" header
            var hdrT = Widgets.Caption(Root, "Hdr", "Choose Your Player", UiTokens.TextDim, UiTokens.Small, TextAnchor.UpperCenter);
            Widgets.Place(hdrT.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000f, 40f), new Vector2(0f, -52f));

            // PREV button
            _prevBtn = Widgets.Ghost(Root, "PrevBtn", "<", UiTokens.H2, () => Navigate(-1), 14);
            Widgets.Place((RectTransform)_prevBtn.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, 160f), new Vector2(24f, 0f));

            // NEXT button
            _nextBtn = Widgets.Ghost(Root, "NextBtn", ">", UiTokens.H2, () => Navigate(1), 14);
            Widgets.Place((RectTransform)_nextBtn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(100f, 160f), new Vector2(-24f, 0f));

            // Bottom dock — chamfered, gold accent
            var dockPlate = Widgets.Card(Root, "Dock",
                UiTokens.Alpha(UiTokens.Carbon, 0.96f), UiTokens.Gold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1080f, 520f), new Vector2(0f, 0f), 22, true, true);
            _dock = (RectTransform)dockPlate.transform.parent;

            // Jersey number chip
            _numText = Widgets.Display(dockPlate.transform, "Num", "#00", UiTokens.H2, UiTokens.Alpha(UiTokens.Gold, 0.40f), TextAnchor.UpperRight);
            Widgets.Place(_numText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(200f, 80f), new Vector2(-32f, -24f));

            // Nickname / role
            _nicknameText = Widgets.Caption(dockPlate.transform, "Nick", "SHOOTER", UiTokens.Gold, UiTokens.Small, TextAnchor.UpperLeft);
            Widgets.Place(_nicknameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(600f, 32f), new Vector2(40f, -28f));

            // Name
            _nameText = Widgets.Display(dockPlate.transform, "Name", "PLAYER NAME", UiTokens.DisplayM, UiTokens.Ice, TextAnchor.UpperLeft);
            Widgets.Place(_nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, 90f), new Vector2(40f, -60f));

            // Gold rule separator
            var ruleGo = new GameObject("Rule", typeof(RectTransform), typeof(UiImage));
            ruleGo.GetComponent<RectTransform>().SetParent(dockPlate.transform, false);
            Widgets.Place(ruleGo.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000f, 1f), new Vector2(0f, -170f));
            ruleGo.GetComponent<UiImage>().color = new Color(1f, 1f, 1f, 0.12f);

            // Three stat bars
            for (int i = 0; i < 3; i++)
            {
                float yOff = -192f - i * 72f;
                BuildStatBar(dockPlate.transform, i, StatLabels[i], yOff);
            }

            // Dot pips row
            var dotRow = new GameObject("DotRow", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            dotRow.SetParent(dockPlate.transform, false);
            Widgets.Place(dotRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(400f, 28f), new Vector2(0f, 130f));
            var hlg = dotRow.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            // Dots will be built in Present() since we need count
            _dots = null; // filled in Present

            // SELECT button
            _selectBtn = Widgets.Primary(dockPlate.transform, "SelectBtn", "SELECT PLAYER", UiTokens.H3, () => OnConfirm?.Invoke(_index), 16);
            Widgets.Place((RectTransform)_selectBtn.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(640f, 84f), new Vector2(0f, 40f));
        }

        void BuildStatBar(Transform parent, int i, string statName, float yOff)
        {
            var row = new GameObject("StatRow" + i, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            Widgets.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000f, 52f), new Vector2(0f, yOff));
            var lbl = Widgets.Caption(row, "Lbl", statName, UiTokens.TextDim, UiTokens.Small, TextAnchor.MiddleLeft);
            Widgets.Place(lbl.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(130f, 36f), new Vector2(0f, 0f));
            // Bar background
            var bgGo = new GameObject("BarBg", typeof(RectTransform), typeof(UiImage));
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.SetParent(row, false);
            Widgets.Stretch(bgRt, 140f, 18f, 80f, 18f);
            bgGo.GetComponent<UiImage>().sprite = UiSprites.Chamfer(4);
            bgGo.GetComponent<UiImage>().type = UiImage.Type.Sliced;
            bgGo.GetComponent<UiImage>().color = new Color(0f, 0f, 0f, 0.50f);
            // Bar fill
            var fillGo = new GameObject("BarFill", typeof(RectTransform), typeof(UiImage));
            fillGo.GetComponent<RectTransform>().SetParent(bgGo.GetComponent<RectTransform>(), false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f); fillRt.anchoredPosition = Vector2.zero; fillRt.sizeDelta = new Vector2(0f, 0f);
            fillGo.GetComponent<UiImage>().sprite = UiSprites.Chamfer(4);
            fillGo.GetComponent<UiImage>().type = UiImage.Type.Sliced;
            fillGo.GetComponent<UiImage>().color = UiTokens.Win;
            _statFills[i] = fillRt;
            // Value label
            _statVals[i] = Widgets.Label(row, "Val", "0", UiTokens.Small, UiTokens.Ice, TextAnchor.MiddleRight);
            Widgets.Place(_statVals[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(60f, 36f), new Vector2(0f, 0f));
        }

        void RefreshDots(int count, Transform dotRowParent)
        {
            // Destroy old
            if (_dots != null)
                foreach (var d in _dots) if (d != null) UnityEngine.Object.Destroy(d.gameObject);
            _dots = new UiImage[count];
            var dotRow = dotRowParent.Find("DotRow");
            if (dotRow == null) return;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Dot" + i, typeof(RectTransform), typeof(UiImage));
                go.GetComponent<RectTransform>().SetParent(dotRow, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(14f, 14f);
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = 14f; le.preferredHeight = 14f;
                go.GetComponent<UiImage>().sprite = UiSprites.Chamfer(4);
                go.GetComponent<UiImage>().type = UiImage.Type.Sliced;
                go.GetComponent<UiImage>().color = new Color(1f, 1f, 1f, 0.18f);
                _dots[i] = go.GetComponent<UiImage>();
            }
        }

        void RefreshDotActive(int active)
        {
            if (_dots == null) return;
            for (int i = 0; i < _dots.Length; i++)
                if (_dots[i] != null) _dots[i].color = i == active ? UiTokens.Gold : new Color(1f, 1f, 1f, 0.18f);
        }

        public void Present(int index, CharacterDefinition[] all)
        {
            _all = all;
            if (all == null || all.Length == 0) return;
            index = Mathf.Clamp(index, 0, all.Length - 1);
            if (_dots == null || _dots.Length != all.Length)
            {
                // find dotrow parent — it's the dock plate
                var plateT = _dock.Find("Plate");
                if (plateT != null) RefreshDots(all.Length, _dock);
            }
            SelectCharacter(index);
            Show();
            if (_dock != null) UiTween.Stagger(_dock, new Vector2(0f, -40f), 0.34f, 0.05f);
        }

        void SelectCharacter(int i)
        {
            _index = i;
            var def = _all[i];
            if (_nameText != null) _nameText.text = def.DisplayName?.ToUpper() ?? "PLAYER";
            if (_nicknameText != null) _nicknameText.text = Widgets.Track(def.Nickname ?? "SHOOTER");
            if (_numText != null) _numText.text = "#" + def.JerseyNumber.ToString("00");

            // Stats: shooting, threePoint, releaseSpeed (normalised /100)
            float[] vals = {
                def.Attributes.shooting,
                def.Attributes.threePoint,
                def.Attributes.releaseSpeed
            };
            for (int s = 0; s < 3; s++)
            {
                float n = Mathf.Clamp01(vals[s] / 100f);
                if (_statFills[s] != null)
                {
                    _statFills[s].anchorMin = new Vector2(0f, 0f);
                    _statFills[s].anchorMax = new Vector2(n, 1f);
                    _statFills[s].sizeDelta = Vector2.zero;
                }
                if (_statVals[s] != null) _statVals[s].text = Mathf.RoundToInt(vals[s]).ToString();
            }
            RefreshDotActive(i);
            OnPreview?.Invoke(i);
        }

        void Navigate(int dir)
        {
            if (_all == null || _all.Length == 0) return;
            int next = (_index + dir + _all.Length) % _all.Length;
            SelectCharacter(next);
            UiTween.Stagger(_dock, new Vector2(dir * -60f, 0f), 0.28f, 0.04f);
        }
    }
}
