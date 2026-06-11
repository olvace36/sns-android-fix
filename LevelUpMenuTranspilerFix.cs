using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SnsAndroidFix;

public class LevelUpMenuTranspilerFix
{
    public static void Apply(Harmony harmony)
    {
        var types = new[]
        {
            "SwordAndSorcerySMAPI.Framework.ModSkills.LevelUpMenuRevalidateHealthPatch",
            "SwordAndSorcerySMAPI.Framework.ModSkills.LevelUpMenuRevalidateHealthPatchAgain"
        };

        foreach (var typeName in types)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null) continue;

            var transpiler = type.GetMethod("Transpiler",
                BindingFlags.Public | BindingFlags.Static);
            if (transpiler == null) continue;

            harmony.Patch(transpiler,
                prefix: new HarmonyMethod(typeof(LevelUpMenuTranspilerFix)
                    .GetMethod(nameof(SafeTranspilerPrefix))));
        }
    }

    public static bool SafeTranspilerPrefix(MethodBase original,
        ref IEnumerable<CodeInstruction> __result,
        IEnumerable<CodeInstruction> insns)
    {
        try
        {
            return true;
        }
        catch
        {
            __result = insns;
            return false;
        }
    }
}
