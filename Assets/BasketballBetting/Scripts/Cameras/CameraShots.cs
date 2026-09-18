using BasketballBetting.Shooting;
using UnityEngine;

namespace BasketballBetting.Cameras
{
    /// <summary>Everything a camera shot may look at. Filled every frame by whoever drives the director.</summary>
    public sealed class CameraContext
    {
        public Transform Shooter;
        public Transform Defender;
        public Vector3 ShooterHead;       // approx eye height point (set by the driver)
        public Vector3 BallPosition;
        public Vector3 BallVelocity;
        public Vector3 RimCenter;
        public Vector3 TowardCourt = Vector3.back;   // horizontal unit vector from the rim toward the court
        public float RimHeight = CourtMetrics.RimHeight;
        public GameModeType Mode;
        public ThreePointSpot Spot;
        public bool Made;
        public bool ResultKnown;
        /// <summary>0 = 16:9 landscape, 1 = tall phone portrait. Shots widen/pull back with it.</summary>
        public float Narrowness;

        public Vector3 ShooterPosition => Shooter != null ? Shooter.position : RimCenter + TowardCourt * 4.5f;

        /// <summary>Horizontal unit vector from the shooter toward the rim.</summary>
        public Vector3 Lane
        {
            get
            {
                Vector3 d = RimCenter - ShooterPosition;
                d.y = 0f;
                return d.sqrMagnitude > 1e-4f ? d.normalized : -TowardCourt;
            }
        }

        /// <summary>Shooter's right-hand side.</summary>
        public Vector3 Side => Vector3.Cross(Vector3.up, Lane);

        public float ShotDistance => ShotSolver.HorizontalDistance(ShooterPosition, RimCenter);
    }

    /// <summary>A camera pose the director blends toward.</summary>
    public struct CameraPose
    {
        public Vector3 Position;
        public Vector3 LookAt;
        public float Fov;
        public float Roll;

