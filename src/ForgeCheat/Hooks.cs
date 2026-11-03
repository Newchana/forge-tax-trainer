using System;
using UnityEngine;

namespace ForgeCheat
{
    /// <summary>
    /// Replacement bodies for game methods the patcher rewires.
    /// Signatures must match the originals exactly, including the struct 'this' pointer.
    /// </summary>
    public static class Hooks
    {
        /// <summary>
        /// Replaces ForgeChances.Roll(int). Original behaviour is preserved verbatim when
        /// the cheat is off; when on, a hammer blow resolves as a great success, which also
        /// removes the destruction branch (a broken sword ends the strike).
        ///
        /// Decks that contain no great-success tickets keep their original roll: the start
        /// screen deck is {0,0,500,500,0} and the game only advances on a plain Success
        /// (GPT6ForgeRoom.Strike sets startScreenSucceeded on that outcome alone), while the
        /// candle relic overrides the deck to {0,0,1,0,0} to force a Hold.
        /// </summary>
        public static ForgeOutcome Roll(ref ForgeChances chances, int ticket)
        {
            if (CheatConfig.AlwaysGreat && chances.greatSuccess > 0)
            {
                return ForgeOutcome.GreatSuccess;
            }

            ticket = Mathf.Clamp(ticket, 0, 999);
            int total = 0;
            for (int i = 0; i < 5; i++)
            {
                total += chances[i];
                if (ticket < total)
                {
                    return (ForgeOutcome)i;
                }
            }

            throw new InvalidOperationException("Forge probabilities must total 1,000 tickets.");
        }

        /// <summary>
        /// Replaces ForgeReworkRules.Speed(ForgeCampaign, bool). Drives both the hammer
        /// animation timing and the strike cooldown.
        /// </summary>
        public static float Speed(ForgeCampaign run, bool fever)
        {
            float speed = 1f
                + 0.15f * run.Rank("speed")
                + (fever ? 0f : 0.2f * run.Rank("loop"))
                + (fever ? 0.3f * run.Rank("bellows") : 0f);

            if (CheatConfig.FastForge)
            {
                speed *= Mathf.Clamp(CheatConfig.ForgeSpeed, 1f, 10f);
            }

            return speed;
        }
    }
}
