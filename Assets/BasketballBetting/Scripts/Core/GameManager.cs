using System;
using System.Collections;
using BasketballBetting.Cameras;
using BasketballBetting.Characters;
using BasketballBetting.Presentation;
using BasketballBetting.Shooting;
using UnityEngine;

namespace BasketballBetting
{
    /// <summary>
    /// Round orchestrator. Owns the betting round flow (unchanged), the lobby/roster state, and hands each
    /// shot to the ShotController. Presentation (cameras, replay, UI, audio) only ever reads results.
    ///
    /// BettingEngine → ShotIntent → ShotController (physics + detection + locked result) → presentation.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Presentation")]
        [SerializeField] ReplayPolicy _replay = new ReplayPolicy();
        [SerializeField] ShotTuning _shotTuning;
        [SerializeField] ShotAnimationProfile _freeThrowMotion = ShotAnimationProfile.FreeThrow;
        [SerializeField] ShotAnimationProfile _jumpShotMotion = ShotAnimationProfile.JumpShot;

        BettingConfigData _cfg;
        BettingEngine _engine;
        GameUI _ui;
        BasketballAudio _audio;
        ArenaSceneRoot _arena;
        Hoop _hoop;
        HoopNet _net;
        Basketball _ball;
        ShotController _shot;
        ShotRecorder _recorder;
        ReplayDirector _replayDirector;
        ReplayView _replayView;

        GameCameraDirector _cameras;
        CameraLook _look;
        readonly ShotMeter _meter = new ShotMeter();

        HumanoidRig _shooter;
        ShooterAnimator _shooterAnim;
        HumanoidRig _defender;
        ShooterAnimator _defenderAnim;
        CharacterDefinition _defenderDef;
        HumanoidRig[] _lineup;
        ShooterAnimator[] _lineupAnim;

        RoundRequest _activeRequest;
        RoundMathResult _pendingMath;
        RoundMathResult _lastResult;
        TimingZone _lastTiming;
        bool _roundLive;
        bool _paused;
        GameModeType _previewMode = GameModeType.FreeThrow;
        int _previewCharacter = -1;
        UiScreen _previewScreen;

        // Live shot bookkeeping (cleared every round).
        ShotIntent _liveIntent;
        bool _liveReleased;
        bool _liveLocked;
        ShotResultState _liveResult;

        public ShotController Shot => _shot;
        public GameCameraDirector Cameras => _cameras;

        [Serializable]
        public sealed class ReplayPolicy
        {
            public bool PlayForMakes = true;
            public bool PlayForMisses = true;
            public bool PlayForAirBalls = false;
            public float MaxWaitForRestAfterLock = 2.2f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            // Only boot inside the game scene (an authored arena). Test scenes and tools must stay untouched.
            if (FindAnyObjectByType<ArenaSceneRoot>() == null)
                return;
            if (FindAnyObjectByType<GameManager>() != null)
                return;
            var go = new GameObject("BasketballBettingGame");
            go.AddComponent<GameManager>();
        }

        // ------------------------------------------------------------------ boot

