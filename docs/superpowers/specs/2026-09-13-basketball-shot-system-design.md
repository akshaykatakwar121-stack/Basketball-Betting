# Basketball Betting — Gameplay, Camera and Presentation Re-engineering

Date: 2026-09-13
Project: `Basketball Betting` (Unity 6000.3.13f1 project, URP 17.3, Input System 1.19, UGUI 2.0)
Status: design for implementation. The betting system is out of scope and must not change.

---

## 1. Audit summary (what exists today)

### 1.1 Betting system — PRESERVED, UNTOUCHED

| File | Role |
|---|---|
| `Scripts/Betting/BettingEngine.cs` | `Resolve(RoundRequest, TimingZone?)` → `RoundMathResult { Won, Payout, Multiplier, Animation, DefenderScenario … }`. `BindPresentation()` may swap a losing `Animation` to `AirBall` (presentation only). |
| `Scripts/Betting/BettingConfig.cs` | JSON config, RTP validation, money formatting. |
| `Scripts/Betting/Wallet.cs` | `WalletService`, `LocalWalletProvider`, `MoneyAppBridge` events. |
| `Scripts/Core/GameTypes.cs` | `RoundRequest`, `RoundMathResult`, `ShotAnimationId`, `CourtMetrics`. |
| `GameManager.StartRound / Settle` | Debit → `Resolve` → (presentation) → Credit → `RoundSettled`. |

The betting decision is `RoundMathResult.Won` plus a presentation hint `RoundMathResult.Animation`.
The new gameplay consumes exactly these two fields and never writes to a `RoundMathResult`.
`StartRound`/`Settle` in `GameManager` keep their code verbatim; only the presentation call between them changes.

### 1.2 Existing basketball system — REPLACED

`Scripts/Shooting/ShotPresentation.cs` (1292 lines) contains four generations of ball logic stacked on top of each other, all switched by the global `BallPlayPolicy.Current` flags:

* `ShotPathLibrary` — hand-keyed spline paths (dead: never called any more, but still compiled).
* `ShotPhysics` / `ShotTruth` — two competing launch solvers (`ShotTruth.Enabled` picks one).
* `BallController` — a 900-line monobehaviour that: parents to a hand, launches, runs a **scripted torus rim solver** (`ResolveRimSolid` / `ResolveRimSweep`) that teleports the rigidbody every fixed step, a **scripted glass clamp** (`BounceOffGlass`), four different scoring tests (`TryHolePass`, `WentInForBet`, `TryScoreTrigger`, legacy "bag" scoring), name-string collider classification (`col.name == "Backboard"`, `StartsWith("RimIron")`), and `Physics.IgnoreCollision` toggling of rim/backboard **on purpose when the bet is a win** (`ShotOutcomeDriver.ShouldIgnoreHoop`), which is why made shots visibly pass *through* the iron.
* `ShotOutcomeDriver` — "bet owns basket": on a win the ball ignores the rim entirely and `MarkNet()` fires from a loose radial test (`WentInForBet`: radial < 0.4 m and `FlightClock > 0.95`), which is why makes register while the ball is nowhere near the hole.
* `GameManager.ObservedMade()` evaluates three different truths depending on flags; it is evaluated **again after the replay** while the hidden live ball is still simulating in slow motion, so the celebration pose can disagree with the payout.
* `ShotTape` (camera folder) both records the shot and re-classifies the result (`PathWentIn`), creating a circular dependency `ShotTape → ShotTruth → BallPlayPolicy → BallController`.
* Slow motion is written by two classes (`SlowMotionController`, `BasketballShotCinematicController`) through `Time.timeScale`/`Time.fixedDeltaTime`, and the physics step therefore changes mid-flight.
* Physics: `DynamicsManager` default solver 6/1, fixed step 0.02 s. Ball CCD is `ContinuousDynamic` but the rim is a **non-convex MeshCollider torus whose collisions are deliberately ignored on wins**, so tunnelling on misses and pass-through on makes are both by construction, not by accident.

