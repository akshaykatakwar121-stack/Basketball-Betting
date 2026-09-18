using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BasketballBetting.EditorTools
{
    /// <summary>Accumulates simple primitives into one mesh with one submesh per material slot.</summary>
    public sealed class MeshBatch
    {
        readonly List<Vector3> _verts = new List<Vector3>(65536);
        readonly List<Vector3> _normals = new List<Vector3>(65536);
        readonly List<Vector2> _uvs = new List<Vector2>(65536);
        readonly List<List<int>> _sub = new List<List<int>>();

        public int VertexCount => _verts.Count;

        void Ensure(int submesh)
        {
            while (_sub.Count <= submesh)
                _sub.Add(new List<int>(8192));
        }

        public void AddBox(Vector3 center, Vector3 size, Quaternion rot, int submesh)
        {
            Ensure(submesh);
            Vector3 h = size * 0.5f;
            Vector3[] faceNormals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = faceNormals[f];
                Vector3 t = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 b = Vector3.Cross(n, t).normalized;
                t = Vector3.Cross(b, n).normalized;
                Vector3 fc = new Vector3(n.x * h.x, n.y * h.y, n.z * h.z);
                Vector3 ext1 = new Vector3(t.x * h.x, t.y * h.y, t.z * h.z);
                Vector3 ext2 = new Vector3(b.x * h.x, b.y * h.y, b.z * h.z);
                int baseIndex = _verts.Count;
                Vector3 wn = rot * n;
                _verts.Add(center + rot * (fc - ext1 - ext2)); _normals.Add(wn); _uvs.Add(new Vector2(0, 0));
                _verts.Add(center + rot * (fc + ext1 - ext2)); _normals.Add(wn); _uvs.Add(new Vector2(1, 0));
                _verts.Add(center + rot * (fc + ext1 + ext2)); _normals.Add(wn); _uvs.Add(new Vector2(1, 1));
                _verts.Add(center + rot * (fc - ext1 + ext2)); _normals.Add(wn); _uvs.Add(new Vector2(0, 1));
                var tris = _sub[submesh];
                tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 1);
                tris.Add(baseIndex); tris.Add(baseIndex + 3); tris.Add(baseIndex + 2);
            }
        }

        public void AddBox(Vector3 center, Vector3 size, int submesh) => AddBox(center, size, Quaternion.identity, submesh);

        /// <summary>Vertical prism with flat top/bottom (bodies, poles).</summary>
        public void AddPrism(Vector3 baseCenter, float radius, float height, int sides, int submesh, float topScale = 1f)
        {
            Ensure(submesh);
            var tris = _sub[submesh];
            int start = _verts.Count;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                Vector3 n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                _verts.Add(baseCenter + n * radius); _normals.Add(n); _uvs.Add(new Vector2(i / (float)sides, 0f));
                _verts.Add(baseCenter + n * (radius * topScale) + Vector3.up * height); _normals.Add(n); _uvs.Add(new Vector2(i / (float)sides, 1f));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = start + i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
            // top cap
            int topCenter = _verts.Count;
            _verts.Add(baseCenter + Vector3.up * height); _normals.Add(Vector3.up); _uvs.Add(new Vector2(0.5f, 0.5f));
            int capStart = _verts.Count;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                Vector3 n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                _verts.Add(baseCenter + n * (radius * topScale) + Vector3.up * height); _normals.Add(Vector3.up); _uvs.Add(new Vector2(0.5f + n.x * 0.5f, 0.5f + n.z * 0.5f));
            }
            for (int i = 0; i < sides; i++)
            {
                tris.Add(topCenter); tris.Add(capStart + i + 1); tris.Add(capStart + i);
            }
        }

        /// <summary>Low-poly sphere (heads).</summary>
        public void AddSphere(Vector3 center, float radius, int submesh, int rings = 4, int segments = 6)
        {
            Ensure(submesh);
            var tris = _sub[submesh];
            int start = _verts.Count;
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float phi = (v - 0.5f) * Mathf.PI;
                for (int s = 0; s <= segments; s++)
                {
                    float u = s / (float)segments;
                    float theta = u * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta));
                    _verts.Add(center + n * radius); _normals.Add(n); _uvs.Add(new Vector2(u, v));
                }
            }
            int stride = segments + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = start + r * stride + s, b = a + 1, c = a + stride, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
        }

        /// <summary>Flat quad with explicit normal and UVs.</summary>
        public void AddQuad(Vector3 center, Vector3 right, Vector3 up, Vector3 normal, int submesh)
        {
            Ensure(submesh);
            int b = _verts.Count;
            _verts.Add(center - right - up); _normals.Add(normal); _uvs.Add(new Vector2(0, 0));
            _verts.Add(center + right - up); _normals.Add(normal); _uvs.Add(new Vector2(1, 0));
            _verts.Add(center + right + up); _normals.Add(normal); _uvs.Add(new Vector2(1, 1));
            _verts.Add(center - right + up); _normals.Add(normal); _uvs.Add(new Vector2(0, 1));
            var tris = _sub[submesh];
            // winding chosen so the face is visible from the +normal side
            if (Vector3.Dot(Vector3.Cross(right, up), normal) > 0f)
            {
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
            else
            {
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = _verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.subMeshCount = _sub.Count;
            for (int i = 0; i < _sub.Count; i++)
                mesh.SetTriangles(_sub[i], i, true);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public int SubmeshCount => _sub.Count;
    }
}
