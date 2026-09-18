using System;
using System.Collections;
using System.Collections.Generic;
using BasketballBetting.Cameras;
using BasketballBetting.Shooting;
using UnityEngine;

namespace BasketballBetting.Presentation
{
    /// <summary>
    /// Plays a ShotRecording back on ghost actors with a cut list from <see cref="ReplayPlanner"/>.
    /// Purely presentational: the live ball and players are hidden (not moved), no physics runs on
    /// ghosts, Time.timeScale is never touched (slow motion is the cut's playback rate), and the
    /// result shown is the one already locked.
    /// </summary>
    public sealed class ReplayDirector : MonoBehaviour
    {
        [SerializeField] float _tailHold = 0.35f;
        [SerializeField] float _maxWallTime = 14f;

        GhostActor _ghostShooter;
        GhostActor _ghostDefender;
        Transform _ghostBall;
        readonly List<Renderer> _hidden = new List<Renderer>(64);
        bool _skipRequested;

        public bool IsPlaying { get; private set; }
        public float PlaybackTime { get; private set; }
        public string CurrentCut { get; private set; } = string.Empty;
        public Transform GhostBall => _ghostBall;
        public event Action<ReplayCut> CutStarted;
        public event Action ReplayFinished;

        public void Skip() => _skipRequested = true;

        public IEnumerator Play(ShotRecording rec, ShotResultState result, GameCameraDirector cameras, CameraLook look,
            Transform liveShooter, Transform liveDefender, Basketball liveBall, Vector3 rimCenter, Vector3 towardCourt)
        {
            if (IsPlaying || rec == null || !rec.HasFrames || cameras == null)
                yield break;

            IsPlaying = true;
            _skipRequested = false;
            float wall = 0f;
            try
            {
                var root = new GameObject("ReplayGhosts").transform;
                root.SetParent(transform, false);
                _ghostShooter = GhostActor.Clone(liveShooter, root, "ReplayShooter");
                _ghostDefender = liveDefender != null && liveDefender.gameObject.activeInHierarchy && rec.DefenderBoneCount > 0
                    ? GhostActor.Clone(liveDefender, root, "ReplayDefender") : null;
                _ghostBall = CloneBall(liveBall, root);

                HideLive(liveShooter);
                HideLive(liveDefender);
                if (liveBall != null && liveBall.Visual != null)
                    HideLive(liveBall.Visual);

                if (look != null)
                    look.SetReplay(true);

                CameraContext ctx = cameras.Context;
                ctx.Shooter = _ghostShooter != null ? _ghostShooter.Root.transform : liveShooter;
                ctx.Defender = _ghostDefender != null ? _ghostDefender.Root.transform : null;
                ctx.RimCenter = rimCenter;
                ctx.TowardCourt = towardCourt;
                ctx.Made = result != null && result.Final == ShotOutcome.Make;
                ctx.ResultKnown = true;

                List<ReplayCut> cuts = ReplayPlanner.Build(rec, result, ctx.Shooter, ctx.Defender);
                for (int i = 0; i < cuts.Count && wall < _maxWallTime && !_skipRequested; i++)
                {
                    ReplayCut cut = cuts[i];
                    CurrentCut = cut.Shot.Name;
                    CutStarted?.Invoke(cut);
                    float t = cut.From;
                    PlaybackTime = t;
                    ApplyFrame(rec, t, ctx);
                    cameras.Cut(cut.Shot, cut.Blend);
                    if (i == 0)
                        cameras.SnapToShot();

                    while (t < cut.To && wall < _maxWallTime && !_skipRequested)
                    {
                        float dt = Time.unscaledDeltaTime;
                        wall += dt;
                        t += dt * cut.Rate;
                        PlaybackTime = Mathf.Min(t, cut.To);
                        ApplyFrame(rec, PlaybackTime, ctx);
                        if (look != null)
                            look.SetFocus(Vector3.Distance(cameras.Camera.transform.position, ctx.BallPosition));
                        yield return null;
                    }

                    if (cut.HoldLastFrame && !_skipRequested)
                    {
                        float hold = 0f;
                        while (hold < _tailHold && !_skipRequested)
                        {
                            hold += Time.unscaledDeltaTime;
                            wall += Time.unscaledDeltaTime;
                            PlaybackTime = cut.To;
                            ApplyFrame(rec, cut.To, ctx);
                            yield return null;
                        }
                    }
                }
            }
            finally
            {
                if (look != null)
                    look.SetReplay(false);
                RestoreLive();
                if (_ghostShooter != null) _ghostShooter.Destroy();
                if (_ghostDefender != null) _ghostDefender.Destroy();
                if (_ghostBall != null) UnityUtil.SafeDestroy(_ghostBall.gameObject);
                _ghostShooter = null;
                _ghostDefender = null;
                _ghostBall = null;
                Transform stale = transform.Find("ReplayGhosts");
                if (stale != null)
                    UnityUtil.SafeDestroy(stale.gameObject);
                CurrentCut = string.Empty;
                IsPlaying = false;
                ReplayFinished?.Invoke();
            }
        }

        void ApplyFrame(ShotRecording rec, float time, CameraContext ctx)
        {
            if (!rec.TryBracket(time, out int a, out int b, out float u))
                return;
            ShotRecording.Frame fa = rec.Frames[a];
            ShotRecording.Frame fb = rec.Frames[b];
            if (_ghostShooter != null)
                _ghostShooter.Apply(fa.ShooterPositions, fa.ShooterRotations, fb.ShooterPositions, fb.ShooterRotations, u);
            if (_ghostDefender != null)
                _ghostDefender.Apply(fa.DefenderPositions, fa.DefenderRotations, fb.DefenderPositions, fb.DefenderRotations, u);
            Vector3 ballPos = Vector3.Lerp(fa.BallPosition, fb.BallPosition, u);
            if (_ghostBall != null)
                _ghostBall.SetPositionAndRotation(ballPos, Quaternion.Slerp(fa.BallRotation, fb.BallRotation, u));
            ctx.BallPosition = ballPos;
            ctx.BallVelocity = rec.BallVelocityAt(time);
            if (ctx.Shooter != null)
                ctx.ShooterHead = ctx.Shooter.position + Vector3.up * 1.7f;
        }

        static Transform CloneBall(Basketball liveBall, Transform parent)
        {
            var go = new GameObject("ReplayBall");
            go.transform.SetParent(parent, false);
            if (liveBall != null && liveBall.Visual != null)
            {
                GameObject vis = UnityEngine.Object.Instantiate(liveBall.Visual.gameObject, go.transform);
                vis.name = "Visual";
                vis.transform.localPosition = Vector3.zero;
                vis.transform.localRotation = Quaternion.identity;
                vis.SetActive(true);
                foreach (var c in vis.GetComponentsInChildren<Collider>(true))
                    UnityUtil.SafeDestroy(c);
            }
            return go.transform;
        }

        void HideLive(Transform root)
        {
            if (root == null)
                return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (r != null && r.enabled)
                {
                    r.enabled = false;
                    _hidden.Add(r);
                }
            }
        }

        void RestoreLive()
        {
            for (int i = 0; i < _hidden.Count; i++)
                if (_hidden[i] != null)
                    _hidden[i].enabled = true;
            _hidden.Clear();
        }
    }
}