Verdict: nothing in `ShotPresentation.cs`, `ShotTruth.cs`, `ShotOutcomeDriver.cs`, `RimHole.cs` (scoring part), `ShotTape.cs`, `BallPlayPolicy.cs` is worth keeping. They are deleted, not patched.

### 1.3 Characters / animation

* No `.anim`, no `AnimatorController`, no `Avatar` anywhere. "Animation" is `ProceduralAnimator` + `MixamoPoseDriver`: hard-coded pose targets with exponential smoothing and a hand-rolled two-bone IK.
* Only three FBX sources have a skeleton and skin weights: `blaze+player+3d+model.fbx`, `kingsley game style 3d+model.fbx`, `kingsley street style 3d+model.fbx` (all `mixamorig:` bones). The six Tripo "game/street style" conversions are single rigid meshes with **no bones** — 4 of the 5 roster characters currently point at those, so they never move.
* Release point: `HumanoidRig.BallHand` (a child of the right hand). `GameManager` snaps `ShootLoad → ShootRelease → FollowThrough` with hard-coded waits; the ball is released by `_ball.ReleaseToWorld()` on a timer, not by the animation.

### 1.4 Cameras / replay

* `CameraDirector` (1008 lines): one camera, 12 states, 7 "packages", ~16 dead public methods, magic numbers everywhere, aspect-ratio blends inline.
* Replay = `ShotTape` transform tape + ghosts, driven by a 200-line coroutine inside `GameManager`. It leaves the live ball simulating (hidden) in slow motion during replay and re-reads scoring afterwards (see 1.2).

### 1.5 UI

`GameUI.cs` (1211 lines): procedural UGUI, seven screens, dark palette with translucent rectangles, `Outline` on every label, no TextMeshPro. Public surface used by `GameManager` is small and well defined (see §7).

### 1.6 Environment / rendering

Filled in from the environment audit (see §6): procedural arena built by `ArenaBuilder`, 644 generated per-instance materials (crowd), URP PC/Mobile assets, single volume profile.

---

## 2. Goals and non-goals

Goals (from the brief, in priority order):

1. Reliable basketball engineering: one authoritative shot pipeline, no pass-through, no double scoring, no contradictory results.
2. Betting untouched; gameplay consumes `Won` (+ `Animation` as a style hint).
3. Shot/animation synchronisation with a real release event.
4. Camera system and cinematic replay rebuilt as data-driven shots.
5. AAA visual direction: dark arena, strong overhead lighting, rich court, premium ball.
6. Premium UI replacing the translucent-box language, keeping every betting control working.
7. Real-time performance.

Non-goals: new basketball rules, fouls, dribbling, shot clocks, new betting outcomes, new game modes.

---

## 3. Architecture

```
BettingEngine (untouched)
      │  RoundMathResult { Won, Animation }
      ▼
ShotIntent.FromBet()                      ← the ONLY betting→gameplay adapter
      │  ShotIntent { Outcome: Make|Miss, Style, Seed }
      ▼
ShotController (lifecycle state machine, MonoBehaviour)
      ├─ ShooterAnimator ──── release event ──▶ release pose/hand position
      ├─ ShotPlanner  ─── ShotSolver (pure math) ──▶ ShotPlan (v0, ω0, aim, flight time, contacts)
      ├─ Basketball (Rigidbody, CCD, layers)  ◀── ShotGuidance (bounded corrective force, pre-contact only)
      ├─ Hoop (rim/backboard/net colliders + score gates + HoopGeometry)
      ├─ BasketDetector (state machine: Armed → AboveRim → Through → Confirmed | Missed, locked)
      └─ ShotResultState (Intent + Observed → Final, locked once, mismatch telemetry)
      │  events: Released, BallContact(kind), ResultLocked(ShotResult), ShotFinished
      ▼
Presentation (read-only consumers)
      ├─ ShotRecorder → ShotRecording (ball + bones per frame)
      ├─ ReplayDirector (ghost playback, unscaled time, shot list by result)
      ├─ CameraDirector (gameplay + replay shots, one Camera)
      ├─ GameUI (betting screens preserved, new visual system)
      └─ BasketballAudio (existing, event-fed)
```

