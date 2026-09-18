using System;
using System.Collections;
using BasketballBetting.Ui;
using UnityEngine;
using UnityEngine.UI;
using UiImage = UnityEngine.UI.Image;

namespace BasketballBetting
{
    public enum UiScreen { Main, Roster, Setup, Hud, Result, Pause, Settings }

    public sealed class GameUI : MonoBehaviour
    {
        public UiScreen CurrentScreen { get; private set; } = UiScreen.Main;
        public GameModeType SelectedMode { get; private set; } = GameModeType.FreeThrow;
        public int SelectedCharacter { get; private set; }
        public double Stake { get; private set; } = 10;

        public event Action OnPlaceBet;
        public event Action OnShootAgain;
        public event Action OnChangeSetup;
        public event Action OnDoubleOrNothing;
        public event Action OnUiClick;
        public event Action OnPauseResume;
        public event Action OnPauseQuit;
        public event Action OnCharacterChanged;
        public event Action OnSkipReplay;

        BettingConfigData _cfg;
        double _balanceShown = double.NaN;
        
        SplashView _splash;
        MainMenuView _main;
        CharacterSelectView _roster;
        BetPanelView _lobby;
        HudView _hud;
        ResultView _result;
        PostShotView _postShot;
        
        RectTransform _safeArea;
        CharacterSelectStudio _studio;
        
        GameObject _pause, _settings;
        CanvasGroup _pauseGroup, _settingsGroup;
        
        GameObject _calloutGo;
        RectTransform _calloutRt;
        Text _calloutText;
        CanvasGroup _calloutGroup;
        Coroutine _calloutRoutine;
        
        GameObject _meterRoot;
        RectTransform _meterNeedle;
        UiImage _meterGlow;
        Text _meterZone;
        
        UiScreen _screenBeforeSettings;
        Text _qualityLabel;

        public static GameUI Create(Transform parent, BettingConfigData cfg)
        {
            var go = new GameObject("GameUI");
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<GameUI>();
            ui._cfg = cfg ?? BettingConfigLoader.CreateDefaults();
            ui.Build();
            return ui;
        }

        public CharacterDefinition CurrentCharacter()
        {
            var all = CharacterCatalog.All;
            if (all.Length == 0) return null;
            return all[Mathf.Clamp(SelectedCharacter, 0, all.Length - 1)];
        }

        public void CycleCharacter(int dir)
        {
            var all = CharacterCatalog.All;
            if (all.Length == 0) return;
            SelectedCharacter = (SelectedCharacter + dir + all.Length) % all.Length;
            Click();
            if (CurrentScreen == UiScreen.Roster) _roster?.Present(SelectedCharacter, all);
            OnCharacterChanged?.Invoke();
        }

        public void RefreshBalance(double balance)
        {
            _balanceShown = balance;
            _main?.SetBalance(balance);
            _hud?.SetBalance(balance);
            if (CurrentScreen == UiScreen.Setup)
                _lobby?.Present(_cfg, balance, SelectedMode == GameModeType.FreeThrow ? 0 : 1, Stake);
        }

        void Show(UiScreen next)
        {
            CurrentScreen = next;
            if (_main != null) _main.SetVisible(next == UiScreen.Main);
            if (_roster != null) _roster.SetVisible(next == UiScreen.Roster);
            if (_lobby != null) _lobby.SetVisible(next == UiScreen.Setup);
            if (_hud != null) _hud.SetVisible(next == UiScreen.Hud);
            if (_result != null) _result.SetVisible(next == UiScreen.Result);
            if (_postShot != null) _postShot.SetVisible(next == UiScreen.Result);
            if (_pause != null) _pause.SetActive(next == UiScreen.Pause);
            if (_settings != null) _settings.SetActive(next == UiScreen.Settings);
        }

        public void ShowMainMenu()
        {
            SetBroadcast(false);
            Show(UiScreen.Main);
            
            if (_splash != null && !_splash.IsDone)
            {
                _main.SetVisible(false);
                StartCoroutine(SplashFlow());
            }
            else
            {
                _main.Present(double.IsNaN(_balanceShown) ? 1000 : _balanceShown);
                _main.PlayIntro();
            }
        }

