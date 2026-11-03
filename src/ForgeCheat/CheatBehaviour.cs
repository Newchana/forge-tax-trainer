using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForgeCheat
{
    /// <summary>
    /// Draws the in-game panel and keeps the continuous cheats applied every frame.
    /// </summary>
    public sealed class CheatBehaviour : MonoBehaviour
    {
        // Relics that compound sword value the hardest (see ForgeEconomy.RefreshValue / Apply).
        private static readonly string[] GodRelics =
        {
            "coal", "token", "blackstone", "thorn", "idol", "bell", "knot", "incense",
        };

        private static readonly string[] Consumables =
        {
            "ward", "rewind", "chalk", "powder", "starshard", "oblivion", "delivery",
        };

        private bool showPanel = true;
        private bool mouseTakeover;
        private string notice = "";
        private float noticeUntil;

        private GPT6ForgeRoom room;
        private FieldInfo nextStrikeField;
        private FieldInfo balanceField;
        private bool quotaApplied;
        private int nextRoomSearch;
        private float fps = 60f;
        private float nextDiagnostic;

        private static readonly Rect PanelRect = new Rect(12f, 12f, 392f, 520f);

        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle hintStyle;
        private GUIStyle onStyle;
        private GUIStyle offStyle;
        private GUIStyle actionStyle;
        private GUIStyle statusStyle;
        private Font uiFont;

        private void Update()
        {
            TrackFrameRate();

            try
            {
                HandleHotkeys();
                ApplyContinuous();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ForgeCheat] update error: " + ex);
            }
        }

        private void TrackFrameRate()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                float instant = 1f / dt;
                fps = fps <= 0f ? instant : Mathf.Lerp(fps, instant, 0.05f);
            }

            if (CheatConfig.Diagnostics && Time.unscaledTime >= nextDiagnostic)
            {
                nextDiagnostic = Time.unscaledTime + 10f;
                Debug.Log("[ForgeCheat] fps=" + fps.ToString("0.0") + " panel=" + showPanel
                    + " mouse=" + mouseTakeover + " " + CheatConfig.Summary());
            }
        }

        // The panel never touches the cursor unless mouse takeover is explicitly enabled.
        // Forcing Cursor.lockState while the player controller kept re-capturing on click is
        // what previously left the game stuck on "click to restore view control". Takeover
        // mirrors the game's own menu mode (see ForgeLanguageMenu): release the cursor and
        // disable the controller, then put both back exactly as they were.
        private void LateUpdate()
        {
            if (!mouseTakeover || room == null || room.player == null)
            {
                return;
            }

            if (room.player.enabled)
            {
                room.player.enabled = false;
            }

            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
            }

            if (!Cursor.visible)
            {
                Cursor.visible = true;
            }
        }

        private void SetMouseTakeover(bool on)
        {
            mouseTakeover = on;

            if (room != null && room.player != null)
            {
                if (on)
                {
                    room.player.enabled = false;
                    room.player.ReleaseCursor();
                }
                else
                {
                    room.player.enabled = true;
                    room.player.Capture();
                }
            }

            Notice(on ? "鼠标接管已开启：面板可点击（F11 关闭并恢复视角）" : "鼠标接管已关闭，视角控制已恢复");
        }

        private void HandleHotkeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            if (kb.f8Key.wasPressedThisFrame)
            {
                showPanel = !showPanel;
                Notice(showPanel ? "面板已显示" : "面板已隐藏（F8 再按一次显示）");
            }

            if (kb.f11Key.wasPressedThisFrame)
            {
                SetMouseTakeover(!mouseTakeover);
            }

            if (kb.f1Key.wasPressedThisFrame)
            {
                Toggle(ref CheatConfig.AlwaysGreat, "必定大成功");
            }

            if (kb.f2Key.wasPressedThisFrame)
            {
                Toggle(ref CheatConfig.InfiniteGold, "无限金币");
            }

            if (kb.f3Key.wasPressedThisFrame)
            {
                Toggle(ref CheatConfig.ZeroQuota, "税赋归零");
            }

            if (kb.f4Key.wasPressedThisFrame)
            {
                Toggle(ref CheatConfig.FastForge, "锻造加速");
            }

            if (kb.f5Key.wasPressedThisFrame)
            {
                BoostValue(CheatConfig.ValueFactor);
            }

            if (kb.f6Key.wasPressedThisFrame)
            {
                BoostGrade(CheatConfig.GradeStep);
            }

            if (kb.f7Key.wasPressedThisFrame)
            {
                GiveEverything();
            }

            if (kb.f9Key.wasPressedThisFrame)
            {
                Toggle(ref CheatConfig.InfiniteUses, "消耗品无限");
            }

            if (kb.f10Key.wasPressedThisFrame)
            {
                CheatConfig.AllOff();
                CheatConfig.Save();

                if (mouseTakeover)
                {
                    SetMouseTakeover(false);
                }

                Notice("已关闭全部作弊（若仍卡顿请重启游戏）");
            }
        }

        private void Toggle(ref bool value, string name)
        {
            value = !value;
            CheatConfig.Save();
            Notice(name + (value ? " 开" : " 关"));
        }

        private void ApplyContinuous()
        {
            // A full object scan every frame is measurable on a busy scene, so retry at most
            // once a second while the room is absent.
            if (room == null && Time.frameCount >= nextRoomSearch)
            {
                nextRoomSearch = Time.frameCount + 60;
                room = FindAnyObjectByType<GPT6ForgeRoom>();
            }

            if (room == null)
            {
                return;
            }

            ForgeCampaign campaign = room.Campaign;
            if (campaign == null)
            {
                return;
            }

            if (CheatConfig.InfiniteGold && campaign.wallet < CheatConfig.GoldTarget)
            {
                campaign.wallet = CheatConfig.GoldTarget;
            }

            if (CheatConfig.ZeroQuota)
            {
                campaign.resumedBaseQuota = 1.0;
                quotaApplied = true;
            }
            else if (quotaApplied)
            {
                if (campaign.resumedBaseQuota == 1.0)
                {
                    campaign.resumedBaseQuota = 0.0;
                }

                quotaApplied = false;
            }

            if (CheatConfig.InfiniteUses)
            {
                if (campaign.wardCharges < 99)
                {
                    campaign.wardCharges = 99;
                }

                if (campaign.rewindCharges < 99)
                {
                    campaign.rewindCharges = 99;
                }
            }

            if (CheatConfig.FastForge)
            {
                SetNextStrike(0f);
            }
        }

        private void SetNextStrike(float value)
        {
            if (nextStrikeField == null)
            {
                nextStrikeField = typeof(GPT6ForgeRoom).GetField("nextStrike", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            if (nextStrikeField != null && room != null)
            {
                nextStrikeField.SetValue(room, value);
            }
        }

        private ForgeEnhancementBalance Balance()
        {
            if (balanceField == null)
            {
                balanceField = typeof(GPT6ForgeRoom).GetField("enhancementBalance", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            if (balanceField == null || room == null)
            {
                return null;
            }

            return balanceField.GetValue(room) as ForgeEnhancementBalance;
        }

        private GPT6Sword CurrentSword()
        {
            if (room == null)
            {
                return null;
            }

            GPT6Sword sword = room.OnAnvil;
            if (sword == null)
            {
                sword = room.Held;
            }

            return sword;
        }

        private void BoostValue(double factor)
        {
            GPT6Sword sword = CurrentSword();
            if (sword == null || room == null)
            {
                Notice("没有可操作的剑（需手持或放在铁砧上）");
                return;
            }

            sword.effects.multiplier *= factor;
            ForgeEconomy.RefreshValue(sword, room.Campaign, Balance());
            Notice("剑价值 ×" + factor.ToString("0") + " → " + ForgeEnhancementBalance.Money(sword.value));
        }

        private void BoostGrade(int step)
        {
            GPT6Sword sword = CurrentSword();
            if (sword == null || room == null)
            {
                Notice("没有可操作的剑（需手持或放在铁砧上）");
                return;
            }

            ForgeEnhancementBalance balance = Balance();
            if (balance == null)
            {
                Notice("强化数据未就绪");
                return;
            }

            int cap = ForgeEconomy.GradeCap(balance);
            int before = sword.grade;
            sword.grade = Mathf.Clamp(sword.grade + step, 0, cap);
            ForgeEconomy.RefreshValue(sword, room.Campaign, balance);
            Notice("剑等级 +" + (sword.grade - before) + " → +" + sword.grade);
        }

        private void GiveEverything()
        {
            if (room == null)
            {
                Notice("游戏尚未开始");
                return;
            }

            ForgeCampaign campaign = room.Campaign;
            if (campaign == null)
            {
                return;
            }

            for (int i = 0; i < campaign.relics.Length; i++)
            {
                string id = i < GodRelics.Length ? GodRelics[i] : "";
                campaign.relics[i] = id.Length == 0 ? null : new ForgeRelic { id = id, rank = 1 };
            }

            campaign.relicRevision++;
            campaign.wardCharges = 99;
            campaign.rewindCharges = 99;

            foreach (string id in Consumables)
            {
                campaign.AddUses(id, 99);
            }

            GPT6Sword sword = CurrentSword();
            if (sword != null)
            {
                ForgeEconomy.RefreshValue(sword, campaign, Balance());
            }

            Notice("已发放 8 件遗物 + 全消耗品 ×99");
        }

        private void Notice(string text)
        {
            notice = text;
            noticeUntil = Time.realtimeSinceStartup + 4f;
            Debug.Log("[ForgeCheat] " + text);
        }

        private void OnGUI()
        {
            if (!showPanel)
            {
                return;
            }

            try
            {
                // The game sorts OnGUI by depth with lower (more negative) values drawn on top:
                // its HUD sits at 0..-950, the language menu at -1000, and only the full-screen
                // recovery/fade layer at -9990 goes above this.
                GUI.depth = -1500;
                EnsureStyles();

                GUILayout.BeginArea(PanelRect, GUI.skin.box);
                GUILayout.Label("ForgeTax 修改器  v1.0   " + fps.ToString("0") + " FPS", headerStyle);
                GUILayout.Label("F8 显示/隐藏 · F11 鼠标接管（可点击面板）· F10 全部关闭", hintStyle);
                GUILayout.Space(8f);

                DrawToggle("F1  必定大成功（每次敲击都是大成功，不会破碎）", ref CheatConfig.AlwaysGreat);
                DrawToggle("F2  无限金币", ref CheatConfig.InfiniteGold);
                DrawToggle("F3  税赋归零（每轮配额=1，必达标）", ref CheatConfig.ZeroQuota);
                DrawToggle("F4  锻造加速 ×" + CheatConfig.ForgeSpeed.ToString("0.#") + "（提速会明显掉帧）", ref CheatConfig.FastForge);
                DrawToggle("F9  消耗品无限（护符/回溯等）", ref CheatConfig.InfiniteUses);
                DrawToggle("F11  鼠标接管（开启后可点击下面按钮）", ref mouseTakeover, SetMouseTakeover);

                GUILayout.Space(8f);
                GUILayout.Label("—— 单次操作（对准铁砧上的剑）——", hintStyle);

                if (GUILayout.Button("F5  当前剑价值 ×" + CheatConfig.ValueFactor.ToString("0"), actionStyle))
                {
                    BoostValue(CheatConfig.ValueFactor);
                }

                if (GUILayout.Button("F6  当前剑等级 +" + CheatConfig.GradeStep, actionStyle))
                {
                    BoostGrade(CheatConfig.GradeStep);
                }

                if (GUILayout.Button("F7  一键满配（8 件遗物 + 消耗品 ×99）", actionStyle))
                {
                    GiveEverything();
                }

                GUILayout.Space(8f);
                GUILayout.Label("—— 状态 ——", hintStyle);
                GUILayout.Label(BuildStatus(), statusStyle);

                if (notice.Length > 0 && Time.realtimeSinceStartup < noticeUntil)
                {
                    GUILayout.Space(6f);
                    GUILayout.Label("» " + notice, labelStyle);
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("设置写入 ForgeCheat.cfg · 修改会随自动存档保存", hintStyle);
                GUILayout.EndArea();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ForgeCheat] gui error: " + ex.Message);
            }
        }

        private string BuildStatus()
        {
            if (room == null || room.Campaign == null)
            {
                return "未进入游戏";
            }

            ForgeCampaign campaign = room.Campaign;
            GPT6Sword sword = CurrentSword();
            string status = "轮次 " + campaign.round
                + " / 截止 " + campaign.ContractDeadline
                + "\n金币 " + ForgeEnhancementBalance.Money(campaign.wallet)
                + "\n本轮配额 " + ForgeEnhancementBalance.Money(campaign.Quota)
                + "\n已交付 " + ForgeEnhancementBalance.Money(campaign.pendingTribute);

            if (sword != null)
            {
                status += "\n当前剑 +" + sword.grade
                    + " / 价值 " + ForgeEnhancementBalance.Money(sword.value)
                    + (sword.broken ? " / 已破碎" : "");
            }

            return status;
        }

        // GUILayout.Toggle proved unreliable under this game's IMGUI usage (it silently
        // returned false for every control), so state is flipped by an explicit button.
        private void DrawToggle(string text, ref bool value)
        {
            if (GUILayout.Button("  " + text + (value ? "   [开]" : "   [关]"), value ? onStyle : offStyle))
            {
                value = !value;
                CheatConfig.Save();
            }
        }

        private void DrawToggle(string text, ref bool value, Action<bool> apply)
        {
            if (GUILayout.Button("  " + text + (value ? "   [开]" : "   [关]"), value ? onStyle : offStyle))
            {
                value = !value;
                apply?.Invoke(value);
            }
        }

        private void EnsureStyles()
        {
            if (headerStyle != null)
            {
                return;
            }

            uiFont = LoadFont();

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                font = uiFont,
            };

            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, font = uiFont, wordWrap = true };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, font = uiFont, wordWrap = true };
            statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, font = uiFont, wordWrap = true };

            onStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, font = uiFont, alignment = TextAnchor.MiddleLeft };
            onStyle.normal.textColor = new Color(0.55f, 1f, 0.6f);
            onStyle.hover.textColor = new Color(0.7f, 1f, 0.75f);

            offStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, font = uiFont, alignment = TextAnchor.MiddleLeft };

            actionStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, font = uiFont };
        }

        private static Font LoadFont()
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun", "Arial Unicode MS" }, 15);
                if (font != null)
                {
                    return font;
                }
            }
            catch (Exception)
            {
                // fall through to the built-in font
            }

            try
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
