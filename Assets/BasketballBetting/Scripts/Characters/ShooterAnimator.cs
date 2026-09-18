using System;
using UnityEngine;

namespace BasketballBetting.Characters
{
    /// <summary>
    /// Drives a HumanoidRig with authored BodyPoses and a keyed shooting timeline.
    ///
    /// Preparation → Dip → Set point → Release → Follow-through → Land → Recover.
    /// The release is an explicit event at <see cref="ShotAnimationProfile.ReleaseTime"/>; the shot
    /// controller launches the ball from the hand position at that exact frame, so the ball and the
    /// motion can never disagree.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class ShooterAnimator : MonoBehaviour
    {
        public HumanoidRig Rig;

        [SerializeField] float _poseBlendRate = 10f;
        [SerializeField] float _shotBlendRate = 34f;
        [SerializeField] float _headMaxDegrees = 42f;

        BodyPose _current;
        bool _seeded;
        float _poseClock;
        HumanoidPose _pose = HumanoidPose.Idle;

        ShotAnimationProfile _shot;
        float _shotTime;
        bool _released;
        bool _shotFinished = true;
        HumanoidPose _afterShotPose = HumanoidPose.Idle;
        bool _contest;

        public HumanoidPose Pose => _pose;
        public bool IsPlayingShot => _shot != null && !_shotFinished;
        public bool HasReleased => _released;
        public float ShotTime => _shotTime;
        public Vector3 LookTarget { get; set; }
        public bool HasLookTarget { get; set; }
        public Transform BallHand => Rig != null ? Rig.BallHand : null;

        /// <summary>The ball leaves the hand now. Subscribers must launch from Rig.BallHand.position.</summary>
        public event Action ReleaseReached;
        public event Action ShotFinished;

        public void SetPose(HumanoidPose pose, bool snap = false)
        {
            _pose = pose;
            _poseClock = 0f;
            if (IsPlayingShot)
                _afterShotPose = pose;
            if (snap)
                ForceEvaluate(pose, true);
        }

        /// <summary>Starts the shooting motion from the current pose.</summary>
        public void PlayShot(ShotAnimationProfile profile, HumanoidPose afterShot = HumanoidPose.Idle)
        {
            _shot = profile ?? ShotAnimationProfile.FreeThrow;
            _shotTime = 0f;
            _released = false;
            _shotFinished = false;
            _contest = false;
            _afterShotPose = afterShot;
            _pose = HumanoidPose.ShootLoad;
        }

        /// <summary>Defender: rise with hands up and jump at the shot (block = higher).</summary>
        public void PlayContest(bool block)
        {
            _shot = ShotAnimationProfile.ContestJump;
            if (!block)
                _shot.JumpHeight = 0.12f;
            _shotTime = 0f;
            _released = false;
            _shotFinished = false;
            _contest = true;
            _afterShotPose = HumanoidPose.Idle;
            _pose = HumanoidPose.Contest;
        }

        public void CancelShot()
        {
            _shot = null;
            _shotFinished = true;
            _released = false;
        }

        public void ForceEvaluate(HumanoidPose pose, bool snap = true)
        {
            _pose = pose;
            BodyPose target = BodyPoseLibrary.Static(pose, _poseClock);
            if (snap || !_seeded)
            {
                _current = target;
                _seeded = true;
            }
            Apply(_current);
        }

        void LateUpdate()
        {
            if (Rig == null || Rig.Root == null)
                return;
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            _poseClock += dt;

            BodyPose target;
            float rate;
            if (IsPlayingShot)
            {
                _shotTime += dt;
                target = EvaluateShot(_shotTime);
                rate = _shotBlendRate;
            }
            else
            {
                target = BodyPoseLibrary.Static(_pose, _poseClock);
                rate = _poseBlendRate;
            }

            if (!_seeded)
            {
                _current = target;
                _seeded = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-rate * dt);
                _current = BodyPose.Lerp(_current, target, k);
            }

            Apply(_current);

            if (IsPlayingShot)
            {
                if (!_released && _shotTime >= _shot.ReleaseTime)
                {
                    _released = true;
                    ReleaseReached?.Invoke();
                }
                if (_shotTime >= _shot.RecoverTime)
                {
                    _shotFinished = true;
                    _pose = _afterShotPose;
                    _poseClock = 0f;
                    ShotFinished?.Invoke();
                }
            }
        }