        void Awake()
        {
            GameInput.EnsureEventSystem();
            PhysicsSetup.Apply();

            _cfg = BettingConfigLoader.Load();
            MoneyAppBridge.RaiseConfigLoaded(_cfg);

            if (WalletService.Provider == null)
                WalletService.Initialize(new LocalWalletProvider(_cfg));

            _engine = new BettingEngine(_cfg);
            _defenderDef = CharacterDefinition.CreateRuntime("house_defender", new CharacterVisualProfile
            {
                displayName = "House Defender",
                nickname = "LOCK",
                jerseyNumber = 0,
                skin = new Color(0.42f, 0.28f, 0.18f),
                hair = Color.black,
                jersey = Color.white,
                shorts = new Color(0.08f, 0.08f, 0.1f),
                trim = new Color(0.15f, 0.15f, 0.18f),
                shoes = Color.black,
                socks = Color.white,
                height = 2.03f,
                muscle = 1.12f,
                bulk = 1.08f,
                bald = true,
                attributes = new CharacterAttributes { shooting = 70, accuracy = 70, releaseSpeed = 70, power = 90, threePoint = 60 }
            }, "kingsley_game");

            BindArena();

            _cameras = GameCameraDirector.Create();
            _look = CameraLook.Attach(_cameras.gameObject);
            _cameras.Context.RimCenter = _hoop.RimCenter;
            _cameras.Context.TowardCourt = _hoop.Geometry.TowardCourt;

            _shot = gameObject.AddComponent<ShotController>();
            _shot.Bind(_ball, _hoop, _shotTuning);
            _shot.Released += OnShotReleased;
            _shot.Contact += OnShotContact;
            _shot.ResultLocked += OnShotResultLocked;

            _recorder = gameObject.AddComponent<ShotRecorder>();
            _replayDirector = gameObject.AddComponent<ReplayDirector>();
            _audio = BasketballAudio.Create(transform);
            _ui = GameUI.Create(transform, _cfg);

            // ── ReplayView: broadcast chrome that lives above the HUD ──
            // Parent to the canvas SafeArea so it's correctly layered in screen-space
            var uiCanvas = _ui.transform.Find("GameCanvas");
            var uiSafe = uiCanvas != null ? uiCanvas.Find("SafeArea") : _ui.transform;
            var replayParent = uiSafe != null ? uiSafe : _ui.transform;
            _replayView = new ReplayView(replayParent);
            int _cutIndex = 0, _cutTotal = 1;
            _replayDirector.CutStarted += cut =>
            {
                // ReplayDirector fires CutStarted per cut; we track the index ourselves
                _replayView.NotifyCut(cut.Shot.Name, _cutIndex, _cutTotal);
                _cutIndex++;
            };
            _replayDirector.ReplayFinished += () => { _cutIndex = 0; };
            _replayView.OnSkip += () => { if (_replayDirector != null) _replayDirector.Skip(); };


            _ui.OnPlaceBet += () => StartRound(false);
            _ui.OnShootAgain += () => StartRound(false);
            _ui.OnChangeSetup += ReturnToLobby;
            _ui.OnDoubleOrNothing += () => StartRound(true);
            _ui.OnUiClick += () => _audio.Play(AudioCue.UiClick);
            _ui.OnPauseResume += TogglePause;
            _ui.OnPauseQuit += QuitToMenu;
            _ui.OnCharacterChanged += PreviewFromUi;
            // OnSkipReplay now handled via _replayView.OnSkip above

            WalletService.BalanceChanged += _ui.RefreshBalance;
            _ui.RefreshBalance(WalletService.Balance);

            SpawnShooter(_ui.CurrentCharacter());
            PlaceActors(GameModeType.FreeThrow, ThreePointSpot.Top);
            ApplyLobbyCamera();
            _cameras.SnapToShot();
            _previewCharacter = _ui.SelectedCharacter;
            _previewMode = _ui.SelectedMode;
            _ui.ShowMainMenu();
        }