        IEnumerator SplashFlow()
        {
            _splash.Begin();
            while (!_splash.IsDone) yield return null;
            _splash.Close();
            _splash = null;
            Show(UiScreen.Main);
            _main.Present(double.IsNaN(_balanceShown) ? 1000 : _balanceShown);
            _main.PlayIntro();
        }

        public void ShowCharacterSelect()
        {
            Show(UiScreen.Roster);
            _roster.Present(SelectedCharacter, CharacterCatalog.All);
            if (_studio != null) { _studio.SetVisible(true); _studio.Focus(SelectedCharacter); }
        }

        public void ShowLobby()
        {
            Show(UiScreen.Setup);
            _lobby.Present(_cfg, double.IsNaN(_balanceShown) ? 1000 : _balanceShown, SelectedMode == GameModeType.FreeThrow ? 0 : 1, Stake);
            _lobby.SetShooterName(CurrentCharacter()?.DisplayName, CurrentCharacter()?.Nickname);
            if (_studio != null) _studio.SetVisible(true);
        }

        public void ShowHud(string hint)
        {
            Show(UiScreen.Hud);
            string modeName = SelectedMode == GameModeType.FreeThrow ? "FREE THROW" : "THREE-POINT";
            _hud.Present(modeName, double.IsNaN(_balanceShown) ? 1000 : _balanceShown, Stake);
            if (_studio != null) _studio.SetVisible(false);
        }

        public void ShowResult(RoundMathResult result, TimingZone timing, bool canAffordAgain, bool offerDon)
        {
            SetBroadcast(false);
            Show(UiScreen.Result);
            _result.Present(result, timing, false);
            double payout = result != null ? result.Payout : 0;
            _postShot.Present(Stake, payout, canAffordAgain, offerDon, _cfg);
        }

        public void ShowPause()
        {
            if (_pause != null)
            {
                Show(UiScreen.Pause);
                _pauseGroup.alpha = 1f;
            }
        }

        public void HidePause()
        {
            Show(UiScreen.Hud);
        }

        public void SetBroadcast(bool on) 
        {
            if (_hud != null) _hud.SetVisible(!on, true);
        }

        public void SetReplayTag(bool on) { }

        public void SetMeterVisible(bool visible)
        {
            if (_meterRoot != null) _meterRoot.SetActive(visible);
        }

        public void UpdateMeter(float normalized, TimingZone zone)
        {
            if (_meterRoot == null || !_meterRoot.activeSelf) return;
            if (_meterNeedle != null)
            {
                var p = _meterNeedle.parent as RectTransform;
                if (p != null)
                {
                    float w = p.rect.width;
                    _meterNeedle.anchoredPosition = new Vector2(normalized * w, 0f);
                }
            }
            if (_meterGlow != null) _meterGlow.color = UiTokens.Alpha(zone == TimingZone.Perfect ? UiTokens.Win : (zone == TimingZone.Good ? UiTokens.Gold : UiTokens.Loss), 0.25f);
            if (_meterZone != null) _meterZone.text = zone.ToString().ToUpperInvariant();
        }

        public void ShowShotCallout(string text)
        {
            // Disabled per user request
        }

        IEnumerator CalloutRoutine()
        {
            yield return null;
        }

        void Click() => OnUiClick?.Invoke();

        void Build()
        {
            var canvas = Widgets.CreateCanvas(transform, 20);
            canvas.name = "GameCanvas";
            _safeArea = Widgets.Rect(canvas.transform, "SafeArea");
            Widgets.Stretch(_safeArea, Vector2.zero, Vector2.one);
            _safeArea.gameObject.AddComponent<SafeAreaFitter>();
            UiTween.Runner = this;

            _splash = new SplashView(_safeArea);

            _main = new MainMenuView(_safeArea);
            _main.OnPlay += () => { Click(); ShowCharacterSelect(); };
            _main.OnSettings += () => { Click(); OpenSettings(UiScreen.Main); };
            _main.OnQuickMode += (mode) => { SelectedMode = mode; Click(); ShowCharacterSelect(); };

            _roster = new CharacterSelectView(_safeArea);
            _roster.OnBack += () => { Click(); ShowMainMenu(); };
            _roster.OnPreview += (idx) => { SelectedCharacter = idx; OnCharacterChanged?.Invoke(); };
            _roster.OnConfirm += (idx) => { SelectedCharacter = idx; Click(); ShowLobby(); };

            _lobby = new BetPanelView(_safeArea);
            _lobby.OnBack += () => { Click(); ShowCharacterSelect(); };
            _lobby.OnLockIn += () => { Stake = _lobby.Stake; SelectedMode = _lobby.SelectedMode; Click(); OnPlaceBet?.Invoke(); };

            _hud = new HudView(_safeArea);
            _hud.OnMenu += () => { Click(); ShowPause(); };

            _result = new ResultView(_safeArea);
            _postShot = new PostShotView(_safeArea);
            _postShot.OnShootAgain += () => { Click(); OnShootAgain?.Invoke(); };
            _postShot.OnChangeSetup += () => { Click(); OnChangeSetup?.Invoke(); };
            _postShot.OnDoubleOrNothing += () => { Click(); OnDoubleOrNothing?.Invoke(); };

            BuildMeter(_safeArea);
            BuildCallout(_safeArea);
            BuildPause();
            BuildSettings();
            EnsureStudio();

            Show(UiScreen.Main);
        }

