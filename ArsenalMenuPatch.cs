using System.Collections;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
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

        var invMenu = field.GetValue(__instance);
        if (invMenu == null) return;

        var type = invMenu.GetType();

        var actualInventory = type.GetField("actualInventory")?.GetValue(invMenu);
        var invType = actualInventory?.GetType();
        int count = (int)(invType?.GetProperty("Count")?.GetValue(actualInventory) ?? -1);
        Monitor?.Log($"inventory count: {count}", LogLevel.Info);

        var moveMethod = type.GetMethod("movePosition",
            BindingFlags.Public | BindingFlags.Instance);

        int y = (int)(type.GetField("yPositionOnScreen")?.GetValue(invMenu) ?? 0);
        moveMethod?.Invoke(invMenu, new object[] { 0, -y });

        int newY = Game1.uiViewport.Height / 2 + 50;
        moveMethod?.Invoke(invMenu, new object[] { 0, newY });

        Monitor?.Log($"invMenu moved to Y={newY}", LogLevel.Info);
    }
}
