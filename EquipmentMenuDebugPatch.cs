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
    private static readonly string LogPath = "/storage/emulated/0/sns_equipment_debug.txt";
    private static Rectangle _btnBounds = Rectangle.Empty;

    static void FileLog(string msg)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}: {msg}\n"); }
        catch { }
    }

    public static void Apply(Harmony harmony)
    {
        // 1. ลบปุ่ม + ออกจาก allClickableComponents ป้องกัน native crash
        var populate = typeof(IClickableMenu).GetMethod("populateClickableComponentList",
            BindingFlags.Public | BindingFlags.Instance);
        if (populate != null)
        {
            harmony.Patch(populate,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(PopulatePostfix))));
            Monitor?.Log("patched populateClickableComponentList", LogLevel.Info);
        }

        // 2. วาดปุ่ม + เองใน InventoryPage.draw
        var draw = typeof(InventoryPage).GetMethod("draw",
            new[] { typeof(SpriteBatch) });
        if (draw != null)
        {
            harmony.Patch(draw,
                postfix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(DrawPostfix))));
            Monitor?.Log("patched InventoryPage.draw", LogLevel.Info);
        }

        // 3. handle click เอง
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

    // ลบปุ่ม ID 1348000 ออกจาก allClickableComponents ป้องกัน native crash
    public static void PopulatePostfix(IClickableMenu __instance)
    {
        if (__instance is not InventoryPage page) return;
        var all = __instance.allClickableComponents;
        if (all == null) return;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            if (all[i].myID == 1348000)
            {
                all.RemoveAt(i);
                Monitor?.Log("Removed ID 1348000 from allClickableComponents", LogLevel.Info);
                break;
            }
        }

        // คำนวณ bounds ของปุ่มที่เราจะวาดเอง
        _btnBounds = new Rectangle(
            page.xPositionOnScreen - 80,
            page.yPositionOnScreen + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 4 + 384 - 12,
            64, 64);
    }

    // วาดปุ่ม + เอง
    public static void DrawPostfix(InventoryPage __instance, SpriteBatch b)
    {
        if (_btnBounds == Rectangle.Empty) return;

        // โหลด texture เดิมของ SpaceCore
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

        FileLog($"[{source}] Hit! ({x},{y}) bounds={_btnBounds}");
        try
        {
            var equipmentMenuType = AccessTools.TypeByName("SpaceCore.EquipmentMenu");
            FileLog($"[{source}] type={equipmentMenuType?.FullName ?? "null"}");
            if (equipmentMenuType == null) return;

            FileLog($"[{source}] Calling constructor...");
            var menu = Activator.CreateInstance(equipmentMenuType);
            FileLog($"[{source}] Constructor OK!");

            var setChildMenu = typeof(IClickableMenu).GetMethod("SetChildMenu",
                BindingFlags.Public | BindingFlags.Instance);
            setChildMenu?.Invoke(Game1.activeClickableMenu, new object[] { menu });
            FileLog($"[{source}] SetChildMenu OK!");
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

