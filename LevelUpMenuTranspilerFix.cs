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

        var patches = Harmony.GetPatchInfo(method);
        if (patches == null) return;

        foreach (var patch in patches.Transpilers)
        {
            if (patch.owner.Contains("SwordAndSorcery"))
            {
                var h = new Harmony(patch.owner);
                h.Unpatch(method, patch.PatchMethod);
            }
        }
    }
}