        public static CameraPose Lerp(CameraPose a, CameraPose b, float t)
        {
            return new CameraPose
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                LookAt = Vector3.Lerp(a.LookAt, b.LookAt, t),
                Fov = Mathf.Lerp(a.Fov, b.Fov, t),
                Roll = Mathf.Lerp(a.Roll, b.Roll, t)
            };
        }
    }

    /// <summary>
    /// One camera composition. Stateless apart from tuning: given the context and how long the
    /// shot has been active it returns where the camera should be. The director does the blending.
    /// </summary>
    public abstract class CameraShot
    {
        public abstract string Name { get; }
        /// <summary>Position damping time (s). 0 = locked.</summary>
        public virtual float PositionSmooth => 0.16f;
        /// <summary>Look-at damping time (s).</summary>
        public virtual float LookSmooth => 0.10f;
        public virtual float FovSmooth => 0.18f;
        public abstract CameraPose Compose(CameraContext ctx, float time);

        protected static float Wide(float fov, float narrowness, float extra = 14f) => fov + narrowness * extra;
    }

    /// <summary>Low three-quarter view from behind the shooter's off hand: player, ball, basket and arena in one frame.</summary>
    public sealed class BehindShooterShot : CameraShot
    {
        public float Distance = 4.2f;
        public float Height = 1.9f;
        public float SideOffset = -1.35f;   // negative = shooter's left (off hand for a right-handed shooter)
        public float Fov = 40f;
        public float LookMix = 0.42f;       // 0 = look at the shooter, 1 = look at the rim
        public bool Tight;

        public override string Name => Tight ? "BehindShooter/Tight" : "BehindShooter";
        public override CameraPose Compose(CameraContext ctx, float time)
        {
            Vector3 lane = ctx.Lane;
            Vector3 side = ctx.Side;
            float dist = Distance + ctx.Narrowness * 1.6f + (ctx.Mode == GameModeType.ThreePoint ? 0.5f : 0f);
            float height = Height + ctx.Narrowness * 0.4f;
            Vector3 anchor = ctx.ShooterPosition;
            Vector3 pos = anchor - lane * dist + side * SideOffset + Vector3.up * height;
            Vector3 look = Vector3.Lerp(anchor + Vector3.up * 1.35f, ctx.RimCenter, LookMix + ctx.Narrowness * 0.12f);
            float fov = Wide(Tight ? Fov - 6f : Fov, ctx.Narrowness);
            return new CameraPose { Position = pos, LookAt = look, Fov = fov };
        }
    }

    /// <summary>Tight on the upper body from the front-side during the release.</summary>
    public sealed class ShooterCloseUpShot : CameraShot
    {
        public float Distance = 2.4f;
        public float Height = 1.75f;
        public float Fov = 30f;
        public override string Name => "CloseUp";
        public override float PositionSmooth => 0.22f;
        public override CameraPose Compose(CameraContext ctx, float time)
        {
            Vector3 lane = ctx.Lane;
            Vector3 side = ctx.Side;
            Vector3 anchor = ctx.ShooterPosition;
            Vector3 pos = anchor + lane * (Distance * 0.55f) + side * (Distance * 0.85f) + Vector3.up * Height;
            Vector3 look = anchor + Vector3.up * 1.55f + lane * 0.1f;
            return new CameraPose { Position = pos, LookAt = look, Fov = Wide(Fov, ctx.Narrowness, 10f) };
        }
    }

    /// <summary>Chases the ball from behind and slightly above, leading toward the basket.</summary>
    public sealed class BallFollowShot : CameraShot
    {
        public float Back = 3.2f;
        public float Height = 1.1f;
        public float SideOffset = 0.9f;
        public float Fov = 42f;
        public override string Name => "BallFollow";
        public override float PositionSmooth => 0.12f;
        public override float LookSmooth => 0.05f;
        public override CameraPose Compose(CameraContext ctx, float time)
        {
            Vector3 lane = ctx.Lane;
            Vector3 side = ctx.Side;
            Vector3 ball = ctx.BallPosition;
            Vector3 pos = ball - lane * Back + side * SideOffset + Vector3.up * Height;
            pos.y = Mathf.Max(pos.y, 0.6f);
            Vector3 look = Vector3.Lerp(ball, ctx.RimCenter, 0.35f);
            float toRim = ShotSolver.HorizontalDistance(ball, ctx.RimCenter);
            float fov = Mathf.Lerp(Fov - 8f, Fov, Mathf.Clamp01(toRim / 6f));
            return new CameraPose { Position = pos, LookAt = look, Fov = Wide(fov, ctx.Narrowness) };
        }
    }

    /// <summary>Planted on the sideline, pans with the ball. Shows the whole arc.</summary>
    public sealed class SideTrackingShot : CameraShot
    {
        public float Height = 2.4f;
        public float Fov = 44f;
        public override string Name => "SideTracking";
        public override float PositionSmooth => 0f;
        public override float LookSmooth => 0.08f;
        public override CameraPose Compose(CameraContext ctx, float time)
        {
            Vector3 lane = ctx.Lane;
            Vector3 side = ctx.Side;
            Vector3 mid = Vector3.Lerp(ctx.ShooterPosition, ctx.RimCenter, 0.5f);
            mid.y = 0f;
            float span = Mathf.Max(3.5f, ctx.ShotDistance);
            // Sit on the side that keeps the glass on the far side of frame.
            float sideSign = Vector3.Dot(side, ctx.TowardCourt) >= 0f ? 1f : -1f;
            Vector3 pos = mid + side * (sideSign * (span * 1.15f + 2.5f + ctx.Narrowness * 3f)) + Vector3.up * Height;
            Vector3 look = Vector3.Lerp(ctx.BallPosition, mid + Vector3.up * 2.2f, 0.45f);
            return new CameraPose { Position = pos, LookAt = look, Fov = Wide(Fov, ctx.Narrowness, 18f) };
        }
    }

    /// <summary>Low and close beside the rim, ball and iron fill the frame.</summary>
    public sealed class RimCamShot : CameraShot
    {
        public float Distance = 2.3f;
        public float Drop = 0.35f;
        public float Fov = 34f;
        public override string Name => "RimCam";
        public override float PositionSmooth => 0.2f;
        public override float LookSmooth => 0.06f;
        public override CameraPose Compose(CameraContext ctx, float time)
        {
            Vector3 lane = ctx.Lane;
            Vector3 side = ctx.Side;
            float sideSign = Vector3.Dot(side, ctx.TowardCourt) >= 0f ? 1f : -1f;
            Vector3 pos = ctx.RimCenter + side * (sideSign * Distance) - lane * (Distance * 0.35f) + Vector3.up * (0.2f - Drop + ctx.Narrowness * 0.5f);
            Vector3 look = Vector3.Lerp(ctx.RimCenter, ctx.BallPosition, 0.3f);
            float toRim = Vector3.Distance(ctx.BallPosition, ctx.RimCenter);
            float fov = Mathf.Lerp(Fov - 8f, Fov + 6f, Mathf.Clamp01(toRim / 3f));
            return new CameraPose { Position = pos, LookAt = look, Fov = Wide(fov, ctx.Narrowness, 12f) };
        }
    }

    /// <summary>Behind and above the glass, looking back down the lane at the incoming ball.</summary>
    public sealed class BackboardCamShot : CameraShot
    {
        public float Fov = 46f;
        public override string Name => "BackboardCam";
        public override float PositionSmooth => 0.25f;
        public override float LookSmooth => 0.07f;
        public override CameraPose Compose(CameraContext ctx, float time)
        {
            Vector3 side = ctx.Side;
            float sideSign = Vector3.Dot(side, ctx.TowardCourt) >= 0f ? -1f : 1f;
            Vector3 pos = ctx.RimCenter - ctx.TowardCourt * 1.35f + side * (sideSign * 1.1f) + Vector3.up * (1.05f + ctx.Narrowness * 0.4f);
            Vector3 look = Vector3.Lerp(ctx.RimCenter, ctx.BallPosition, 0.4f);
            return new CameraPose { Position = pos, LookAt = look, Fov = Wide(Fov, ctx.Narrowness, 12f) };
        }
    }

    /// <summary>High elevated wide: court, goal, crowd and lights.</summary>
    public sealed class WideArenaShot : CameraShot
    {
        public float Fov = 50f;
        public override string Name => "WideArena";
        public override float PositionSmooth => 0.6f;
        public override float LookSmooth => 0.25f;
        public override CameraPose Compose(CameraContext ctx, float time)
        {
            Vector3 lane = ctx.Lane;
            Vector3 side = ctx.Side;
            float drift = Mathf.Sin(time * 0.35f) * 0.6f;
            Vector3 mid = Vector3.Lerp(ctx.ShooterPosition, ctx.RimCenter, 0.45f);
            Vector3 pos = mid - lane * 9.5f + side * (4.5f + drift) + Vector3.up * (5.2f + ctx.Narrowness * 1.5f);
            Vector3 look = Vector3.Lerp(mid, ctx.RimCenter, 0.3f) + Vector3.up * 1.6f;
            return new CameraPose { Position = pos, LookAt = look, Fov = Wide(Fov, ctx.Narrowness, 16f) };
        }
    }

    /// <summary>Front three-quarter on a subject's face: celebration or frustration.</summary>
    public sealed class ReactionShot : CameraShot
    {
        public Transform Subject;
        public float Fov = 32f;
        public override string Name => "Reaction";
        public override float PositionSmooth => 0.28f;
        public override CameraPose Compose(CameraContext ctx, float time)
        {
            Transform subject = Subject != null ? Subject : ctx.Shooter;
            Vector3 anchor = subject != null ? subject.position : ctx.ShooterPosition;
            Vector3 fwd = subject != null ? subject.forward : ctx.Lane;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 pos = anchor + fwd * 2.6f + right * 1.1f + Vector3.up * (1.7f + ctx.Narrowness * 0.2f);
            Vector3 look = anchor + Vector3.up * 1.5f;
            return new CameraPose { Position = pos, LookAt = look, Fov = Wide(Fov, ctx.Narrowness, 10f) };
        }
    }

    /// <summary>Slow orbit around the goal for the title screen.</summary>
    public sealed class TitleShot : CameraShot
    {
        public float Fov = 38f;
        public override string Name => "Title";
        public override float PositionSmooth => 0.9f;
        public override float LookSmooth => 0.5f;
        public override CameraPose Compose(CameraContext ctx, float time)
        {
            float a = time * 0.09f;
            Vector3 pos = ctx.RimCenter + ctx.TowardCourt * (6.5f + ctx.Narrowness * 2.5f) + Vector3.right * Mathf.Sin(a) * 3.2f + Vector3.up * (0.6f + Mathf.Cos(a * 0.7f) * 0.5f);
            Vector3 look = ctx.RimCenter + Vector3.down * 0.3f;
            return new CameraPose { Position = pos, LookAt = look, Fov = Wide(Fov, ctx.Narrowness, 10f) };
        }
    }

    /// <summary>Roster / lobby showcase: front three-quarter full body with the goal behind.</summary>
    public sealed class ShowcaseShot : CameraShot
    {
        public float Fov = 34f;
        public override string Name => "Showcase";
        public override float PositionSmooth => 0.5f;
        public override float LookSmooth => 0.3f;
        public override CameraPose Compose(CameraContext ctx, float time)
        {
            Vector3 lane = ctx.Lane;
            Vector3 side = ctx.Side;
            Vector3 anchor = ctx.ShooterPosition;
            Vector3 pos = anchor - lane * (3.6f + ctx.Narrowness * 1.5f) + side * 1.9f + Vector3.up * 1.55f;
            Vector3 look = anchor + Vector3.up * (1.05f + ctx.Narrowness * 0.1f);
            return new CameraPose { Position = pos, LookAt = look, Fov = Wide(Fov, ctx.Narrowness, 12f) };
        }
    }
}
