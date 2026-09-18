using UnityEngine;
using System.Collections.Generic;

public class CrowdAnimator : MonoBehaviour
{
    private List<CrowdChunk> _chunks = new List<CrowdChunk>();
    private float _cheer;
    private float _cheerVel;
    private float _cheerTarget;

    void Start()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            if (child.name.StartsWith("Crowd") && child != this.transform)
            {
                var chunk = child.gameObject.AddComponent<CrowdChunk>();
                chunk.Capture();
                _chunks.Add(chunk);
            }
        }
    }

    [ContextMenu("Test Cheer")]
    public void TestCheerMake() { React(true); }

    public void React(bool made)
    {
        if (_chunks.Count == 0 || !made) return;
        _cheerTarget = 1f;
        // Keep jumping for a long time (15s) unless explicitly stopped by GameManager
        CancelInvoke(nameof(StopCheer));
        Invoke(nameof(StopCheer), 15f);
    }

    public void StopCheer()
    {
        _cheerTarget = 0f;
    }

    private void LateUpdate()
    {
        if (_chunks.Count == 0) return;
        
        float dt = Time.deltaTime;
        _cheer = Mathf.SmoothDamp(_cheer, _cheerTarget, ref _cheerVel, 0.16f, 10f, dt);
        float t = Time.time;
        
        for (int i = 0; i < _chunks.Count; i++)
        {
            var c = _chunks[i];
            if (c == null) continue;
            
            float jump = 0f;
            if (_cheer > 0.01f)
            {
                // 25% less hyper: Height from 0.45 to 0.3375. Speed from 9.5 to 7.125
                jump = _cheer * 0.3375f * Mathf.Abs(Mathf.Sin(t * 7.125f + c.Phase * 1.6f));
            }
            
            c.ApplyOffset(jump);
        }
    }
}