Rules enforced by the structure:

* Only `ShotController` and its `Basketball` may write to the ball rigidbody. Replay uses ghosts.
* `BasketDetector` emits at most one confirmation per `shotId`; `ShotResultState` locks on the first result and rejects later writes.
* Nothing touches `Time.timeScale` except `PauseController` (0 or 1). Slow motion is a **playback rate** inside `ReplayDirector`.
* No name-string lookups: rim/backboard/floor are identified by **layers** (`Ball`, `Hoop`, `Backboard`, `Court`) and by component references.

---

## 4. Shot pipeline in detail

### 4.1 ShotIntent (betting → gameplay)

```csharp
public enum ShotOutcome { Make, Miss }
public enum ShotStyle {
    // makes
    Swish, RimIn, BankIn, RattleIn, HighArc,
    // misses
    Short, Long, Left, Right, RimOut, InAndOut, BackRim, BankMiss, AirBall, Blocked, Deflected
}
public readonly struct ShotIntent { ShotOutcome Outcome; ShotStyle Style; int Seed; string RoundId; }
public static ShotIntent ShotIntent.FromBet(RoundMathResult math, TimingZone timing)
```

Mapping (`ShotAnimationId` → style): Swish→Swish, RimIn→RimIn, BackboardIn→BankIn, MultiRimIn→RattleIn, HighArc→HighArc, ContestedMake/BuzzerStyle→Swish (or RimIn by seed), RimOut→RimOut, BackRimMiss→BackRim, InAndOut→InAndOut, BackboardMiss→BankMiss, AirBall→AirBall (Short/Left/Right/Long picked by timing zone and seed), DefenderBlock→Blocked, DefenderDeflection→Deflected. `Outcome` is always `math.Won ? Make : Miss`; a style can never flip the outcome. `Seed` = hash of `RoundId` so a round is reproducible.

### 4.2 ShotSolver (pure, testable, no MonoBehaviour)

* `LaunchVelocity(from, to, flightTime, gravity, fixedStep)` — closed form with the semi-implicit-Euler correction `v0 = Δ/T − g·(T + dt)/2` so the rigidbody lands on the analytic target at the discrete level.
* `FlightTimeForEntryAngle(from, to, entryAngleDeg, gravity)` — picks T so the descent angle at the target equals the requested entry angle.
* `Predict(from, v0, t)`, `TimeToPlane(from, v0, planeY)`, `ApexHeight`.
* `ReflectOffGlass(vel, restitution)` for bank planning.
* Determinism: all randomness comes from `System.Random(seed)` inside `ShotPlanner`; no `UnityEngine.Random`.

### 4.3 ShotPlanner

Inputs: `ShotIntent`, `HoopGeometry` (rim centre, inner radius 0.2286, iron tube radius, backboard plane, glass restitution), `BallSpec` (radius 0.12, mass 0.62), release point, `ShotVariation` tunables. Output: `ShotPlan`.

Geometric guarantees (with r = ball radius, R = rim inner radius, θ = entry angle):

