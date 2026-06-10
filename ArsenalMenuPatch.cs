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
        int x = (int)(type.GetField("xPositionOnScreen")?.GetValue(invMenu) ?? 0);
        int y = (int)(type.GetField("yPositionOnScreen")?.GetValue(invMenu) ?? 0);

        Monitor?.Log($"invMenu before: X={x}, Y={y}", LogLevel.Info);

        int newY = (Game1.uiViewport.Height - 280) / 2 + 100;
        type.GetField("yPositionOnScreen")?.SetValue(invMenu, newY);

        Monitor?.Log($"invMenu after: X={x}, Y={newY}", LogLevel.Info);
    }
}
