using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>Forwards trigger events from a score gate to its Hoop. Lives on the gate object.</summary>
    [DisallowMultipleComponent]
    public sealed class HoopGateRelay : MonoBehaviour
    {
        [SerializeField] Hoop _hoop;
        [SerializeField] bool _entry;

        public void Bind(Hoop hoop, bool entry)
        {
            _hoop = hoop;
            _entry = entry;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_hoop != null)
                _hoop.RaiseGate(_entry, other);
        }
    }
}
