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
    }

    public static void GetLocalIndexPostfix(string local, ref List<int> __result)
    {
        // ถ้า Android ไม่เจอ local ให้ return {-1} เพื่อให้ Transpiler ข้ามไป
        if (__result == null || __result.Count == 0)
            __result = new List<int> { -1 };
    }
}
