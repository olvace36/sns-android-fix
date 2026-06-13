using System;
using System.IO;
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
    private static readonly string LogPath = "/storage/emulated/0/Android/data/abc.smapi.gameloader/files/sns_debug.txt";
    private static Rectangle _btnBounds = Rectangle.Empty;

    static void FileLog(string msg)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}: {msg}\n"); }
        catch { }
    }

    public static void Apply(Harmony harmony)
    {
        // 1. ลบปุ่ม + ออกจาก allClickableComponents + fix leftNeighborID
        var populate = typeof(IClickableMenu).GetMethod("populateClickableComponentList",
            BindingFlags.Public | BindingFlags.Instance);
        if (populate != null)
        {
            harmony.Patch(populate,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(PopulatePostfix))));
            Monitor?.Log("patched populateClickableComponentList", LogLevel.Info);
        }

        // 2. patch getComponentWithID ให้ return null เมื่อ ID = 1348000
        var getComp = typeof(IClickableMenu).GetMethod("getComponentWithID",
            BindingFlags.Public | BindingFlags.Instance);
        if (getComp != null)
        {
            harmony.Patch(getComp,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(GetComponentWithIDPrefix))));
            Monitor?.Log("patched getComponentWithID", LogLevel.Info);
        }

        // 3. วาดปุ่ม + เองใน InventoryPage.draw
        var draw = typeof(InventoryPage).GetMethod("draw",
            new[] { typeof(SpriteBatch) });
        if (draw != null)
        {
            harmony.Patch(draw,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(DrawPostfix))));
            Monitor?.Log("patched InventoryPage.draw", LogLevel.Info);
        }

        // 4. handle click เอง
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

        // fix leftNeighborID ที่ชี้ไป 1348000
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

        // คำนวณ bounds ปุ่มที่เราวาดเอง
        _btnBounds = new Rectangle(
            page.xPositionOnScreen - 80,
            page.yPositionOnScreen + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 4 + 384 - 12,
            64, 64);
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
        if (!_btnBounds.Contains(x, y)) return;

        FileLog($"[{source}] Hit! ({x},{y})");
        try
        {
            FileLog($"[{source}] Opening SnsEquipmentMenu...");
            var menu = new SnsEquipmentMenu();
            Game1.activeClickableMenu.SetChildMenu(menu);
            FileLog($"[{source}] SnsEquipmentMenu opened!");
        }
        catch (Exception ex)
        {
            FileLog($"[{source}] CRASH: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                FileLog($"[{source}] Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            FileLog($"[{source}] Stack: {ex.StackTrace}");
        }
    }

    public static void ReceiveLeftClickPostfix(InventoryPage __instance, int x, int y)
    {
        FileLog($"receiveLeftClick ({x},{y})");
        TryOpenEquipmentMenu(x, y, "receive");
    }

    public static void ReleaseLeftClickPostfix(InventoryPage __instance, int x, int y)
    {
        FileLog($"releaseLeftClick ({x},{y})");
        TryOpenEquipmentMenu(x, y, "release");
    }
}
