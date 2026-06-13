using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace SnsAndroidFix;

public class SkillRingsPatch
{
    internal static IMonitor? Monitor;
    private static string[] _snsSkillIds = new[]
    {
        "DestyNova.SwordAndSorcery.Rogue",
        "DestyNova.SwordAndSorcery.Druidics",
        "DestyNova.SwordAndSorcery.Bardics",
        "DestyNova.SwordAndSorcery.Sorcery",
        "DestyNova.SwordAndSorcery.Paladin"
    };

    public static void Apply(IModHelper helper, Harmony harmony)
    {
        var skillRingsType = AccessTools.TypeByName("SkillRings.ModEntry");
        if (skillRingsType == null)
        {
            Monitor?.Log("SkillRings not found", LogLevel.Warn);
            return;
        }

        var onUpdateTicked = skillRingsType.GetMethod("onUpdateTicked",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (onUpdateTicked == null)
        {
            Monitor?.Log("onUpdateTicked not found", LogLevel.Warn);
            return;
        }

        harmony.Patch(onUpdateTicked,
            prefix: new HarmonyMethod(typeof(SkillRingsPatch)
                .GetMethod(nameof(OnUpdateTickedPrefix))),
            transpiler: new HarmonyMethod(typeof(SkillRingsPatch)
                .GetMethod(nameof(OnUpdateTickedTranspiler))));

        Monitor?.Log("SkillRingsPatch applied!", LogLevel.Info);
    }

    // remove buff เก่าทุก SNS skill ก่อน onUpdateTicked ทำงาน
    public static void OnUpdateTickedPrefix(object __instance, object sender, object e)
    {
        if (e == null) return;
        var isOneSecond = (bool?)e.GetType()
            .GetProperty("IsOneSecond")?.GetValue(e) ?? false;
        if (!isOneSecond) return;

        foreach (var skillId in _snsSkillIds)
        {
            string buffId = $"AlphaMeece.SkillRings_{skillId}Buff";
            if (Game1.player.hasBuff(buffId))
            {
                Game1.player.buffs.Remove(buffId);
                Monitor?.Log($"Prefix: removed buff {buffId}", LogLevel.Info);
            }
        }
    }

    // ลบเงื่อนไข IsPlayerFree
    public static IEnumerable<CodeInstruction> OnUpdateTickedTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var isPlayerFreeGetter = AccessTools.PropertyGetter(
            typeof(StardewModdingAPI.Context), "IsPlayerFree");

        var codes = new List<CodeInstruction>(instructions);
        bool found = false;

        for (int i = 0; i < codes.Count - 2; i++)
        {
            if (codes[i].Calls(isPlayerFreeGetter))
            {
                codes[i] = new CodeInstruction(OpCodes.Nop);
                if (i + 1 < codes.Count && (
                    codes[i + 1].opcode == OpCodes.Brfalse ||
                    codes[i + 1].opcode == OpCodes.Brfalse_S ||
                    codes[i + 1].opcode == OpCodes.Brtrue ||
                    codes[i + 1].opcode == OpCodes.Brtrue_S))
                {
                    codes[i + 1] = new CodeInstruction(OpCodes.Nop);
                }
                found = true;
                Monitor?.Log("Removed IsPlayerFree check from SkillRings", LogLevel.Info);
                break;
            }
        }

        if (!found)
            Monitor?.Log("IsPlayerFree check not found in SkillRings!", LogLevel.Warn);

        return codes;
    }
}