        BodyPose EvaluateShot(float t)
        {
            ShotAnimationProfile p = _shot;
            float lift = p.JumpHeight * p.JumpCurve(t);
            BodyPose set = _contest ? BodyPoseLibrary.Contest(0f) : BodyPoseLibrary.ShotKeys.Set();
            BodyPose dip = _contest ? WithHips(BodyPoseLibrary.Contest(0f), -p.DipDepth) : BodyPoseLibrary.ShotKeys.Dip(p.DipDepth);
            BodyPose setPoint = _contest ? BodyPoseLibrary.Contest(0f) : BodyPoseLibrary.ShotKeys.SetPoint(p.DipDepth, p.JumpHeight);
            BodyPose release = _contest ? BodyPoseLibrary.Block(0f) : BodyPoseLibrary.ShotKeys.Release(p.ForwardShift, p.JumpHeight);
            BodyPose follow = _contest ? BodyPoseLibrary.Block(0f) : BodyPoseLibrary.ShotKeys.FollowThrough(p.JumpHeight);
            BodyPose land = _contest ? WithHips(BodyPoseLibrary.Contest(0f), -p.DipDepth * 0.6f) : BodyPoseLibrary.ShotKeys.Land(p.DipDepth);
            BodyPose recover = _contest ? BodyPoseLibrary.Idle(0f) : BodyPoseLibrary.Static(_afterShotPose, 0f);

            BodyPose pose;
            if (t < p.DipTime)
                pose = BodyPose.Lerp(set, dip, EaseInOut(t / p.DipTime));
            else if (t < p.SetPointTime)
                pose = BodyPose.Lerp(dip, setPoint, EaseOut((t - p.DipTime) / (p.SetPointTime - p.DipTime)));
            else if (t < p.ReleaseTime)
                pose = BodyPose.Lerp(setPoint, release, EaseIn((t - p.SetPointTime) / (p.ReleaseTime - p.SetPointTime)));
            else if (t < p.FollowThroughTime)
                pose = BodyPose.Lerp(release, follow, EaseOut((t - p.ReleaseTime) / (p.FollowThroughTime - p.ReleaseTime)));
            else if (t < p.LandTime)
                pose = BodyPose.Lerp(follow, land, EaseInOut((t - p.FollowThroughTime) / (p.LandTime - p.FollowThroughTime)));
            else
                pose = BodyPose.Lerp(land, recover, EaseInOut((t - p.LandTime) / Mathf.Max(0.01f, p.RecoverTime - p.LandTime)));

            // The jump curve is authoritative for height and for whether the feet are planted.
            pose.RootLift = lift;
            pose.FeetPlant = lift > 0.01f ? 0f : pose.FeetPlant;
            return pose;
        }

        static BodyPose WithHips(BodyPose pose, float dy)
        {
            pose.HipsOffset += new Vector3(0f, dy, 0f);
            return pose;
        }

        static float EaseIn(float t) { t = Mathf.Clamp01(t); return t * t; }
        static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        // ------------------------------------------------------------------ pose application