        void BindArena()
        {
            _arena = FindAnyObjectByType<ArenaSceneRoot>();
            _hoop = _arena != null && _arena.PlayGoal != null ? _arena.PlayGoal : FindAnyObjectByType<Hoop>();
            if (_hoop == null)
            {
                Debug.LogWarning("[Game] No Hoop in the scene; creating a bare regulation goal.");
                _hoop = Hoop.CreateGoal(null, CourtMetrics.RimCenter, Vector3.forward);
            }
            _hoop.Build();

            _net = _arena != null && _arena.PlayNet != null ? _arena.PlayNet : _hoop.GetComponentInChildren<HoopNet>(true);

            _ball = _arena != null && _arena.Ball != null ? _arena.Ball : FindAnyObjectByType<Basketball>();
            if (_ball == null)
                _ball = Basketball.Create(null);
            _ball.name = "GameplayBasketball";
            _ball.gameObject.SetActive(true);
            _ball.Configure();
            if (_net != null)
                _net.SetBallSource(_ball.transform, _ball.Radius);

            // Exactly one authoritative ball.
            foreach (var other in FindObjectsByType<Basketball>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (other != _ball)
                    Destroy(other.gameObject);
        }

        void OnDestroy()
        {
            if (_ui != null)
                WalletService.BalanceChanged -= _ui.RefreshBalance;
            if (_shot != null)
            {
                _shot.Released -= OnShotReleased;
                _shot.Contact -= OnShotContact;
                _shot.ResultLocked -= OnShotResultLocked;
            }
        }

        // ------------------------------------------------------------------ per-frame

        void Update()
        {
            if (!_roundLive && _ui != null && _shooter != null)
            {
                if (_ui.SelectedCharacter != _previewCharacter || _ui.SelectedMode != _previewMode)
                    PreviewFromUi();
                else if (_ui.CurrentScreen != _previewScreen)
                {
                    _previewScreen = _ui.CurrentScreen;
                    if (_ui.CurrentScreen == UiScreen.Roster)
                    {
                        SetTitleCastHidden(false);
                        UpdateRosterLineup();
                    }
                    else if (_ui.CurrentScreen == UiScreen.Main)
                    {
                        TeardownLineupKeepSelected();
                        EnsureDefender(false);
                        ApplyLobbyCamera();
                    }
                    else
                    {
                        TeardownLineupKeepSelected();
                        PlaceActors(_ui.SelectedMode, ThreePointSpot.Top);
                        ApplyLobbyCamera();
                    }
                }
            }

            if (GameInput.MenuPressedThisFrame() && _ui != null && _ui.CurrentScreen == UiScreen.Hud)
                TogglePause();
        }

        void LateUpdate()
        {
            if (_cameras == null)
                return;
            if (_replayDirector != null && _replayDirector.IsPlaying)
                return; // the replay owns the camera context while it plays
            CameraContext ctx = _cameras.Context;
            ctx.Shooter = _shooter != null ? _shooter.Root : null;
            ctx.Defender = _defender != null && _defender.Root != null && _defender.Root.gameObject.activeInHierarchy ? _defender.Root : null;
            ctx.ShooterHead = _shooter != null && _shooter.Head != null ? _shooter.Head.position : (ctx.Shooter != null ? ctx.Shooter.position + Vector3.up * 1.7f : Vector3.zero);
            ctx.BallPosition = _ball != null ? _ball.transform.position : ctx.RimCenter;
            ctx.BallVelocity = _ball != null ? _ball.Velocity : Vector3.zero;
            ctx.RimCenter = _hoop.RimCenter;
            ctx.Mode = _activeRequest != null ? _activeRequest.Mode : (_ui != null ? _ui.SelectedMode : GameModeType.FreeThrow);
            ctx.Spot = _activeRequest != null ? _activeRequest.Spot : ThreePointSpot.Top;
            ctx.ResultKnown = _liveLocked;
            ctx.Made = _liveLocked && _liveResult != null && _liveResult.Final == ShotOutcome.Make;
            if (!_roundLive && _ui != null && _ui.CurrentScreen == UiScreen.Roster && _lineup != null)
                FaceLineupToCamera();
        }

        // ------------------------------------------------------------------ betting round flow (unchanged)

        void StartRound(bool doubleOrNothing)
        {
            if (_roundLive || _paused)
                return;

            CharacterDefinition character = _ui.CurrentCharacter();
            if (character == null)
                return;
            TeardownLineupKeepSelected();

            double stake;
            if (doubleOrNothing)
            {
                if (_lastResult == null || !_lastResult.Won || !_cfg.doubleOrNothing.enabled)
                    return;
                stake = _lastResult.Payout;
            }
            else
            {
                stake = _cfg.ClampStake(_ui.Stake);
            }

            var debit = WalletService.Debit(new WalletTxContext
            {
                RoundId = Guid.NewGuid().ToString("N"),
                Mode = _ui.SelectedMode,
                CharacterId = character.Id,
                Reason = doubleOrNothing ? WalletTxReason.DoubleOrNothingStake : WalletTxReason.Stake,
                Amount = stake
            });

            if (!debit.Success)
            {
                Debug.LogWarning("[Betting] Debit failed: " + debit.Error);
                ReturnToLobby();
                return;
            }

            var request = new RoundRequest
            {
                RoundId = debit.TransactionId,
                Mode = _ui.SelectedMode,
                Stake = stake,
                Character = character,
                Spot = requestSpot(_ui.SelectedMode),
                IsDoubleOrNothing = doubleOrNothing,
                DoubleOrNothingBaseWin = doubleOrNothing ? _lastResult.Payout : 0
            };

            MoneyAppBridge.RaiseBetPlaced(new BetPlacedEvent
            {
                RoundId = request.RoundId,
                Mode = request.Mode,
                CharacterId = character.Id,
                Stake = stake,
                DebitTransactionId = debit.TransactionId,
                TimestampUtc = DateTimeOffset.UtcNow
            });

            _audio.Play(AudioCue.BetPlace);
            _pendingMath = _cfg.timingMeterAffectsOdds ? null : _engine.Resolve(request);
            StartCoroutine(RunRound(request));
        }

        ThreePointSpot requestSpot(GameModeType mode)
        {
            if (mode != GameModeType.ThreePoint)
                return ThreePointSpot.Top;
            return (ThreePointSpot)UnityEngine.Random.Range(0, 5);
        }

        IEnumerator RunRound(RoundRequest request)
        {
            _roundLive = true;
            _activeRequest = request;
            SpawnShooter(request.Character);
            PlaceActors(request.Mode, request.Spot, _pendingMath != null ? _pendingMath.DefenderScenario : DefenderScenario.None);
            _ui.SetMeterVisible(false);

            if (request.Mode == GameModeType.ThreePoint)
                yield return RunThreePointLeadIn(request);
            else
                yield return RunFreeThrowLeadIn();

            _ui.SetMeterVisible(true);
            
            float rndSpeed = UnityEngine.Random.Range(0.6f, 1.1f);
            float duration = _cfg.meterDurationSeconds * rndSpeed;
            if (request.Mode == GameModeType.ThreePoint) duration *= 0.75f; // 25% tougher for 3-pointer
            
            _meter.Begin(duration);
            _ui.ShowHud("TAP / CLICK / SPACE TO SHOOT");
            _shooterAnim.SetPose(HumanoidPose.ShootLoad);
            _cameras.Cut(new BehindShooterShot { Tight = true }, 0.6f);

            while (_meter.Running)
            {
                if (!_paused)
                {
                    _meter.Tick(Time.unscaledDeltaTime);
                    _ui.UpdateMeter(_meter.Normalized, _meter.Zone);
                    if (GameInput.ShootPressedThisFrame() || _meter.Cycles >= Mathf.Max(1, _cfg.meterAutoReleaseCycles))
                        _meter.Stop();
                }
                yield return null;
            }

            _lastTiming = _meter.Zone;
            _ui.SetMeterVisible(false);
            if (_pendingMath == null)
                _pendingMath = _engine.Resolve(request, _lastTiming);
            _engine.BindPresentation(_pendingMath, _lastTiming);

            yield return PlayShot(_pendingMath, _lastTiming, request);
            Settle(_pendingMath, _lastTiming);
            _roundLive = false;
            _activeRequest = null;
            _pendingMath = null;
        }

        IEnumerator RunFreeThrowLeadIn()
        {
            _cameras.Cut(new BehindShooterShot(), 0.8f);
            _ui.ShowHud("FREE THROW  ·  NO DEFENDER");
            _audio.Play(AudioCue.Whistle);
            yield return new WaitForSeconds(0.42f);
            _audio.Play(AudioCue.ShoeSqueak);
        }

        IEnumerator RunThreePointLeadIn(RoundRequest request)
        {
            DefenderScenario scenario = _pendingMath != null ? _pendingMath.DefenderScenario : DefenderScenario.ContestedShot;
            _cameras.Cut(new BehindShooterShot(), 0.8f);
            _ui.ShowHud(SpotLabel(request.Spot) + "  ·  DEFENDER CLOSING");
            yield return DefenderApproach(scenario);
            _audio.Play(AudioCue.Whistle);
            yield return new WaitForSeconds(0.5f);
            _audio.Play(AudioCue.ShoeSqueak);
        }

        IEnumerator DefenderApproach(DefenderScenario scenario)
        {
            if (_defender == null)
                yield break;
            Vector3 end = DefenderStandPosition(_shooter.Root.position, scenario);
            Vector3 start = end + (end - _shooter.Root.position).normalized * 2.4f;
            start.y = 0f;
            _defender.Root.position = start;
            _defender.LookAt(_shooter.Root.position);
            _defenderAnim.SetPose(HumanoidPose.Idle);
            float t = 0f;
            float duration = scenario == DefenderScenario.CleanShot ? 0.85f : 1.15f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                _defender.Root.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                _defender.LookAt(_shooter.Root.position);
                if (t > 0.2f && t < 0.25f)
                    _audio.Play(AudioCue.ShoeSqueak);
                yield return null;
            }
            _defender.Root.position = end;
        }

