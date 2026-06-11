using System.Reflection;
using HarmonyLib;

namespace SnsAndroidFix;

public class LevelUpMenuTranspilerFix
{
    public static void Apply(Harmony harmony)
    {
        var spaceCoreType = AccessTools.TypeByName("SpaceCore.ISpaceCoreApi");
        if (spaceCoreType == null) return;

        var method = spaceCoreType.GetMethod("GetLocalIndexForMethod");
        if (method == null) return;

        harmony.Patch(method,
            postfix: new HarmonyMethod(
                typeof(LevelUpMenuTranspilerFix)
                .GetMethod(nameof(GetLocalIndexPostfix))));
    }

    public static void GetLocalIndexPostfix(ref int[] __result)
    {
        if (__result == null || __result.Length == 0)
            __result = new int[] { 0 };
    }
}
