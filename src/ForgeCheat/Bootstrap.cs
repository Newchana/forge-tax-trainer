using UnityEngine;

namespace ForgeCheat
{
    /// <summary>
    /// Entry point. The patcher injects a call to Boot() at the head of ForgeFramePacing.Boot,
    /// which Unity already invokes via [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)].
    /// </summary>
    public static class Bootstrap
    {
        private static GameObject host;

        public static void Boot()
        {
            if (host != null)
            {
                return;
            }

            CheatConfig.Load();

            host = new GameObject("ForgeCheat");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<CheatBehaviour>();

            Debug.Log("[ForgeCheat] loaded (F8 = panel)");
            SelfTest();
        }

        /// <summary>
        /// Exercises the two rewritten game methods through their real runtime paths so a
        /// broken patch shows up in Player.log immediately instead of silently doing nothing.
        /// </summary>
        private static void SelfTest()
        {
            try
            {
                ForgeChances probe = new ForgeChances(new[] { 9, 2, 5, 12, 2 });
                ForgeOutcome outcome = probe.Roll(0);
                ForgeOutcome expected = CheatConfig.AlwaysGreat ? ForgeOutcome.GreatSuccess : ForgeOutcome.Destruction;
                Debug.Log("[ForgeCheat] selftest Roll(0) -> " + outcome + " expected " + expected
                    + " [" + (outcome == expected ? "PASS" : "FAIL") + "]");

                // The start screen deck must keep rolling Hold/Success or the game can never
                // advance past it (startScreenSucceeded requires a plain Success).
                ForgeChances startDeck = new ForgeChances(new[] { 0, 0, 500, 500, 0 });
                ForgeOutcome startOutcome = startDeck.Roll(0);
                bool startOk = startOutcome == ForgeOutcome.Hold || startOutcome == ForgeOutcome.Success;
                Debug.Log("[ForgeCheat] selftest start-screen deck -> " + startOutcome
                    + " [" + (startOk ? "PASS" : "FAIL") + "]");

                ForgeCampaign probeCampaign = new ForgeCampaign();
                float speed = ForgeReworkRules.Speed(probeCampaign, false);
                float expectedSpeed = CheatConfig.FastForge ? CheatConfig.ForgeSpeed : 1f;
                Debug.Log("[ForgeCheat] selftest Speed() -> " + speed + " expected " + expectedSpeed
                    + " [" + (Mathf.Abs(speed - expectedSpeed) < 0.001f ? "PASS" : "FAIL") + "]");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[ForgeCheat] selftest failed: " + ex);
            }
        }
    }
}
