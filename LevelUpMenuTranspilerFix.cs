using System.Reflection;
using HarmonyLib;

namespace SnsAndroidFix;

public class LevelUpMenuTranspilerFix
{
    public static void Apply(Harmony harmony)
    {
        var snsType = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS");
        if (snsType == null) return;

        var method = snsType.GetMethod("GameLoop_GameLaunched",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) return;

        harmony.Patch(method,
            prefix: new HarmonyMethod(
                typeof(LevelUpMenuTranspilerFix)
                .GetMethod(nameof(BeforeGameLaunched))));
    }

    public static void BeforeGameLaunched()
    {
        // patch RevalidateHealth ก่อน SNS PatchAll
        var method = AccessTools.Method(
            typeof(StardewValley.Menus.LevelUpMenu), "RevalidateHealth");
        if (method == null) return;

        var harmony = new Harmony("You.SnsAndroidFix.PrePatch");
        try
        {
            harmony.Patch(method,
                transpiler: new HarmonyMethod(
                    typeof(LevelUpMenuTranspilerFix)
                    .GetMethod(nameof(EmptyTranspiler))));
        }
        catch { }
    }

    public static System.Collections.Generic.IEnumerable<CodeInstruction> EmptyTranspiler(
        System.Collections.Generic.IEnumerable<CodeInstruction> insns) => insns;
}
