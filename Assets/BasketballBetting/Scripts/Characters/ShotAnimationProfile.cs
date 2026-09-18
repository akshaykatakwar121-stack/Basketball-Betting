using System;
using UnityEngine;

namespace BasketballBetting.Characters
{
    /// <summary>
    /// Timing and amplitude of one shooting motion. Times are seconds from the start of the motion;
    /// ReleaseTime is the instant the ball leaves the hand and is the only time the shot system cares about.
    /// </summary>
    [Serializable]
    public sealed class ShotAnimationProfile
    {
        public float DipTime = 0.32f;
        public float SetPointTime = 0.62f;
        public float ReleaseTime = 0.78f;
        public float FollowThroughTime = 0.98f;
        public float LandTime = 1.28f;
        public float RecoverTime = 1.60f;

        [Tooltip("How far the hips drop in the dip (m).")]
        public float DipDepth = 0.14f;
        [Tooltip("Peak height of the feet above the floor (m). 0 = set shot.")]
        public float JumpHeight = 0.02f;
        [Tooltip("Forward drift of the hips through the release (m).")]
        public float ForwardShift = 0.05f;

        public static ShotAnimationProfile FreeThrow => new ShotAnimationProfile();

        public static ShotAnimationProfile JumpShot => new ShotAnimationProfile
        {
            DipTime = 0.36f,
            SetPointTime = 0.70f,
            ReleaseTime = 0.86f,
            FollowThroughTime = 1.06f,
            LandTime = 1.42f,
            RecoverTime = 1.80f,
            DipDepth = 0.19f,
            JumpHeight = 0.26f,
            ForwardShift = 0.03f
        };

        /// <summary>Defender rising into the shot: hands up, then a block jump.</summary>
        public static ShotAnimationProfile ContestJump => new ShotAnimationProfile
        {
            DipTime = 0.18f,
            SetPointTime = 0.30f,
            ReleaseTime = 0.42f,
            FollowThroughTime = 0.58f,
            LandTime = 0.92f,
            RecoverTime = 1.25f,
            DipDepth = 0.12f,
            JumpHeight = 0.32f,
            ForwardShift = 0.08f
        };

        public bool Airborne => JumpHeight > 0.05f;

        /// <summary>Vertical profile of the jump: 0 on the floor, 1 at the apex (between release and follow-through).</summary>
        public float JumpCurve(float t)
        {
            if (!Airborne)
                return 0f;
            float takeoff = Mathf.Lerp(SetPointTime, ReleaseTime, 0.25f);
            float apex = Mathf.Lerp(ReleaseTime, FollowThroughTime, 0.6f);
            float land = LandTime;
            if (t <= takeoff || t >= land)
                return 0f;
            if (t < apex)
            {
                float u = (t - takeoff) / (apex - takeoff);
                return 1f - (1f - u) * (1f - u);
            }
            float d = (t - apex) / (land - apex);
            return 1f - d * d;
        }
    }
}
