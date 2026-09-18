using UnityEngine;

namespace BasketballBetting.Characters
{
    /// <summary>
    /// A full-body pose expressed in the rig's own frame: hand/pole targets are hips-relative
    /// (x right, y up, z forward, in metres for a 1.85 m athlete and scaled with height),
    /// palm directions are root-relative, hips/root offsets are root-relative metres.
    /// Poses are plain data so they can be keyed, lerped and unit-tested.
    /// </summary>
    [System.Serializable]
    public struct BodyPose
    {
        public Vector3 RightHand;
        public Vector3 LeftHand;
        public Vector3 RightPole;
        public Vector3 LeftPole;
        public Vector3 RightPalm;
        public Vector3 LeftPalm;
        public float RightWristFlex;   // degrees, positive = fingers down (the "gooseneck")
        public Vector3 HipsOffset;     // dip / weight shift
        public float RootLift;         // whole body off the floor (jump)
        public Vector3 HipsEuler;
        public Vector3 SpineEuler;
        public Vector3 ChestEuler;
        public Vector3 NeckEuler;
        public float FeetPlant;        // 1 = feet pinned to the floor, 0 = feet travel with the body
        public Vector3 LeftFootOffset; // stance adjustments (root-relative)
        public Vector3 RightFootOffset;
        public float LookWeight;       // head aim at the look target

        public static BodyPose Lerp(in BodyPose a, in BodyPose b, float t)
        {
            t = Mathf.Clamp01(t);
            return new BodyPose
            {
                RightHand = Vector3.Lerp(a.RightHand, b.RightHand, t),
                LeftHand = Vector3.Lerp(a.LeftHand, b.LeftHand, t),
                RightPole = Vector3.Lerp(a.RightPole, b.RightPole, t),
                LeftPole = Vector3.Lerp(a.LeftPole, b.LeftPole, t),
                RightPalm = Vector3.Slerp(a.RightPalm.normalized, b.RightPalm.normalized, t),
                LeftPalm = Vector3.Slerp(a.LeftPalm.normalized, b.LeftPalm.normalized, t),
                RightWristFlex = Mathf.Lerp(a.RightWristFlex, b.RightWristFlex, t),
                HipsOffset = Vector3.Lerp(a.HipsOffset, b.HipsOffset, t),
                RootLift = Mathf.Lerp(a.RootLift, b.RootLift, t),
                HipsEuler = Vector3.Lerp(a.HipsEuler, b.HipsEuler, t),
                SpineEuler = Vector3.Lerp(a.SpineEuler, b.SpineEuler, t),
                ChestEuler = Vector3.Lerp(a.ChestEuler, b.ChestEuler, t),
                NeckEuler = Vector3.Lerp(a.NeckEuler, b.NeckEuler, t),
                FeetPlant = Mathf.Lerp(a.FeetPlant, b.FeetPlant, t),
                LeftFootOffset = Vector3.Lerp(a.LeftFootOffset, b.LeftFootOffset, t),
                RightFootOffset = Vector3.Lerp(a.RightFootOffset, b.RightFootOffset, t),
                LookWeight = Mathf.Lerp(a.LookWeight, b.LookWeight, t)
            };
        }
    }

    /// <summary>Hand-authored poses. Right-handed shooter; values are for a 1.85 m body.</summary>
    public static class BodyPoseLibrary
    {
        static Vector3 N(float x, float y, float z) => new Vector3(x, y, z).normalized;

        public static BodyPose Idle(float t)
        {
            float breathe = Mathf.Sin(t * 1.7f) * 0.012f;
            return new BodyPose
            {
                RightHand = new Vector3(0.22f, 0.02f + breathe, 0.16f),
                LeftHand = new Vector3(-0.22f, 0.02f + breathe, 0.16f),
                RightPole = new Vector3(0.55f, 0.15f, -0.15f),
                LeftPole = new Vector3(-0.55f, 0.15f, -0.15f),
                RightPalm = N(-0.2f, 0.3f, 0.93f),
                LeftPalm = N(0.2f, 0.3f, 0.93f),
                HipsOffset = new Vector3(0f, -0.03f + breathe, 0.01f),
                HipsEuler = new Vector3(3f, 0f, 0f),
                SpineEuler = new Vector3(3f, 0f, 0f),
                NeckEuler = new Vector3(-3f, 0f, 0f),
                FeetPlant = 1f,
                LeftFootOffset = new Vector3(-0.03f, 0f, 0f),
                RightFootOffset = new Vector3(0.03f, 0f, 0.02f),
                LookWeight = 0.6f
            };
        }

