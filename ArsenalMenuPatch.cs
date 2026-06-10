using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using SwordAndSorcerySMAPI;

namespace SnsAndroidFix;

[HarmonyPatch(typeof(ArsenalMenu), MethodType.Constructor)]
public class ArsenalMenuPatch
{
    internal static IMonitor? Monitor;

    static void Postfix(ArsenalMenu __instance)
    {
        var field = typeof(ArsenalMenu).GetField("invMenu",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return;

        var oldInvMenu = field.GetValue(__instance);
        if (oldInvMenu == null) return;

        var type = oldInvMenu.GetType();
        int sq = (int)(type.GetField("squareSide")?.GetValue(oldInvMenu) ?? 0);
        if (sq != 0) return;

        int menuY = Game1.uiViewport.Height / 2 - 150 - 100 - IClickableMenu.borderWidth;
        int menuH = 300 + IClickableMenu.borderWidth * 2;
        int startY = menuY + menuH + 8;
        int startX = Game1.xEdge;

        // สร้าง InventoryMenu ใหม่
        var newInvMenu = new InventoryMenu(startX, startY, true);
        field.SetValue(__instance, newInvMenu);

        Monitor?.Log($"replaced invMenu: X={startX}, Y={startY}", LogLevel.Info);
    }
}
