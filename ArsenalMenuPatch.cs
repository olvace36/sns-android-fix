using System;
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

        int sq = (int)(type.GetField("squareSide")?.GetValue(invMenu) ?? 0);
        if (sq != 0) return;

        int newSq = 64;
        int hGap = 8;
        int verticalGap = 8;
        int xOff = newSq / 2;
        int yOff = 0;
        int rows = 3;
        int capacity = 36;
        int cols = capacity / rows;

        int menuY = Game1.uiViewport.Height / 2 - 150 - 100 - IClickableMenu.borderWidth;
        int menuH = 300 + IClickableMenu.borderWidth * 2;
        int startX = (int)(type.GetField("xPositionOnScreen")?.GetValue(invMenu) ?? 0);
        int startY = menuY + menuH + 8;

        type.GetField("squareSide")?.SetValue(invMenu, newSq);
        type.GetField("scaleFactor")?.SetValue(invMenu, (float)newSq / 64f);
        type.GetField("yPositionOnScreen")?.SetValue(invMenu, startY);
        type.GetField("xOffset")?.SetValue(invMenu, xOff);
        type.GetField("yOffset")?.SetValue(invMenu, yOff);
        type.GetField("hGap")?.SetValue(invMenu, hGap);

        // rebuild slot bounds
        var inventorySlots = type.GetField("inventory")?.GetValue(invMenu) as List<ClickableComponent>;
        if (inventorySlots != null)
        {
            for (int j = 0; j < inventorySlots.Count; j++)
            {
                int row = j / cols;
                int col = j % cols;
                inventorySlots[j].bounds.X = startX + xOff + col * (newSq + hGap);
                inventorySlots[j].bounds.Y = startY + yOff + row * (newSq + verticalGap);
                inventorySlots[j].bounds.Width = newSq + hGap;
                inventorySlots[j].bounds.Height = newSq + verticalGap;
            }
        }

        Monitor?.Log($"rebuilt slots: startX={startX}, startY={startY}, sq={newSq}, slots={inventorySlots?.Count}", LogLevel.Info);
    }
}
