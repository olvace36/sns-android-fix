using System.Reflection;
using HarmonyLib;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class LevelUpMenuTranspilerFix
{
    public static void Apply()
    {
        var method = AccessTools.Method(typeof(LevelUpMenu), "RevalidateHealth");
        if (method == null) return;

        var patches = Harmony.GetPatchInfo(method);
        if (patches == null) return;

        foreach (var patch in patches.Transpilers)
        {
            if (patch.owner.Contains("SwordAndSorcery"))
            {
                var harmony = new Harmony(patch.owner);
                harmony.Unpatch(method, HarmonyPatchType.Transpiler, patch.owner);
            }
        }
    }
}