        // ------------------------------------------------------------------ the shot

        /// <summary>
        /// Shooting motion → release event → ShotController.Fire → physics → locked result → reveal → replay.
        /// The betting result is already final when this starts; nothing here can change it.
        /// </summary>
        IEnumerator PlayShot(RoundMathResult math, TimingZone timing, RoundRequest request)
        {
            _liveIntent = ShotIntent.FromBet(math, timing);
            _liveReleased = false;
            _liveLocked = false;
            _liveResult = null;

            bool threePoint = request.Mode == GameModeType.ThreePoint;
            DefenderScenario scenario = math.DefenderScenario;
            bool blocked = _liveIntent.Style == ShotStyle.Blocked;
            bool contestClose = scenario == DefenderScenario.VeryCloseContest || scenario == DefenderScenario.ContestedShot
                                || scenario == DefenderScenario.SlightDeflection || blocked;

            _ui.SetBroadcast(true);
            _cameras.Cut(new BehindShooterShot(), 0.5f);
            if (_net != null)
            {
                _net.SetBallSource(_ball.transform, _ball.Radius);
                _net.ResetRest();
            }
            _recorder.Begin(_shooter.Root, threePoint && _defender != null ? _defender.Root : null, _ball.transform, _hoop.RimCenter, _liveIntent.Style);

            ShotAnimationProfile motion = threePoint ? _jumpShotMotion : _freeThrowMotion;
            _shooterAnim.PlayShot(motion, HumanoidPose.Idle);
            if (threePoint && _defenderAnim != null && contestClose)
                StartCoroutine(DefenderContest(motion.ReleaseTime, blocked));

            // Wait for the release event (the animator fires it at the exact frame the ball leaves the hand).
            float guard = 0f;
            while (!_liveReleased && guard < motion.ReleaseTime + 1.5f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!_liveReleased)
            {
                Debug.LogWarning("[Game] Release event never arrived; firing from the hand now.");
                OnShooterRelease();
            }

            // Ball in flight: wait for the authoritative lock, then for the ball to settle.
            yield return _shot.WaitForResult(8f);
            if (!_liveLocked)
            {
                Debug.LogError("[Game] Shot never locked a result; treating the betting outcome as final.");
                _liveResult = _shot.Result;
                _liveLocked = true;
            }

            float rest = 0f;
            while (_shot.Phase != ShotPhase.Finished && rest < _replay.MaxWaitForRestAfterLock)
            {
                rest += Time.unscaledDeltaTime;
                yield return null;
            }

            ShotRecording recording = _recorder.Stop();
            _ball.Freeze();

            bool made = _liveResult != null && _liveResult.Final == ShotOutcome.Make;
            _shooterAnim.SetPose(made ? HumanoidPose.Celebrate : HumanoidPose.Frustrated);
            if (_defenderAnim != null)
                _defenderAnim.SetPose(HumanoidPose.Idle);
            if (!made && !blocked)
                _audio.Play(AudioCue.CrowdGroan);

            if (_arena != null && _arena.CrowdRoot != null) 
            {
                var crowdAnim = _arena.CrowdRoot.GetComponent<CrowdAnimator>();
                if (crowdAnim != null) crowdAnim.React(made);
            }

            if (ShouldReplay(recording))
            {
                if (_arena != null && _arena.CrowdRoot != null)
                {
                    var crowdAnim = _arena.CrowdRoot.GetComponent<CrowdAnimator>();
                    if (crowdAnim != null) crowdAnim.StopCheer();
                }

                if (_net != null && recording != null)
                    StartCoroutine(NetFollowsReplay(recording));
                _replayView?.Show();
                yield return _replayDirector.Play(recording, _liveResult, _cameras, _look, _shooter.Root,
                    threePoint && _defender != null ? _defender.Root : null, _ball, _hoop.RimCenter, _hoop.Geometry.TowardCourt);
                _replayView?.Hide();
                if (_net != null)
                    _net.SetBallSource(_ball.transform, _ball.Radius);
            }


            // Changed blend from 0.25f to 0f to prevent camera swooping through the player after replay
            // You can uncomment the line below and delete the 0f line to revert:
            // _cameras.Cut(new ReactionShot { Subject = _shooter.Root }, 0.25f);
            _cameras.Cut(new ReactionShot { Subject = _shooter.Root }, 0f);
            yield return new WaitForSecondsRealtime(made ? 0.7f : 0.5f);
            _ui.SetBroadcast(false);
            
            // Changed blend from 0.6f to 0f to prevent camera moving through the player
            // You can uncomment the line below and delete the 0f line to revert:
            // _cameras.Cut(new BehindShooterShot(), 0.6f);
            _cameras.Cut(new BehindShooterShot(), 0f);
        }

