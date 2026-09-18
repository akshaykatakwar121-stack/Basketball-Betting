using UnityEngine;

namespace BasketballBetting
{
    public enum HumanoidPose
    {
        Idle,
        TripleThreat,
        ShootLoad,
        ShootRelease,
        FollowThrough,
        Celebrate,
        Frustrated,
        Contest,
        Block
    }

    [System.Serializable]
    public sealed class HumanoidRig
    {
        public Transform Root;
        public Transform Hips;
        public Transform Spine;
        public Transform Chest;
        public Transform Head;
        public Transform LeftUpperArm;
        public Transform LeftLowerArm;
        public Transform LeftHand;
        public Transform RightUpperArm;
        public Transform RightLowerArm;
        public Transform RightHand;
        public Transform Neck;
        public Transform LeftShoulder;
        public Transform RightShoulder;
        public Transform LeftUpperLeg;
        public Transform LeftLowerLeg;
        public Transform LeftFoot;
        public Transform RightUpperLeg;
        public Transform RightLowerLeg;
        public Transform RightFoot;
        public Transform BallAttach;
        public CharacterDefinition Definition;
        public float Height = 1.95f;
        public bool ImportedSkeleton;
        public MixamoBindPose Bind = new MixamoBindPose();

        public Transform BallHand => BallAttach != null ? BallAttach : RightHand;

        public void LookAt(Vector3 worldPoint)
        {
            if (Root == null)
                return;
            Vector3 flat = worldPoint;
            flat.y = Root.position.y;
            Vector3 dir = flat - Root.position;
            if (dir.sqrMagnitude > 0.01f)
                Root.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        public void FaceCamera(Vector3 cameraPos)
        {
            LookAt(cameraPos);
        }
    }

    /// <summary>Bind-pose snapshot of a rig, captured once after the model is bound. All IK is relative to it.</summary>
    [System.Serializable]
    public sealed class MixamoBindPose
    {
        public bool Valid;
        public Vector3 HipsLocalPos;
        public Quaternion Hips, Spine, Chest, Neck, Head;
        public Quaternion LeftShoulder, RightShoulder;
        public Quaternion LeftArm, LeftFore, LeftHand;
        public Quaternion RightArm, RightFore, RightHand;
        public Quaternion LeftUpLeg, LeftLeg, LeftFoot;
        public Quaternion RightUpLeg, RightLeg, RightFoot;
        public Vector3 LeftFootRootLocal;
        public Vector3 RightFootRootLocal;
        public Vector3 LeftPalmLocal;
        public Vector3 RightPalmLocal;
        public Vector3 LeftFingerLocal;
        public Vector3 RightFingerLocal;

        public static void Capture(HumanoidRig rig, Transform root)
        {
            var bind = rig.Bind ?? new MixamoBindPose();
            rig.Bind = bind;
            if (rig.Hips == null || root == null)
                return;

            bind.HipsLocalPos = rig.Hips.localPosition;
            bind.Hips = Rot(rig.Hips);
            bind.Spine = Rot(rig.Spine);
            bind.Chest = Rot(rig.Chest);
            bind.Neck = Rot(rig.Neck);
            bind.Head = Rot(rig.Head);
            bind.LeftShoulder = Rot(rig.LeftShoulder);
            bind.RightShoulder = Rot(rig.RightShoulder);
            bind.LeftArm = Rot(rig.LeftUpperArm);
            bind.LeftFore = Rot(rig.LeftLowerArm);
            bind.LeftHand = Rot(rig.LeftHand);
            bind.RightArm = Rot(rig.RightUpperArm);
            bind.RightFore = Rot(rig.RightLowerArm);
            bind.RightHand = Rot(rig.RightHand);
            bind.LeftUpLeg = Rot(rig.LeftUpperLeg);
            bind.LeftLeg = Rot(rig.LeftLowerLeg);
            bind.LeftFoot = Rot(rig.LeftFoot);
            bind.RightUpLeg = Rot(rig.RightUpperLeg);
            bind.RightLeg = Rot(rig.RightLowerLeg);
            bind.RightFoot = Rot(rig.RightFoot);

            if (rig.LeftFoot != null)
                bind.LeftFootRootLocal = root.InverseTransformPoint(rig.LeftFoot.position);
            if (rig.RightFoot != null)
                bind.RightFootRootLocal = root.InverseTransformPoint(rig.RightFoot.position);

            Vector3 down = -root.up;
            Vector3 forward = root.forward;
            bind.LeftPalmLocal = PalmLocal(rig.LeftHand, down);
            bind.RightPalmLocal = PalmLocal(rig.RightHand, down);
            bind.LeftFingerLocal = FingerLocal(rig.LeftHand, forward);
            bind.RightFingerLocal = FingerLocal(rig.RightHand, forward);
            bind.Valid = true;
        }

        static Quaternion Rot(Transform t) => t != null ? t.localRotation : Quaternion.identity;

        static Vector3 PalmLocal(Transform hand, Vector3 worldPalm)
        {
            if (hand == null)
                return Vector3.up;
            return hand.InverseTransformDirection(worldPalm).normalized;
        }

        static Vector3 FingerLocal(Transform hand, Vector3 fallback)
        {
            if (hand == null)
                return Vector3.forward;
            Transform tip = null;
            for (int i = 0; i < hand.childCount; i++)
            {
                Transform c = hand.GetChild(i);
                if (c.name.Contains("BallAttach"))
                    continue;
                if (c.name.Contains("Middle") || tip == null)
                    tip = c;
                if (c.name.Contains("Middle1"))
                    break;
            }
            if (tip != null)
            {
                Vector3 dir = tip.position - hand.position;
                if (dir.sqrMagnitude > 1e-6f)
                    return hand.InverseTransformDirection(dir.normalized);
            }
            return hand.InverseTransformDirection(fallback).normalized;
        }
    }
}
