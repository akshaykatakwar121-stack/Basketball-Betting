using System.Collections.Generic;
using UnityEngine;

namespace BasketballBetting.Presentation
{
    /// <summary>
    /// A visual-only clone of an actor for replay. Every component except transforms and renderers is
    /// removed, so a ghost cannot simulate, collide, animate or run scripts.
    /// </summary>
    public sealed class GhostActor
    {
        public GameObject Root { get; }
        public Transform[] Bones { get; }

        GhostActor(GameObject root, Transform[] bones)
        {
            Root = root;
            Bones = bones;
        }

        public static GhostActor Clone(Transform source, Transform parent, string name)
        {
            if (source == null)
                return null;
            GameObject go = Object.Instantiate(source.gameObject, parent);
            go.name = name;
            go.SetActive(true);
            Strip(go);
            var bones = go.GetComponentsInChildren<Transform>(true);
            return new GhostActor(go, bones);
        }

        /// <summary>Drives every bone from recorded world transforms (parents come before children).</summary>
        public void Apply(Vector3[] posA, Quaternion[] rotA, Vector3[] posB, Quaternion[] rotB, float u)
        {
            int n = Mathf.Min(Bones.Length, posA != null ? posA.Length : 0);
            for (int i = 0; i < n; i++)
            {
                Transform b = Bones[i];
                if (b == null)
                    continue;
                if (posB != null && i < posB.Length)
                {
                    b.SetPositionAndRotation(Vector3.Lerp(posA[i], posB[i], u), Quaternion.Slerp(rotA[i], rotB[i], u));
                }
                else
                {
                    b.SetPositionAndRotation(posA[i], rotA[i]);
                }
            }
        }

        public void SetVisible(bool visible)
        {
            if (Root != null)
                Root.SetActive(visible);
        }

        public void Destroy()
        {
            if (Root != null)
                UnityUtil.SafeDestroy(Root);
        }

        static readonly List<Component> Scratch = new List<Component>(64);

        static void Strip(GameObject go)
        {
            go.GetComponentsInChildren(true, Scratch);
            // Remove dependants first (e.g. joints before rigidbodies), then the rest.
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < Scratch.Count; i++)
                {
                    Component c = Scratch[i];
                    if (c == null)
                        continue;
                    if (c is Transform || c is Renderer || c is MeshFilter || c is TextMesh)
                        continue;
                    UnityUtil.SafeDestroy(c);
                }
            }
            Scratch.Clear();
        }
    }
}
