using System.Collections.Generic;
using BasketballBetting.Shooting;
using UnityEngine;

namespace BasketballBetting.Presentation
{
    /// <summary>A recorded contact during the live shot.</summary>
    public struct RecordedContact
    {
        public float Time;
        public ShotContactKind Kind;
        public Vector3 Point;
    }

    /// <summary>
    /// A transform tape of one shot: ball pose plus every bone of the shooter (and defender) per frame,
    /// with the timeline markers presentation needs. Pure data; produced by ShotRecorder, consumed by
    /// ReplayDirector. Nothing in here can affect the simulation.
    /// </summary>
    public sealed class ShotRecording
    {
        public struct Frame
        {
            public float Time;
            public Vector3 BallPosition;
            public Quaternion BallRotation;
            public Vector3[] ShooterPositions;
            public Quaternion[] ShooterRotations;
            public Vector3[] DefenderPositions;
            public Quaternion[] DefenderRotations;
        }

        public readonly List<Frame> Frames = new List<Frame>(512);
        public readonly List<RecordedContact> Contacts = new List<RecordedContact>(8);
        public Vector3 RimCenter;
        public float ReleaseTime = -1f;
        public float LockTime = -1f;
        public float CrossingTime = -1f;
        public bool Made;
        public ShotStyle Style;
        public int ShooterBoneCount;
        public int DefenderBoneCount;

        public float StartTime => Frames.Count > 0 ? Frames[0].Time : 0f;
        public float EndTime => Frames.Count > 0 ? Frames[Frames.Count - 1].Time : 0f;
        public float Duration => EndTime - StartTime;
        public bool HasFrames => Frames.Count > 1;
        public bool HasRelease => ReleaseTime >= 0f;

        public float FirstGoalContactTime
        {
            get
            {
                for (int i = 0; i < Contacts.Count; i++)
                    if (Contacts[i].Kind == ShotContactKind.Rim || Contacts[i].Kind == ShotContactKind.Backboard)
                        return Contacts[i].Time;
                return -1f;
            }
        }

        public float DefenderContactTime
        {
            get
            {
                for (int i = 0; i < Contacts.Count; i++)
                    if (Contacts[i].Kind == ShotContactKind.Defender)
                        return Contacts[i].Time;
                return -1f;
            }
        }

        public bool HasContact(ShotContactKind kind)
        {
            for (int i = 0; i < Contacts.Count; i++)
                if (Contacts[i].Kind == kind)
                    return true;
            return false;
        }

        /// <summary>Finds the two frames bracketing <paramref name="time"/> and the blend between them.</summary>
        public bool TryBracket(float time, out int a, out int b, out float u)
        {
            a = b = 0;
            u = 0f;
            int n = Frames.Count;
            if (n == 0)
                return false;
            if (n == 1 || time <= Frames[0].Time)
                return true;
            if (time >= Frames[n - 1].Time)
            {
                a = b = n - 1;
                return true;
            }
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (Frames[mid].Time <= time) lo = mid; else hi = mid;
            }
            a = lo;
            b = hi;
            float span = Frames[hi].Time - Frames[lo].Time;
            u = span > 1e-5f ? (time - Frames[lo].Time) / span : 0f;
            return true;
        }

        public Vector3 BallPositionAt(float time)
        {
            if (!TryBracket(time, out int a, out int b, out float u))
                return RimCenter;
            return Vector3.Lerp(Frames[a].BallPosition, Frames[b].BallPosition, u);
        }

        public Vector3 BallVelocityAt(float time)
        {
            if (!TryBracket(time, out int a, out int b, out _) || a == b)
                return Vector3.zero;
            float dt = Frames[b].Time - Frames[a].Time;
            return dt > 1e-5f ? (Frames[b].BallPosition - Frames[a].BallPosition) / dt : Vector3.zero;
        }

        /// <summary>Closest the ball came to the rim centre after release.</summary>
        public float ClosestApproachToRim()
        {
            float best = float.MaxValue;
            for (int i = 0; i < Frames.Count; i++)
            {
                if (Frames[i].Time < ReleaseTime)
                    continue;
                float d = Vector3.Distance(Frames[i].BallPosition, RimCenter);
                if (d < best)
                    best = d;
            }
            return best;
        }
    }
}