        IEnumerator DefenderContest(float shooterReleaseTime, bool block)
        {
            float lead = block ? 0.34f : 0.26f;
            while (_shooterAnim != null && _shooterAnim.IsPlayingShot && _shooterAnim.ShotTime < shooterReleaseTime - lead)
                yield return null;
            if (_defenderAnim != null)
                _defenderAnim.PlayContest(block);
        }

        IEnumerator NetFollowsReplay(ShotRecording rec)
        {
            yield return null;
            if (!_replayDirector.IsPlaying)
                yield break;

            // The net wraps around whatever ball is visible: during replay that is the ghost.
            Transform ghostBall = _replayDirector.GhostBall;
            if (ghostBall == null)
                yield break;

            _net.SetBallSource(ghostBall, _ball.Radius);
            _net.ResetRest();
            bool swished = false;
            int nextContact = 0;
            float prevTime = -1f;

            while (_replayDirector.IsPlaying)
            {
                float t = _replayDirector.PlaybackTime;
                
                if (t < prevTime)
                {
                    nextContact = 0;
                    swished = false;
                    _net.ResetRest();
                }
                
                while (nextContact < rec.Contacts.Count && rec.Contacts[nextContact].Time <= t)
                {
                    var contact = rec.Contacts[nextContact];
                    if (t - contact.Time < 0.25f)
                    {
                        if (contact.Kind == ShotContactKind.Rim)
                            _net.Rattle(contact.Point - _hoop.RimCenter, 0.6f);
                    }
                    nextContact++;
                }
                prevTime = t;

                if (!swished && rec.Made && rec.CrossingTime >= 0f && _hoop.Geometry.Along(ghostBall.position) < -0.05f && _hoop.Geometry.Radial(ghostBall.position) < _hoop.InnerRadius)
                {
                    swished = true;
                    _net.Swish();
                    
                    if (_arena != null && _arena.CrowdRoot != null)
                    {
                        var crowdAnim = _arena.CrowdRoot.GetComponent<CrowdAnimator>();
                        if (crowdAnim != null) crowdAnim.React(true);
                    }
                }
                yield return null;
            }
        }

        bool ShouldReplay(ShotRecording rec)
        {
            if (rec == null || !rec.HasFrames || _liveResult == null)
                return false;
            if (_liveResult.Final == ShotOutcome.Make)
                return _replay.PlayForMakes;
            if (_liveIntent.Style == ShotStyle.AirBall)
                return _replay.PlayForAirBalls;
            return _replay.PlayForMisses;
        }

