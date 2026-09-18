using UnityEngine;

namespace BasketballBetting
{
    public static class MaterialFactory
    {
        static Shader _lit;

        public static Shader Lit
        {
            get
            {
                if (_lit == null)
                {
                    _lit = Shader.Find("Universal Render Pipeline/Lit");
                    if (_lit == null)
                        _lit = Shader.Find("Standard");
                }
                return _lit;
            }
        }

        public static Material Opaque(Color color, float smoothness = 0.4f, float metallic = 0f, string name = "ArenaMat")
        {
            var mat = new Material(Lit) { color = color, name = name };
            ApplyPbr(mat, color, smoothness, metallic);
            return mat;
        }

        public static Material Textured(Texture2D tex, float smoothness = 0.45f, float metallic = 0f)
        {
            var mat = new Material(Lit) { color = Color.white, mainTexture = tex, name = "CourtFloor" };
            ApplyPbr(mat, Color.white, smoothness, metallic);
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);
            return mat;
        }

        public static Material Transparent(Color color, float smoothness = 0.85f)
        {
            var mat = Opaque(color, smoothness, 0.05f);
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            if (mat.HasProperty("_SrcBlend"))
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))
                mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return mat;
        }

        public static Material Emissive(Color color, float intensity = 2f)
        {
            var mat = Opaque(color, 0.7f, 0.1f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * intensity);
            }
            return mat;
        }

        static void ApplyPbr(Material mat, Color color, float smoothness, float metallic)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
        }
    }

    public static class MeshFactory
    {
        public static Mesh Torus(float radius, float tube, int radial, int tubular)
        {
            var mesh = new Mesh { name = "Torus" };
            int verts = (radial + 1) * (tubular + 1);
            var vertices = new Vector3[verts];
            var normals = new Vector3[verts];
            var uvs = new Vector2[verts];
            var tris = new int[radial * tubular * 6];

            for (int i = 0; i <= radial; i++)
            {
                float u = i / (float)radial * Mathf.PI * 2f;
                for (int j = 0; j <= tubular; j++)
                {
                    float v = j / (float)tubular * Mathf.PI * 2f;
                    int idx = i * (tubular + 1) + j;
                    Vector3 center = new Vector3(Mathf.Cos(u) * radius, 0f, Mathf.Sin(u) * radius);
                    Vector3 offset = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v)) * tube;
                    vertices[idx] = center + offset;
                    normals[idx] = offset.normalized;
                    uvs[idx] = new Vector2(i / (float)radial, j / (float)tubular);
                }
            }

            int t = 0;
            for (int i = 0; i < radial; i++)
            {
                for (int j = 0; j < tubular; j++)
                {
                    int a = i * (tubular + 1) + j;
                    int b = (i + 1) * (tubular + 1) + j;
                    int c = (i + 1) * (tubular + 1) + j + 1;
                    int d = i * (tubular + 1) + j + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = d;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            return mesh;
        }

        public static Texture2D CourtTexture(int width = 1092, int height = 2048)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            Color woodLight = new Color(0.72f, 0.50f, 0.28f);
            Color woodDark = new Color(0.52f, 0.34f, 0.18f);
            Color paint = new Color(0.78f, 0.42f, 0.18f);
            Color line = new Color(0.96f, 0.96f, 0.94f);
            Color key = new Color(0.18f, 0.28f, 0.48f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float fx = x / (float)(width - 1);
                    float fy = y / (float)(height - 1);
                    float grain = Mathf.PerlinNoise(fx * 8f, fy * 48f);
                    Color wood = Color.Lerp(woodDark, woodLight, grain);
                    wood *= 0.92f + 0.08f * Mathf.Sin(y * 0.12f);
                    pixels[y * width + x] = wood;
                }
            }

            void WorldToTex(float worldX, float worldZ, out int px, out int py)
            {
                px = Mathf.Clamp(Mathf.RoundToInt((worldX / CourtMetrics.Width + 0.5f) * (width - 1)), 0, width - 1);
                py = Mathf.Clamp(Mathf.RoundToInt((worldZ / CourtMetrics.Length + 0.5f) * (height - 1)), 0, height - 1);
            }

            void PaintRect(float x0, float z0, float x1, float z1, Color c, float alpha = 1f)
            {
                WorldToTex(x0, z0, out int xa, out int ya);
                WorldToTex(x1, z1, out int xb, out int yb);
                if (xa > xb) { int tmp = xa; xa = xb; xb = tmp; }
                if (ya > yb) { int tmp = ya; ya = yb; yb = tmp; }
                for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                    pixels[y * width + x] = Color.Lerp(pixels[y * width + x], c, alpha);
            }

            float halfL = CourtMetrics.HalfLength;
            float halfW = CourtMetrics.HalfWidth;
            const float tLine = 0.05f;

            PaintRect(-2.44f, halfL - 7.24f, 2.44f, halfL, key, 0.72f);
            PaintRect(-2.44f, -halfL, 2.44f, -halfL + 7.24f, key, 0.72f);
            PaintRect(-2.44f, halfL - 7.24f, 2.44f, halfL, paint, 0.18f);

            PaintRect(-halfW - tLine, -halfL, -halfW + tLine, halfL, line);
            PaintRect(halfW - tLine, -halfL, halfW + tLine, halfL, line);
            PaintRect(-halfW, -halfL - tLine, halfW, -halfL + tLine, line);
            PaintRect(-halfW, halfL - tLine, halfW, halfL + tLine, line);
            PaintRect(-halfW, -tLine, halfW, tLine, line);
            PaintRect(-2.44f - tLine, CourtMetrics.FreeThrowZ, -2.44f + tLine, halfL, line);
            PaintRect(2.44f - tLine, CourtMetrics.FreeThrowZ, 2.44f + tLine, halfL, line);
            PaintRect(-2.44f, CourtMetrics.FreeThrowZ - tLine, 2.44f, CourtMetrics.FreeThrowZ + tLine, line);

            void PaintCircle(float cx, float cz, float radius, float thickness)
            {
                for (int y = 0; y < height; y++)
                {
                    float z = (y / (float)(height - 1) - 0.5f) * CourtMetrics.Length;
                    for (int x = 0; x < width; x++)
                    {
                        float px = (x / (float)(width - 1) - 0.5f) * CourtMetrics.Width;
                        float d = Mathf.Abs(Mathf.Sqrt((px - cx) * (px - cx) + (z - cz) * (z - cz)) - radius);
                        if (d < thickness)
                            pixels[y * width + x] = line;
                    }
                }
            }

            PaintCircle(0f, 0f, 1.83f, tLine);
            PaintCircle(0f, CourtMetrics.FreeThrowZ, 1.83f, tLine);

            for (int y = 0; y < height; y++)
            {
                float z = (y / (float)(height - 1) - 0.5f) * CourtMetrics.Length;
                for (int x = 0; x < width; x++)
                {
                    float px = (x / (float)(width - 1) - 0.5f) * CourtMetrics.Width;
                    if (z > CourtMetrics.RimCenter.z - 0.4f)
                        continue;
                    float d = Mathf.Abs(Mathf.Sqrt(px * px + (z - CourtMetrics.RimCenter.z) * (z - CourtMetrics.RimCenter.z)) - CourtMetrics.ThreePointRadius);
                    if (d < tLine && Mathf.Abs(px) < CourtMetrics.HalfWidth - 0.15f)
                        pixels[y * width + x] = line;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        public static Texture2D BasketballTexture(int size = 256)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color orange = new Color(0.92f, 0.40f, 0.12f);
            Color black = new Color(0.08f, 0.08f, 0.08f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float v = y / (float)(size - 1);
                    bool stripe = Mathf.Abs(u - 0.5f) < 0.03f
                                  || Mathf.Abs(v - 0.5f) < 0.03f
                                  || Mathf.Abs(Mathf.Sin(u * Mathf.PI) - v) < 0.035f
                                  || Mathf.Abs(1f - Mathf.Sin(u * Mathf.PI) - v) < 0.035f;
                    pixels[y * size + x] = stripe ? black : orange * (0.85f + 0.15f * Mathf.PerlinNoise(u * 8f, v * 8f));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }
    }
}
