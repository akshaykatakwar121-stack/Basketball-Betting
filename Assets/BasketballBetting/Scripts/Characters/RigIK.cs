using UnityEngine;

namespace BasketballBetting.Characters
{
    /// <summary>
    /// Small analytic IK toolkit used by the animator. Stateless; every call is a pure transform edit.
    /// </summary>
    public static class RigIK
    {
        /// <summary>Puts every driven bone back to its captured bind rotation (and the hips to the bind position).</summary>
        public static void RestoreBind(HumanoidRig rig)
        {
            MixamoBindPose b = rig.Bind;
            if (b == null || !b.Valid)
                return;
            SetLocal(rig.Hips, b.Hips, b.HipsLocalPos);
            SetRot(rig.Spine, b.Spine);
            SetRot(rig.Chest, b.Chest);
            SetRot(rig.Neck, b.Neck);
            SetRot(rig.Head, b.Head);
            SetRot(rig.LeftShoulder, b.LeftShoulder);
            SetRot(rig.RightShoulder, b.RightShoulder);
            SetRot(rig.LeftUpperArm, b.LeftArm);
            SetRot(rig.LeftLowerArm, b.LeftFore);
            SetRot(rig.LeftHand, b.LeftHand);
            SetRot(rig.RightUpperArm, b.RightArm);
            SetRot(rig.RightLowerArm, b.RightFore);
            SetRot(rig.RightHand, b.RightHand);
            SetRot(rig.LeftUpperLeg, b.LeftUpLeg);
            SetRot(rig.LeftLowerLeg, b.LeftLeg);
            SetRot(rig.LeftFoot, b.LeftFoot);
            SetRot(rig.RightUpperLeg, b.RightUpLeg);
            SetRot(rig.RightLowerLeg, b.RightLeg);
            SetRot(rig.RightFoot, b.RightFoot);
        }

        /// <summary>
        /// Two-bone IK (law of cosines) with a pole. Bones are reset to their bind rotation first so the
        /// result is deterministic and never accumulates.
        /// </summary>
        public static void TwoBone(Transform a, Transform b, Transform c, Quaternion aBind, Quaternion bBind, Vector3 target, Vector3 pole)
        {
            if (a == null || b == null || c == null)
                return;
            a.localRotation = aBind;
            b.localRotation = bBind;

            float lenA = Vector3.Distance(a.position, b.position);
            float lenB = Vector3.Distance(b.position, c.position);
            if (lenA < 1e-4f || lenB < 1e-4f)
                return;

            Vector3 aPos = a.position;
            Vector3 toTarget = target - aPos;
            float dist = toTarget.magnitude;
            if (dist < 1e-4f)
                return;

            dist = Mathf.Clamp(dist, Mathf.Abs(lenA - lenB) + 0.003f, lenA + lenB - 0.003f);
            Vector3 dir = toTarget / toTarget.magnitude;

            float cosA = (lenA * lenA + dist * dist - lenB * lenB) / (2f * lenA * dist);
            float angA = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f));

            Vector3 towardPole = Vector3.ProjectOnPlane(pole - aPos, dir);
            if (towardPole.sqrMagnitude < 1e-6f)
                towardPole = Vector3.ProjectOnPlane(a.up, dir);
            if (towardPole.sqrMagnitude < 1e-6f)
                towardPole = Vector3.right;
            towardPole.Normalize();

            Vector3 upperDir = (Mathf.Cos(angA) * dir + Mathf.Sin(angA) * towardPole).normalized;
            SwingTo(a, b, upperDir);
            SwingTo(b, c, target - b.position);
        }

        /// <summary>Rotates <paramref name="bone"/> so that its child points along <paramref name="worldDir"/>.</summary>
        public static void SwingTo(Transform bone, Transform child, Vector3 worldDir)
        {
            Vector3 from = child.position - bone.position;
            if (from.sqrMagnitude < 1e-8f || worldDir.sqrMagnitude < 1e-8f)
                return;
            bone.rotation = Quaternion.FromToRotation(from.normalized, worldDir.normalized) * bone.rotation;
        }

        /// <summary>Twists the forearm about its axis so the palm normal faces <paramref name="desiredPalmWorld"/>.</summary>
        public static void TwistPalm(Transform forearm, Transform hand, Vector3 palmLocal, Vector3 desiredPalmWorld, float maxDegrees = 120f)
        {
            if (forearm == null || hand == null || palmLocal.sqrMagnitude < 1e-6f || desiredPalmWorld.sqrMagnitude < 1e-6f)
                return;
            Vector3 axis = (hand.position - forearm.position).normalized;
            if (axis.sqrMagnitude < 1e-6f)
                return;
            Vector3 current = Vector3.ProjectOnPlane(hand.TransformDirection(palmLocal), axis);
            Vector3 want = Vector3.ProjectOnPlane(desiredPalmWorld, axis);
            if (current.sqrMagnitude < 1e-6f || want.sqrMagnitude < 1e-6f)
                return;
            float angle = Vector3.SignedAngle(current, want, axis);
            forearm.rotation = Quaternion.AngleAxis(Mathf.Clamp(angle, -maxDegrees, maxDegrees), axis) * forearm.rotation;
        }

        /// <summary>Flexes the wrist (hand bone) about the forearm's side axis, in degrees; positive = fingers down.</summary>
        public static void FlexWrist(Transform forearm, Transform hand, Vector3 palmWorld, float degrees)
        {
            if (forearm == null || hand == null || Mathf.Abs(degrees) < 0.01f)
                return;
            Vector3 axis = Vector3.Cross(palmWorld.normalized, (hand.position - forearm.position).normalized);
            if (axis.sqrMagnitude < 1e-6f)
                return;
            hand.rotation = Quaternion.AngleAxis(degrees, axis.normalized) * hand.rotation;
        }

        /// <summary>Aims a bone's forward at a world point with a clamped cone and a blend weight.</summary>
        public static void AimHead(Transform head, Vector3 bindForward, Vector3 target, float maxDegrees, float weight)
        {
            if (head == null || weight <= 0f)
                return;
            Vector3 dir = target - head.position;
            if (dir.sqrMagnitude < 1e-4f)
                return;
            dir.Normalize();
            float angle = Vector3.Angle(bindForward, dir);
            if (angle > maxDegrees)
                dir = Vector3.Slerp(bindForward, dir, maxDegrees / angle);
            Quaternion delta = Quaternion.FromToRotation(bindForward, dir);
            head.rotation = Quaternion.Slerp(head.rotation, delta * head.rotation, weight);
        }

        public static void SetLocal(Transform t, Quaternion rot, Vector3 pos)
        {
            if (t == null)
                return;
            t.localRotation = rot;
            t.localPosition = pos;
        }

        public static void SetRot(Transform t, Quaternion rot)
        {
            if (t != null)
                t.localRotation = rot;
        }

        public static void AddLocal(Transform t, Quaternion delta)
        {
            if (t != null)
                t.localRotation *= delta;
        }
    }
}
