# Basketball Betting — engineering handoff (2026-09-13)

## What changed

The betting system is untouched: `Scripts/Betting/*`, `Scripts/Core/GameTypes.cs`, and the
`StartRound` / `Settle` flow inside `GameManager` are the same code and produce the same
`RoundMathResult`, wallet transactions and `MoneyAppBridge` events as before.

Everything that turns that result into a basketball shot was rebuilt as separate systems:

| Responsibility | Where | Notes |
|---|---|---|
| Betting → gameplay adapter | `Scripts/Shot/ShotTypes.cs` (`ShotIntent.FromBet`) | Only place that reads `RoundMathResult`. Outcome is always `Won`; `Animation` only picks the style. |
| Trajectory math | `Scripts/Shot/ShotSolver.cs` | Closed form, corrected for PhysX's semi-implicit Euler step. Unit-tested. |
| Shot planning | `Scripts/Shot/ShotPlanner.cs`, `ShotTuning.cs` | Style → aim point, entry angle, spin, predicted rim-plane crossing, expected first contact. Seeded by the round id, so a round is reproducible. |
| Ball | `Scripts/Ball/Basketball.cs` | Rigidbody + sphere collider, CCD, layer-based contacts, sweep tunnel guard. |
| Guidance | `Scripts/Shot/ShotGuidance.cs` | Bounded assists: tracking (≤2.5 m/s²) before contact, funnel for makes after contact, guard for misses. |
| Goal | `Scripts/Hoop/Hoop.cs`, `HoopNet.cs`, `TorusMeshBuilder.cs` | Torus mesh collider, thick glass box, entry/exit trigger gates, `HoopGeometry` as the single source of truth. Net is visual only. |
| Detection | `Scripts/Shot/BasketDetectorCore.cs`, `Scripts/Hoop/BasketDetector.cs` | Armed → AboveRim → Through → Confirmed / Missed; locks once per shot id. |
| Result authority | `Scripts/Shot/ShotResultState.cs` | Final = betting intent. A physical mismatch is logged as an error and counted (tests fail on it). |
| Lifecycle | `Scripts/Shot/ShotController.cs` | Initiated → Targeted → Released → Simulating → Interacting → ResultLocked → Finished. |
| Animation | `Scripts/Characters/ShooterAnimator.cs`, `BodyPose.cs`, `RigIK.cs`, `ShotAnimationProfile.cs` | Keyed shooting timeline with an explicit release event; the ball launches from the hand at that frame. |
| Cameras | `Scripts/Cameras/*` | One camera, data-driven shots (behind shooter, close-up, ball follow, side tracking, rim cam, backboard cam, wide arena, reaction, title, showcase), blended by `GameCameraDirector`. |
| Replay | `Scripts/Presentation/*` | Transform tape → ghost actors → result-driven cut list. No physics, no `Time.timeScale`. |
| UI | `Scripts/UI/UiKit.cs`, `GameUI.cs` | Token-based kit, layered panels, tweened transitions, same public contract. |
| Scene | `Editor/ArenaSceneBuilder.cs` | Rebuilds `Assets/Scenes/SampleScene.unity` from code (court, goals, batched stands/crowd, lighting rig, probe, camera, ball, host). |

Removed (obsolete, conflicting): `ShotPresentation`, `ShotTruth`, `ShotOutcomeDriver`, the whole
`Scripts/Camera` folder, `RimHole`, `HoopNetCloth`, `ArenaBuilder`, `ScenePreviewActors`,
`MixamoPoseDriver`, `MixamoPoseCapture`, `RimHoleEditor`, `BettingSceneAssembler`, `RestorePoints`,
and the 644 generated per-renderer materials.

## Project settings touched

* `TagManager`: layers 8 Ball, 9 Hoop, 10 Backboard, 11 Court, 12 Player, 13 Arena, 31 Preview.
* `TimeManager`: fixed step 0.01 s. `DynamicsManager`: solver 12/8, bounce threshold 0.8.
* `PC_RPAsset`: opaque texture off, shadow distance 45, 2 cascades, 8 lights per object.
* `DefaultVolumeProfile`: removed leaked URP test components (they produced "missing script" warnings).

## How to run / verify

1. Open the project in Unity 6 (6000.3 or newer). The scene is `Assets/Scenes/SampleScene.unity`.
2. Menu **Basketball Betting → Build Arena Scene** regenerates the scene and art assets from code.
3. Tests (Window → General → Test Runner, or batch mode):
   * EditMode: `ShotSolverTests`, `ShotPlannerTests`, `BasketDetectorCoreTests`.
   * PlayMode: `ShotOutcomeMatrixTests` (16 styles × 4 spots × 12 seeds, no tunnelling, no double locks),
     `GameFlowTests` (boots the scene and plays full rounds through the UI), `ShotDiagnosticsTests`,
     `UiCaptureTests` (set `BB_CAPTURE_UI=1` to write screenshots).
4. Debug: `[Shot]` log line per shot prints the plan (velocity, entry angle, predicted crossing, first contact).

## Tunables

* `Assets/BasketballBetting/Settings/ShotTuning.asset` — every planner/guidance/detector number.
* `GameManager` inspector — replay policy, free-throw and jump-shot motion timings.
* `Hoop` inspector — rim inner radius, iron radius, glass size.

## Known limits

* Only three FBX models carry a skeleton (Blaze player, Kingsley game, Kingsley street). The roster maps
  every character onto those; the six rigid Tripo conversions cannot animate.
* Legacy UGUI `Text` (no TextMeshPro): letter spacing is not available.
* The far goal is visual only (no `Hoop` component): the game shoots on the play end.
