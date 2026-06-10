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

        int newSq = 80;
        int hGap = 8;
        int verticalGap = 8;
        int xOff = 0;
        int yOff = 0;
        int rows = 3;
        int capacity = 36;
        int cols = capacity / rows;

        int menuY = Game1.uiViewport.Height / 2 - 150 - 100 - IClickableMenu.borderWidth;
        int menuH = 300 + IClickableMenu.borderWidth * 2;
        int startX = Game1.xEdge;
        int startY = menuY + menuH + 8;

        type.GetField("squareSide")?.SetValue(invMenu, newSq);
        type.GetField("scaleFactor")?.SetValue(invMenu, (float)newSq / 64f);
        type.GetField("yPositionOnScreen")?.SetValue(invMenu, startY);
        type.GetField("xPositionOnScreen")?.SetValue(invMenu, startX);
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
                inventorySlots[j].bounds.X = startX + col * (newSq + hGap);
                inventorySlots[j].bounds.Y = startY + row * (newSq + verticalGap);
                inventorySlots[j].bounds.Width = newSq + hGap;
                inventorySlots[j].bounds.Height = newSq + verticalGap;
            }
        }

        // fix trashCan และ organizeButton
        int lastSlotX = startX + (cols - 1) * (newSq + hGap);
        int lastSlotY = startY + (rows - 1) * (newSq + verticalGap);

        var trashCan = type.GetField("trashCan")?.GetValue(invMenu);
        if (trashCan != null)
        {
            var trashType = trashCan.GetType();
            var bounds = trashType.GetField("bounds")?.GetValue(trashCan);
            if (bounds != null)
            {
                var newBounds = new Rectangle(lastSlotX + newSq + 16, startY, 64, 150);
                trashType.GetField("bounds")?.SetValue(trashCan, newBounds);
                type.GetField("trashX")?.SetValue(invMenu, newBounds.X);
                type.GetField("trashY")?.SetValue(invMenu, newBounds.Y);
            }
        }

        var organizeButton = type.GetField("organizeButton")?.GetValue(invMenu);
        if (organizeButton != null)
        {
            var orgType = organizeButton.GetType();
            var newOrgBounds = new Rectangle(lastSlotX + newSq + 16, startY + 80, 64, 64);
            orgType.GetField("bounds")?.SetValue(organizeButton, newOrgBounds);
            type.GetField("orgX")?.SetValue(invMenu, newOrgBounds.X);
            type.GetField("orgY")?.SetValue(invMenu, newOrgBounds.Y);
        }

        Monitor?.Log($"rebuilt: startX={startX}, startY={startY}, sq={newSq}", LogLevel.Info);
    }
}
