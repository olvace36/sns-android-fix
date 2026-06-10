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
    // เพิ่มตรงนี้
    Monitor?.Log($"ArsenalMenu: X={__instance.xPositionOnScreen}, Y={__instance.yPositionOnScreen}, W={__instance.width}, H={__instance.height}", LogLevel.Info);

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
    var xField = type.GetField("xPositionOnScreen");
    var yField = type.GetField("yPositionOnScreen");

    int x = (int)(xField?.GetValue(invMenu) ?? 0);
    int y = (int)(yField?.GetValue(invMenu) ?? 0);

    Monitor?.Log($"invMenu position: X={x}, Y={y}, ViewportH={Game1.uiViewport.Height}, ViewportW={Game1.uiViewport.Width}", LogLevel.Info);
    }
}
