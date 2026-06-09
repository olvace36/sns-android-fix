using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using SwordAndSorcerySMAPI;

namespace SnsAndroidFix;

[HarmonyPatch(typeof(ArsenalMenu), MethodType.Constructor)]
public class ArsenalMenuPatch
{
    static void Postfix(ArsenalMenu __instance)
    {
        var invMenu = Traverse.Create(__instance)
            .Field("invMenu")
            .GetValue<InventoryMenu>();

        if (invMenu == null) return;

        if (invMenu.xPositionOnScreen < 0)
            invMenu.xPositionOnScreen = 0;

        int maxY = Game1.uiViewport.Height - 280;
        if (invMenu.yPositionOnScreen > maxY)
            invMenu.yPositionOnScreen = maxY;
    }
}
