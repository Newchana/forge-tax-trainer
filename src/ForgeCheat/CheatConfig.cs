using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ForgeCheat
{
    /// <summary>
    /// Toggle state, persisted to ForgeTax/ForgeCheat.cfg so settings survive restarts.
    /// </summary>
    public static class CheatConfig
    {
        public static bool AlwaysGreat = true;
        public static bool InfiniteGold = true;
        public static bool ZeroQuota = true;
        public static bool InfiniteUses = true;

        // Off by default: raising the strike rate multiplies the game's own per-strike work
        // (100 particles per great success, display refresh, codex/achievement scans), which
        // tanks the frame rate. The one-shot value/grade buttons remove grind far more cheaply.
        public static bool FastForge = false;

        public static float ForgeSpeed = 2f;
        public static double GoldTarget = 1000000000.0;
        public static double ValueFactor = 100.0;
        public static int GradeStep = 5;
        public static bool Diagnostics = true;

        public static string Summary()
        {
            return "great=" + AlwaysGreat + " gold=" + InfiniteGold + " quota=" + ZeroQuota
                + " speed=" + FastForge + "/" + ForgeSpeed.ToString("0.#") + " uses=" + InfiniteUses;
        }

        public static void AllOff()
        {
            AlwaysGreat = false;
            InfiniteGold = false;
            ZeroQuota = false;
            FastForge = false;
            InfiniteUses = false;
        }

        private static string FilePath
        {
            get { return Path.Combine(Application.dataPath, "..", "ForgeCheat.cfg"); }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }

                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                    {
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "AlwaysGreat": AlwaysGreat = ParseBool(value, AlwaysGreat); break;
                        case "InfiniteGold": InfiniteGold = ParseBool(value, InfiniteGold); break;
                        case "ZeroQuota": ZeroQuota = ParseBool(value, ZeroQuota); break;
                        case "FastForge": FastForge = ParseBool(value, FastForge); break;
                        case "InfiniteUses": InfiniteUses = ParseBool(value, InfiniteUses); break;
                        case "ForgeSpeed": ForgeSpeed = Mathf.Clamp(ParseFloat(value, ForgeSpeed), 1f, 10f); break;
                        case "GoldTarget": GoldTarget = ParseDouble(value, GoldTarget); break;
                        case "ValueFactor": ValueFactor = ParseDouble(value, ValueFactor); break;
                        case "GradeStep": GradeStep = (int)ParseDouble(value, GradeStep); break;
                        case "Diagnostics": Diagnostics = ParseBool(value, Diagnostics); break;
                    }
                }

                Debug.Log("[ForgeCheat] config loaded from " + FilePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ForgeCheat] config load failed: " + ex.Message);
            }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllLines(FilePath, new[]
                {
                    "# ForgeTax trainer settings",
                    "AlwaysGreat=" + AlwaysGreat,
                    "InfiniteGold=" + InfiniteGold,
                    "ZeroQuota=" + ZeroQuota,
                    "FastForge=" + FastForge,
                    "InfiniteUses=" + InfiniteUses,
                    "ForgeSpeed=" + ForgeSpeed.ToString(CultureInfo.InvariantCulture),
                    "GoldTarget=" + GoldTarget.ToString("R", CultureInfo.InvariantCulture),
                    "ValueFactor=" + ValueFactor.ToString("R", CultureInfo.InvariantCulture),
                    "GradeStep=" + GradeStep,
                    "Diagnostics=" + Diagnostics,
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ForgeCheat] config save failed: " + ex.Message);
            }
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            if (bool.TryParse(value, out parsed))
            {
                return parsed;
            }
            return value == "1" ? true : (value == "0" ? false : fallback);
        }

        private static float ParseFloat(string value, float fallback)
        {
            float parsed;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static double ParseDouble(string value, double fallback)
        {
            double parsed;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }
    }
}
