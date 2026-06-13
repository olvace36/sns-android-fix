using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class EquipmentMenuDebugPatch
{
    internal static IMonitor? Monitor;
    private static Rectangle _btnBounds = Rectangle.Empty;

    public static void Apply(Harmony harmony)
    {
        var populate = typeof(IClickableMenu).GetMethod("populateClickableComponentList",
            BindingFlags.Public | BindingFlags.Instance);
        if (populate != null)
        {
            harmony.Patch(populate,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(PopulatePostfix))));
            Monitor?.Log("patched populateClickableComponentList", LogLevel.Info);
        }

        var spaceCorePrefix = AccessTools.TypeByName("SpaceCore.InventoryPageLeftClickPatch")
            ?.GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
        if (spaceCorePrefix != null)
        {
            harmony.Patch(spaceCorePrefix,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(BlockSpaceCorePrefix))));
            Monitor?.Log("patched SpaceCore.InventoryPageLeftClickPatch.Prefix", LogLevel.Info);
        }

        var spaceCoreDrawPostfix = AccessTools.TypeByName("SpaceCore.InventoryPageDrawTooltipPatch")
            ?.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
        if (spaceCoreDrawPostfix != null)
        {
            harmony.Patch(spaceCoreDrawPostfix,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(BlockSpaceCorePrefix))));
            Monitor?.Log("patched SpaceCore.InventoryPageDrawTooltipPatch.Postfix", LogLevel.Info);
        }

        var getComp = typeof(IClickableMenu).GetMethod("getComponentWithID",
            BindingFlags.Public | BindingFlags.Instance);
        if (getComp != null)
        {
            harmony.Patch(getComp,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(GetComponentWithIDPrefix))));
            Monitor?.Log("patched getComponentWithID", LogLevel.Info);
        }

        var draw = typeof(InventoryPage).GetMethod("draw", new[] { typeof(SpriteBatch) });
        if (draw != null)
        {
            harmony.Patch(draw,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(DrawPostfix))));
            Monitor?.Log("patched InventoryPage.draw", LogLevel.Info);
        }

        // แก้ข้อ 1: เปลี่ยนเป็น prefix แทน postfix และลบ releaseLeftClick ออก
        var receiveLeftClick = typeof(InventoryPage).GetMethod("receiveLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (receiveLeftClick != null)
        {
            harmony.Patch(receiveLeftClick,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(ReceiveLeftClickPrefix))));
            Monitor?.Log("patched InventoryPage.receiveLeftClick (prefix)", LogLevel.Info);
        }

        var gameMenuReceive = typeof(GameMenu).GetMethod("receiveLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (gameMenuReceive != null)
        {
            harmony.Patch(gameMenuReceive,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(GameMenuReceivePrefix))));
            Monitor?.Log("patched GameMenu.receiveLeftClick (prefix)", LogLevel.Info);
        }

        Monitor?.Log("EquipmentMenuDebugPatch applied!", LogLevel.Info);
    }

    public static bool BlockSpaceCorePrefix() => false;

    public static void PopulatePostfix(IClickableMenu __instance)
    {
        if (__instance is not InventoryPage page) return;

        var all = __instance.allClickableComponents;
        if (all != null)
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (all[i].myID == 1348000)
                {
                    all.RemoveAt(i);
                    Monitor?.Log("Removed ID 1348000 from allClickableComponents", LogLevel.Info);
                    break;
                }
            }
        }

        var equipmentIcons = typeof(InventoryPage)
            .GetField("equipmentIcons", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(page) as System.Collections.Generic.List<ClickableComponent>;
        if (equipmentIcons != null)
        {
            foreach (var icon in equipmentIcons)
            {
                if (icon.leftNeighborID == 1348000)
                {
                    icon.leftNeighborID = -1;
                    Monitor?.Log("Fixed leftNeighborID 1348000 → -1", LogLevel.Info);
                }
            }
        }

        _btnBounds = new Rectangle(
            page.xPositionOnScreen - 80,
            page.yPositionOnScreen + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 4 + 384 - 12 + 100,
            64, 64);
    }

    public static bool GetComponentWithIDPrefix(int id, ref ClickableComponent __result)
    {
        if (id == 1348000) { __result = null; return false; }
        return true;
    }

    public static void DrawPostfix(InventoryPage __instance, SpriteBatch b)
    {
        if (_btnBounds == Rectangle.Empty) return;
        try
        {
            var tex = Game1.content.Load<Texture2D>("spacechase0.SpaceCore/ExtraEquipmentIcon");
            b.Draw(tex, new Vector2(_btnBounds.X, _btnBounds.Y),
                new Rectangle(0, 0, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.87f);
        }
        catch { }
    }

    static bool TryOpenEquipmentMenu(int x, int y, string source)
    {
        if (_btnBounds == Rectangle.Empty) return false;
        if (!_btnBounds.Contains(x, y)) return false;
        if (Game1.activeClickableMenu?.GetChildMenu() is SnsEquipmentMenu) return false;

        var menu = Game1.activeClickableMenu;
        var chain = menu?.GetType().Name ?? "null";
        var cur = menu;
        while (cur?.GetChildMenu() != null)
        {
            cur = cur.GetChildMenu();
            chain += $" → {cur.GetType().Name}";
        }
        Monitor?.Log($"[{source}] chain: {chain} | SetChildMenu on: {cur?.GetType().Name ?? "null"}", LogLevel.Info);

        try
        {
            cur?.SetChildMenu(new SnsEquipmentMenu());
            Monitor?.Log($"[{source}] SnsEquipmentMenu opened as child!", LogLevel.Info);
            return true;
        }
        catch (Exception ex)
        {
            Monitor?.Log($"[{source}] CRASH: {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
            return false;
        }
    }

    // แก้ข้อ 1: prefix แทน postfix — ถ้าเปิด SnsEquipmentMenu ได้ return false หยุด original method
    public static bool ReceiveLeftClickPrefix(InventoryPage __instance, int x, int y)
    {
        return !TryOpenEquipmentMenu(x, y, "inv-receive");
    }

    public static bool GameMenuReceivePrefix(GameMenu __instance, int x, int y)
    {
        return !TryOpenEquipmentMenu(x, y, "gm-receive");
    }
}
