using System;
using UnityEngine;

namespace BasketballBetting.Shooting
{
    /// <summary>
    /// The authoritative result of one shot. Locks exactly once.
    ///
    /// The intended outcome comes from betting and is final the moment the ball is released.
    /// The observation comes from the basket detector and exists to time presentation and to
    /// prove the physical realisation matched. A mismatch is a bug (surfaced as an error and
    /// counted), never a different payout.
    /// </summary>
    public sealed class ShotResultState
    {
        public int ShotId { get; }
        public ShotIntent Intent { get; }
        public ShotObservation Observed { get; private set; } = ShotObservation.Pending;
        public ShotOutcome Final => Intent.Outcome;
        public bool Locked { get; private set; }
        public bool Mismatch { get; private set; }
        public float LockedAt { get; private set; }
        public string LockReason { get; private set; } = string.Empty;

        public event Action<ShotResultState> OnLocked;

        public ShotResultState(int shotId, ShotIntent intent)
        {
            ShotId = shotId;
            Intent = intent;
        }

        /// <summary>Records what the detector saw and locks. Later calls are ignored.</summary>
        public bool TryLock(ShotObservation observed, float now, string reason)
        {
            if (Locked)
                return false;
            if (observed == ShotObservation.Pending)
                observed = ShotObservation.Miss;
            Observed = observed;
            Locked = true;
            LockedAt = now;
            LockReason = reason ?? string.Empty;
            Mismatch = (observed == ShotObservation.Make) != Intent.IsMake;
            if (Mismatch)
            {
                Debug.LogError($"[ShotResult] MISMATCH shot #{ShotId} intent={Intent} observed={observed} ({LockReason}). " +
                               "Presenting the betting outcome; the physical realisation must be fixed.");
            }
            OnLocked?.Invoke(this);
            return true;
        }

        public override string ToString()
        {
            return $"shot #{ShotId} {Intent} observed={Observed} final={Final} locked={Locked}{(Mismatch ? " MISMATCH" : "")}";
        }
    }
}
