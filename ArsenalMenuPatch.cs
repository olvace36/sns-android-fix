using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using SwordAndSorcerySMAPI;

namespace SnsAndroidFix;

[HarmonyPatch(typeof(ArsenalMenu), MethodType.Constructor)]
public class ArsenalMenuPatch
{
    static void Postfix(ArsenalMenu __instance, InventoryMenu ___invMenu)
    {
        if (___invMenu == null) return;

        int maxY = Game1.uiViewport.Height - 280;
        if (___invMenu.yPositionOnScreen > maxY)
        {
            ___invMenu.yPositionOnScreen = maxY;
        }
    }
}