        void BuildMeter(Transform t)
        {
            var meterGo = new GameObject("MeterBlock", typeof(RectTransform));
            var meter = meterGo.GetComponent<RectTransform>();
            meter.SetParent(t, false);
            Widgets.PlaceAt(meter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 320f), new Vector2(860f, 72f));
            _meterRoot = meterGo;
            
            var bgCard = Widgets.Card(meter, "Bg", UiTokens.Alpha(UiTokens.Carbon, 0.95f), UiTokens.Hairline,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(860f, 72f), Vector2.zero, 18, true, true);
            var bgRt = bgCard.transform.parent.GetComponent<RectTransform>();

            void Zone(string n, float a, float b, Color c, string lblTxt)
            {
                var z = Widgets.Panel(bgRt, n, UiTokens.Alpha(c, 0.7f), new Vector2(a, 0.15f), new Vector2(b, 0.85f), UiSprites.Chamfer(6));
                z.offsetMin = new Vector2(2f, 0f); z.offsetMax = new Vector2(-2f, 0f);
                z.GetComponent<UiImage>().raycastTarget = false;
                var lbl = Widgets.Caption(z, "Lbl", lblTxt, UiTokens.Alpha(UiTokens.Carbon, 0.8f), UiTokens.Small, TextAnchor.MiddleCenter);
                Widgets.Stretch(lbl.rectTransform, 0, 0, 0, 0);
            }
            
            Zone("Z0", 0.01f, 0.28f, UiTokens.Loss, "EARLY");
            Zone("Z1", 0.28f, 0.42f, UiTokens.Gold, "GOOD");
            Zone("Z2", 0.42f, 0.58f, UiTokens.Win, "PERFECT");
            Zone("Z3", 0.58f, 0.72f, UiTokens.Gold, "GOOD");
            Zone("Z4", 0.72f, 0.99f, UiTokens.Loss, "LATE");
            
            _meterGlow = Widgets.Glow(bgRt, "Glow", UiTokens.Alpha(UiTokens.Win, 0.25f), new Vector2(0.42f, -0.4f), new Vector2(0.58f, 1.4f)).GetComponent<UiImage>();
            
            _meterNeedle = Widgets.Panel(bgRt, "Needle", UiTokens.Ice, new Vector2(0f, 0f), new Vector2(0f, 1f), UiSprites.Pill);
            _meterNeedle.anchorMax = new Vector2(0f, 1f); 
            _meterNeedle.sizeDelta = new Vector2(10f, 0f);
            _meterNeedle.pivot = new Vector2(0.5f, 0.5f);
            _meterNeedle.offsetMin = new Vector2(-5f, -8f);
            _meterNeedle.offsetMax = new Vector2(5f, 8f);
            
            var tGo = Widgets.Display(bgRt, "ZoneText", "", UiTokens.DisplayM, UiTokens.Ice, TextAnchor.LowerCenter);
            Widgets.PlaceAt(tGo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(500f, 80f));
            _meterZone = tGo;
            _meterRoot.SetActive(false);
        }

