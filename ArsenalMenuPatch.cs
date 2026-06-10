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

        int rows = 3;
        int capacity = 36;
        int cols = capacity / rows;
        int hGap = 4;
        int verticalGap = 4;
        int newSq = 80;

        int menuY = Game1.uiViewport.Height / 2 - 150 - 100 - IClickableMenu.borderWidth;
        int menuH = 300 + IClickableMenu.borderWidth * 2;
        int menuX = Game1.uiViewport.Width / 2 - 350 - IClickableMenu.borderWidth;
        int startY = menuY + menuH + 8;
        int startX = menuX;

        type.GetField("squareSide")?.SetValue(invMenu, newSq);
        type.GetField("scaleFactor")?.SetValue(invMenu, (float)newSq / 64f);
        type.GetField("yPositionOnScreen")?.SetValue(invMenu, startY);
        type.GetField("xPositionOnScreen")?.SetValue(invMenu, startX);
        type.GetField("hGap")?.SetValue(invMenu, hGap);
        type.GetField("xOffset")?.SetValue(invMenu, 0);
        type.GetField("yOffset")?.SetValue(invMenu, 0);

        // ปิดถังขยะและปุ่มจัดของ
        type.GetField("showTrash", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, false);
        type.GetField("showOrganizeButton", BindingFlags.Public | BindingFlags.Instance)?.SetValue(invMenu, false);

        // rebuild slots
        var inventorySlots = type.GetField("inventory")?.GetValue(invMenu) as List<ClickableComponent>;
        if (inventorySlots != null)
        {
            for (int j = 0; j < inventorySlots.Count; j++)
            {
                int row = j / cols;
                int col = j % cols;
                inventorySlots[j].bounds.X = startX + col * (newSq + hGap);
                inventorySlots[j].bounds.Y = startY + row * (newSq + verticalGap);
                inventorySlots[j].bounds.Width = newSq + hGap;
                inventorySlots[j].bounds.Height = newSq + verticalGap;
            }
        }

        Monitor?.Log($"rebuilt: startX={startX}, startY={startY}, sq={newSq}, menuX={menuX}", LogLevel.Info);
    }
}
