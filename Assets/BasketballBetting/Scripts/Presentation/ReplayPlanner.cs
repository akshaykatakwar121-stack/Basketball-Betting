using System.Collections.Generic;
using BasketballBetting.Cameras;
using BasketballBetting.Shooting;
using UnityEngine;

namespace BasketballBetting.Presentation
{
    /// <summary>One replay segment: which camera, which slice of the recording, how slow.</summary>
    public sealed class ReplayCut
    {
        public CameraShot Shot;
        public float From;       // recording time
        public float To;         // recording time
        public float Rate = 0.5f; // playback speed
        public float Blend;      // seconds of cross-blend into this cut (0 = hard cut)
        public bool HoldLastFrame;

        public float RealDuration => Mathf.Max(0f, To - From) / Mathf.Max(0.05f, Rate);
    }

    /// <summary>Chooses the replay cut list from the finalised result and what the tape shows.</summary>
    public static class ReplayPlanner
    {
        public static List<ReplayCut> Build(ShotRecording rec, ShotResultState result, Transform ghostShooter, Transform ghostDefender)
        {
            var cuts = new List<ReplayCut>(4);
            if (rec == null || !rec.HasFrames)
                return cuts;

            float release = rec.HasRelease ? rec.ReleaseTime : rec.StartTime + 0.4f;
            float contact = rec.FirstGoalContactTime;
            float crossing = rec.CrossingTime;
            float lock_ = rec.LockTime >= 0f ? rec.LockTime : rec.EndTime;
            float end = rec.EndTime;
            bool made = result != null ? result.Final == ShotOutcome.Make : rec.Made;
            ShotStyle style = result != null ? result.Intent.Style : rec.Style;
            float arrive = crossing >= 0f ? crossing : (contact >= 0f ? contact : Mathf.Min(end, release + 1.1f));

            var reaction = new ReactionShot { Subject = ghostShooter };

            switch (style)
            {
                case ShotStyle.Swish:
                case ShotStyle.HighArc:
                    cuts.Add(new ReplayCut { Shot = new ShooterCloseUpShot(), From = rec.StartTime, To = release + 0.12f, Rate = 0.55f });
                    cuts.Add(new ReplayCut { Shot = new BallFollowShot(), From = release + 0.08f, To = Mathf.Max(release + 0.3f, arrive - 0.28f), Rate = 0.7f });
                    cuts.Add(new ReplayCut { Shot = new RimCamShot(), From = Mathf.Max(release + 0.3f, arrive - 0.28f), To = Mathf.Min(end, arrive + 0.55f), Rate = 0.35f });
                    break;
                case ShotStyle.RimIn:
                case ShotStyle.RattleIn:
                    cuts.Add(new ReplayCut { Shot = new BehindShooterShot { Tight = true }, From = rec.StartTime, To = release + 0.1f, Rate = 0.6f });
                    cuts.Add(new ReplayCut { Shot = new SideTrackingShot(), From = release + 0.06f, To = Mathf.Max(release + 0.3f, arrive - 0.2f), Rate = 0.65f });
                    cuts.Add(new ReplayCut { Shot = new RimCamShot(), From = Mathf.Max(release + 0.3f, arrive - 0.2f), To = Mathf.Min(end, arrive + 0.8f), Rate = 0.3f });
                    break;
                case ShotStyle.BankIn:
                    cuts.Add(new ReplayCut { Shot = new ShooterCloseUpShot(), From = rec.StartTime, To = release + 0.1f, Rate = 0.55f });
                    cuts.Add(new ReplayCut { Shot = new BackboardCamShot(), From = release + 0.06f, To = Mathf.Min(end, arrive + 0.65f), Rate = 0.42f });
                    break;
                case ShotStyle.BankMiss:
                    cuts.Add(new ReplayCut { Shot = new BehindShooterShot(), From = rec.StartTime, To = release + 0.1f, Rate = 0.6f });
                    cuts.Add(new ReplayCut { Shot = new BackboardCamShot(), From = release + 0.06f, To = Mathf.Min(end, (contact >= 0f ? contact : arrive) + 0.7f), Rate = 0.4f });
                    cuts.Add(new ReplayCut { Shot = new WideArenaShot(), From = Mathf.Min(end, (contact >= 0f ? contact : arrive) + 0.7f), To = Mathf.Min(end, lock_ + 0.4f), Rate = 0.9f });
                    break;
                case ShotStyle.AirBall:
                    cuts.Add(new ReplayCut { Shot = new SideTrackingShot(), From = rec.StartTime, To = Mathf.Min(end, lock_ + 0.35f), Rate = 0.75f });
                    break;
                case ShotStyle.Blocked:
                case ShotStyle.Deflected:
                    {
                        float dc = rec.DefenderContactTime >= 0f ? rec.DefenderContactTime : release + 0.15f;
                        cuts.Add(new ReplayCut { Shot = new ShooterCloseUpShot(), From = rec.StartTime, To = release, Rate = 0.55f });
                        cuts.Add(new ReplayCut { Shot = new SideTrackingShot(), From = release - 0.05f, To = Mathf.Min(end, dc + 0.55f), Rate = 0.3f });
                        cuts.Add(new ReplayCut { Shot = new WideArenaShot(), From = Mathf.Min(end, dc + 0.55f), To = Mathf.Min(end, lock_ + 0.3f), Rate = 0.9f });
                        reaction.Subject = ghostDefender != null ? ghostDefender : ghostShooter;
                        break;
                    }
                default: // rim misses: Short, Long, BackRim, Left, Right, RimOut, InAndOut
                    {
                        float c = contact >= 0f ? contact : arrive;
                        cuts.Add(new ReplayCut { Shot = new SideTrackingShot(), From = rec.StartTime, To = Mathf.Max(release + 0.25f, c - 0.08f), Rate = 0.6f });
                        cuts.Add(new ReplayCut { Shot = new RimCamShot(), From = Mathf.Max(release + 0.25f, c - 0.08f), To = Mathf.Min(end, c + 0.75f), Rate = 0.32f });
                        cuts.Add(new ReplayCut { Shot = new WideArenaShot(), From = Mathf.Min(end, c + 0.75f), To = Mathf.Min(end, lock_ + 0.35f), Rate = 0.9f });
                        break;
                    }
            }

            // Every replay ends on the reaction, played at real speed from the lock.
            cuts.Add(new ReplayCut { Shot = reaction, From = Mathf.Max(rec.StartTime, Mathf.Min(end - 0.1f, lock_)), To = end, Rate = 1f, Blend = 0f, HoldLastFrame = true });

            // Sanitise: monotonic, non-empty.
            float cursor = rec.StartTime;
            for (int i = 0; i < cuts.Count; i++)
            {
                ReplayCut c = cuts[i];
                c.From = Mathf.Clamp(c.From, rec.StartTime, end);
                c.To = Mathf.Clamp(c.To, c.From, end);
                if (c.To - c.From < 0.05f && i < cuts.Count - 1)
                {
                    cuts.RemoveAt(i);
                    i--;
                    continue;
                }
                cursor = c.To;
            }
            _ = cursor;
            _ = made;
            return cuts;
        }
    }
}