        void Apply(in BodyPose pose)
        {
            HumanoidRig rig = Rig;
            if (rig == null || rig.Root == null || rig.Hips == null || rig.Bind == null || !rig.Bind.Valid)
                return;

            RigIK.RestoreBind(rig);

            Transform root = rig.Root;
            float s = Mathf.Max(0.5f, rig.Height / 1.85f);

            // Torso.
            Vector3 hipsWorldOffset = root.TransformVector(pose.HipsOffset * s) + Vector3.up * pose.RootLift;
            rig.Hips.position += hipsWorldOffset;
            RigIK.AddLocal(rig.Hips, Quaternion.Euler(pose.HipsEuler));
            RigIK.AddLocal(rig.Spine, Quaternion.Euler(pose.SpineEuler));
            RigIK.AddLocal(rig.Chest, Quaternion.Euler(pose.ChestEuler));
            RigIK.AddLocal(rig.Neck, Quaternion.Euler(pose.NeckEuler * 0.5f));
            RigIK.AddLocal(rig.Head, Quaternion.Euler(pose.NeckEuler));

            // Arms (targets are hips-relative so they move with the dip and the jump).
            Vector3 H(Vector3 v) => rig.Hips.position + root.rotation * (v * s);
            RigIK.TwoBone(rig.RightUpperArm, rig.RightLowerArm, rig.RightHand, rig.Bind.RightArm, rig.Bind.RightFore, H(pose.RightHand), H(pose.RightPole));
            RigIK.TwoBone(rig.LeftUpperArm, rig.LeftLowerArm, rig.LeftHand, rig.Bind.LeftArm, rig.Bind.LeftFore, H(pose.LeftHand), H(pose.LeftPole));
            Vector3 rightPalmWorld = root.rotation * pose.RightPalm;
            Vector3 leftPalmWorld = root.rotation * pose.LeftPalm;
            RigIK.TwistPalm(rig.RightLowerArm, rig.RightHand, rig.Bind.RightPalmLocal, rightPalmWorld);
            RigIK.TwistPalm(rig.LeftLowerArm, rig.LeftHand, rig.Bind.LeftPalmLocal, leftPalmWorld);
            RigIK.FlexWrist(rig.RightLowerArm, rig.RightHand, rig.RightHand.TransformDirection(rig.Bind.RightPalmLocal), pose.RightWristFlex);

            // Legs: planted feet stay on the floor while the hips move; airborne feet ride with the body.
            if (rig.LeftFoot != null && rig.RightFoot != null && rig.LeftUpperLeg != null && rig.RightUpperLeg != null)
            {
                Vector3 lPlanted = root.TransformPoint(rig.Bind.LeftFootRootLocal + pose.LeftFootOffset * s);
                Vector3 rPlanted = root.TransformPoint(rig.Bind.RightFootRootLocal + pose.RightFootOffset * s);
                Vector3 lAir = lPlanted + hipsWorldOffset * 0.85f;
                Vector3 rAir = rPlanted + hipsWorldOffset * 0.85f;
                Vector3 lFoot = Vector3.Lerp(lAir, lPlanted, pose.FeetPlant);
                Vector3 rFoot = Vector3.Lerp(rAir, rPlanted, pose.FeetPlant);
                Vector3 lPole = lFoot + root.rotation * new Vector3(-0.08f, 0.45f, 0.6f);
                Vector3 rPole = rFoot + root.rotation * new Vector3(0.08f, 0.45f, 0.6f);
                RigIK.TwoBone(rig.LeftUpperLeg, rig.LeftLowerLeg, rig.LeftFoot, rig.Bind.LeftUpLeg, rig.Bind.LeftLeg, lFoot, lPole);
                RigIK.TwoBone(rig.RightUpperLeg, rig.RightLowerLeg, rig.RightFoot, rig.Bind.RightUpLeg, rig.Bind.RightLeg, rFoot, rPole);
            }

            // Head aim.
            if (HasLookTarget && pose.LookWeight > 0f && rig.Head != null)
                RigIK.AimHead(rig.Head, root.forward, LookTarget, _headMaxDegrees, pose.LookWeight);

            PlaceBallAttach(rig, s);
        }

        /// <summary>The ball sits on the shooting hand's palm, out toward the fingers.</summary>
        static void PlaceBallAttach(HumanoidRig rig, float s)
        {
            if (rig.BallAttach == null || rig.RightHand == null)
                return;
            Vector3 palm = rig.RightHand.TransformDirection(rig.Bind.RightPalmLocal).normalized;
            Vector3 fingers = rig.RightHand.TransformDirection(rig.Bind.RightFingerLocal).normalized;
            rig.BallAttach.position = rig.RightHand.position + fingers * (0.075f * s) + palm * (0.105f * s);
            rig.BallAttach.rotation = rig.RightHand.rotation;
        }
    }
}
