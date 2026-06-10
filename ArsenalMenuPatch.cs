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
        var moveMethod = type.GetMethod("movePosition",
            BindingFlags.Public | BindingFlags.Instance);

        int y = (int)(type.GetField("yPositionOnScreen")?.GetValue(invMenu) ?? 0);

        // reset Y เป็น 0 ก่อน แล้ว set ใหม่ (เหมือน ForgeMenu)
        moveMethod?.Invoke(invMenu, new object[] { 0, -y });

        int newY = Game1.uiViewport.Height / 2 + 50;
        moveMethod?.Invoke(invMenu, new object[] { 0, newY });

        Monitor?.Log($"invMenu moved to Y={newY}", LogLevel.Info);
    }
}
