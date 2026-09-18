using BasketballBetting.Shooting;
using UnityEngine;

namespace BasketballBetting.Presentation
{
    /// <summary>
    /// Samples the live shot every rendered frame (after animation and physics have posed everything)
    /// into a <see cref="ShotRecording"/>. Read-only observer: it never writes to any transform.
    /// </summary>
    [DefaultExecutionOrder(400)]
    public sealed class ShotRecorder : MonoBehaviour
    {
        public const int MaxFrames = 1200;

        ShotRecording _rec;
        Transform[] _shooterBones;
        Transform[] _defenderBones;
        Transform _ball;
        float _clock;
        bool _recording;

        public bool IsRecording => _recording;
        public ShotRecording Current => _rec;
        public float Clock => _clock;

        public void Begin(Transform shooterRoot, Transform defenderRoot, Transform ball, Vector3 rimCenter, ShotStyle style)
        {
            _rec = new ShotRecording { RimCenter = rimCenter, Style = style };
            _shooterBones = shooterRoot != null ? shooterRoot.GetComponentsInChildren<Transform>(true) : new Transform[0];
            _defenderBones = defenderRoot != null && defenderRoot.gameObject.activeInHierarchy ? defenderRoot.GetComponentsInChildren<Transform>(true) : new Transform[0];
            _rec.ShooterBoneCount = _shooterBones.Length;
            _rec.DefenderBoneCount = _defenderBones.Length;
            _ball = ball;
            _clock = 0f;
            _recording = true;
            Sample();
        }

        public void MarkRelease()
        {
            if (_rec != null && _rec.ReleaseTime < 0f)
                _rec.ReleaseTime = _clock;
        }

        public void MarkContact(ShotContactKind kind, Vector3 point)
        {
            if (_rec == null || !_recording)
                return;
            _rec.Contacts.Add(new RecordedContact { Time = _clock, Kind = kind, Point = point });
        }

        public void MarkCrossing()
        {
            if (_rec != null && _rec.CrossingTime < 0f)
                _rec.CrossingTime = _clock;
        }

        public void MarkLock(bool made)
        {
            if (_rec == null)
                return;
            _rec.Made = made;
            if (_rec.LockTime < 0f)
                _rec.LockTime = _clock;
        }

        public ShotRecording Stop()
        {
            if (_recording)
            {
                Sample();
                _recording = false;
            }
            ShotRecording done = _rec;
            _shooterBones = null;
            _defenderBones = null;
            _ball = null;
            return done;
        }

        public void Abort()
        {
            _recording = false;
            _rec = null;
            _shooterBones = null;
            _defenderBones = null;
            _ball = null;
        }

        void LateUpdate()
        {
            if (!_recording)
                return;
            _clock += Time.unscaledDeltaTime;
            Sample();
        }

        void Sample()
        {
            if (_rec == null || _rec.Frames.Count >= MaxFrames)
                return;
            var f = new ShotRecording.Frame
            {
                Time = _clock,
                BallPosition = _ball != null ? _ball.position : _rec.RimCenter,
                BallRotation = _ball != null ? _ball.rotation : Quaternion.identity
            };
            f.ShooterPositions = Copy(_shooterBones, out f.ShooterRotations);
            f.DefenderPositions = Copy(_defenderBones, out f.DefenderRotations);
            _rec.Frames.Add(f);
        }

        static Vector3[] Copy(Transform[] bones, out Quaternion[] rotations)
        {
            int n = bones != null ? bones.Length : 0;
            var pos = new Vector3[n];
            rotations = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                if (bones[i] == null)
                    continue;
                pos[i] = bones[i].position;
                rotations[i] = bones[i].rotation;
            }
            return pos;
        }
    }
}