        void BuildCallout(Transform t)
        {
            var callGo = new GameObject("Callout");
            _calloutRt = callGo.AddComponent<RectTransform>();
            _calloutRt.SetParent(t, false);
            Widgets.Place(_calloutRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400f, 80f), new Vector2(0f, -80f));
            _calloutGo = callGo;
            _calloutGroup = callGo.AddComponent<CanvasGroup>();
            var plate = Widgets.Card(_calloutRt, "Bg", new Vector2(0f, 0f), new Vector2(1f, 1f), UiTokens.Alpha(UiTokens.Win, 0.9f));
            _calloutText = Widgets.Label(plate, "T", "PERFECT", UiTokens.H2, UiTokens.Ice, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 1f), FontStyle.Bold, true);
            _calloutGo.SetActive(false);
        }

        void BuildPause()
        {
            _pause = ScreenRoot("Pause", out _pauseGroup);
            _pause.SetActive(false);
            Transform t = _pause.transform;
            Widgets.Panel(t, "Scrim", UiTokens.Alpha(UiTokens.Ink, 0.9f), Vector2.zero, Vector2.one, UiSprites.Round12, true);
            var card = Widgets.Card(t, "Card", new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.7f), UiTokens.Alpha(UiTokens.Panel, 1f));
            Widgets.Label(card, "T", "PAUSED", UiTokens.H2, UiTokens.Ice, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.96f), FontStyle.Bold, true);
            Widgets.Button(card, "R", "RESUME", Widgets.ButtonStyle.Primary, () => { Click(); OnPauseResume?.Invoke(); }, UiTokens.H3, new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.72f));
            Widgets.Button(card, "S", "SETTINGS", Widgets.ButtonStyle.Ghost, () => { Click(); OpenSettings(UiScreen.Pause); }, UiTokens.Body, new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.48f));
            Widgets.Button(card, "Q", "QUIT TO MENU", Widgets.ButtonStyle.Quiet, () => { Click(); OnPauseQuit?.Invoke(); }, UiTokens.Body, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.26f));
        }

        void BuildSettings()
        {
            _settings = ScreenRoot("Settings", out _settingsGroup);
            _settings.SetActive(false);
            Transform t = _settings.transform;
            
            var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(UiImage));
            scrim.GetComponent<RectTransform>().SetParent(t, false);
            Widgets.Stretch(scrim.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            scrim.GetComponent<UiImage>().color = UiTokens.Scrim;
            scrim.GetComponent<UiImage>().raycastTarget = true;

            var cardGo = Widgets.Card(t, "Card", UiTokens.Alpha(UiTokens.Carbon, 0.98f), UiTokens.Gold,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(960f, 1500f), Vector2.zero, 24, true, true);
            var card = cardGo.transform.parent.GetComponent<RectTransform>();

            var title = Widgets.Caption(card, "Title", "SETTINGS", UiTokens.Gold, UiTokens.H2, TextAnchor.UpperLeft);
            Widgets.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, 60f), new Vector2(80f, -80f));

            var r = new GameObject("Rule", typeof(RectTransform), typeof(UiImage));
            r.GetComponent<RectTransform>().SetParent(card, false);
            Widgets.Place(r.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, 4f), new Vector2(80f, -160f));
            r.GetComponent<UiImage>().color = UiTokens.Alpha(UiTokens.Gold, 0.3f);
            
            var audioLbl = Widgets.Caption(card, "A", "AUDIO", UiTokens.Muted, UiTokens.Micro, TextAnchor.UpperLeft);
            Widgets.Place(audioLbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, 32f), new Vector2(80f, -240f));

            var slGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            var slRt = slGo.GetComponent<RectTransform>();
            slRt.SetParent(card, false);
            Widgets.Place(slRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(800f, 80f), new Vector2(0f, -320f));
            var slBg = new GameObject("Bg", typeof(RectTransform), typeof(UiImage));
            slBg.GetComponent<RectTransform>().SetParent(slRt, false);
            Widgets.Stretch(slBg.GetComponent<RectTransform>(), 0f, 32f, 0f, 32f);
            var slBgImg = slBg.GetComponent<UiImage>(); slBgImg.sprite = UiSprites.Pill; slBgImg.type = UiImage.Type.Sliced; slBgImg.color = UiTokens.Alpha(UiTokens.Line, 0.5f);
            var slFillArea = new GameObject("FillArea", typeof(RectTransform));
            slFillArea.GetComponent<RectTransform>().SetParent(slRt, false);
            Widgets.Stretch(slFillArea.GetComponent<RectTransform>(), 16f, 32f, 16f, 32f);
            var slFill = new GameObject("Fill", typeof(RectTransform), typeof(UiImage));
            slFill.GetComponent<RectTransform>().SetParent(slFillArea.transform, false);
            var slFillImg = slFill.GetComponent<UiImage>(); slFillImg.sprite = UiSprites.Pill; slFillImg.type = UiImage.Type.Sliced; slFillImg.color = UiTokens.Gold;
            var slHandle = new GameObject("Handle", typeof(RectTransform), typeof(UiImage));
            slHandle.GetComponent<RectTransform>().SetParent(slRt, false);
            Widgets.Place(slHandle.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, 40f), Vector2.zero);
            var slHandleImg = slHandle.GetComponent<UiImage>(); slHandleImg.sprite = UiSprites.Dot; slHandleImg.color = UiTokens.Ice;
            var sl = slGo.GetComponent<Slider>();
            sl.targetGraphic = slHandleImg; sl.fillRect = slFill.GetComponent<RectTransform>(); sl.handleRect = slHandle.GetComponent<RectTransform>();
            sl.value = AudioListener.volume;
            sl.onValueChanged.AddListener(v => { Click(); AudioListener.volume = v; PlayerPrefs.SetFloat("Hoops.MasterVol", v); });

            var gfxLbl = Widgets.Caption(card, "G", "GRAPHICS", UiTokens.Muted, UiTokens.Micro, TextAnchor.UpperLeft);
            Widgets.Place(gfxLbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, 32f), new Vector2(80f, -480f));
            
            var btnM = Widgets.Secondary(card, "Q-", "-", UiTokens.H2, () => ShiftQuality(-1), 16);
            Widgets.Place((RectTransform)btnM.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, 120f), new Vector2(160f, -560f));
            
            _qualityLabel = Widgets.Caption(card, "QL", QualitySettings.names[QualitySettings.GetQualityLevel()].ToUpperInvariant(), UiTokens.Ice, UiTokens.Body, TextAnchor.MiddleCenter);
            Widgets.Place(_qualityLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(300f, 120f), new Vector2(0f, -560f));

            var btnP = Widgets.Secondary(card, "Q+", "+", UiTokens.H2, () => ShiftQuality(1), 16);
            Widgets.Place((RectTransform)btnP.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(160f, 120f), new Vector2(-160f, -560f));

            var ctrlLbl = Widgets.Caption(card, "C", "CONTROLS", UiTokens.Muted, UiTokens.Micro, TextAnchor.UpperLeft);
            Widgets.Place(ctrlLbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, 32f), new Vector2(80f, -760f));

            var ctrlTxt = Widgets.Caption(card, "Cx", "TAP · CLICK · SPACE TO SHOOT\nESC TO PAUSE", UiTokens.Ice, UiTokens.Small, TextAnchor.UpperLeft);
            Widgets.Place(ctrlTxt.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, 100f), new Vector2(80f, -820f));

            var disc = Widgets.Caption(card, "Disc", _cfg.disclaimer, UiTokens.Muted, UiTokens.Micro, TextAnchor.UpperLeft);
            Widgets.Place(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(800f, 200f), new Vector2(0f, -1000f));
            disc.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;

            var btnBack = Widgets.Primary(card, "BackBtn", "BACK", UiTokens.H2, CloseSettings, 16);
            Widgets.Place((RectTransform)btnBack.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(800f, 100f), new Vector2(0f, 80f));

            AudioListener.volume = PlayerPrefs.GetFloat("Hoops.MasterVol", 1f);
        }

        void OpenSettings(UiScreen from)
        {
            _screenBeforeSettings = from;
            Show(UiScreen.Settings);
        }

        void CloseSettings()
        {
            Click();
            Show(_screenBeforeSettings);
        }

        void ShiftQuality(int dir)
        {
            Click();
            int max = QualitySettings.names.Length - 1;
            int next = Mathf.Clamp(QualitySettings.GetQualityLevel() + dir, 0, max);
            QualitySettings.SetQualityLevel(next, true);
            if (_qualityLabel != null) _qualityLabel.text = QualitySettings.names[next].ToUpperInvariant();
        }

        GameObject ScreenRoot(string name, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_safeArea, false);
            Widgets.Stretch(rt, Vector2.zero, Vector2.one);
            group = go.AddComponent<CanvasGroup>();
            return go;
        }

        void EnsureStudio()
        {
            if (_studio == null)
                _studio = CharacterSelectStudio.Create(transform);
        }
    }
}


