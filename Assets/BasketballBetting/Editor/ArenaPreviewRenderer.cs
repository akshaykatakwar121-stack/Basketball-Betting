using System.IO;
using BasketballBetting.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BasketballBetting.EditorTools
{
    /// <summary>
    /// Renders a few fixed camera compositions of the built scene to PNG files (batch-mode visual QA).
    /// Batch: -executeMethod BasketballBetting.EditorTools.ArenaPreviewRenderer.RenderFromCommandLine
    /// </summary>
    public static class ArenaPreviewRenderer
    {
        public static void RenderFromCommandLine()
        {
            string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(ArenaSceneBuilder.ScenePath, OpenSceneMode.Single);

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Preview] No main camera in scene");
                return;
            }
            var director = cam.GetComponent<GameCameraDirector>();
            var look = CameraLook.Attach(cam.gameObject);
            var ctx = director.Context;
            ctx.RimCenter = CourtMetrics.RimCenter;
            ctx.TowardCourt = Vector3.back;
            var shooter = new GameObject("PreviewShooterAnchor").transform;
            shooter.position = CourtMetrics.FreeThrowSpot;
            shooter.LookAt(new Vector3(0f, 0f, CourtMetrics.RimCenter.z));
            ctx.Shooter = shooter;
            ctx.BallPosition = CourtMetrics.FreeThrowSpot + Vector3.up * 2.2f + Vector3.forward * 0.3f;

            var probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
            foreach (var p in probes)
                p.RenderProbe();

            Shoot(cam, director, new TitleShot(), Path.Combine(outDir, "01_title.png"));
            Shoot(cam, director, new BehindShooterShot(), Path.Combine(outDir, "02_behind_shooter.png"));
            Shoot(cam, director, new RimCamShot(), Path.Combine(outDir, "03_rim_cam.png"));
            Shoot(cam, director, new WideArenaShot(), Path.Combine(outDir, "04_wide_arena.png"));
            Shoot(cam, director, new BackboardCamShot(), Path.Combine(outDir, "05_backboard_cam.png"));
            Shoot(cam, director, new SideTrackingShot(), Path.Combine(outDir, "06_side_tracking.png"));
            Object.DestroyImmediate(shooter.gameObject);
            Debug.Log("[Preview] wrote screenshots to " + outDir);
        }

        static void Shoot(Camera cam, GameCameraDirector director, CameraShot shot, string path, int width = 1920, int height = 1080)
        {
            var pose = shot.Compose(director.Context, 0f);
            cam.transform.position = pose.Position;
            cam.transform.rotation = Quaternion.LookRotation(pose.LookAt - pose.Position, Vector3.up);
            cam.fieldOfView = pose.Fov;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
