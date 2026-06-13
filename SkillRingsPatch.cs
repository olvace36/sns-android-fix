using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace SnsAndroidFix;

public class SkillRingsPatch
{
    internal static IMonitor? Monitor;

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
            transpiler: new HarmonyMethod(typeof(SkillRingsPatch)
                .GetMethod(nameof(OnUpdateTickedTranspiler))));

        Monitor?.Log("SkillRingsPatch applied!", LogLevel.Info);
    }

    public static IEnumerable<CodeInstruction> OnUpdateTickedTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var isPlayerFreeGetter = AccessTools.PropertyGetter(
            typeof(StardewModdingAPI.Context), "IsPlayerFree");

        var codes = new List<CodeInstruction>(instructions);
        bool found = false;

        for (int i = 0; i < codes.Count - 2; i++)
        {
            // หา pattern: call IsPlayerFree -> brfalse/brtrue
            if (codes[i].Calls(isPlayerFreeGetter))
            {
                // ลบ call IsPlayerFree และ branch instruction ที่ตามมา
                // แทนด้วย nop
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
