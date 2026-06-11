using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SnsAndroidFix;

public class LevelUpMenuTranspilerFix
{
    public static void Apply(Harmony harmony)
    {
        // หา implementation จริงของ GetLocalIndexForMethod
        var allTypes = typeof(HarmonyLib.Harmony).Assembly.GetTypes();
        
        var spaceCoreAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "SpaceCore");
        if (spaceCoreAssembly == null) return;

        foreach (var type in spaceCoreAssembly.GetTypes())
        {
            var method = type.GetMethod("GetLocalIndexForMethod",
                BindingFlags.Public | BindingFlags.Instance);
            if (method == null) continue;

            harmony.Patch(method,
                postfix: new HarmonyMethod(
                    typeof(LevelUpMenuTranspilerFix)
                    .GetMethod(nameof(GetLocalIndexPostfix))));
            break;
        }
    }

    public static void GetLocalIndexPostfix(ref List<int> __result)
    {
        if (__result == null || __result.Count == 0)
            __result = new List<int> { 0 };
    }
}
