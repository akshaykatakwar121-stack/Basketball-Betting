using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BasketballBetting.Tests
{
    /// <summary>
    /// Visual QA helper: boots the scene and renders every UI screen (portrait and landscape) to PNG files
    /// under &lt;project&gt;/Screenshots/UI. Skipped unless the BB_CAPTURE_UI environment variable is set.
    /// </summary>
    public class UiCaptureTests
    {
        [UnityTest]
        public IEnumerator CaptureEveryScreen()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("BB_CAPTURE_UI")))
            {
                Assert.Ignore("set BB_CAPTURE_UI=1 to render UI screenshots");
                yield break;
            }
            LogAssert.ignoreFailingMessages = true;
            string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots", "UI");
            Directory.CreateDirectory(outDir);

            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;
            GameUI ui = Object.FindAnyObjectByType<GameUI>();
            Assert.IsNotNull(ui);
            var canvas = ui.GetComponentInChildren<Canvas>(true);
            Camera main = Camera.main;
            // A dedicated, disabled capture camera: rendered explicitly, no post stack, so the player loop never touches it.
            var camGo = new GameObject("UiCaptureCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.CopyFrom(main);
            cam.targetTexture = null;
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.5f;

            foreach (var size in new[] { new Vector2Int(1080, 1920), new Vector2Int(1920, 1080) })
            {
                string tag = size.x < size.y ? "portrait" : "landscape";
                var rt = new RenderTexture(size.x, size.y, 24);
                cam.targetTexture = rt;
                cam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
                cam.fieldOfView = main.fieldOfView;
                // let the canvas scaler see the new aspect
                yield return null;
                yield return null;

                ui.ShowMainMenu();
                yield return Settle(ui, 0.6f);
                Capture(cam, rt, Path.Combine(outDir, $"main_{tag}.png"));

                ui.ShowCharacterSelect();
                yield return Settle(ui, 0.8f);
                Capture(cam, rt, Path.Combine(outDir, $"roster_{tag}.png"));

                ui.ShowLobby();
                yield return Settle(ui, 0.6f);
                Capture(cam, rt, Path.Combine(outDir, $"lobby_{tag}.png"));

                ui.ShowHud("TAP / CLICK / SPACE TO SHOOT");
                ui.SetMeterVisible(true);
                ui.UpdateMeter(0.5f, TimingZone.Perfect);
                ui.ShowShotCallout("CLEAN");
                yield return Settle(ui, 0.35f);
                Capture(cam, rt, Path.Combine(outDir, $"hud_{tag}.png"));

                var win = new RoundMathResult { Won = true, Payout = 20, Stake = 10, Animation = ShotAnimationId.Swish, Multiplier = 2 };
                ui.ShowResult(win, TimingZone.Perfect, true, true);
                yield return Settle(ui, 0.9f);
                Capture(cam, rt, Path.Combine(outDir, $"result_win_{tag}.png"));

                var loss = new RoundMathResult { Won = false, Payout = 0, Stake = 10, Animation = ShotAnimationId.RimOut, Multiplier = 2 };
                ui.ShowResult(loss, TimingZone.Late, true, false);
                yield return Settle(ui, 0.9f);
                Capture(cam, rt, Path.Combine(outDir, $"result_loss_{tag}.png"));

                ui.ShowHud("");
                ui.ShowPause();
                yield return Settle(ui, 0.4f);
                Capture(cam, rt, Path.Combine(outDir, $"pause_{tag}.png"));
                ui.HidePause();

                cam.targetTexture = null;
                rt.Release();
                Object.Destroy(rt);
            }
            Object.Destroy(camGo);
            Debug.Log("[UiCapture] wrote " + outDir);
        }

        static IEnumerator Settle(GameUI ui, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        static void Capture(Camera cam, RenderTexture rt, string path)
        {
            Camera main = Camera.main;
            if (main != null)
            {
                cam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
                cam.fieldOfView = main.fieldOfView;
            }
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(tex);
        }
    }
}
