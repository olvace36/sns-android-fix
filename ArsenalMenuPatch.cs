using System.Reflection;
using HarmonyLib;
using StardewValley;
using SwordAndSorcerySMAPI;

namespace SnsAndroidFix;

[HarmonyPatch(typeof(ArsenalMenu), MethodType.Constructor)]
public class ArsenalMenuPatch
{
    static void Postfix(ArsenalMenu __instance)
    {
        var field = typeof(ArsenalMenu).GetField("invMenu", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return;

        var invMenu = field.GetValue(__instance);
        if (invMenu == null) return;

        var type = invMenu.GetType();

        var xField = type.GetField("xPositionOnScreen");
        var yField = type.GetField("yPositionOnScreen");

        if (xField != null)
        {
            int x = (int)xField.GetValue(invMenu);
            if (x < 0) xField.SetValue(invMenu, 0);
        }

        if (yField != null)
        {
            int y = (int)yField.GetValue(invMenu);
            int maxY = Game1.uiViewport.Height - 280;
            if (y > maxY) yField.SetValue(invMenu, maxY);
        }
    }
}
