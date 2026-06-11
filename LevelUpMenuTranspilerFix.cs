using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SnsAndroidFix;

public class LevelUpMenuTranspilerFix
{
    public static void Apply(Harmony harmony)
    {
        var spaceCoreApiType = AccessTools.TypeByName("SpaceCore.Api");
        if (spaceCoreApiType == null) return;

        var method = spaceCoreApiType.GetMethod("GetLocalIndexForMethod",
            BindingFlags.Public | BindingFlags.Instance);
        if (method == null) return;

        harmony.Patch(method,
            postfix: new HarmonyMethod(
                typeof(LevelUpMenuTranspilerFix)
                .GetMethod(nameof(GetLocalIndexPostfix))));

        // patch Transpiler ของ SNS ให้ข้ามการ inject ถ้า index ว่าง
        var types = new[]
        {
            "SwordAndSorcerySMAPI.Framework.ModSkills.LevelUpMenuRevalidateHealthPatch",
            "SwordAndSorcerySMAPI.Framework.ModSkills.LevelUpMenuRevalidateHealthPatchAgain"
        };

        foreach (var typeName in types)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null) continue;
            var transpiler = type.GetMethod("Transpiler", BindingFlags.Public | BindingFlags.Static);
            if (transpiler == null) continue;
            harmony.Patch(transpiler,
                prefix: new HarmonyMethod(typeof(LevelUpMenuTranspilerFix)
                    .GetMethod(nameof(TranspilerPrefix))));
        }
    }

    public static void GetLocalIndexPostfix(ref List<int> __result)
    {
        if (__result == null)
            __result = new List<int>();
    }

    public static bool TranspilerPrefix(MethodBase original,
        ref IEnumerable<CodeInstruction> __result,
        IEnumerable<CodeInstruction> insns)
    {
        try
        {
            var spaceCoreApi = AccessTools.TypeByName("SpaceCore.Api");
            var getLocalIndex = spaceCoreApi?.GetMethod("GetLocalIndexForMethod",
                BindingFlags.Public | BindingFlags.Instance);
            // ถ้าหา instance ไม่ได้ให้ข้ามไป
            if (getLocalIndex == null)
            {
                __result = insns;
                return false;
            }
            return true;
        }
        catch
        {
            __result = insns;
            return false;
        }
    }
}
