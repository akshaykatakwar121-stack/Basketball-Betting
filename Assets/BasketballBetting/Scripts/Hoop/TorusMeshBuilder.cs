using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>Procedural torus (rim iron) used for both the visual and the physics mesh.</summary>
    public static class TorusMeshBuilder
    {
        public static Mesh Build(float ringRadius, float tubeRadius, int segments, int sides, string name = "Torus")
        {
            segments = Mathf.Max(8, segments);
            sides = Mathf.Max(4, sides);
            int vertCount = (segments + 1) * (sides + 1);
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segments * sides * 6];

            int v = 0;
            for (int s = 0; s <= segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                Vector3 ringDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 ringCenter = ringDir * ringRadius;
                for (int t = 0; t <= sides; t++)
                {
                    float b = (float)t / sides * Mathf.PI * 2f;
                    Vector3 n = ringDir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    verts[v] = ringCenter + n * tubeRadius;
                    normals[v] = n;
                    uvs[v] = new Vector2((float)s / segments * 8f, (float)t / sides);
                    v++;
                }
            }

            int i = 0;
            for (int s = 0; s < segments; s++)
            {
                for (int t = 0; t < sides; t++)
                {
                    int a = s * (sides + 1) + t;
                    int b = a + sides + 1;
                    tris[i++] = a; tris[i++] = a + 1; tris[i++] = b;
                    tris[i++] = a + 1; tris[i++] = b + 1; tris[i++] = b;
                }
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
