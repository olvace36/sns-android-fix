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
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(PopulatePostfix))));
            Monitor?.Log("patched populateClickableComponentList", LogLevel.Info);
        }

        var getComp = typeof(IClickableMenu).GetMethod("getComponentWithID",
            BindingFlags.Public | BindingFlags.Instance);
        if (getComp != null)
        {
            harmony.Patch(getComp,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(GetComponentWithIDPrefix))));
            Monitor?.Log("patched getComponentWithID", LogLevel.Info);
        }

        var draw = typeof(InventoryPage).GetMethod("draw",
            new[] { typeof(SpriteBatch) });
        if (draw != null)
        {
            harmony.Patch(draw,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(DrawPostfix))));
            Monitor?.Log("patched InventoryPage.draw", LogLevel.Info);
        }

        var receiveLeftClick = typeof(InventoryPage).GetMethod("receiveLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (receiveLeftClick != null)
        {
            harmony.Patch(receiveLeftClick,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(ReceiveLeftClickPostfix))));
            Monitor?.Log("patched InventoryPage.receiveLeftClick", LogLevel.Info);
        }

        var releaseLeftClick = typeof(InventoryPage).GetMethod("releaseLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (releaseLeftClick != null)
        {
            harmony.Patch(releaseLeftClick,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(ReleaseLeftClickPostfix))));
            Monitor?.Log("patched InventoryPage.releaseLeftClick", LogLevel.Info);
        }

        var gameMenuReceive = typeof(GameMenu).GetMethod("receiveLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (gameMenuReceive != null)
        {
            harmony.Patch(gameMenuReceive,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(GameMenuReceivePostfix))));
            Monitor?.Log("patched GameMenu.receiveLeftClick", LogLevel.Info);
        }

        var gameMenuRelease = typeof(GameMenu).GetMethod("releaseLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (gameMenuRelease != null)
        {
            harmony.Patch(gameMenuRelease,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(GameMenuReleasePostfix))));
            Monitor?.Log("patched GameMenu.releaseLeftClick", LogLevel.Info);
        }

        Monitor?.Log("EquipmentMenuDebugPatch applied!", LogLevel.Info);
    }

    public static void PopulatePostfix(IClickableMenu __instance)
    {
        if (__instance is not InventoryPage page) return;

        // ลบ ID 1348000 ออกจาก allClickableComponents
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

        // fix leftNeighborID
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

        // ย้ายปุ่มไปกลางจอ เพื่อทดสอบ
        _btnBounds = new Rectangle(
            Game1.uiViewport.Width / 2 - 32,
            Game1.uiViewport.Height / 2 - 32,
            64, 64);
        Monitor?.Log($"btnBounds={_btnBounds}", LogLevel.Info);
    }

    public static bool GetComponentWithIDPrefix(int id, ref ClickableComponent __result)
    {
        if (id == 1348000)
        {
            __result = null;
            return false;
        }
        return true;
    }

    public static void DrawPostfix(InventoryPage __instance, SpriteBatch b)
    {
        if (_btnBounds == Rectangle.Empty) return;
        try
        {
            // วาดปุ่มที่กลางจอ
            var tex = Game1.content.Load<Texture2D>("spacechase0.SpaceCore/ExtraEquipmentIcon");
            b.Draw(tex, new Vector2(_btnBounds.X, _btnBounds.Y),
                new Rectangle(0, 0, 16, 16), Color.White,
                0f, Vector2.Zero, 4f, SpriteEffects.None, 0.87f);
        }
        catch { }
    }

    static void TryOpenEquipmentMenu(int x, int y, string source)
    {
        if (_btnBounds == Rectangle.Empty) return;
        Monitor?.Log($"[{source}] click=({x},{y}) btnBounds={_btnBounds} contains={_btnBounds.Contains(x, y)}", LogLevel.Info);
        if (!_btnBounds.Contains(x, y)) return;

        Monitor?.Log($"[{source}] Hit! Opening SnsEquipmentMenu...", LogLevel.Info);
        try
        {
            var menu = new SnsEquipmentMenu();
            Game1.activeClickableMenu.SetChildMenu(menu);
            Monitor?.Log($"[{source}] SnsEquipmentMenu opened!", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor?.Log($"[{source}] CRASH: {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
            if (ex.InnerException != null)
                Monitor?.Log($"[{source}] Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}", LogLevel.Error);
        }
    }

    public static void ReceiveLeftClickPostfix(InventoryPage __instance, int x, int y)
    {
        Monitor?.Log($"InventoryPage.receiveLeftClick ({x},{y})", LogLevel.Info);
        TryOpenEquipmentMenu(x, y, "inv-receive");
    }

    public static void ReleaseLeftClickPostfix(InventoryPage __instance, int x, int y)
    {
        Monitor?.Log($"InventoryPage.releaseLeftClick ({x},{y})", LogLevel.Info);
        TryOpenEquipmentMenu(x, y, "inv-release");
    }

    public static void GameMenuReceivePostfix(GameMenu __instance, int x, int y)
    {
        Monitor?.Log($"GameMenu.receiveLeftClick ({x},{y})", LogLevel.Info);
        TryOpenEquipmentMenu(x, y, "gm-receive");
    }

    public static void GameMenuReleasePostfix(GameMenu __instance, int x, int y)
    {
        Monitor?.Log($"GameMenu.releaseLeftClick ({x},{y})", LogLevel.Info);
        TryOpenEquipmentMenu(x, y, "gm-release");
    }
}

