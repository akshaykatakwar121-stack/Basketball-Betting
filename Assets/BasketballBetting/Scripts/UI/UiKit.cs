using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UiImage = UnityEngine.UI.Image;

namespace BasketballBetting.Ui
{
    /// <summary>Design tokens: one palette, one type scale, one spacing rhythm for every screen.</summary>
    public static class UiTokens
    {
        // Surfaces
        public static readonly Color Ink = Hex("07080C");
        public static readonly Color Slate = Hex("12151C");
        public static readonly Color Panel = Hex("171B24");
        public static readonly Color PanelDeep = Hex("0E1118");
        public static readonly Color Line = new Color(0.36f, 0.40f, 0.50f, 0.35f);
        public static readonly Color LineStrong = new Color(0.55f, 0.60f, 0.72f, 0.55f);
        public static readonly Color Scrim = new Color(0.02f, 0.025f, 0.04f, 0.80f);

        // Brand
        public static readonly Color Accent = Hex("FF6A2B");       // court orange: primary actions, live moments
        public static readonly Color AccentDeep = Hex("C74A15");
        public static readonly Color Gold = Hex("E8C067");         // money
        public static readonly Color Navy = Hex("1B2A6B");
        public static readonly Color Ice = Hex("EAF0FF");          // primary text
        public static readonly Color Muted = Hex("8C96A8");
        public static readonly Color Win = Hex("3DDC97");
        public static readonly Color Loss = Hex("FF4D5E");

        // Extended broadcast palette
        public static readonly Color Carbon = Hex("0B0F17");       // deepest plates and bugs
        public static readonly Color Steel = Hex("1E2836");        // raised controls / secondary buttons
        public static readonly Color TextDim = Hex("8C96A8");      // alias matches football UITheme.TextDim
        public static readonly Color TextFaint = Hex("5B6779");    // footer text, audit lines
        public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.14f); // edge tint

        // Type scale (reference 1080-wide portrait; scaled by the CanvasScaler)
        public const int Display = 112;
        public const int DisplayXL = 132;
        public const int DisplayL = 92;
        public const int DisplayM = 64;
        public const int TitleL = 44;
        public const int TitleM = 34;
        public const int H1 = 64;
        public const int H2 = 40;
        public const int H3 = 28;
        public const int Body = 20;
        public const int Small = 16;
        public const int Micro = 13;

