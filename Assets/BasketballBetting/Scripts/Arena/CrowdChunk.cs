using UnityEngine;

public sealed class CrowdChunk : MonoBehaviour
{
    public float Phase;
    private float _baseY;
    private bool _captured;

    public void Capture()
    {
        _baseY = transform.position.y;
        _captured = true;
        if (Mathf.Abs(Phase) < 0.001f)
            Phase = (transform.GetSiblingIndex() * 0.37f) % (Mathf.PI * 2f);
    }

    public void ApplyOffset(float y)
    {
        if (!_captured) Capture();
        var p = transform.position;
        p.y = _baseY + y;
        transform.position = p;
    }
}