        /// <summary>Ball held at the hip/chest, knees soft, eyes on the rim.</summary>
        public static BodyPose TripleThreat(float t)
        {
            float breathe = Mathf.Sin(t * 1.7f) * 0.01f;
            return new BodyPose
            {
                RightHand = new Vector3(0.20f, 0.28f + breathe, 0.30f),
                LeftHand = new Vector3(0.00f, 0.26f + breathe, 0.34f),
                RightPole = new Vector3(0.55f, 0.20f, -0.10f),
                LeftPole = new Vector3(-0.45f, 0.18f, 0.02f),
                RightPalm = N(-0.15f, 0.80f, 0.58f),
                LeftPalm = N(0.92f, 0.10f, 0.38f),
                HipsOffset = new Vector3(0f, -0.06f + breathe, 0.02f),
                HipsEuler = new Vector3(6f, 4f, 0f),
                SpineEuler = new Vector3(6f, 2f, 0f),
                ChestEuler = new Vector3(2f, 0f, 0f),
                NeckEuler = new Vector3(-8f, 0f, 0f),
                FeetPlant = 1f,
                LeftFootOffset = new Vector3(-0.04f, 0f, -0.04f),
                RightFootOffset = new Vector3(0.05f, 0f, 0.06f),
                LookWeight = 0.85f
            };
        }

        public static BodyPose Celebrate(float t)
        {
            float pump = Mathf.Abs(Mathf.Sin(t * 8f));
            return new BodyPose
            {
                RightHand = new Vector3(0.30f, 0.92f + pump * 0.06f, 0.16f),
                LeftHand = new Vector3(-0.30f, 0.90f + pump * 0.06f, 0.14f),
                RightPole = new Vector3(0.48f, 0.48f, -0.30f),
                LeftPole = new Vector3(-0.48f, 0.46f, -0.30f),
                RightPalm = N(-0.35f, 0.2f, 0.9f),
                LeftPalm = N(0.35f, 0.2f, 0.9f),
                HipsOffset = new Vector3(0f, 0.02f + pump * 0.05f, 0f),
                HipsEuler = new Vector3(-6f, 0f, 0f),
                SpineEuler = new Vector3(-10f, 0f, 0f),
                ChestEuler = new Vector3(-8f, 0f, 0f),
                NeckEuler = new Vector3(-10f, 0f, 0f),
                FeetPlant = 1f,
                LookWeight = 0.3f
            };
        }

        public static BodyPose Frustrated(float t)
        {
            return new BodyPose
            {
                RightHand = new Vector3(0.14f, 0.70f, 0.10f),
                LeftHand = new Vector3(-0.14f, 0.70f, 0.10f),
                RightPole = new Vector3(0.42f, 0.38f, 0.02f),
                LeftPole = new Vector3(-0.42f, 0.38f, 0.02f),
                RightPalm = N(-0.75f, 0.15f, 0.64f),
                LeftPalm = N(0.75f, 0.15f, 0.64f),
                HipsOffset = new Vector3(0f, -0.04f, 0f),
                HipsEuler = new Vector3(8f, 0f, 0f),
                SpineEuler = new Vector3(8f, 0f, 0f),
                ChestEuler = new Vector3(6f, 0f, 0f),
                NeckEuler = new Vector3(24f, Mathf.Sin(t * 2.4f) * 8f, 0f),
                FeetPlant = 1f,
                LookWeight = 0f
            };
        }

        public static BodyPose Contest(float t)
        {
            return new BodyPose
            {
                RightHand = new Vector3(0.22f, 1.02f, 0.10f),
                LeftHand = new Vector3(-0.22f, 1.00f, 0.10f),
                RightPole = new Vector3(0.40f, 0.55f, -0.20f),
                LeftPole = new Vector3(-0.40f, 0.53f, -0.20f),
                RightPalm = Vector3.forward,
                LeftPalm = Vector3.forward,
                HipsOffset = new Vector3(0f, 0.02f, 0.04f),
                ChestEuler = new Vector3(-8f, 0f, 0f),
                NeckEuler = new Vector3(-12f, 0f, 0f),
                FeetPlant = 1f,
                LookWeight = 0.8f
            };
        }

