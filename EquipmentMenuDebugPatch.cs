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

        var spaceCoreDrawPostfix = AccessTools.TypeByName("SpaceCore.InventoryPageDrawTooltipPatch")
            ?.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
        if (spaceCoreDrawPostfix != null)
        {
            harmony.Patch(spaceCoreDrawPostfix,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(BlockDrawPrefix))));
            Monitor?.Log("patched SpaceCore.InventoryPageDrawTooltipPatch.Postfix", LogLevel.Info);
        }

        // block SpaceCore click เสมอ ป้องกัน EquipmentMenu crash
        // แต่ถ้ากดปุ่มใหม่ (_btnBounds) → เปิด SnsEquipmentMenu แทน
        var spaceCoreClickPrefix = AccessTools.TypeByName("SpaceCore.InventoryPageLeftClickPatch")
            ?.GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
        if (spaceCoreClickPrefix != null)
        {
            harmony.Patch(spaceCoreClickPrefix,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch).GetMethod(nameof(SpaceCoreClickPrefix))));
            Monitor?.Log("patched SpaceCore.InventoryPageLeftClickPatch.Prefix", LogLevel.Info);
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

        Monitor?.Log("EquipmentMenuDebugPatch applied!", LogLevel.Info);
    }

    public static bool BlockDrawPrefix() => false;

    // block SpaceCore เสมอ ไม่ให้เปิด EquipmentMenu ที่ crash
    // ถ้ากดตรง _btnBounds (ปุ่มใหม่ที่เราวาด) → เปิด SnsEquipmentMenu แทน
    // ไม่ใช้ reflection เลย ไม่มี exception
    public static bool SpaceCoreClickPrefix(InventoryPage __instance, int x, int y, ref bool __result)
    {
        if (_btnBounds != Rectangle.Empty && _btnBounds.Contains(x, y))
        {
            Monitor?.Log($"SpaceCoreClickPrefix: Hit! Opening SnsEquipmentMenu", LogLevel.Info);
            try
            {
                var menu = Game1.activeClickableMenu;
                var cur = menu;
                while (cur?.GetChildMenu() != null)
                    cur = cur.GetChildMenu();
                cur?.SetChildMenu(new SnsEquipmentMenu());
                Monitor?.Log("SnsEquipmentMenu opened!", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"SpaceCoreClickPrefix CRASH: {ex.Message}", LogLevel.Error);
            }
        }
        else
        {
            Monitor?.Log($"SpaceCoreClickPrefix: blocked SpaceCore at ({x},{y})", LogLevel.Info);
        }
        __result = false;
        return false;
    }

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
}

