using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BasketballBetting.Cameras
{
    /// <summary>
    /// Owns the single game camera. Something (the round flow or the replay director) picks a
    /// <see cref="CameraShot"/>, keeps the <see cref="CameraContext"/> fresh, and this component blends
    /// the camera toward the shot's pose with per-shot damping. Cuts are hard or cross-blended.
    /// No gameplay knowledge lives here.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public sealed class GameCameraDirector : MonoBehaviour
    {
        [SerializeField] Camera _camera;
        [SerializeField] float _defaultBlend = 0.35f;
        [SerializeField] float _minHeight = 0.25f;

        readonly CameraContext _context = new CameraContext();
        CameraShot _shot;
        CameraShot _previousShot;
        float _shotTime;
        float _blendTime;
        float _blendDuration;
        CameraPose _current;
        CameraPose _fromPose;
        Vector3 _posVel;
        Vector3 _lookVel;
        float _fovVel;
        bool _initialised;
        float _shakeAmplitude;
        float _shakeDecay;
        float _shakeTime;
        bool _frozen;

        public Camera Camera => _camera;
        public CameraContext Context => _context;
        public CameraShot ActiveShot => _shot;
        public string ActiveShotName => _shot != null ? _shot.Name : "none";
        public float TimeInShot => _shotTime;

        public static GameCameraDirector Create(Camera camera = null)
        {
            var cam = camera != null ? camera : Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            var director = cam.GetComponent<GameCameraDirector>();
            if (director == null)
                director = cam.gameObject.AddComponent<GameCameraDirector>();
            director._camera = cam;
            director.ConfigureCamera();
            return director;
        }

        void Awake()
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();
            ConfigureCamera();
        }

        void ConfigureCamera()
        {
            if (_camera == null)
                return;
            _camera.nearClipPlane = 0.08f;
            _camera.farClipPlane = 120f;
            _camera.allowHDR = true;
            _camera.cullingMask &= ~(1 << GameLayers.Preview);
            var data = _camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
                data = _camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
        }

        /// <summary>Switches shots. blend ≤ 0 is a hard cut.</summary>
        public void Cut(CameraShot shot, float blend = -1f)
        {
            if (shot == null)
                return;
            if (blend < 0f)
                blend = _defaultBlend;
            _previousShot = _shot;
            _shot = shot;
            _shotTime = 0f;
            _blendDuration = _initialised ? blend : 0f;
            _blendTime = 0f;
            _fromPose = _current;
            _frozen = false;
            if (_blendDuration <= 0f && _initialised)
            {
                // Hard cut: start exactly on the new composition, damping continues from there.
                _current = Clamp(shot.Compose(_context, 0f));
                _posVel = Vector3.zero;
                _lookVel = Vector3.zero;
                _fovVel = 0f;
                ApplyPose(_current);
            }
        }

        /// <summary>Hard cut to a new shot.</summary>
        public void CutHard(CameraShot shot) => Cut(shot, 0f);

        /// <summary>Holds the current pose until the next cut (used for result freezes).</summary>
        public void Freeze() => _frozen = true;

        public void Shake(float amplitude, float decay = 6f)
        {
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, amplitude);
            _shakeDecay = decay;
            _shakeTime = 0f;
        }

        /// <summary>Immediate evaluation (editor previews, first frame after a scene load).</summary>
        public void SnapToShot()
        {
            if (_shot == null)
                return;
            _current = Clamp(_shot.Compose(_context, _shotTime));
            _posVel = Vector3.zero;
            _lookVel = Vector3.zero;
            _fovVel = 0f;
            _initialised = true;
            ApplyPose(_current);
        }

        void LateUpdate()
        {
            if (_camera == null || _shot == null)
                return;
            float dt = Time.unscaledDeltaTime;
            _shotTime += dt;
            _context.Narrowness = ComputeNarrowness();

            if (_frozen)
            {
                ApplyPose(_current);
                return;
            }

            CameraPose target = Clamp(_shot.Compose(_context, _shotTime));
            if (!_initialised)
            {
                _current = target;
                _initialised = true;
            }
            else
            {
                _current.Position = Vector3.SmoothDamp(_current.Position, target.Position, ref _posVel, _shot.PositionSmooth, Mathf.Infinity, dt);
                _current.LookAt = Vector3.SmoothDamp(_current.LookAt, target.LookAt, ref _lookVel, _shot.LookSmooth, Mathf.Infinity, dt);
                _current.Fov = Mathf.SmoothDamp(_current.Fov, target.Fov, ref _fovVel, _shot.FovSmooth, Mathf.Infinity, dt);
                _current.Roll = target.Roll;
            }

            CameraPose applied = _current;
            if (_blendDuration > 0f && _blendTime < _blendDuration)
            {
                _blendTime += dt;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_blendTime / _blendDuration));
                applied = CameraPose.Lerp(_fromPose, _current, t);
            }

            if (_shakeAmplitude > 0.0005f)
            {
                _shakeTime += dt;
                float a = _shakeAmplitude * Mathf.Exp(-_shakeDecay * _shakeTime);
                applied.Position += new Vector3(Mathf.PerlinNoise(_shakeTime * 21f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, _shakeTime * 19f) - 0.5f, 0f) * (a * 2f);
                if (a < 0.0005f)
                    _shakeAmplitude = 0f;
            }

            ApplyPose(applied);
        }

        CameraPose Clamp(CameraPose pose)
        {
            if (pose.Position.y < _minHeight)
                pose.Position.y = _minHeight;
            pose.Fov = Mathf.Clamp(pose.Fov, 18f, 80f);
            return pose;
        }

        void ApplyPose(CameraPose pose)
        {
            Transform t = _camera.transform;
            t.position = pose.Position;
            Vector3 dir = pose.LookAt - pose.Position;
            if (dir.sqrMagnitude > 1e-6f)
                t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, pose.Roll);
            _camera.fieldOfView = pose.Fov;
        }

        float ComputeNarrowness()
        {
            float aspect = _camera.pixelHeight > 0 ? (float)_camera.pixelWidth / _camera.pixelHeight : 16f / 9f;
            // 16:9 → 0, 9:19.5 → 1.
            return Mathf.Clamp01(Mathf.InverseLerp(16f / 9f, 9f / 19.5f, aspect));
        }
    }
}