        public static BodyPose Block(float t)
        {
            return new BodyPose
            {
                RightHand = new Vector3(0.20f, 1.10f, 0.18f),
                LeftHand = new Vector3(-0.18f, 1.04f, 0.14f),
                RightPole = new Vector3(0.38f, 0.60f, -0.12f),
                LeftPole = new Vector3(-0.36f, 0.58f, -0.12f),
                RightPalm = Vector3.forward,
                LeftPalm = Vector3.forward,
                HipsOffset = new Vector3(0f, 0.04f, 0.08f),
                RootLift = 0.30f,
                ChestEuler = new Vector3(-10f, 0f, 0f),
                NeckEuler = new Vector3(-16f, 0f, 0f),
                FeetPlant = 0f,
                LookWeight = 0.8f
            };
        }

        public static BodyPose Static(HumanoidPose pose, float t)
        {
            switch (pose)
            {
                case HumanoidPose.TripleThreat: return TripleThreat(t);
                case HumanoidPose.ShootLoad: return ShotKeys.SetPoint(0.14f, 0f);
                case HumanoidPose.ShootRelease: return ShotKeys.Release(0.05f, 0f);
                case HumanoidPose.FollowThrough: return ShotKeys.FollowThrough(0f);
                case HumanoidPose.Celebrate: return Celebrate(t);
                case HumanoidPose.Frustrated: return Frustrated(t);
                case HumanoidPose.Contest: return Contest(t);
                case HumanoidPose.Block: return Block(t);
                default: return Idle(t);
            }
        }

        /// <summary>The seven keys of a jump/set shot, parameterised by dip depth and jump height.</summary>
        public static class ShotKeys
        {
            public static BodyPose Set() => TripleThreat(0f);

            /// <summary>Knees bend, ball drops to the pocket, weight loads over the front foot.</summary>
            public static BodyPose Dip(float depth)
            {
                return new BodyPose
                {
                    RightHand = new Vector3(0.18f, 0.20f, 0.30f),
                    LeftHand = new Vector3(-0.02f, 0.18f, 0.34f),
                    RightPole = new Vector3(0.55f, 0.10f, -0.05f),
                    LeftPole = new Vector3(-0.45f, 0.10f, 0.05f),
                    RightPalm = N(-0.15f, 0.85f, 0.5f),
                    LeftPalm = N(0.92f, 0.1f, 0.38f),
                    HipsOffset = new Vector3(0f, -depth, 0.03f),
                    HipsEuler = new Vector3(10f, 4f, 0f),
                    SpineEuler = new Vector3(10f, 2f, 0f),
                    ChestEuler = new Vector3(4f, 0f, 0f),
                    NeckEuler = new Vector3(-16f, 0f, 0f),
                    FeetPlant = 1f,
                    LeftFootOffset = new Vector3(-0.04f, 0f, -0.04f),
                    RightFootOffset = new Vector3(0.05f, 0f, 0.06f),
                    LookWeight = 1f
                };
            }

            /// <summary>Ball above the forehead, elbow under the ball, body rising.</summary>
            public static BodyPose SetPoint(float depth, float lift)
            {
                return new BodyPose
                {
                    RightHand = new Vector3(0.15f, 0.66f, 0.24f),
                    LeftHand = new Vector3(0.00f, 0.62f, 0.30f),
                    RightPole = new Vector3(0.42f, 0.42f, -0.05f),
                    LeftPole = new Vector3(-0.40f, 0.40f, 0.00f),
                    RightPalm = N(-0.1f, 0.55f, 0.83f),
                    LeftPalm = N(0.95f, 0.05f, 0.3f),
                    HipsOffset = new Vector3(0f, -depth * 0.35f, 0.04f),
                    RootLift = lift * 0.15f,
                    HipsEuler = new Vector3(2f, 2f, 0f),
                    SpineEuler = new Vector3(-2f, 1f, 0f),
                    ChestEuler = new Vector3(-6f, 0f, 0f),
                    NeckEuler = new Vector3(-20f, 0f, 0f),
                    FeetPlant = lift > 0.05f ? 0.6f : 1f,
                    LeftFootOffset = new Vector3(-0.04f, 0f, -0.04f),
                    RightFootOffset = new Vector3(0.05f, 0f, 0.06f),
                    LookWeight = 1f
                };
            }

