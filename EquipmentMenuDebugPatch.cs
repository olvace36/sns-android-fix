using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class EquipmentMenuDebugPatch
{
    internal static IMonitor? Monitor;
    private static readonly string LogPath = "/storage/emulated/0/sns_equipment_debug.txt";

    static void FileLog(string msg)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}: {msg}\n"); }
        catch { }
    }

    public static void Apply(Harmony harmony)
    {
        var receiveLeftClick = typeof(InventoryPage).GetMethod("receiveLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (receiveLeftClick != null)
        {
            harmony.Patch(receiveLeftClick,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(ReceiveLeftClickPrefix))));
            Monitor?.Log("EquipmentMenuDebugPatch: patched receiveLeftClick", LogLevel.Info);
        }

        var releaseLeftClick = typeof(InventoryPage).GetMethod("releaseLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (releaseLeftClick != null)
        {
            harmony.Patch(releaseLeftClick,
                prefix: new HarmonyMethod(typeof(EquipmentMenuDebugPatch)
                    .GetMethod(nameof(ReleaseLeftClickPrefix))));
            Monitor?.Log("EquipmentMenuDebugPatch: patched releaseLeftClick", LogLevel.Info);
        }

        Monitor?.Log("EquipmentMenuDebugPatch applied!", LogLevel.Info);
    }

    static bool TryOpenEquipmentMenu(InventoryPage instance, int x, int y, string source)
    {
        try
        {
            var compsField = AccessTools.TypeByName("SpaceCore.InventoryPageConstructorPatch")
                ?.GetField("comps", BindingFlags.Public | BindingFlags.Static);
            var comps = compsField?.GetValue(null);
            var getOrCreateValue = comps?.GetType().GetMethod("GetOrCreateValue");
            var holder = getOrCreateValue?.Invoke(comps, new object[] { instance });
            var btn = holder?.GetType()
                .GetField("Value", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(holder) as ClickableTextureComponent;

            if (btn == null)
            {
                FileLog($"[{source}] btn=null");
                return false;
            }

            FileLog($"[{source}] btn bounds={btn.bounds}, click=({x},{y}), contains={btn.bounds.Contains(x, y)}");

            if (!btn.bounds.Contains(x, y)) return false;

            FileLog($"[{source}] Equipment button clicked! Opening menu...");

            try
            {
                var equipmentMenuType = AccessTools.TypeByName("SpaceCore.EquipmentMenu");
                FileLog($"[{source}] EquipmentMenu type={equipmentMenuType?.FullName ?? "null"}");
                if (equipmentMenuType == null) return true;

                FileLog($"[{source}] Calling constructor...");
                var menu = Activator.CreateInstance(equipmentMenuType);
                FileLog($"[{source}] Constructor done!");

                var setChildMenu = typeof(IClickableMenu).GetMethod("SetChildMenu",
                    BindingFlags.Public | BindingFlags.Instance);
                setChildMenu?.Invoke(Game1.activeClickableMenu, new object[] { menu });
                FileLog($"[{source}] SetChildMenu done!");
            }
            catch (Exception ex)
            {
                FileLog($"[{source}] CRASH: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    FileLog($"[{source}] Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                FileLog($"[{source}] Stack: {ex.StackTrace}");
            }

            return true;
        }
        catch (Exception ex)
        {
            FileLog($"[{source}] Prefix error: {ex.Message}");
            return false;
        }
    }

    public static bool ReceiveLeftClickPrefix(InventoryPage __instance, int x, int y)
    {
        FileLog($"receiveLeftClick ({x},{y})");
        if (TryOpenEquipmentMenu(__instance, x, y, "receive"))
            return false;
        return true;
    }

    public static bool ReleaseLeftClickPrefix(InventoryPage __instance, int x, int y)
    {
        FileLog($"releaseLeftClick ({x},{y})");
        if (TryOpenEquipmentMenu(__instance, x, y, "release"))
            return false;
        return true;
    }
}