        void OnShooterRelease()
        {
            if (_liveReleased || _shot == null)
                return;
            _liveReleased = true;
            _ball.SyncToHand();
            ShotPlan plan = _shot.Fire(_liveIntent);
            _recorder.MarkRelease();
            _audio.PlayRelease();
            Debug.Log("[Shot] " + plan.Describe());
        }

        void OnShotReleased(ShotPlan plan)
        {
            // Keep the wide framing; the lens tightens toward the rim as the ball arrives.
            _cameras.Cut(new BehindShooterShot { Tight = false, LookMix = 0.55f }, 0.9f);
        }

        void OnShotContact(ShotContactKind kind, Vector3 point)
        {
            _audio.HandleContact(kind);
            _recorder.MarkContact(kind, point);
            if (kind == ShotContactKind.Rim && _net != null)
                _net.Rattle(point - _hoop.RimCenter, 0.6f);
            if (kind == ShotContactKind.Rim || kind == ShotContactKind.Backboard)
                _cameras.Shake(0.012f, 9f);
        }

        void OnShotResultLocked(ShotResultState result)
        {
            _liveLocked = true;
            _liveResult = result;
            bool made = result.Final == ShotOutcome.Make;
            _recorder.MarkLock(made);

            if (made && _arena != null && _arena.CrowdRoot != null)
            {
                var crowdAnim = _arena.CrowdRoot.GetComponent<CrowdAnimator>();
                if (crowdAnim != null) crowdAnim.React(true);
            }

            if (made)
            {
                _recorder.MarkCrossing();
                if (_net != null)
                    _net.Swish();
                _audio.PlayBasket();
                _cameras.Shake(0.01f, 8f);
            }
            _ui.ShowShotCallout(ShotCallout.For(_pendingMath));
            _shooterAnim.SetPose(made ? HumanoidPose.Celebrate : HumanoidPose.Frustrated);
        }

        // ------------------------------------------------------------------ settle (unchanged)

        void Settle(RoundMathResult result, TimingZone timing)
        {
            _lastResult = result;
            string payoutTx = null;
            if (result.Won && result.Payout > 0)
            {
                var credit = WalletService.Credit(new WalletTxContext
                {
                    RoundId = result.RoundId,
                    Mode = result.Mode,
                    CharacterId = result.CharacterId,
                    Reason = result.IsDoubleOrNothing ? WalletTxReason.DoubleOrNothingPayout : WalletTxReason.Payout,
                    Amount = result.Payout
                });
                payoutTx = credit.TransactionId;
            }

            MoneyAppBridge.RaiseRoundSettled(new RoundSettledEvent
            {
                Result = result,
                PayoutTransactionId = payoutTx,
                BalanceAfter = WalletService.Balance,
                Timing = timing
            });

            bool canDon = result.Won && _cfg.doubleOrNothing.enabled && WalletService.Balance + 0.0001 >= result.Payout;
            _audio.Play(result.Won ? AudioCue.BetWin : AudioCue.BetLose);
            
            if (_arena != null && _arena.CrowdRoot != null)
            {
                var crowdAnim = _arena.CrowdRoot.GetComponent<CrowdAnimator>();
                if (crowdAnim != null) crowdAnim.StopCheer();
            }

            _ui.ShowResult(result, timing, WalletService.Balance >= _ui.Stake, canDon);
            _ui.RefreshBalance(WalletService.Balance);
        }

        // ------------------------------------------------------------------ lobby / menus

        void ReturnToLobby()
        {
            StopAllCoroutines();
            if (_shot != null)
                _shot.Abort();
            if (_recorder != null)
                _recorder.Abort();
            if (_shooterAnim != null)
                _shooterAnim.CancelShot();
            _paused = false;
            Time.timeScale = 1f;
            _roundLive = false;
            _activeRequest = null;
            _pendingMath = null;
            _look.SetReplay(false);
            TeardownLineupKeepSelected();
            SpawnShooter(_ui.CurrentCharacter());
            PlaceActors(_ui.SelectedMode, ThreePointSpot.Top);
            ApplyLobbyCamera();
            if (_shooterAnim != null)
                _shooterAnim.SetPose(HumanoidPose.TripleThreat);
            
            if (_arena != null && _arena.CrowdRoot != null)
            {
                var crowdAnim = _arena.CrowdRoot.GetComponent<CrowdAnimator>();
                if (crowdAnim != null) crowdAnim.StopCheer();
            }

            _ui.ShowLobby();
            _ui.RefreshBalance(WalletService.Balance);
        }

        void PreviewFromUi()
        {
            if (_ui == null)
                return;
            _previewCharacter = _ui.SelectedCharacter;
            _previewMode = _ui.SelectedMode;
            _previewScreen = _ui.CurrentScreen;
            if (_ui.CurrentScreen == UiScreen.Roster)
            {
                UpdateRosterLineup();
                return;
            }

            TeardownLineupKeepSelected();
            SpawnShooter(_ui.CurrentCharacter());
            PlaceActors(_ui.SelectedMode, ThreePointSpot.Top);
            if (_ui.CurrentScreen == UiScreen.Main)
                EnsureDefender(false);
            ApplyLobbyCamera();
            if (_shooterAnim != null)
                _shooterAnim.SetPose(HumanoidPose.TripleThreat);
        }

