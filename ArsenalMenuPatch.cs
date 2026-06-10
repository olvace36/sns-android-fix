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
        var instanceType = __instance.GetType().BaseType;
        int ax = (int)(instanceType?.GetField("xPositionOnScreen")?.GetValue(__instance) ?? -1);
        int ay = (int)(instanceType?.GetField("yPositionOnScreen")?.GetValue(__instance) ?? -1);
        int aw = (int)(instanceType?.GetField("width")?.GetValue(__instance) ?? -1);
        int ah = (int)(instanceType?.GetField("height")?.GetValue(__instance) ?? -1);
        Monitor?.Log($"ArsenalMenu: X={ax}, Y={ay}, W={aw}, H={ah}", LogLevel.Info);

        var field = typeof(ArsenalMenu).GetField("invMenu",
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