        /// <summary>Horizontal condensing applied to display type so the default face reads like a sports condensed cut.</summary>
        public const float DisplayCondense = 0.92f;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }

        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    /// <summary>Procedural sprites shared by every widget (generated once, never from disk).</summary>
    public static class UiSprites
    {
        static Sprite _round12, _round20, _round32, _pill, _glow, _sheen, _vGrad, _hGrad, _ring, _dot, _outline20, _outlinePill, _pixel, _chevron;

        public static Sprite Round12 => _round12 != null ? _round12 : (_round12 = Rounded(64, 12));
        public static Sprite Round20 => _round20 != null ? _round20 : (_round20 = Rounded(96, 20));
        public static Sprite Round32 => _round32 != null ? _round32 : (_round32 = Rounded(128, 32));
        public static Sprite Pill => _pill != null ? _pill : (_pill = Rounded(128, 63));
        public static Sprite Glow => _glow != null ? _glow : (_glow = Radial(256));
        public static Sprite Sheen => _sheen != null ? _sheen : (_sheen = Gradient(4, 128, true, 0.35f, 0f));
        public static Sprite VerticalGradient => _vGrad != null ? _vGrad : (_vGrad = Gradient(4, 128, true, 1f, 0f));
        public static Sprite HorizontalGradient => _hGrad != null ? _hGrad : (_hGrad = Gradient(128, 4, false, 1f, 0f));
        public static Sprite Ring => _ring != null ? _ring : (_ring = RingSprite(128, 10));
        public static Sprite Dot => _dot != null ? _dot : (_dot = Radial(64, hard: true));
        /// <summary>2.5px stroke rounded rectangle (radius 20), sliced.</summary>
        public static Sprite Outline20 => _outline20 != null ? _outline20 : (_outline20 = RoundedOutline(96, 20, 2.5f));
        public static Sprite OutlinePill => _outlinePill != null ? _outlinePill : (_outlinePill = RoundedOutline(128, 63, 2.5f));
        /// <summary>1Ã—1 white pixel for hairline rules and solid fills.</summary>
        public static Sprite Pixel => _pixel != null ? _pixel : (_pixel = PixelSprite());
        /// <summary>A right-pointing chevron icon (flip with negative x scale for left).</summary>
        public static Sprite Chevron => _chevron != null ? _chevron : (_chevron = ChevronSprite());

        static Sprite RoundedOutline(int size, int radius, float stroke)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UiOutline" + radius };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Min(x + 0.5f, size - x - 0.5f);
                    float dy = Mathf.Min(y + 0.5f, size - y - 0.5f);
                    // signed distance to the rounded-rect edge (positive inside)
                    float d;
                    if (dx < radius && dy < radius)
                        d = radius - Vector2.Distance(new Vector2(dx, dy), new Vector2(radius, radius));
                    else
                        d = Mathf.Min(dx, dy);
                    float outer = Mathf.Clamp01(d + 0.5f);
                    float inner = Mathf.Clamp01(d - stroke + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, outer - inner);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Sprite Rounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UiRound" + radius };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Min(x + 0.5f, size - x - 0.5f);
                    float dy = Mathf.Min(y + 0.5f, size - y - 0.5f);
                    float a = 1f;
                    if (dx < radius && dy < radius)
                    {
                        float d = Vector2.Distance(new Vector2(dx, dy), new Vector2(radius, radius));
                        a = Mathf.Clamp01(radius - d + 0.5f);
                    }
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Sprite Radial(int size, bool hard = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = hard ? "UiDot" : "UiGlow" };
            var px = new Color[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                    float a = hard ? Mathf.Clamp01((1f - d) * c) : Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite Gradient(int w, int h, bool vertical, float top, float bottom)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UiGradient" };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = vertical ? y / (float)(h - 1) : x / (float)(w - 1);
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Lerp(bottom, top, t));
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite RingSprite(int size, int thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UiRing" };
            var px = new Color[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float a = Mathf.Clamp01(c - d) * Mathf.Clamp01(d - (c - thickness));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite PixelSprite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "UiPixel" };
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite ChevronSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UiChevron" };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = (x + 0.5f) / size, fy = (y + 0.5f) / size;
                    float upper = Mathf.Abs((fy - 0.5f) - (0.7f - fx));
                    float lower = Mathf.Abs((0.5f - fy) - (0.7f - fx));
                    float d = fy >= 0.5f ? upper : lower;
                    bool inRange = fx > 0.28f && fx < 0.72f;
                    float a = inRange ? Mathf.Clamp01((0.09f - d) * 22f) : 0f;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // â”€â”€ Broadcast sprite generators (chamfer, stripes, slash, etc.) â”€â”€

        static readonly System.Collections.Generic.Dictionary<string, Sprite> _cache
            = new System.Collections.Generic.Dictionary<string, Sprite>();

        static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        static Sprite Finish(string key, Texture2D tex, Vector4 border)
        {
            tex.Apply(false, false);
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            _cache[key] = s;
            return s;
        }

        static bool TryCached(string key, out Sprite s) =>
            _cache.TryGetValue(key, out s) && s != null;

        static float RndAlpha(float x, float y, float w, float h, float r)
        {
            float cx = Mathf.Clamp(x, r, w - r);
            float cy = Mathf.Clamp(y, r, h - r);
            return Mathf.Clamp01(r - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) + 0.5f);
        }

        /// <summary>9-sliced chamfered plate (diagonal top-right and bottom-left cuts). The broadcast shape.</summary>
        public static Sprite Chamfer(int cut)
        {
            string key = "chamfer" + cut;
            if (TryCached(key, out var cached)) return cached;
            const int sz = 64;
            cut = Mathf.Clamp(cut, 2, sz / 2 - 2);
            var tex = NewTex(sz, sz);
            var px = new Color32[sz * sz];
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float fx = x + .5f, fy = y + .5f;
                    float a = Mathf.Clamp01(Mathf.Min((sz - fx) + (sz - fy) - cut, fx + fy - cut) * .7071f + .5f);
                    px[y * sz + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            return Finish(key, tex, new Vector4(cut + 2, cut + 2, cut + 2, cut + 2));
        }

        /// <summary>9-sliced outline of the chamfer plate. Use for hairline edges.</summary>
        public static Sprite ChamferOutline(int cut, int thickness = 2)
        {
            string key = "chamferline" + cut + "_" + thickness;
            if (TryCached(key, out var cached)) return cached;
            const int sz = 64;
            cut = Mathf.Clamp(cut, 2, sz / 2 - 2);
            var tex = NewTex(sz, sz);
            var px = new Color32[sz * sz];
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float fx = x + .5f, fy = y + .5f;
                    float d = Mathf.Min(Mathf.Min(fx, fy), Mathf.Min(sz - fx, sz - fy));
                    d = Mathf.Min(d, ((sz - fx) + (sz - fy) - cut) * .7071f);
                    d = Mathf.Min(d, (fx + fy - cut) * .7071f);
                    float a = Mathf.Clamp01(d + .5f) - Mathf.Clamp01(d - thickness + .5f);
                    px[y * sz + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            int border = cut + thickness + 2;
            return Finish(key, tex, new Vector4(border, border, border, border));
        }

        /// <summary>Tileable 45Â° hairline stripes for faint surface texture.</summary>
        public static Sprite Stripes()
        {
            const string key = "stripes";
            if (TryCached(key, out var cached)) return cached;
            const int sz = 32;
            var tex = NewTex(sz, sz);
            tex.wrapMode = TextureWrapMode.Repeat;
            var px = new Color32[sz * sz];
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                    px[y * sz + x] = new Color32(255, 255, 255, (byte)(((x + y) % 8 < 2) ? 255 : 0));
            tex.SetPixels32(px);
            return Finish(key, tex, Vector4.zero);
        }

        /// <summary>Antialiased filled circle.</summary>
        public static Sprite Circle()
        {
            const string key = "circle";
            if (TryCached(key, out var cached)) return cached;
            const int sz = 128;
            var tex = NewTex(sz, sz);
            var px = new Color32[sz * sz];
            float r = sz * .5f - 1f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(sz * .5f, sz * .5f));
                    px[y * sz + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + .5f) * 255f));
                }
            tex.SetPixels32(px);
            return Finish(key, tex, Vector4.zero);
        }

        /// <summary>Ring sprite for radial countdowns. thickness01 is fraction of radius.</summary>
        public static Sprite RadialRing(float thickness01 = 0.16f)
        {
            string key = "ring" + Mathf.RoundToInt(thickness01 * 100f);
            if (TryCached(key, out var cached)) return cached;
            const int sz = 256;
            var tex = NewTex(sz, sz);
            var px = new Color32[sz * sz];
            float outer = sz * .5f - 1f, inner = outer * (1f - thickness01);
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(sz * .5f, sz * .5f));
                    float a = Mathf.Clamp01(outer - d + .5f) * Mathf.Clamp01(d - inner + .5f);
                    px[y * sz + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            return Finish(key, tex, Vector4.zero);
        }

        /// <summary>Vertical gradient solid-at-bottom transparent-at-top. Used for floor scrims.</summary>
        public static Sprite GradientDown()
        {
            const string key = "gradient_down";
            if (TryCached(key, out var cached)) return cached;
            const int w = 4, h = 64;
            var tex = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) { float a = 1f - (float)y / (h - 1); a = a * a; for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f)); }
            tex.SetPixels32(px);
            return Finish(key, tex, Vector4.zero);
        }

        /// <summary>Vertical gradient solid-at-top transparent-at-bottom. Used for ceiling scrims and sheens.</summary>
        public static Sprite GradientUp()
        {
            const string key = "gradient_up";
            if (TryCached(key, out var cached)) return cached;
            const int w = 4, h = 64;
            var tex = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) { float a = (float)y / (h - 1); a = a * a; for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f)); }
            tex.SetPixels32(px);
            return Finish(key, tex, Vector4.zero);
        }

        /// <summary>Diagonal broadcast slash used behind verdict banners.</summary>
        public static Sprite Slash()
        {
            const string key = "slash";
            if (TryCached(key, out var cached)) return cached;
            const int w = 128, h = 64;
            var tex = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w, v = (y + .5f) / h;
                    float d = Mathf.Abs((v - .5f) - (u - .5f) * .35f);
                    float a = Mathf.Clamp01(1f - d * 4.5f);
                    a *= Mathf.SmoothStep(0f, 1f, u) * Mathf.SmoothStep(0f, 1f, 1f - u);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            return Finish(key, tex, Vector4.zero);
        }

        /// <summary>9-sliced rounded rectangle (for shadows).</summary>
        public static Sprite RoundedRect(int radius)
        {
            string key = "rr" + radius;
            if (TryCached(key, out var cached)) return cached;
            const int sz = 64;
            radius = Mathf.Clamp(radius, 1, sz / 2 - 1);
            var tex = NewTex(sz, sz);
            var px = new Color32[sz * sz];
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                    px[y * sz + x] = new Color32(255, 255, 255, (byte)(RndAlpha(x + .5f, y + .5f, sz, sz, radius) * 255f));
            tex.SetPixels32(px);
            return Finish(key, tex, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        }
    }

    /// <summary>Layout + widget factory. Every element is anchored by fractions so both orientations work.</summary>
    public static class Widgets
    {
        public static Canvas CreateCanvas(Transform parent, int order)
        {
            var go = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            canvas.pixelPerfect = false;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt, Vector2.zero, Vector2.one);
            return rt;
        }

        public static RectTransform Panel(Transform parent, string name, Color color, Vector2 min, Vector2 max, Sprite sprite = null, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UiImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var img = go.GetComponent<UiImage>();
            img.sprite = sprite != null ? sprite : UiSprites.Round20;
            img.type = UiImage.Type.Sliced;
            img.color = color;
            img.raycastTarget = raycast;
            Stretch(rt, min, max);
            return rt;
        }

        /// <summary>A layered card: deep base, gradient body, hairline top highlight. The signature surface.</summary>
        public static RectTransform Card(Transform parent, string name, Vector2 min, Vector2 max, Color? tint = null, bool raycast = false)
        {
            var root = Panel(parent, name, tint ?? UiTokens.Panel, min, max, UiSprites.Round20, raycast);
            var body = Panel(root, "Body", new Color(1f, 1f, 1f, 0.05f), Vector2.zero, Vector2.one, UiSprites.VerticalGradient);
            body.GetComponent<UiImage>().type = UiImage.Type.Simple;
            var hair = Panel(root, "Hairline", UiTokens.Alpha(Color.white, 0.10f), new Vector2(0.02f, 1f), new Vector2(0.98f, 1f), UiSprites.Round12);
            hair.offsetMin = new Vector2(0f, -2f);
            hair.offsetMax = new Vector2(0f, 0f);
            Panel(root, "Edge", UiTokens.Alpha(UiTokens.LineStrong, 0.45f), Vector2.zero, Vector2.one, UiSprites.Outline20);
            return root;
        }

        public static RectTransform Glow(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var rt = Panel(parent, name, color, min, max, UiSprites.Glow);
            rt.GetComponent<UiImage>().type = UiImage.Type.Simple;
            return rt;
        }

        public static RectTransform Rule(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            return Panel(parent, name, color, min, max, UiSprites.Round12);
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Bold, bool shadow = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt, Vector2.zero, Vector2.one);
            var t = go.GetComponent<Text>();
            t.font = FontUtil.Get();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            if (shadow)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
                sh.effectDistance = new Vector2(0f, -Mathf.Max(2f, size * 0.04f));
            }
            return t;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor, Vector2 min, Vector2 max, FontStyle style = FontStyle.Bold, bool shadow = false)
        {
            Text t = Label(parent, name, text, size, color, anchor, style, shadow);
            Stretch(t.rectTransform, min, max);
            return t;
        }

        public enum ButtonStyle { Primary, Gold, Ghost, Quiet, Danger }

        public static Button Button(Transform parent, string name, string label, ButtonStyle style, Action onClick, int size, Vector2 min, Vector2 max)
        {
            Color bg, fg, edge;
            switch (style)
            {
                case ButtonStyle.Primary: bg = UiTokens.Accent; fg = UiTokens.Ice; edge = UiTokens.Alpha(Color.white, 0.25f); break;
                case ButtonStyle.Gold: bg = UiTokens.Alpha(UiTokens.Gold, 0.14f); fg = UiTokens.Gold; edge = UiTokens.Alpha(UiTokens.Gold, 0.8f); break;
                case ButtonStyle.Danger: bg = UiTokens.Alpha(UiTokens.Loss, 0.16f); fg = UiTokens.Loss; edge = UiTokens.Alpha(UiTokens.Loss, 0.7f); break;
                case ButtonStyle.Quiet: bg = UiTokens.Alpha(UiTokens.Slate, 0.85f); fg = UiTokens.Muted; edge = UiTokens.Line; break;
                default: bg = UiTokens.Alpha(UiTokens.Panel, 0.92f); fg = UiTokens.Ice; edge = UiTokens.LineStrong; break;
            }
            var rt = Panel(parent, name, bg, min, max, UiSprites.Round20, true);
            var img = rt.GetComponent<UiImage>();
            if (style == ButtonStyle.Primary)
            {
                var sheen = Panel(rt, "Sheen", UiTokens.Alpha(Color.white, 0.22f), new Vector2(0f, 0.5f), Vector2.one, UiSprites.Sheen);
                sheen.GetComponent<UiImage>().type = UiImage.Type.Simple;
            }
            Panel(rt, "Edge", edge, Vector2.zero, Vector2.one, UiSprites.Outline20);
            Label(rt, "Label", label, size, fg, TextAnchor.MiddleCenter, FontStyle.Bold, style == ButtonStyle.Primary);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            c.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            c.selectedColor = Color.white;
            c.fadeDuration = 0.06f;
            btn.colors = c;
            btn.onClick.AddListener(() => onClick?.Invoke());
            rt.gameObject.AddComponent<ButtonFx>();
            return btn;
        }

        public static RawImage Raw(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt, min, max);
            var raw = go.GetComponent<RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = false;
            return raw;
        }

        public static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        /// <summary>Four L-shaped corner marks: the broadcast viewfinder look.</summary>
        public static RectTransform CornerBrackets(Transform parent, string name, float inset, float arm, float thickness, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var root = go.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            Stretch(root, Vector2.zero, Vector2.one);
            for (int i = 0; i < 4; i++)
            {
                bool right = (i & 1) == 1;
                bool top   = i < 2;
                Vector2 anchor = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
                float sx = right ? -1f : 1f;
                float sy = top   ? -1f : 1f;
                // Horizontal arm
                var h = Panel(root, "H" + i, color, Vector2.zero, Vector2.zero, UiSprites.Pixel);
                h.GetComponent<UiImage>().type = UiImage.Type.Simple;
                var hrt = h.GetComponent<RectTransform>();
                hrt.anchorMin = hrt.anchorMax = anchor;
                hrt.pivot = anchor;
                hrt.anchoredPosition = new Vector2(sx * inset, sy * inset);
                hrt.sizeDelta = new Vector2(arm, thickness);
                // Vertical arm
                var v = Panel(root, "V" + i, color, Vector2.zero, Vector2.zero, UiSprites.Pixel);
                v.GetComponent<UiImage>().type = UiImage.Type.Simple;
                var vrt = v.GetComponent<RectTransform>();
                vrt.anchorMin = vrt.anchorMax = anchor;
                vrt.pivot = anchor;
                vrt.anchoredPosition = new Vector2(sx * inset, sy * inset);
                vrt.sizeDelta = new Vector2(thickness, arm);
            }
            return root;
        }

        public static Text SetText(Text t, string s)
        {
            if (t != null) t.text = s;
            return t;
        }

        // â”€â”€ Broadcast widget helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Anchor at a normalised point with a fixed size. Same as Place but (anchor,pivot,pos,size) order matching football UIKit.</summary>
        public static void PlaceAt(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot; rt.anchoredPosition = position; rt.sizeDelta = size;
        }

        /// <summary>Stretch with pixel insets on all four sides.</summary>
        public static void Stretch(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Full-width bar anchored to top or bottom edge of parent.</summary>
        public static void Bar(RectTransform rt, bool top, float height, float offset = 0f)
        {
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.anchoredPosition = new Vector2(0f, top ? -offset : offset);
            rt.sizeDelta = new Vector2(0f, height);
        }

        /// <summary>Full-stretch panel with a solid sprite (no slicing).</summary>
        public static UiImage PanelStretched(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UiImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt, Vector2.zero, Vector2.one);
            var img = go.GetComponent<UiImage>();
            img.sprite = sprite;
            img.color = color;
            img.type = UiImage.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Layered broadcast plate: shadow + chamfered body + stripes + sheen + hairline edge + accent strip.</summary>
        public static UiImage Layered(Transform parent, string name, Color body, Color accent, int chamfer = 14,
            bool stripes = true, bool shadow = true, bool raycast = true)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            Stretch(root, Vector2.zero, Vector2.one);
            if (shadow)
            {
                var shGo = new GameObject("Shadow", typeof(RectTransform), typeof(UiImage));
                shGo.GetComponent<RectTransform>().SetParent(root, false);
                Stretch(shGo.GetComponent<RectTransform>(), 6f, 2f, 6f, 12f);
                var shImg = shGo.GetComponent<UiImage>();
                shImg.sprite = UiSprites.RoundedRect(20);
                shImg.type = UiImage.Type.Sliced;
                shImg.color = new Color(0f, 0f, 0f, 0.45f);
                shImg.raycastTarget = false;
            }
            var plateGo = new GameObject("Plate", typeof(RectTransform), typeof(UiImage));
            var plateRt = plateGo.GetComponent<RectTransform>();
            plateRt.SetParent(root, false);
            Stretch(plateRt, Vector2.zero, Vector2.one);
            var plate = plateGo.GetComponent<UiImage>();
            plate.sprite = UiSprites.Chamfer(chamfer);
            plate.type = UiImage.Type.Sliced;
            plate.color = body;
            plate.raycastTarget = raycast;
            if (stripes)
            {
                var stGo = new GameObject("Stripes", typeof(RectTransform), typeof(UiImage));
                stGo.GetComponent<RectTransform>().SetParent(plateGo.transform, false);
                Stretch(stGo.GetComponent<RectTransform>(), 6f, 6f, 6f, 6f);
                var stImg = stGo.GetComponent<UiImage>();
                stImg.sprite = UiSprites.Stripes();
                stImg.type = UiImage.Type.Tiled;
                stImg.color = new Color(1f, 1f, 1f, 0.035f);
                stImg.raycastTarget = false;
            }
            MakeLayerImage(plateGo.transform, "Sheen", UiSprites.GradientUp(), new Color(1f, 1f, 1f, 0.05f), 2f, 2f, 2f, 2f);
            var edgeGo = MakeLayerImage(plateGo.transform, "Edge", UiSprites.ChamferOutline(chamfer), new Color(1f, 1f, 1f, 0.14f), 0f, 0f, 0f, 0f);
            edgeGo.type = UiImage.Type.Sliced;
            var acGo = new GameObject("Accent", typeof(RectTransform), typeof(UiImage));
            var acRt = acGo.GetComponent<RectTransform>();
            acRt.SetParent(plateGo.transform, false);
            acRt.anchorMin = new Vector2(0f, 1f); acRt.anchorMax = new Vector2(1f, 1f);
            acRt.pivot = new Vector2(0.5f, 1f); acRt.anchoredPosition = Vector2.zero;
            acRt.sizeDelta = new Vector2(-(chamfer * 2f + 6f), 3f);
            acGo.GetComponent<UiImage>().sprite = UiSprites.Pixel;
            acGo.GetComponent<UiImage>().color = accent;
            acGo.GetComponent<UiImage>().raycastTarget = false;
            return plate;
        }

        static UiImage MakeLayerImage(Transform parent, string name, Sprite sprite, Color color,
            float left, float top, float right, float bottom)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UiImage));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), left, top, right, bottom);
            var img = go.GetComponent<UiImage>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            return img;
        }

        /// <summary>Layered plate placed in one call. Returns the plate image; its parent carries the placement.</summary>
        public static UiImage Card(Transform parent, string name, Color body, Color accent,
            Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position,
            int chamfer = 14, bool stripes = true, bool shadow = true)
        {
            var plate = Layered(parent, name, body, accent, chamfer, stripes, shadow);
            PlaceAt((RectTransform)plate.transform.parent, anchor, pivot, position, size);
            return plate;
        }

        /// <summary>Returns the RectTransform that carries a card's placement (the plate's parent).</summary>
        public static RectTransform RootOf(Component plateOrChild)
        {
            var t = plateOrChild.transform;
            while (t.parent != null && t.name != "Plate") t = t.parent;
            return (RectTransform)(t.name == "Plate" && t.parent != null ? t.parent : t);
        }

        // â”€â”€ Button factories â”€â”€

        public static Button Primary(Transform parent, string name, string label, int fontSize, System.Action onClick, int chamfer = 14)
        {
            var btn = ChamferButton(parent, name, label, fontSize, onClick, chamfer, UiTokens.Gold, UiTokens.Ink, new Color(1f, 1f, 1f, 0.45f));
            var stripes = MakeLayerImage((RectTransform)btn.transform, "Stripes", UiSprites.Stripes(), new Color(0f, 0f, 0f, 0.08f), 0f, 0f, 0f, 0f);
            stripes.type = UiImage.Type.Tiled;
            stripes.transform.SetSiblingIndex(1);
            return btn;
        }

        public static Button Secondary(Transform parent, string name, string label, int fontSize, System.Action onClick, int chamfer = 14)
            => ChamferButton(parent, name, label, fontSize, onClick, chamfer, UiTokens.Steel, UiTokens.Ice, UiTokens.Hairline);

        public static Button Ghost(Transform parent, string name, string label, int fontSize, System.Action onClick, int chamfer = 12)
            => ChamferButton(parent, name, label, fontSize, onClick, chamfer, new Color(1f, 1f, 1f, 0.04f), UiTokens.Muted, new Color(1f, 1f, 1f, 0.22f));

        static Button ChamferButton(Transform parent, string name, string label, int fontSize,
            System.Action onClick, int chamfer, Color bg, Color fg, Color edge)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UiImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var img = go.GetComponent<UiImage>();
            img.sprite = UiSprites.Chamfer(chamfer);
            img.type = UiImage.Type.Sliced;
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            go.AddComponent<ButtonFx>();
            MakeLayerImage(rt, "Sheen", UiSprites.GradientUp(), new Color(1f, 1f, 1f, 0.09f), 3f, 2f, 3f, 2f);
            var edgeImg = MakeLayerImage(rt, "Edge", UiSprites.ChamferOutline(chamfer), edge, 0f, 0f, 0f, 0f);
            edgeImg.type = UiImage.Type.Sliced;
            Label(rt, "Label", label, fontSize, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            return btn;
        }

        public static Button InvisibleButton(Transform parent, string name, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UiImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt, Vector2.zero, Vector2.one);
            var img = go.GetComponent<UiImage>();
            img.color = new Color(0f, 0f, 0f, 0f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static Button Chip(Transform parent, string name, string label, int fontSize, System.Action onClick)
        {
            var b = ChamferButton(parent, name, label, fontSize, onClick, 10, UiTokens.Steel, UiTokens.Ice, UiTokens.Hairline);
            var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(UiImage));
            glowGo.GetComponent<RectTransform>().SetParent(b.transform, false);
            Stretch(glowGo.GetComponent<RectTransform>(), -4f, -4f, -4f, -4f);
            var gImg = glowGo.GetComponent<UiImage>();
            gImg.sprite = UiSprites.Chamfer(10); gImg.type = UiImage.Type.Sliced;
            gImg.color = UiTokens.Alpha(UiTokens.Gold, 0f); gImg.raycastTarget = false;
            glowGo.transform.SetAsFirstSibling();
            return b;
        }

        public static void SetChipSelected(Button chip, bool on)
        {
            if (chip == null) return;
            var img = chip.GetComponent<UiImage>();
            if (img != null) img.color = on ? UiTokens.Gold : UiTokens.Steel;
            var lbl = chip.transform.Find("Label")?.GetComponent<Text>();
            if (lbl != null) lbl.color = on ? UiTokens.Ink : UiTokens.Ice;
            var glow = chip.transform.Find("Glow")?.GetComponent<UiImage>();
            if (glow != null) glow.color = UiTokens.Alpha(UiTokens.Gold, on ? 0.35f : 0f);
            var edgeImg = chip.transform.Find("Edge")?.GetComponent<UiImage>();
            if (edgeImg != null) edgeImg.color = on ? new Color(1f, 1f, 1f, 0.5f) : UiTokens.Hairline;
            if (on) UiTween.Punch((RectTransform)chip.transform, 0.22f, 1.06f, 0.94f);
        }

        // â”€â”€ Text factories â”€â”€

        /// <summary>Bold italic condensed display type with outline. For titles and verdict banners.</summary>
        public static Text Display(Transform parent, string name, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, bool italic = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt, Vector2.zero, Vector2.one);
            var t = go.GetComponent<Text>();
            t.font = FontUtil.Get();
            t.text = text; t.fontSize = size; t.color = color;
            t.alignment = anchor;
            t.fontStyle = italic ? FontStyle.BoldAndItalic : FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; t.supportRichText = true;
            rt.localScale = new Vector3(UiTokens.DisplayCondense, 1f, 1f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.45f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        /// <summary>Tracked uppercase caption label (spaced characters like sports broadcast).</summary>
        public static Text Caption(Transform parent, string name, string text, Color color,
            int size, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt, Vector2.zero, Vector2.one);
            var t = go.GetComponent<Text>();
            t.font = FontUtil.Get();
            t.text = Track(text); t.fontSize = size; t.color = color;
            t.alignment = anchor; t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Inserts thin spaces between characters for the broadcast tracked-type look.</summary>
        public static string Track(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new System.Text.StringBuilder(text.Length * 2);
            string up = text.ToUpperInvariant();
            for (int i = 0; i < up.Length; i++) { sb.Append(up[i]); if (i < up.Length - 1 && up[i] != ' ') sb.Append(' '); }
            return sb.ToString();
        }

        public static Image Hairline(Transform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UiImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.sizeDelta = size;
            var img = go.GetComponent<UiImage>();
            img.sprite = UiSprites.Pixel; img.color = color; img.raycastTarget = false;
            return img;
        }

        // â”€â”€ Layout helpers â”€â”€

        public static CanvasGroup Group(Component c)
        {
            return c.gameObject.GetComponent<CanvasGroup>() ?? c.gameObject.AddComponent<CanvasGroup>();
        }

        public static HorizontalLayoutGroup Row(Transform parent, string name, float spacing)
        {
            var rt = Rect(parent, name);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false; h.childControlHeight = false;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        public static LayoutElement Size(Component c, float width, float height)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = height;
            le.minWidth = width; le.minHeight = height;
            ((RectTransform)c.transform).sizeDelta = new Vector2(width, height);
            return le;
        }

        public static void DestroyGo(UnityEngine.Object obj, bool immediate = false)
        {
            if (obj == null) return;
            if (!Application.isPlaying || immediate) UnityEngine.Object.DestroyImmediate(obj);
            else UnityEngine.Object.Destroy(obj);
        }

        public static void SetButtonLabel(Button b, string text)
        {
            var t = b != null ? b.transform.Find("Label") : null;
            var tx = t != null ? t.GetComponent<Text>() : null;
            if (tx != null) tx.text = text;
        }

        /// <summary>Simple segmented control: a track with a sliding chamfered highlight.</summary>
        public sealed class Segmented
        {
            public RectTransform Root;
            public Button[] Buttons;
            public Text[] Labels;
            int _selected = -1;
            float _segW;

            public Segmented(Transform parent, string name, string[] labels, int fontSize, System.Action<int> onPick, Vector2 size)
            {
                Root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                Root.SetParent(parent, false); Root.sizeDelta = size;
                _segW = (size.x - 8f) / labels.Length;
                var trackGo = new GameObject("Track", typeof(RectTransform), typeof(UiImage));
                trackGo.GetComponent<RectTransform>().SetParent(Root, false);
                Stretch(trackGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
                trackGo.GetComponent<UiImage>().sprite = UiSprites.Chamfer(12);
                trackGo.GetComponent<UiImage>().type = UiImage.Type.Sliced;
                trackGo.GetComponent<UiImage>().color = UiTokens.Alpha(UiTokens.Carbon, 0.9f);
                var edgeGo = new GameObject("Edge", typeof(RectTransform), typeof(UiImage));
                edgeGo.GetComponent<RectTransform>().SetParent(Root, false);
                Stretch(edgeGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
                edgeGo.GetComponent<UiImage>().sprite = UiSprites.ChamferOutline(12);
                edgeGo.GetComponent<UiImage>().type = UiImage.Type.Sliced;
                edgeGo.GetComponent<UiImage>().color = UiTokens.Hairline;
                edgeGo.GetComponent<UiImage>().raycastTarget = false;
                var hlGo = new GameObject("Highlight", typeof(RectTransform), typeof(UiImage));
                var hlRt = hlGo.GetComponent<RectTransform>();
                hlRt.SetParent(Root, false);
                Place(hlRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(_segW, size.y - 8f), new Vector2(4f, 0f));
                hlGo.GetComponent<UiImage>().sprite = UiSprites.Chamfer(10);
                hlGo.GetComponent<UiImage>().type = UiImage.Type.Sliced;
                hlGo.GetComponent<UiImage>().color = UiTokens.Accent;
                Buttons = new Button[labels.Length];
                Labels = new Text[labels.Length];
                for (int i = 0; i < labels.Length; i++)
                {
                    int idx = i;
                    var b = InvisibleButton(Root, "Seg" + i, () => onPick?.Invoke(idx));
                    var brt = (RectTransform)b.transform;
                    Place(brt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(_segW, size.y), new Vector2(4f + _segW * i, 0f));
                    Buttons[i] = b;
                    Labels[i] = Label(brt, "Label", labels[i], fontSize, UiTokens.Muted, TextAnchor.MiddleCenter);
                    Stretch(Labels[i].rectTransform, Vector2.zero, Vector2.one);
                }
            }

            public void Select(int i)
            {
                if (i < 0 || i >= Buttons.Length) return;
                var hlRt = Root != null ? Root.Find("Highlight") as RectTransform : null;
                var target = new Vector2(4f + _segW * i, 0f);
                if (hlRt != null)
                {
                    if (_selected < 0) hlRt.anchoredPosition = target;
                    else if (UiTween.Runner != null) UiTween.Runner.StartCoroutine(MoveHL(hlRt, target));
                    else hlRt.anchoredPosition = target;
                }
                _selected = i;
                for (int k = 0; k < Labels.Length; k++)
                    if (Labels[k] != null) Labels[k].color = k == i ? UiTokens.Ink : UiTokens.Muted;
            }

            static System.Collections.IEnumerator MoveHL(RectTransform rt, Vector2 to)
            {
                Vector2 from = rt.anchoredPosition; float t = 0f;
                while (t < 0.2f && rt != null)
                {
                    t += Time.unscaledDeltaTime;
                    rt.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.2f));
                    yield return null;
                }
                if (rt != null) rt.anchoredPosition = to;
            }
        }
    }

    /// <summary>Press/hover feedback for every button: scale down on press, lift on hover. Unscaled time.</summary>
    public sealed class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        RectTransform _rt;
        float _target = 1f;
        float _current = 1f;
        bool _pressed;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
        }

        public void OnPointerDown(PointerEventData e) { _pressed = true; _target = 0.96f; }
        public void OnPointerUp(PointerEventData e) { _pressed = false; _target = 1.02f; }
        public void OnPointerEnter(PointerEventData e) { if (!_pressed) _target = 1.02f; }
        public void OnPointerExit(PointerEventData e) { if (!_pressed) _target = 1f; }

        void Update()
        {
            float k = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
            _current = Mathf.Lerp(_current, _target, k);
            if (_rt != null)
                _rt.localScale = new Vector3(_current, _current, 1f);
            if (!_pressed && Mathf.Abs(_target - 1.02f) < 0.001f && Mathf.Abs(_current - _target) < 0.002f)
                _target = 1f;
        }
    }

    /// <summary>Tiny tween runner (unscaled time) for reveals, slides, pops and number roll-ups.</summary>
    public static class UiTween
    {
        /// <summary>MonoBehaviour that owns UI coroutines. Set this once by the main UI bootstrap.</summary>
        public static MonoBehaviour Runner;

        // ------------------------------------------------------------------ easing

        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        public static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); float i = 1f - t; return 1f - i * i * i; }
        public static float EaseOutBack(float t) { t = Mathf.Clamp01(t); const float c1 = 1.4f; const float c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }
        public static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private static bool CanAnimate => Runner != null && Runner.isActiveAndEnabled && Application.isPlaying;

        // ------------------------------------------------------------------ IEnumerator tweens (called with StartCoroutine by a MonoBehaviour)

        public static IEnumerator Fade(CanvasGroup g, float from, float to, float duration, float delay = 0f)
        {
            if (g == null) yield break;
            g.alpha = from;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
                g.alpha = Mathf.Lerp(from, to, Smooth(t));
                yield return null;
            }
            g.alpha = to;
        }

        public static IEnumerator Slide(RectTransform rt, Vector2 fromOffset, float duration, float delay = 0f)
        {
            if (rt == null) yield break;
            Vector2 rest = rt.anchoredPosition;
            rt.anchoredPosition = rest + fromOffset;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
                rt.anchoredPosition = Vector2.Lerp(rest + fromOffset, rest, EaseOutBack(t));
                yield return null;
            }
            rt.anchoredPosition = rest;
        }

        public static IEnumerator Pop(RectTransform rt, float from, float duration, float delay = 0f)
        {
            if (rt == null) yield break;
            rt.localScale = new Vector3(from, from, 1f);
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
                float s = Mathf.LerpUnclamped(from, 1f, EaseOutBack(t));
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        public static IEnumerator Count(Text label, double from, double to, float duration, Func<double, string> format)
        {
            if (label == null) yield break;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
                double v = from + (to - from) * Smooth(t);
                label.text = format(v);
                yield return null;
            }
            label.text = format(to);
        }

        public static IEnumerator Flash(UiImage img, Color peak, float duration)
        {
            if (img == null) yield break;
            Color rest = img.color;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
                img.color = Color.Lerp(peak, rest, Smooth(t));
                yield return null;
            }
            img.color = rest;
        }

        // ------------------------------------------------------------------ fire-and-forget helpers (require Runner)

        /// <summary>Fade a group in while sliding its rect from an offset, all in one call.</summary>
        public static void Reveal(RectTransform rt, CanvasGroup group, Vector2 fromOffset, float seconds = 0.30f, float delay = 0f)
        {
            if (rt == null) return;
            if (!CanAnimate)
            {
                if (group != null) group.alpha = 1f;
                return;
            }
            Runner.StartCoroutine(RevealRoutine(rt, group, fromOffset, seconds, delay));
        }

        private static IEnumerator RevealRoutine(RectTransform rt, CanvasGroup group, Vector2 from, float seconds, float delay)
        {
            Vector2 rest = rt.anchoredPosition;
            if (group != null) group.alpha = 0f;
            rt.anchoredPosition = rest + from;
            if (delay > 0f) { float d = 0f; while (d < delay && rt != null) { d += Time.unscaledDeltaTime; yield return null; } }
            float t = 0f;
            while (t < seconds && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float k = EaseOutCubic(t / seconds);
                rt.anchoredPosition = Vector2.LerpUnclamped(rest + from, rest, k);
                if (group != null) group.alpha = k;
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = rest;
            if (group != null) group.alpha = 1f;
        }

        /// <summary>Reveal every direct active child in order with a stagger.</summary>
        public static void Stagger(Transform parent, Vector2 fromOffset, float seconds = 0.28f, float step = 0.05f, float delay = 0f)
        {
            if (!CanAnimate || parent == null) return;
            int i = 0;
            foreach (Transform child in parent)
            {
                var rt = child as RectTransform;
                if (rt == null || !child.gameObject.activeSelf) continue;
                var g = child.GetComponent<CanvasGroup>();
                if (g == null) g = child.gameObject.AddComponent<CanvasGroup>();
                Reveal(rt, g, fromOffset, seconds, delay + step * i);
                i++;
            }
        }

        /// <summary>Scale punch: overshoot then settle. For verdicts and money.</summary>
        public static void Punch(RectTransform rt, float seconds = 0.42f, float overshoot = 1.14f, float from = 0.7f)
        {
            if (rt == null) return;
            if (!CanAnimate) { rt.localScale = Vector3.one; return; }
            Runner.StartCoroutine(PunchRoutine(rt, seconds, overshoot, from));
        }

        private static IEnumerator PunchRoutine(RectTransform rt, float seconds, float overshoot, float from)
        {
            float t = 0f;
            while (t < seconds && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float s = k < 0.55f
                    ? Mathf.Lerp(from, overshoot, EaseOutCubic(k / 0.55f))
                    : Mathf.Lerp(overshoot, 1f, EaseInOut((k - 0.55f) / 0.45f));
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        /// <summary>Continuous alpha pulse on a graphic while the predicate holds. Returns the running coroutine.</summary>
        public static Coroutine Pulse(Graphic g, Color baseColor, float minAlpha, float maxAlpha, float hz, Func<bool> alive)
        {
            if (g == null || !CanAnimate) { if (g != null) g.color = UiTokens.Alpha(baseColor, maxAlpha); return null; }
            return Runner.StartCoroutine(PulseRoutine(g, baseColor, minAlpha, maxAlpha, hz, alive));
        }

        private static IEnumerator PulseRoutine(Graphic g, Color baseColor, float minAlpha, float maxAlpha, float hz, Func<bool> alive)
        {
            while (g != null && (alive == null || alive()))
            {
                float a = Mathf.Lerp(minAlpha, maxAlpha, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * hz * Mathf.PI * 2f));
                g.color = UiTokens.Alpha(baseColor, a);
                yield return null;
            }
            if (g != null) g.color = UiTokens.Alpha(baseColor, maxAlpha);
        }

        /// <summary>A flash that decays: broadcast cut wipes, impact highlights.</summary>
        public static void FlashGraphic(Graphic g, float peak, float seconds = 0.22f)
        {
            if (g == null) return;
            if (!CanAnimate) { g.color = UiTokens.Alpha(g.color, 0f); return; }
            Runner.StartCoroutine(FlashGraphicRoutine(g, peak, seconds));
        }

        private static IEnumerator FlashGraphicRoutine(Graphic g, float peak, float seconds)
        {
            Color c = g.color;
            float t = 0f;
            while (t < seconds && g != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float a = k < 0.2f ? Mathf.Lerp(0f, peak, k / 0.2f) : Mathf.Lerp(peak, 0f, EaseOutCubic((k - 0.2f) / 0.8f));
                g.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
            if (g != null) g.color = new Color(c.r, c.g, c.b, 0f);
        }
    }

    /// <summary>Base for all screen-level views: a root rect with a canvas group and show/hide fades.</summary>
    public abstract class UiView
    {
        public RectTransform Root { get; protected set; }
        public CanvasGroup Group { get; protected set; }
        public bool IsVisible { get; private set; }

        protected void Init(Transform parent, string name)
        {
            var existing = parent != null ? parent.Find(name) : null;
            if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);
            var go = new GameObject(name, typeof(RectTransform));
            Root = go.GetComponent<RectTransform>();
            Root.SetParent(parent, false);
            Widgets.Stretch(Root, Vector2.zero, Vector2.one);
            Group = go.AddComponent<CanvasGroup>();
            SetVisible(false, true);
        }

        public void Show(bool instant = false) => SetVisible(true, instant);
        public void Hide(bool instant = false) => SetVisible(false, instant);

        public virtual void SetVisible(bool visible, bool instant = false)
        {
            IsVisible = visible;
            Group.interactable = visible;
            Group.blocksRaycasts = visible;
            if (instant || UiTween.Runner == null || !Application.isPlaying)
            {
                Group.alpha = visible ? 1f : 0f;
            }
            else
            {
                UiTween.Runner.StartCoroutine(UiTween.Fade(Group, Group.alpha, visible ? 1f : 0f, visible ? 0.18f : 0.12f));
            }
        }
    }

    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rt;
        Rect _lastSafeArea;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        void Update()
        {
            if (_lastSafeArea != Screen.safeArea)
                ApplySafeArea();
        }

        void ApplySafeArea()
        {
            _lastSafeArea = Screen.safeArea;
            Vector2 anchorMin = _lastSafeArea.position;
            Vector2 anchorMax = _lastSafeArea.position + _lastSafeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;
            _rt.anchorMin = anchorMin;
            _rt.anchorMax = anchorMax;
        }
    }
}
