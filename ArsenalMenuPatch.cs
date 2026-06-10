using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        int rows = 3;
        int capacity = 36;
        int cols = capacity / rows;

        int menuX = Game1.uiViewport.Width / 2 - 350 - IClickableMenu.borderWidth;
        int menuWidth = 700 + IClickableMenu.borderWidth * 2;
        int menuY = Game1.uiViewport.Height / 2 - 150 - 100 - IClickableMenu.borderWidth;
        int menuH = 300 + IClickableMenu.borderWidth * 2;

        int totalWidth = cols * (newSq + hGap) - hGap;
        int startX = menuX + (menuWidth - totalWidth) / 2;
        int startY = menuY + menuH + 8;
        int totalHeight = Game1.uiViewport.Height - startY - 44;

        type.GetField("squareSide")?.SetValue(invMenu, newSq);
        type.GetField("scaleFactor")?.SetValue(invMenu, (float)newSq / 64f);
        type.GetField("yPositionOnScreen")?.SetValue(invMenu, startY);
        type.GetField("xPositionOnScreen")?.SetValue(invMenu, startX);
        type.GetField("width", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, totalWidth);
        type.GetField("height", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, totalHeight);
        type.GetField("xOffset")?.SetValue(invMenu, 0);
        type.GetField("yOffset")?.SetValue(invMenu, 0);
        type.GetField("hGap")?.SetValue(invMenu, hGap);

        type.GetField("drawSlots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, true);
        type.GetField("showTrash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, false);
        type.GetField("showOrganizeButton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, false);

// เพิ่มปุ่ม X เหมือน IClickableMenu
        var closeButton = new ClickableTextureComponent(
            new Rectangle(Game1.uiViewport.Width - 68 - Game1.xEdge, 0, 68 + Game1.xEdge, 80),
            Game1.mobileSpriteSheet,
            new Rectangle(62, 0, 17, 17),
            4f, true);
        typeof(IClickableMenu).GetField("upperRightCloseButton",
            BindingFlags.Public | BindingFlags.Instance)
            ?.SetValue(__instance, closeButton);

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

        Monitor?.Log($"rebuilt: startX={startX}, startY={startY}, sq={newSq}, totalWidth={totalWidth}, totalHeight={totalHeight}", LogLevel.Info);
    }
}

[HarmonyPatch(typeof(ArsenalMenu), "draw", new[] { typeof(SpriteBatch) })]
public class ArsenalMenuDrawPatch
{
    static void Prefix(ArsenalMenu __instance)
    {
        var field = typeof(ArsenalMenu).GetField("invMenu",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return;

        var invMenu = field.GetValue(__instance);
        if (invMenu == null) return;

        var type = invMenu.GetType();

        int newSq = 80;
        int hGap = 8;
        int cols = 12;
        int totalWidth = cols * (newSq + hGap) - hGap;

        int startX = (int)(type.GetField("xPositionOnScreen",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(invMenu) ?? 0);
        int startY = (int)(type.GetField("yPositionOnScreen",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(invMenu) ?? 0);
        int totalHeight = Game1.uiViewport.Height - startY - 44;

        type.GetField("width", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(invMenu, totalWidth);
        type.GetField("height", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(invMenu, totalHeight);
        type.GetField("xPositionOnScreen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(invMenu, startX);
    }
}

[HarmonyPatch(typeof(ArsenalMenu), "receiveLeftClick")]
public class ArsenalMenuClickPatch
{
    static void Prefix(ArsenalMenu __instance, int x, int y, ref Item __state)
    {
        // เซฟ CursorSlotItem ก่อน
        __state = Game1.player.CursorSlotItem;
    }

    static void Postfix(ArsenalMenu __instance, int x, int y, Item __state)
    {
        var field = typeof(ArsenalMenu).GetField("invMenu",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var invMenu = field?.GetValue(__instance);
        if (invMenu == null) return;

        var type = invMenu.GetType();
        var selectedField = type.GetField("currentlySelectedItem",
            BindingFlags.Public | BindingFlags.Instance);
        int selected = (int)(selectedField?.GetValue(invMenu) ?? -1);

        // ถ้ามีการ select item ใหม่ ให้ reset CursorSlotItem
        if (selected != -1 && Game1.player.CursorSlotItem != __state)
        {
            // คืน item กลับ inventory แทนที่จะลอยติดนิ้ว
            if (Game1.player.CursorSlotItem != null)
            {
                Game1.player.addItemToInventory(Game1.player.CursorSlotItem);
                Game1.player.CursorSlotItem = null;
            }
        }
    }
}
