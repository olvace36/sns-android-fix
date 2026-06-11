using System;
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

        try
        {
            harmony.Patch(method,
                transpiler: new HarmonyMethod(
                    typeof(LevelUpMenuTranspilerFix)
                    .GetMethod(nameof(EmptyTranspiler))));
        }
        catch { }
    }

    public static IEnumerable<CodeInstruction> EmptyTranspiler(
        IEnumerable<CodeInstruction> insns) => insns;
}
