using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class LevelUpMenuTranspilerFix
{
    public static void Apply(Harmony harmony)
    {
        var method = AccessTools.Method(typeof(LevelUpMenu), "RevalidateHealth");
        if (method == null) return;

        harmony.Patch(method,
            transpiler: new HarmonyMethod(typeof(LevelUpMenuTranspilerFix)
                .GetMethod(nameof(SafeTranspiler))));
    }

    public static IEnumerable<CodeInstruction> SafeTranspiler(
        IEnumerable<CodeInstruction> insns)
    {
        return insns;
    }
}
