using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
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

        var invMenu = field.GetValue(__instance);
        if (invMenu == null) return;

        var type = invMenu.GetType();

        // fix squareSide=0
        int sq = (int)(type.GetField("squareSide")?.GetValue(invMenu) ?? 0);
        if (sq == 0)
        {
            int viewH = Game1.uiViewport.Height;
            int viewW = Game1.uiViewport.Width;
            int newSq = Math.Min((int)(90f * ((float)viewH / 360f)), (int)(90f * ((float)viewW / 1280f)));
            type.GetField("squareSide")?.SetValue(invMenu, newSq);
            type.GetField("scaleFactor")?.SetValue(invMenu, (float)newSq / 64f);
            Monitor?.Log($"fixed squareSide={newSq}", LogLevel.Info);
        }

        Monitor?.Log($"invMenu w={type.GetField("width")?.GetValue(invMenu)}, squareSide={type.GetField("squareSide")?.GetValue(invMenu)}", LogLevel.Info);
    }
}
