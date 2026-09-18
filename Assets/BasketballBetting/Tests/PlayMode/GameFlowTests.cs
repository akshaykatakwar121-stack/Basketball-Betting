using System.Collections;
using BasketballBetting.Shooting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BasketballBetting.Tests
{
    /// <summary>
    /// Boots the real scene and plays whole rounds through the public UI the way a player would.
    /// Proves the betting flow, the shot pipeline, animation, cameras, replay and UI run together
    /// without errors and settle the wallet correctly.
    /// </summary>
    public class GameFlowTests
    {
        [UnityTest]
        public IEnumerator FullRound_FreeThrow_SettlesWalletAndShowsResult()
        {
            yield return PlayRounds(GameModeType.FreeThrow, 2);
        }

        [UnityTest]
        public IEnumerator FullRound_ThreePoint_SettlesWalletAndShowsResult()
        {
            yield return PlayRounds(GameModeType.ThreePoint, 2);
        }

        static IEnumerator PlayRounds(GameModeType mode, int rounds)
        {
            PlayerPrefs.DeleteKey("BasketballBetting.LocalBalance");
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameManager gm = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(gm, "GameManager did not boot in the scene");
            GameUI ui = Object.FindAnyObjectByType<GameUI>();
            Assert.IsNotNull(ui, "GameUI missing");
            Assert.That(ui.CurrentScreen, Is.EqualTo(UiScreen.Main));

            ui.ShowLobby();
            yield return null;
            // Pick the mode through the same buttons a player taps.
            Button modeButton = FindButton(ui.transform, mode.ToString());
            Assert.IsNotNull(modeButton, "mode button " + mode);
            modeButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.SelectedMode, Is.EqualTo(mode));

            for (int r = 0; r < rounds; r++)
            {
                double before = WalletService.Balance;
                double stake = ui.Stake;
                // First round starts from the lobby's LOCK IN; later rounds from the result screen's SHOOT AGAIN.
                Button start = FindButton(ui.transform, r == 0 ? "Lock" : "Again");
                Assert.IsNotNull(start, r == 0 ? "LOCK IN button" : "SHOOT AGAIN button");
                Debug.Log($"[FlowTest] round {r}: balance before={before:F2} stake={stake:F2} mode={ui.SelectedMode}");
                start.onClick.Invoke();

                // The meter auto-releases after the configured cycles, so the shot fires by itself.
                float t = 0f;
                while (ui.CurrentScreen != UiScreen.Result && t < 40f)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                Assert.That(ui.CurrentScreen, Is.EqualTo(UiScreen.Result), $"round {r}: result screen never appeared after {t:F1}s");

                ShotResultState result = gm.Shot.Result;
                Assert.IsNotNull(result, "shot result");
                Assert.IsTrue(result.Locked, "result locked");
                Assert.IsFalse(result.Mismatch, "physical outcome must match the intent: " + result);

                double after = WalletService.Balance;
                Debug.Log($"[FlowTest] round {r}: {result} balance after={after:F2}");
                string detail = $"round {r}: before={before:F2} stake={stake:F2} after={after:F2} result={result}";
                if (result.Final == ShotOutcome.Make)
                    Assert.That(after, Is.GreaterThan(before - stake + 0.001), "a make must pay out: " + detail);
                else
                    Assert.That(after, Is.EqualTo(before - stake).Within(0.001), "a miss loses exactly the stake: " + detail);
            }
            // Errors and warnings fail the test by default; plain logs (the [Shot] plan lines) are allowed.
        }

        static Button FindButton(Transform root, string name)
        {
            foreach (var b in root.GetComponentsInChildren<Button>(true))
                if (b.name == name)
                    return b;
            return null;
        }
    }
}