        void UpdateRosterLineup()
        {
            var all = CharacterCatalog.All;
            if (all == null || all.Length == 0)
                return;
            EnsureLineup(all);
            EnsureDefender(false);

            int sel = Mathf.Clamp(_ui.SelectedCharacter, 0, all.Length - 1);
            Vector3 stand = CourtMetrics.FreeThrowSpot;
            for (int i = 0; i < all.Length; i++)
            {
                if (_lineup[i] == null || _lineup[i].Root == null)
                    continue;
                bool show = i == sel;
                _lineup[i].Root.gameObject.SetActive(show);
                if (!show)
                    continue;
                _lineup[i].Root.position = stand;
                if (_lineupAnim[i] != null)
                    _lineupAnim[i].SetPose(HumanoidPose.TripleThreat);
            }

            _shooter = _lineup[sel];
            _shooterAnim = _lineupAnim[sel];
            ApplyLobbyCamera();
            FaceLineupToCamera();
            AttachBall();
        }

        void FaceLineupToCamera()
        {
            if (_shooter == null || _shooter.Root == null || !_shooter.Root.gameObject.activeInHierarchy)
                return;
            Vector3 camPos = _cameras != null && _cameras.Camera != null
                ? _cameras.Camera.transform.position
                : _shooter.Root.position + Vector3.back * 3f;
            _shooter.LookAt(camPos);
        }

        void EnsureLineup(CharacterDefinition[] all)
        {
            if (_lineup != null && _lineup.Length == all.Length)
            {
                bool ok = true;
                for (int i = 0; i < all.Length; i++)
                    if (_lineup[i] == null || _lineup[i].Root == null)
                        ok = false;
                if (ok)
                    return;
            }

            var next = new HumanoidRig[all.Length];
            var nextAnim = new ShooterAnimator[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                if (_shooter != null && _shooter.Root != null && _shooter.Definition == all[i])
                {
                    next[i] = _shooter;
                    nextAnim[i] = _shooterAnim;
                    continue;
                }
                if (_lineup != null && i < _lineup.Length && _lineup[i] != null && _lineup[i].Root != null && _lineup[i].Definition == all[i])
                {
                    next[i] = _lineup[i];
                    nextAnim[i] = _lineupAnim[i];
                    continue;
                }
                next[i] = BuildRig(all[i], false, out nextAnim[i]);
            }
            _lineup = next;
            _lineupAnim = nextAnim;
        }

        void TeardownLineupKeepSelected()
        {
            if (_lineup == null)
                return;
            CharacterDefinition keep = _ui != null ? _ui.CurrentCharacter() : null;
            HumanoidRig selected = null;
            ShooterAnimator selectedAnim = null;
            for (int i = 0; i < _lineup.Length; i++)
            {
                if (_lineup[i] == null || _lineup[i].Root == null)
                    continue;
                if (keep != null && _lineup[i].Definition == keep)
                {
                    selected = _lineup[i];
                    selectedAnim = _lineupAnim[i];
                    continue;
                }
                Destroy(_lineup[i].Root.gameObject);
            }
            _lineup = null;
            _lineupAnim = null;
            if (selected != null)
            {
                _shooter = selected;
                _shooterAnim = selectedAnim;
            }
        }

        void TogglePause()
        {
            if (_ui == null)
                return;
            if (!_paused)
            {
                if (_ui.CurrentScreen != UiScreen.Hud && _ui.CurrentScreen != UiScreen.Pause)
                    return;
                _paused = true;
                Time.timeScale = 0f;
                _ui.ShowPause();
            }
            else
            {
                _paused = false;
                Time.timeScale = 1f;
                _ui.HidePause();
            }
        }

        void QuitToMenu()
        {
            _paused = false;
            Time.timeScale = 1f;
            ReturnToLobby();
            _ui.ShowMainMenu();
            ApplyLobbyCamera();
        }

        void ApplyLobbyCamera()
        {
            if (_cameras == null)
                return;
            if (_ui != null && _ui.CurrentScreen == UiScreen.Main)
            {
                SetTitleCastHidden(true);
                _cameras.Cut(new TitleShot(), 1.2f);
                return;
            }

            SetTitleCastHidden(false);
            if (_shooter == null || _shooter.Root == null)
                return;
            if (_ui != null && _ui.CurrentScreen == UiScreen.Roster)
                _cameras.Cut(new ShowcaseShot(), 0.8f);
            else
                _cameras.Cut(new BehindShooterShot(), 0.8f);
        }

