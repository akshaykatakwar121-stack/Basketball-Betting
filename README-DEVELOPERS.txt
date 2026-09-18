HOOPS — Basketball Betting (Unity project)
==========================================

Open
  1. Unzip, then in Unity Hub: Add project from disk -> select the "Basketball Betting" folder.
  2. Editor: Unity 6 (project was created with 6000.3.13f1; also verified on 6000.5.4f1).
     Modules needed: Windows Build Support (default) and, for browser builds, WebGL Build Support.
  3. First open re-imports everything (Library/ is not included) — expect several minutes.
  4. Scene: Assets/Scenes/SampleScene.unity. Press Play.

Read first
  docs/HANDOFF.md                                     what was rebuilt, where each system lives, tunables
  docs/superpowers/specs/2026-09-13-basketball-shot-system-design.md   full design + audit

Menus (Basketball Betting/...)
  Build Arena Scene   regenerates the scene, meshes and materials from code (Editor/ArenaSceneBuilder.cs)
  Build WebGL         browser build with the project's WebGL settings (Editor/WebGLBuilder.cs)

Tests (Window > General > Test Runner)
  EditMode: ShotSolverTests, ShotPlannerTests, BasketDetectorCoreTests
  PlayMode: ShotOutcomeMatrixTests (768 shots), GameFlowTests (full rounds through the UI)

Do not change without the owner's sign-off
  Assets/BasketballBetting/Scripts/Betting/*  (BettingEngine, BettingConfig, Wallet)
  StreamingAssets/BettingConfig.json and Resources/BettingConfig.json (odds / RTP)