* **Swish / HighArc**: aim point on the rim plane inside the *effective opening* `R − r/sin θ`, entry angle 48–56° (HighArc 58–64°), variation ellipse ±0.03 m along-shot, ±0.045 m lateral. At 50° the opening leaves ≥ 6 cm of clearance from the iron, so the ball geometrically cannot touch the rim.
* **RimIn**: aim centre at +(R − r + 0.03..0.06) along-shot (inside back iron), θ 54–60°, reduced speed. The ball clips the inside of the back iron and drops. Funnel assist (4.5) covers the post-contact phase.
* **RattleIn**: like RimIn with lateral ±0.08 so it rattles side-to-side before dropping.
* **BankIn**: aim on the glass; the glass contact height and lateral offset are chosen by scanning candidates and simulating `ReflectOffGlass` so the post-bounce path crosses the rim plane within `R − r` of the centre.
* **Short**: aim −(R + 0.02..0.08) along-shot → front-iron hit, kicks back toward the shooter.
* **Long / BackRim**: flat entry (38–44°), aim +(R + 0.01..0.05) → tops the back iron, bounces up and away.
* **Left / Right**: lateral ±(R + 0.04..0.10) → side iron deflection.
* **RimOut / InAndOut**: aim just inside the back iron with lateral spin; miss guard (4.5) ensures exit.
* **BankMiss**: glass contact ≥ 0.55 m above rim and ≥ 0.35 m lateral → reflected path lands outside `R + r`.
* **AirBall**: crossing point outside `R + r + 0.2`, no rim contact expected.
* **Blocked / Deflected** (3-point mode only): normal make-style plan plus a scheduled `ContactEvent` at t = 0.11 s / 0.18 s applying the defender's impulse; the plan's post-impulse path is checked to land outside the hoop.

Every plan carries `ExpectedContacts` (ordered list: Rim / Glass / None) and `PlaneCrossing` (predicted point, time, inside/outside) so tests and the debug overlay can compare prediction with what happened.

### 4.4 Basketball (rigidbody)

* Sphere collider r = 0.12, mass 0.62, `CollisionDetectionMode.ContinuousDynamic`, `Interpolate`, solver iterations 16/8, `maxDepenetrationVelocity` 5, `linearDamping` 0 in flight, physics material bounce 0.62 / friction 0.55.
* Layer `Ball`. Collision matrix: Ball ↔ Hoop, Backboard, Court, Player-capsules-off.
* States: `Held` (kinematic, follows hand transform each `LateUpdate`), `InFlight`, `Settled`. `Launch(ShotPlan)` sets position = hand position, velocity, angular velocity in one fixed step.
* Reports contacts by layer through `event Action<BallContact>` (Rim / Backboard / Floor / Net / Defender) with debounce 60 ms per kind.
* Belt-and-braces anti-tunnel: each `FixedUpdate` sphere-casts from the previous to the current position against `Hoop|Backboard`; if a hit is found that PhysX did not report, the ball is placed at the hit and its velocity reflected. Logged in tests as `TunnelGuardTriggered` (must be 0 in the suite).
* Fixed timestep is set to 1/100 s by `PhysicsSettingsApplier` (≤ 12 m/s × 0.01 s = 12 cm per step = one radius).

### 4.5 ShotGuidance (deterministic realisation)

Bounded assist that keeps the physical ball on the plan without visible faking:

* **Flight tracking** (pre-contact): every fixed step compares the rigidbody to `Predict(plan, t)`; applies `a = clamp(k_p·Δp + k_d·Δv, |a| ≤ 2.5 m/s²)`. Integration error and drag are ~1 cm; the correction is invisible. Off after the first contact.
* **Funnel (Make only, after a rim/glass contact)**: while the ball centre is within `R + 0.12` of the rim axis and within [−0.05, +0.45] m of the rim plane, apply acceleration toward the axis ≤ 7 m/s² and damp horizontal speed 12 %/step. Emulates the real "soft" back-iron drop. Off once below the plane.
* **Guard (Miss only)**: if the ball centre enters the cylinder above the plane, push radially outward (≤ 9 m/s²) toward the planned exit side; if it would cross the plane inside `R`, its horizontal velocity is redirected outward before the crossing. Visually reads as rim spin-out.

All three are disabled for the replay ghost (no physics there at all) and all gains live in a `ShotTuning` ScriptableObject.

### 4.6 Hoop and BasketDetector

`Hoop` component (on the goal root) owns:

