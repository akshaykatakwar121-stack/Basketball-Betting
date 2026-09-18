using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BasketballBetting
{
    /// <summary>
    /// Character-select only. Renders existing roster models into a hero texture
    /// and one portrait texture per player. Isolated from gameplay cameras/lights.
    /// </summary>
    public sealed class CharacterSelectStudio : MonoBehaviour
    {
        public const int PreviewLayer = 31;

        HumanoidRig[] _rigs;
        Camera _heroCam;
        Camera[] _cardCams;
        RenderTexture _heroRt;
        RenderTexture[] _cardRts;
        int _focus;

        public RenderTexture HeroTexture => _heroRt;

        public static CharacterSelectStudio Create(Transform parent)
        {
            var go = new GameObject("CharacterSelectStudio");
            go.transform.SetParent(parent, false);
            var studio = go.AddComponent<CharacterSelectStudio>();
            studio.Build();
            go.SetActive(false);
            return studio;
        }

        public RenderTexture CardTexture(int index)
        {
            if (_cardRts == null || index < 0 || index >= _cardRts.Length)
                return null;
            return _cardRts[index];
        }

        public void SetVisible(bool on)
        {
            if (gameObject.activeSelf == on)
            {
                if (on)
                    RenderNow();
                return;
            }
            gameObject.SetActive(on);
            if (on)
                RenderNow();
        }

        public void Focus(int index)
        {
            if (_rigs == null || _rigs.Length == 0)
                return;
            _focus = Mathf.Clamp(index, 0, _rigs.Length - 1);
            if (gameObject.activeInHierarchy)
            {
                FrameHero();
                if (_heroCam != null)
                    _heroCam.Render();
            }
        }

        void OnEnable()
        {
            RenderNow();
        }

        void LateUpdate()
        {
            if (!isActiveAndEnabled || _heroCam == null)
                return;
            FrameHero();
        }

        void OnDestroy()
        {
            Release(ref _heroRt);
            if (_cardRts == null)
                return;
            for (int i = 0; i < _cardRts.Length; i++)
                Release(ref _cardRts[i]);
        }

        void Build()
        {
            var all = CharacterCatalog.All;
            int n = all != null ? all.Length : 0;
            _rigs = new HumanoidRig[n];
            _cardCams = new Camera[n];
            _cardRts = new RenderTexture[n];

            transform.position = new Vector3(420f, 0f, 420f);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            StageLights();

            _heroRt = MakeRt(512, 768);
            _heroCam = MakeCamera("HeroCam", _heroRt, 26f, true);

            for (int i = 0; i < n; i++)
            {
                Vector3 stand = transform.position + Vector3.right * (i * 20f);
                _rigs[i] = SpawnAthlete(all[i], stand, i);
                _cardRts[i] = MakeRt(256, 384);
                _cardCams[i] = MakeCamera("CardCam" + i, _cardRts[i], 24f);
                FrameCard(_cardCams[i], _rigs[i]);
            }

            _focus = 0;
            FrameHero();
            RenderNow();
        }

        void RenderNow()
        {
            FrameHero();
            if (_heroCam != null && _heroCam.targetTexture != null)
                _heroCam.Render();
            if (_cardCams == null)
                return;
            for (int i = 0; i < _cardCams.Length; i++)
            {
                if (_cardCams[i] == null)
                    continue;
                FrameCard(_cardCams[i], _rigs[i]);
                _cardCams[i].Render();
            }
        }

        HumanoidRig SpawnAthlete(CharacterDefinition def, Vector3 stand, int index)
        {
            var pad = new GameObject("Stand" + index);
            pad.transform.SetParent(transform, false);
            pad.transform.position = stand;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Pad";
            floor.transform.SetParent(pad.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            floor.transform.localScale = new Vector3(2.2f, 0.02f, 2.2f);
            StripPhysics(floor);
            var floorRend = floor.GetComponent<MeshRenderer>();
            if (floorRend != null)
                floorRend.sharedMaterial = MaterialFactory.Opaque(new Color(0.10f, 0.11f, 0.13f), 0.22f, 0.04f, "SelectPad");

            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.name = "Backdrop";
            back.transform.SetParent(pad.transform, false);
            back.transform.localPosition = new Vector3(0f, 1.35f, 2.1f);
            back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            back.transform.localScale = new Vector3(4.6f, 3.2f, 1f);
            StripPhysics(back);
            var backRend = back.GetComponent<MeshRenderer>();
            if (backRend != null)
                backRend.sharedMaterial = MaterialFactory.Opaque(new Color(0.07f, 0.075f, 0.09f), 0.08f, 0f, "SelectBack");

            HumanoidRig rig = PlaceholderCharacterBuilder.Build(def, pad.transform);
            rig.Root.position = stand;
            rig.Root.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            StripPhysics(rig.Root.gameObject);

            var anim = rig.Root.gameObject.GetComponent<BasketballBetting.Characters.ShooterAnimator>();
            if (anim == null)
                anim = rig.Root.gameObject.AddComponent<BasketballBetting.Characters.ShooterAnimator>();
            anim.Rig = rig;
            anim.ForceEvaluate(HumanoidPose.TripleThreat, true);

            SetLayer(pad.transform, PreviewLayer);
            return rig;
        }

        void StageLights()
        {
            Dir("Key", new Vector3(28f, -32f, 0f), new Color(1f, 0.97f, 0.92f), 1.55f);
            Dir("Fill", new Vector3(62f, 18f, 0f), new Color(0.70f, 0.76f, 0.88f), 0.38f);
            Dir("Rim", new Vector3(12f, 155f, 0f), new Color(0.55f, 0.66f, 0.95f), 0.55f);
        }

        void Dir(string name, Vector3 euler, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << PreviewLayer;
        }

        Camera MakeCamera(string name, RenderTexture rt, float fov, bool live = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.06f, 0.075f, 1f);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 14f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.orthographic = false;
            cam.usePhysicalProperties = false;
            cam.depth = -80f;
            cam.cullingMask = 1 << PreviewLayer;
            cam.targetTexture = rt;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            var data = go.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
                data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            // Card cameras render on demand only; the hero camera stays live while the studio is visible.
            cam.enabled = live;
            go.SetActive(true);
            return cam;
        }

        void FrameHero()
        {
            if (_heroCam == null || _rigs == null || _rigs.Length == 0)
                return;
            FrameToFit(_heroCam, _rigs[_focus], 0.78f, new Vector3(0.22f, 0.04f, -1f));
        }

        static void FrameCard(Camera cam, HumanoidRig rig)
        {
            FrameToFit(cam, rig, 0.86f, new Vector3(0.08f, 0.06f, -1f));
        }

        static void FrameToFit(Camera cam, HumanoidRig rig, float fill, Vector3 view)
        {
            if (cam == null || rig == null || rig.Root == null)
                return;

            Bounds b = CharacterBounds(rig);
            float h = Mathf.Max(1.2f, b.size.y);
            Vector3 center = b.center;
            float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float dist = (h * 0.5f / Mathf.Clamp(fill, 0.45f, 0.95f)) / Mathf.Max(0.08f, Mathf.Tan(halfFov));
            Vector3 dir = view.normalized;
            cam.transform.position = center + dir * dist;
            cam.transform.LookAt(center, Vector3.up);
        }

        static Bounds CharacterBounds(HumanoidRig rig)
        {
            Vector3 fallback = rig.Root.position + Vector3.up * (Mathf.Max(1.6f, rig.Height) * 0.5f);
            var bounds = new Bounds(fallback, new Vector3(0.6f, Mathf.Max(1.6f, rig.Height), 0.6f));
            Renderer[] renderers = rig.Root.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled)
                    continue;
                if (!any)
                {
                    bounds = renderers[i].bounds;
                    any = true;
                }
                else
                    bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        static RenderTexture MakeRt(int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "CharSelectRT"
            };
            rt.Create();
            return rt;
        }

        static void Release(ref RenderTexture rt)
        {
            if (rt == null)
                return;
            rt.Release();
            Destroy(rt);
            rt = null;
        }

        static void StripPhysics(GameObject go)
        {
            if (go == null)
                return;
            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null)
                    continue;
                cols[i].enabled = false;
                Destroy(cols[i]);
            }
        }

        static void SetLayer(Transform root, int layer)
        {
            if (root == null)
                return;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                all[i].gameObject.layer = layer;
        }
    }
}
