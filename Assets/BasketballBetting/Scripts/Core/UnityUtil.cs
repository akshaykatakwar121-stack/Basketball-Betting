using UnityEngine;

namespace BasketballBetting
{
    public static class UnityUtil
    {
        public static void SafeDestroy(Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

        public static void StripCollider(GameObject go)
        {
            if (go == null)
                return;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                SafeDestroy(col);
        }

        public static T FindOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }
}