* `RimCollider` — MeshCollider torus (48×16, non-convex, static) on layer `Hoop`, iron physics material (bounce 0.55, friction 0.4).
* `BackboardCollider` — BoxCollider 1.83 × 1.07 × 0.08 (thick to defeat tunnelling) on layer `Backboard`, glass material (bounce 0.68).
* `NetVisual` — the existing cloth net keeps its visuals; its colliders are removed (cloth must never affect the ball).
* `EntryGate` — trigger box 0.6 × 0.15 × 0.6 centred 0.20 m above the rim plane, `ExitGate` — trigger 0.5 × 0.3 × 0.5 centred 0.30 m below.
* `HoopGeometry Geometry` — the numbers the planner and detector use (single source of truth; the old `RimHole` copies are gone).

`BasketDetector` state machine (per shot id):

```
Idle ──Arm(shotId)──▶ Armed ──ball enters EntryGate from above──▶ AboveRim
AboveRim ──centre crosses rim plane downward inside R (interpolated prev→now on rigidbody position)──▶ Through
Through ──ball enters ExitGate OR centre < plane − 0.25 while radial < R+0.05──▶ Confirmed (locked)
Armed/AboveRim ──ball below plane − 0.5 with radial > R, or Floor contact, or 6 s timeout──▶ Missed (locked)
```

Guarantees: `Confirmed` fires once per shot id; later trigger enters, rim rattles and re-entries are ignored once locked; a ball that bounces back *up* out of the cylinder from `Through` without reaching `ExitGate` within 0.5 s reverts to `AboveRim` (rim-out), it never confirms.

### 4.7 ShotResultState

```csharp
sealed class ShotResultState {
    ShotIntent Intent;          // locked at release
    ShotObservation Observed;   // None | Make | Miss  (from BasketDetector)
    ShotOutcome Final;          // == Intent.Outcome, set when Observed arrives or timeout
    bool Locked; bool Mismatch;  // Mismatch = Observed != Intent → Debug.LogError + telemetry event
}
```

The gameplay result is the intent (the bet was settled on it). The observation exists to time presentation (net moment, callout) and to prove correctness: the automated suite fails on any mismatch, so the shipped game never shows one.

### 4.8 ShotController lifecycle

```
Initiated → Targeted (plan built from the CURRENT hand position at the release event)
→ Released (Launch) → Simulating (guidance) → Interacting (contacts) → Detected
→ ResultLocked → Presenting (callout, net pulse, celebration) → Finished
```

Coroutine `IEnumerator Run(ShotRequest req, Action<ShotResult> onLocked)`; the round flow in `GameManager` yields on it and then calls the untouched `Settle()`.

---

## 5. Animation and release sync

`ShooterAnimator` replaces `ProceduralAnimator.SetPose` snapping with a **timeline** of phases: `Set → Dip → Rise → Release → FollowThrough → Land → Recover`, durations in a `ShooterAnimationProfile`. The pose driver keeps the two-bone IK but is driven by phase curves (knee bend, hip drop, elbow extension, wrist flick, root jump height, landing absorb, weight shift toward the hoop). `Release` is an explicit **event at a normalised time**; `ShotController` subscribes, builds the plan from the hand position **at that instant**, and launches in the same fixed step. The ball is parented to the hand until then, so it cannot float, spawn away, or release late.

Roster fix: every roster character maps to a rigged model (`blaze_player`, `kingsley_game`, `kingsley_street`), with jersey/skin colours from the `CharacterVisualProfile`. The rigid Tripo meshes remain available for the roster showcase only.

---

## 6. Visual direction (as built by `Editor/ArenaSceneBuilder.cs`)

The old scene was 770 objects with 596 unique materials and six shadow-casting lights. The rebuilt scene is
generated from code: a court quad with a painted base map (crimson key, gold centre logo, dark stained apron)
plus a tiling maple detail albedo/normal, regulation goals (torus rim, glass with decal, steel frame, padded
stanchion, procedural net), four stands batched into one mesh per side (steps, seats, ~600 seated fans each,
a dozen draw calls per side instead of 540 renderers), a dark envelope with a lighting truss, an emissive LED
ribbon around the lower bowl, a centre-hung scoreboard, one shadow-casting directional key, four court rigs,
two goal spots, a cool rim light behind the glass, warm fills, four crowd washes, and a box-projected
reflection probe over the court. Ambient is a dark trilight; fog is a faint navy exponential.

