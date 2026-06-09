using HarmonyLib;
using StardewValley;
using SwordAndSorcerySMAPI;

namespace SnsAndroidFix;

[HarmonyPatch(typeof(ArsenalMenu), MethodType.Constructor)]
public class ArsenalMenuPatch
{
    static void Postfix(ArsenalMenu __instance)
    {
        var invMenu = Traverse.Create(__instance)
            .Field("invMenu")
            .GetValue();

        if (invMenu == null) return;

        var traverse = Traverse.Create(invMenu);

        int x = traverse.Field("xPositionOnScreen").GetValue<int>();
        int y = traverse.Field("yPositionOnScreen").GetValue<int>();

        if (x < 0)
            traverse.Field("xPositionOnScreen").SetValue(0);

        int maxY = Game1.uiViewport.Height - 280;
        if (y > maxY)
            traverse.Field("yPositionOnScreen").SetValue(maxY);
    }
}
