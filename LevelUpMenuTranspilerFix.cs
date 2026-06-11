using System.Collections.Generic;
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

        // unpatch transpiler ของ SNS ทั้งหมด
        foreach (var patch in patches.Transpilers)
        {
            if (!patch.owner.Contains("SnsAndroidFix"))
            {
                var h = new Harmony(patch.owner);
                h.Unpatch(method, patch.PatchMethod);
            }
        }
    }
}
