using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace SnsAndroidFix;

public class LevelUpMenuTranspilerFix
{
    internal static IMonitor? Monitor;

    public static void Apply(Harmony harmony, IMonitor monitor)
    {
        Monitor = monitor;
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

    public static void GetLocalIndexPostfix(MethodBase meth, string local, ref List<int> __result)
    {
        Monitor?.Log($"GetLocalIndexForMethod: method={meth?.Name}, local={local}, result count={__result?.Count}", LogLevel.Info);
        if (__result == null || __result.Count == 0)
            __result = new List<int> { 0 };
    }
}
