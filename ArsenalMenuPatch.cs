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

        int oldY = (int)(type.GetField("yPositionOnScreen")?.GetValue(invMenu) ?? 0);

        int menuY = Game1.uiViewport.Height / 2 - 150 - 100 - IClickableMenu.borderWidth;
        int menuH = 300 + IClickableMenu.borderWidth * 2;
        int newY = menuY + menuH + 8;
        int diff = newY - oldY;

        type.GetField("yPositionOnScreen")?.SetValue(invMenu, newY);

        var inventorySlots = type.GetField("inventory")?.GetValue(invMenu) as List<ClickableComponent>;
        if (inventorySlots != null)
        {
            foreach (var slot in inventorySlots)
                slot.bounds.Y += diff;
        }

        // reset fadeRect ให้ครอบคลุมทั้งจอ
        var fadeRectField = type.GetField("fadeRect", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fadeRectField != null)
        {
            var newFadeRect = new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
            fadeRectField.SetValue(invMenu, newFadeRect);
            Monitor?.Log("fadeRect set to full screen", LogLevel.Info);
        }

        if (inventorySlots != null && inventorySlots.Count > 0)
            Monitor?.Log($"slot[0] bounds: X={inventorySlots[0].bounds.X}, Y={inventorySlots[0].bounds.Y}", LogLevel.Info);

        Monitor?.Log($"menuY={menuY}, menuH={menuH}, oldY={oldY}, newY={newY}, diff={diff}, slots={inventorySlots?.Count}", LogLevel.Info);
    }
}
