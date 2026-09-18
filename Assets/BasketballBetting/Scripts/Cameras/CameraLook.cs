using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BasketballBetting.Cameras
{
    /// <summary>
    /// The broadcast grade: one global URP Volume built in code so the look is versioned with the
    /// scripts. Two states: gameplay and replay (slightly tighter contrast, a touch of depth of field).
    /// </summary>
    public sealed class CameraLook : MonoBehaviour
    {
        Volume _volume;
        VolumeProfile _profile;
        Tonemapping _tone;
        Bloom _bloom;
        Vignette _vignette;
        ColorAdjustments _color;
        DepthOfField _dof;
        MotionBlur _motionBlur;
        WhiteBalance _whiteBalance;
        float _replayWeight;
        float _replayTarget;

        public static CameraLook Attach(GameObject host)
        {
            var look = host.GetComponent<CameraLook>();
            if (look == null)
                look = host.AddComponent<CameraLook>();
            look.Build();
            return look;
        }

        void Build()
        {
            _volume = GetComponent<Volume>();
            if (_volume == null)
                _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 50f;
            _volume.weight = 1f;
            if (_profile == null)
            {
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.name = "BroadcastLook (runtime)";
            }
            _volume.sharedProfile = _profile;

            _tone = Get<Tonemapping>();
            _tone.mode.Override(TonemappingMode.ACES);

            _bloom = Get<Bloom>();
            _bloom.threshold.Override(1.05f);
            _bloom.intensity.Override(0.28f);
            _bloom.scatter.Override(0.5f);
            _bloom.highQualityFiltering.Override(true);

            _vignette = Get<Vignette>();
            _vignette.intensity.Override(0.26f);
            _vignette.smoothness.Override(0.42f);
            _vignette.color.Override(new Color(0.02f, 0.02f, 0.04f));

            _color = Get<ColorAdjustments>();
            _color.postExposure.Override(0.15f);
            _color.contrast.Override(14f);
            _color.saturation.Override(12f);
            _color.colorFilter.Override(new Color(1f, 0.985f, 0.96f));

            _whiteBalance = Get<WhiteBalance>();
            _whiteBalance.temperature.Override(4f);
            _whiteBalance.tint.Override(-2f);

            _dof = Get<DepthOfField>();
            _dof.mode.Override(DepthOfFieldMode.Bokeh);
            _dof.focusDistance.Override(6f);
            _dof.focalLength.Override(50f);
            _dof.aperture.Override(5.6f);
            _dof.active = false;

            _motionBlur = Get<MotionBlur>();
            _motionBlur.intensity.Override(0.12f);
            _motionBlur.quality.Override(MotionBlurQuality.Medium);
            _motionBlur.active = true;
        }

        T Get<T>() where T : VolumeComponent
        {
            if (!_profile.TryGet(out T comp))
                comp = _profile.Add<T>(true);
            return comp;
        }

        /// <summary>Replay grade on/off (blended over a few frames).</summary>
        public void SetReplay(bool replay)
        {
            _replayTarget = replay ? 1f : 0f;
        }

        /// <summary>Focus distance for the replay depth of field (metres from the camera).</summary>
        public void SetFocus(float distance)
        {
            if (_dof != null)
                _dof.focusDistance.Override(Mathf.Max(0.3f, distance));
        }

        void Update()
        {
            if (_profile == null)
                return;
            float k = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            _replayWeight = Mathf.Lerp(_replayWeight, _replayTarget, k);
            _color.contrast.Override(Mathf.Lerp(14f, 22f, _replayWeight));
            _color.saturation.Override(Mathf.Lerp(12f, 6f, _replayWeight));
            _vignette.intensity.Override(Mathf.Lerp(0.26f, 0.4f, _replayWeight));
            _bloom.intensity.Override(Mathf.Lerp(0.28f, 0.38f, _replayWeight));
            _dof.active = _replayWeight > 0.05f;
            _dof.aperture.Override(Mathf.Lerp(16f, 4f, _replayWeight));
        }

        void OnDestroy()
        {
            if (_profile != null)
                Destroy(_profile);
        }
    }
}
