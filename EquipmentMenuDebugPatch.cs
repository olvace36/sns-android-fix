using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class EquipmentMenuDebugPatch
{
    internal static IMonitor? Monitor;
    private static Rectangle _btnBounds = Rectangle.Empty;
    private static int _lastHeldX;
    private static int _lastHeldY;
    private static bool _isHolding;
    private static int _updateLogThrottle = 0;

    // เก็บ SnsEquipmentMenu แยกต่างหาก ซ่อนจาก Game1 child menu chain
    internal static SnsEquipmentMenu? HiddenChildMenu;

    public static void Apply(Harmony harmony)
    {
        // ปิด SpaceCore ไม่ให้สร้างปุ่มเก่า
        var constructorPostfix = AccessTools.TypeByName("SpaceCore.InventoryPageConstructorPatch")
            ?.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
        if (constructorPostfix != null)
        {
            harmony.Patch(constructorPostfix,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(BlockConstructorPostfix))));
            Monitor?.Log("patched SpaceCore.InventoryPageConstructorPatch.Postfix (blocked)", LogLevel.Info);
        }

        // ปิด draw ของ SpaceCore
        var spaceCoreDrawPostfix = AccessTools.TypeByName("SpaceCore.InventoryPageDrawTooltipPatch")
            ?.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
        if (spaceCoreDrawPostfix != null)
        {
            harmony.Patch(spaceCoreDrawPostfix,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(BlockDrawPrefix))));
            Monitor?.Log("patched SpaceCore.InventoryPageDrawTooltipPatch.Postfix (blocked)", LogLevel.Info);
        }

        var populate = typeof(IClickableMenu).GetMethod("populateClickableComponentList",
            BindingFlags.Public | BindingFlags.Instance);
        if (populate != null)
        {
            harmony.Patch(populate,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(PopulatePostfix))));
            Monitor?.Log("patched populateClickableComponentList", LogLevel.Info);
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

        var receiveLeftClick = typeof(InventoryPage).GetMethod("receiveLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (receiveLeftClick != null)
        {
            harmony.Patch(receiveLeftClick,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(ReceiveLeftClickPrefix))),
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(ReceiveLeftClickPostfix))));
            Monitor?.Log("patched InventoryPage.receiveLeftClick (prefix+postfix)", LogLevel.Info);
        }

        // patch InventoryPage.leftClickHeld ส่งต่อให้ HiddenChildMenu
        var invHeld = typeof(InventoryPage).GetMethod("leftClickHeld",
            BindingFlags.Public | BindingFlags.Instance);
        if (invHeld != null)
        {
            harmony.Patch(invHeld,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(InventoryPageLeftClickHeldPostfix))));
            Monitor?.Log("patched InventoryPage.leftClickHeld", LogLevel.Info);
        }

        var invRelease = typeof(InventoryPage).GetMethod("releaseLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (invRelease != null)
        {
            harmony.Patch(invRelease,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(InventoryPageReleasePostfix))));
            Monitor?.Log("patched InventoryPage.releaseLeftClick", LogLevel.Info);
        }

        // patch InventoryPage.update ส่งต่อให้ HiddenChildMenu
        var invUpdate = typeof(InventoryPage).GetMethod("update",
            BindingFlags.Public | BindingFlags.Instance);
        if (invUpdate != null)
        {
            harmony.Patch(invUpdate,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(InventoryPageUpdatePostfix))));
            Monitor?.Log("patched InventoryPage.update", LogLevel.Info);
        }

        // patch GameMenu.leftClickHeld เก็บ coordinate ที่ถูกต้อง
        var gameMenuHeld = typeof(GameMenu).GetMethod("leftClickHeld",
            BindingFlags.Public | BindingFlags.Instance);
        if (gameMenuHeld != null)
        {
            harmony.Patch(gameMenuHeld,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(GameMenuLeftClickHeldPrefix))));
            Monitor?.Log("patched GameMenu.leftClickHeld", LogLevel.Info);
        }

        var gameMenuRelease = typeof(GameMenu).GetMethod("releaseLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (gameMenuRelease != null)
        {
            harmony.Patch(gameMenuRelease,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(GameMenuReleasePostfix))));
            Monitor?.Log("patched GameMenu.releaseLeftClick", LogLevel.Info);
        }

        // patch Game1.updateActiveMenu ส่ง coordinate ล่าสุดให้ HiddenChildMenu
        var updateActiveMenu = typeof(Game1).GetMethod("updateActiveMenu",
            BindingFlags.Public | BindingFlags.Static);
        if (updateActiveMenu != null)
        {
            harmony.Patch(updateActiveMenu,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(UpdateActiveMenuPostfix))));
            Monitor?.Log("patched Game1.updateActiveMenu", LogLevel.Info);
        }

        Monitor?.Log("EquipmentMenuDebugPatch applied!", LogLevel.Info);
    }

    public static bool BlockConstructorPostfix() => false;
    public static bool BlockDrawPrefix() => false;

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
        // วาดปุ่มใหม่
        if (_btnBounds != Rectangle.Empty)
        {
            try
            {
                var tex = Game1.content.Load<Texture2D>("spacechase0.SpaceCore/ExtraEquipmentIcon");
                b.Draw(tex, new Vector2(_btnBounds.X, _btnBounds.Y),
                    new Rectangle(0, 0, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.87f);
            }
            catch { }
        }

        // วาด SnsEquipmentMenu ที่ซ่อนไว้
        HiddenChildMenu?.draw(b);
    }

    public static bool ReceiveLeftClickPrefix(InventoryPage __instance, int x, int y)
    {
        // ถ้า SnsEquipmentMenu เปิดอยู่ → ส่ง click ให้มัน
        if (HiddenChildMenu != null)
        {
            Monitor?.Log($"ReceiveLeftClick forwarding to HiddenChildMenu ({x},{y})", LogLevel.Info);
            HiddenChildMenu.receiveLeftClick(x, y);
            // return true เพื่อให้ SMAPI Android ยังส่ง coordinate ถูกต้องใน leftClickHeld
            // แต่ถ้า InventoryPage.receiveLeftClick ทำงาน จะ reset inventoryItemHeld
            // ดังนั้น restore state หลัง original ทำงาน
            return true;
        }

        if (_btnBounds == Rectangle.Empty) return true;
        if (!_btnBounds.Contains(x, y)) return true;

        Monitor?.Log($"Hit new btn! Opening SnsEquipmentMenu", LogLevel.Info);
        try
        {
            HiddenChildMenu = new SnsEquipmentMenu();
            Monitor?.Log("SnsEquipmentMenu opened as HiddenChildMenu!", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor?.Log($"CRASH: {ex.Message}", LogLevel.Error);
        }
        return false;
    }

    // postfix บน InventoryPage.receiveLeftClick
    // restore inventoryItemHeld ของ HiddenChildMenu หลัง original method reset ค่า
    public static void ReceiveLeftClickPostfix(InventoryPage __instance, int x, int y)
    {
        if (HiddenChildMenu == null) return;
        Monitor?.Log($"ReceiveLeftClickPostfix: restoring HiddenChildMenu state ({x},{y})", LogLevel.Info);
        // forward ไปยัง HiddenChildMenu อีกครั้งหลัง InventoryPage ทำงาน
        // เพื่อให้ inventoryItemHeld ถูก set ถูกต้อง
        HiddenChildMenu.receiveLeftClick(x, y);
    }

    // InventoryPage.leftClickHeld ได้รับ coordinate ที่ถูกต้องจาก SMAPI Android
    // ส่งต่อให้ HiddenChildMenu
    public static void InventoryPageLeftClickHeldPostfix(InventoryPage __instance, int x, int y)
    {
        _lastHeldX = x;
        _lastHeldY = y;
        _isHolding = true;
        Monitor?.Log($"InventoryPageLeftClickHeld ({x},{y}) hidden={HiddenChildMenu != null}", LogLevel.Info);
        if (HiddenChildMenu != null)
            HiddenChildMenu.leftClickHeld(x, y);
    }

    public static void InventoryPageReleasePostfix(InventoryPage __instance, int x, int y)
    {
        _isHolding = false;
        Monitor?.Log($"InventoryPageRelease ({x},{y}) hidden={HiddenChildMenu != null}", LogLevel.Info);
        if (HiddenChildMenu != null)
            HiddenChildMenu.releaseLeftClick(x, y);
    }

    public static void InventoryPageUpdatePostfix(InventoryPage __instance, Microsoft.Xna.Framework.GameTime time)
    {
        HiddenChildMenu?.update(time);
    }

    // เก็บ coordinate จาก GameMenu.leftClickHeld
    public static bool GameMenuLeftClickHeldPrefix(GameMenu __instance, int x, int y)
    {
        _lastHeldX = x;
        _lastHeldY = y;
        _isHolding = true;
        Monitor?.Log($"GameMenuLeftClickHeld ({x},{y})", LogLevel.Info);
        return true;
    }

    public static void GameMenuReleasePostfix(GameMenu __instance, int x, int y)
    {
        _isHolding = false;
        Monitor?.Log($"GameMenuRelease ({x},{y})", LogLevel.Info);
    }

    // backup: ส่ง coordinate ล่าสุดให้ HiddenChildMenu ทุก frame
    public static void UpdateActiveMenuPostfix()
    {
        var menu = Game1.activeClickableMenu;
        if (menu == null) return;

        _updateLogThrottle++;
        if (_updateLogThrottle >= 60)
        {
            _updateLogThrottle = 0;
            var chain = menu.GetType().Name;
            var c = menu;
            while (c?.GetChildMenu() != null)
            {
                c = c.GetChildMenu();
                chain += $" → {c.GetType().Name}";
            }
            Monitor?.Log($"UpdateActiveMenu chain: {chain} | isHolding={_isHolding} | hidden={HiddenChildMenu != null}", LogLevel.Info);
        }

        if (HiddenChildMenu != null && _isHolding)
        {
            Monitor?.Log($"UpdateActiveMenu: backup leftClickHeld ({_lastHeldX},{_lastHeldY})", LogLevel.Info);
            HiddenChildMenu.leftClickHeld(_lastHeldX, _lastHeldY);
        }
    }
}
