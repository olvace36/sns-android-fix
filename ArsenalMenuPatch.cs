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
        var instanceType = typeof(ArsenalMenu);
        int ax = (int)(instanceType.GetField("xPositionOnScreen", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)?.GetValue(__instance) ?? -1);
        int ay = (int)(instanceType.GetField("yPositionOnScreen", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)?.GetValue(__instance) ?? -1);
        Monitor?.Log($"ArsenalMenu: X={ax}, Y={ay}", LogLevel.Info);

        var field = instanceType.GetField("invMenu",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            Monitor?.Log("invMenu field not found!", LogLevel.Error);
            return;
        }

        var invMenu = field.GetValue(__instance);
        if (invMenu == null)
        {
            Monitor?.Log("invMenu is null!", LogLevel.Error);
            return;
        }

        var type = invMenu.GetType();
        int x = (int)(type.GetField("xPositionOnScreen")?.GetValue(invMenu) ?? 0);
        int y = (int)(type.GetField("yPositionOnScreen")?.GetValue(invMenu) ?? 0);

        Monitor?.Log($"invMenu: X={x}, Y={y}, ViewportH={Game1.uiViewport.Height}, ViewportW={Game1.uiViewport.Width}", LogLevel.Info);
    }
}