            /// <summary>Arm extends up and out, wrist snaps, the ball leaves the fingertips.</summary>
            public static BodyPose Release(float forwardShift, float lift)
            {
                return new BodyPose
                {
                    RightHand = new Vector3(0.10f, 0.94f, 0.38f),
                    LeftHand = new Vector3(-0.16f, 0.52f, 0.26f),
                    RightPole = new Vector3(0.36f, 0.62f, -0.02f),
                    LeftPole = new Vector3(-0.42f, 0.32f, 0.02f),
                    RightPalm = N(0.0f, 0.35f, 0.94f),
                    LeftPalm = N(0.7f, 0.1f, 0.7f),
                    RightWristFlex = 15f,
                    HipsOffset = new Vector3(0f, 0.0f, forwardShift),
                    RootLift = lift * 0.75f,
                    HipsEuler = new Vector3(-3f, 2f, 0f),
                    SpineEuler = new Vector3(-6f, 1f, 0f),
                    ChestEuler = new Vector3(-10f, 0f, 0f),
                    NeckEuler = new Vector3(-18f, 0f, 0f),
                    FeetPlant = lift > 0.05f ? 0f : 1f,
                    LeftFootOffset = new Vector3(-0.04f, lift > 0.05f ? 0f : 0.03f, -0.04f),
                    RightFootOffset = new Vector3(0.05f, lift > 0.05f ? 0f : 0.03f, 0.06f),
                    LookWeight = 1f
                };
            }

            /// <summary>Full extension, fingers pointing down into the basket.</summary>
            public static BodyPose FollowThrough(float lift)
            {
                return new BodyPose
                {
                    RightHand = new Vector3(0.08f, 1.02f, 0.32f),
                    LeftHand = new Vector3(-0.22f, 0.40f, 0.18f),
                    RightPole = new Vector3(0.30f, 0.66f, -0.06f),
                    LeftPole = new Vector3(-0.42f, 0.24f, 0.0f),
                    RightPalm = N(0.05f, -0.45f, 0.89f),
                    LeftPalm = N(0.6f, 0.2f, 0.78f),
                    RightWristFlex = 55f,
                    HipsOffset = new Vector3(0f, 0.01f, 0.05f),
                    RootLift = lift,
                    HipsEuler = new Vector3(-4f, 2f, 0f),
                    SpineEuler = new Vector3(-8f, 1f, 0f),
                    ChestEuler = new Vector3(-8f, 0f, 0f),
                    NeckEuler = new Vector3(-14f, 0f, 0f),
                    FeetPlant = lift > 0.05f ? 0f : 1f,
                    LeftFootOffset = new Vector3(-0.04f, lift > 0.05f ? 0f : 0.04f, -0.04f),
                    RightFootOffset = new Vector3(0.05f, lift > 0.05f ? 0f : 0.04f, 0.06f),
                    LookWeight = 1f
                };
            }

            /// <summary>Feet back on the floor, knees absorb, arm coming down.</summary>
            public static BodyPose Land(float depth)
            {
                return new BodyPose
                {
                    RightHand = new Vector3(0.16f, 0.72f, 0.26f),
                    LeftHand = new Vector3(-0.22f, 0.32f, 0.18f),
                    RightPole = new Vector3(0.40f, 0.45f, -0.05f),
                    LeftPole = new Vector3(-0.44f, 0.20f, 0.0f),
                    RightPalm = N(0.0f, -0.2f, 0.98f),
                    LeftPalm = N(0.5f, 0.2f, 0.84f),
                    RightWristFlex = 25f,
                    HipsOffset = new Vector3(0f, -depth * 0.55f, 0.04f),
                    HipsEuler = new Vector3(8f, 2f, 0f),
                    SpineEuler = new Vector3(6f, 1f, 0f),
                    ChestEuler = new Vector3(4f, 0f, 0f),
                    NeckEuler = new Vector3(-12f, 0f, 0f),
                    FeetPlant = 1f,
                    LeftFootOffset = new Vector3(-0.05f, 0f, -0.03f),
                    RightFootOffset = new Vector3(0.05f, 0f, 0.05f),
                    LookWeight = 1f
                };
            }
        }
    }
}