        void SetTitleCastHidden(bool hidden)
        {
            if (_shooter != null && _shooter.Root != null)
                _shooter.Root.gameObject.SetActive(!hidden);
            if (hidden && _defender != null && _defender.Root != null)
                _defender.Root.gameObject.SetActive(false);
            if (_ball != null)
                _ball.gameObject.SetActive(!hidden);
            if (!hidden || _lineup == null)
                return;
            for (int i = 0; i < _lineup.Length; i++)
                if (_lineup[i] != null && _lineup[i].Root != null)
                    _lineup[i].Root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ actors

        HumanoidRig BuildRig(CharacterDefinition def, bool defenderPalette, out ShooterAnimator anim)
        {
            Transform parent = _arena != null && _arena.ActorsRoot != null ? _arena.ActorsRoot : transform;
            HumanoidRig rig = PlaceholderCharacterBuilder.Build(def, parent, defenderPalette);
            GameLayers.SetLayerRecursive(rig.Root.gameObject, GameLayers.Player);
            anim = rig.Root.gameObject.AddComponent<ShooterAnimator>();
            anim.Rig = rig;
            anim.LookTarget = _hoop.RimCenter;
            anim.HasLookTarget = true;
            anim.SetPose(HumanoidPose.TripleThreat, true);
            return rig;
        }

        void SpawnShooter(CharacterDefinition def)
        {
            if (_shooter != null && _shooter.Definition == def && _shooter.Root != null)
            {
                AttachBall();
                return;
            }
            if (_shooterAnim != null)
                _shooterAnim.ReleaseReached -= OnShooterRelease;
            if (_shooter != null && _shooter.Root != null)
                Destroy(_shooter.Root.gameObject);
            _shooter = BuildRig(def, false, out _shooterAnim);
            _shooterAnim.ReleaseReached += OnShooterRelease;
            AttachBall();
        }

        void EnsureDefender(bool needed)
        {
            if (!needed)
            {
                if (_defender != null && _defender.Root != null)
                    _defender.Root.gameObject.SetActive(false);
                return;
            }

            if (_defender == null || _defender.Root == null)
                _defender = BuildRig(_defenderDef, true, out _defenderAnim);
            _defender.Root.gameObject.SetActive(true);
            _defenderAnim.SetPose(HumanoidPose.Idle);
        }

        void PlaceActors(GameModeType mode, ThreePointSpot spot, DefenderScenario scenario = DefenderScenario.None)
        {
            Vector3 pos = mode == GameModeType.ThreePoint ? CourtMetrics.GetThreePointSpot(spot) : CourtMetrics.FreeThrowSpot;
            _shooter.Root.position = pos;
            _shooter.LookAt(_hoop.RimCenter);
            if (_shooterAnim != null)
            {
                _shooterAnim.LookTarget = _hoop.RimCenter;
                _shooterAnim.HasLookTarget = true;
            }

            EnsureDefender(mode == GameModeType.ThreePoint);
            if (mode == GameModeType.ThreePoint && _defender != null)
            {
                _defender.Root.position = DefenderStandPosition(pos, scenario);
                _defender.LookAt(_shooter.Root.position);
            }

            AttachBall();
        }

        Vector3 DefenderStandPosition(Vector3 shooterPos, DefenderScenario scenario)
        {
            Vector3 toHoop = _hoop.RimCenter - shooterPos;
            toHoop.y = 0f;
            if (toHoop.sqrMagnitude < 0.01f)
                toHoop = Vector3.forward;
            toHoop.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toHoop);
            float gap = 1.2f;
            float sideOff = 0.35f;
            switch (scenario)
            {
                case DefenderScenario.CleanShot: gap = 2.2f; sideOff = 0.55f; break;
                case DefenderScenario.ContestedShot: gap = 1.05f; sideOff = 0.28f; break;
                case DefenderScenario.VeryCloseContest: gap = 0.7f; sideOff = 0.18f; break;
                case DefenderScenario.Block: gap = 0.55f; sideOff = 0.08f; break;
                case DefenderScenario.SlightDeflection: gap = 0.8f; sideOff = 0.22f; break;
            }
            return shooterPos + toHoop * gap + side * sideOff;
        }

        static string SpotLabel(ThreePointSpot spot)
        {
            switch (spot)
            {
                case ThreePointSpot.LeftWing: return "LEFT WING";
                case ThreePointSpot.RightWing: return "RIGHT WING";
                case ThreePointSpot.LeftCorner: return "LEFT CORNER";
                case ThreePointSpot.RightCorner: return "RIGHT CORNER";
                default: return "TOP OF THE KEY";
            }
        }

        void AttachBall()
        {
            if (_ball == null || _shooter == null)
                return;
            if (_shot != null && _shot.IsBusy)
                return;
            _ball.gameObject.SetActive(true);
            _ball.Hold(_shooter.BallHand != null ? _shooter.BallHand : _shooter.Root, Vector3.zero);
        }
    }
}