Principles:

* Arena: dark envelope (0.02–0.05 albedo seating), 6 overhead spot rigs on Forward+ (one shadow-casting key), rim light from behind the goal, cool fill from the far stands, LED ribbon emissive at the lower bowl, subtle volumetric-like fog card behind the goal.
* Court: generated 2K maple albedo + normal + smoothness with a matte clear-coat feel (smoothness ~0.55, no mirror), painted lines/key/three-point arc as a separate decal layer, planar reflection probe over the court.
* Ball: generated leather albedo with channel seams, pebble normal, smoothness 0.35, spin readable through the seam pattern; motion-blur off on the ball layer.
* Post: ACES tonemap, bloom threshold 1.1 / intensity 0.35, vignette 0.25, slight film grain off, color grading warm-cool split.

---

## 7. UI

Keep the `GameUI` public contract exactly (`Create`, `CurrentCharacter`, `RefreshBalance`, `ShowMainMenu`, `ShowLobby`, `ShowHud`, `ShowResult`, `ShowPause/HidePause`, `ShowShotCallout`, `SetMeterVisible`, `SetBroadcast`, `UpdateMeter`, `CurrentScreen`, `SelectedMode`, `SelectedCharacter`, `Stake`, the eight events). Rebuild the visuals as a **UI kit** (`UiKit`: typography scale, spacing, colour tokens, 9-slice panels with real edges, gradient bars, icon glyphs, tweened transitions via unscaled time) and re-implement each screen on it. Betting controls (mode, wager ±, presets, lock-in, payout/odds, balance, double-or-nothing) keep their behaviour and are covered by a PlayMode test that drives them.

---

## 8. Testing and validation

* EditMode (`Tests/EditMode/`): `ShotSolverTests` (closed-form hits target for 200 random cases, discrete correction matches a step simulation), `ShotPlannerTests` (every make style's predicted crossing is inside `R − r/sinθ`; every miss style's is outside or hits iron; deterministic for equal seeds), `BasketDetectorTests` (pure-logic core: single confirm, re-entry ignored, rim-out reverts, timeouts).
* PlayMode (`Tests/PlayMode/`): `ShotOutcomeMatrixTests` builds the hoop + ball in an empty scene and fires **each style × 12 seeds × 3 distances (FT, top 3pt, corner 3pt)**; asserts `Observed == Intent` for all, tunnel guard = 0, no double confirmations, ball never inside the rim torus, ball never behind the glass; `SequentialShotsTest` fires 10 shots back-to-back and after a scene reload; `ReplayIsolationTest` asserts the live rigidbody state and result are unchanged by a replay.
* `ShotDebugOverlay` (F8): plan vs actual trajectory, detector state, result state, tunnel-guard counter.

---

## 9. Removal plan

Deleted once the new system passes the suite: `ShotPresentation.cs`, `ShotTruth.cs`, `ShotOutcomeDriver.cs`, `ShotTape.cs`, `BallPlayPolicy.cs`, `ReplayPolicy.cs`, `BasketballShotCinematicController.cs`, `ShotCinematicTuning.cs`, `CameraTuning.cs`, `CameraDebugOverlay.cs`, `ReplayCameraEffect.cs`, `RimHole.cs` + `RimHoleEditor.cs` (replaced by `Hoop`), `ScenePreviewActors.cs`, the `RestorePoints/` folder. `GameManager` shrinks to boot + lobby + betting round flow.

---

## 10. Performance budget

Forward+ renderer, one shadow-casting light (2 cascades, 40 m), additional lights without shadows, crowd batched into ≤ 4 shared materials (instancing) instead of 644 unique ones, reflection probe baked at start, physics layers restricted so the ball only tests Hoop/Backboard/Court, fixed step 0.01 s only while a shot is live (0.02 s otherwise), UI canvases split so the HUD meter does not rebuild the betting canvas.
