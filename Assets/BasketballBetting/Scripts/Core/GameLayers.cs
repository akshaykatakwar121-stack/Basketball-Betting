using UnityEngine;

namespace BasketballBetting
{
    /// <summary>
    /// Physics/render layers used by the game. Indices are mirrored in ProjectSettings/TagManager.asset.
    /// Colliders are classified by layer, never by GameObject name.
    /// </summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int Ball = 8;
        public const int Hoop = 9;       // rim iron + score gates
        public const int Backboard = 10;
        public const int Court = 11;     // floor and anything the ball may roll on
        public const int Player = 12;    // shooter/defender bodies (never collide with the ball)
        public const int Arena = 13;     // seats, walls, props (never collide with the ball)
        public const int Preview = 31;   // character-select studio

        public static readonly string[] Names =
        {
            "Ball", "Hoop", "Backboard", "Court", "Player", "Arena"
        };

        public static int Mask(int layer) => 1 << layer;

        /// <summary>Everything the ball is allowed to hit.</summary>
        public static int BallCollidesWith => Mask(Hoop) | Mask(Backboard) | Mask(Court);

        /// <summary>Layers swept by the tunnel guard.</summary>
        public static int GoalMask => Mask(Hoop) | Mask(Backboard);

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null)
                return;
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }
    }

    /// <summary>
    /// Applies the physics configuration the shot system relies on. Idempotent; called at boot
    /// and by the PlayMode test harness. Project files carry the same values so the editor
    /// matches, but nothing depends on the project files being edited by hand.
    /// </summary>
    public static class PhysicsSetup
    {
        public const float FixedStep = 0.01f;
        public const int SolverIterations = 12;
        public const int SolverVelocityIterations = 8;
        public const float BounceThreshold = 0.8f;

        public static void Apply()
        {
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            Physics.defaultSolverIterations = SolverIterations;
            Physics.defaultSolverVelocityIterations = SolverVelocityIterations;
            Physics.bounceThreshold = BounceThreshold;
            Time.fixedDeltaTime = FixedStep;

            // The ball only ever tests the goal and the court. Players and arena dressing are invisible to it.
            for (int layer = 0; layer < 32; layer++)
            {
                bool allowed = (GameLayers.BallCollidesWith & GameLayers.Mask(layer)) != 0;
                Physics.IgnoreLayerCollision(GameLayers.Ball, layer, !allowed);
            }
            // Players never collide with the goal or with each other (they are posed, not simulated).
            Physics.IgnoreLayerCollision(GameLayers.Player, GameLayers.Hoop, true);
            Physics.IgnoreLayerCollision(GameLayers.Player, GameLayers.Backboard, true);
            Physics.IgnoreLayerCollision(GameLayers.Player, GameLayers.Player, true);
            Physics.IgnoreLayerCollision(GameLayers.Player, GameLayers.Arena, true);
            Physics.IgnoreLayerCollision(GameLayers.Arena, GameLayers.Hoop, true);
            Physics.IgnoreLayerCollision(GameLayers.Arena, GameLayers.Backboard, true);
            Physics.IgnoreLayerCollision(GameLayers.Preview, GameLayers.Preview, true);
        }
    }

    /// <summary>Shared physics materials. One instance each; combine modes are explicit so tuning is honest.</summary>
    public static class GamePhysicsMaterials
    {
        public const float BallBounce = 0.60f;
        public const float IronBounce = 0.60f;
        public const float GlassBounce = 0.72f;
        public const float FloorBounce = 0.55f;

        static PhysicsMaterial _ball, _iron, _glass, _floor;

        public static PhysicsMaterial Ball => _ball != null ? _ball : (_ball = Make("Ball", BallBounce, 0.55f, 0.6f));
        public static PhysicsMaterial Iron => _iron != null ? _iron : (_iron = Make("RimIron", IronBounce, 0.45f, 0.5f));
        public static PhysicsMaterial Glass => _glass != null ? _glass : (_glass = Make("Backboard", GlassBounce, 0.18f, 0.2f));
        public static PhysicsMaterial Floor => _floor != null ? _floor : (_floor = Make("CourtFloor", FloorBounce, 0.6f, 0.65f));

        static PhysicsMaterial Make(string name, float bounce, float dynamicFriction, float staticFriction)
        {
            return new PhysicsMaterial(name)
            {
                bounciness = bounce,
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounceCombine = PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Average
            };
        }
    }
}
