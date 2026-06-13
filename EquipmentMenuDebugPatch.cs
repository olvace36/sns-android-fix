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
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}: {msg}\n");
        }
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
            Monitor?.Log("EquipmentMenuDebugPatch applied!", LogLevel.Info);
        }
        else
        {
            Monitor?.Log("ReceiveLeftClick not found!", LogLevel.Warn);
        }
    }

    public static bool ReceiveLeftClickPrefix(InventoryPage __instance, int x, int y)
    {
        try
        {
            var compsField = AccessTools.TypeByName("SpaceCore.InventoryPageConstructorPatch")
                ?.GetField("comps", BindingFlags.Public | BindingFlags.Static);
            var comps = compsField?.GetValue(null);
            var getOrCreateValue = comps?.GetType().GetMethod("GetOrCreateValue");
            var holder = getOrCreateValue?.Invoke(comps, new object[] { __instance });
            var btn = holder?.GetType()
                .GetField("Value", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(holder) as ClickableTextureComponent;

            if (btn == null || !btn.bounds.Contains(x, y))
                return true;

            FileLog("Equipment button clicked!");

            try
            {
                var equipmentMenuType = AccessTools.TypeByName("SpaceCore.EquipmentMenu");
                FileLog($"EquipmentMenu type = {equipmentMenuType?.FullName ?? "null"}");
                if (equipmentMenuType == null) return false;

                // ดู EquipmentSlots ก่อน
                var slotsField = AccessTools.TypeByName("SpaceCore.SpaceCore")
                    ?.GetField("EquipmentSlots", BindingFlags.Public | BindingFlags.Static);
                var slots = slotsField?.GetValue(null);
                FileLog($"EquipmentSlots = {slots?.GetType().Name ?? "null"}");

                // ดู GetExtData
                FileLog("Calling GetExtData...");
                var getExtData = AccessTools.Method(
                    AccessTools.TypeByName("SpaceCore.Extensions"),
                    "GetFarmerExtData",
                    new[] { typeof(Farmer) });
                FileLog($"GetFarmerExtData method = {getExtData?.Name ?? "null"}");

                var extData = getExtData?.Invoke(null, new object[] { Game1.player });
                FileLog($"extData = {extData?.GetType().Name ?? "null"}");

                // ดู ExtraEquippables
                var extraEquippables = extData?.GetType()
                    .GetField("ExtraEquippables", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(extData);
                FileLog($"ExtraEquippables = {extraEquippables?.GetType().Name ?? "null"}");

                FileLog("Calling EquipmentMenu constructor...");
                var menu = Activator.CreateInstance(equipmentMenuType);
                FileLog("EquipmentMenu created successfully!");

                var setChildMenu = typeof(IClickableMenu).GetMethod("SetChildMenu",
                    BindingFlags.Public | BindingFlags.Instance);
                setChildMenu?.Invoke(Game1.activeClickableMenu, new object[] { menu });
                FileLog("SetChildMenu called!");
            }
            catch (Exception ex)
            {
                FileLog($"CRASH: {ex.GetType().Name}: {ex.Message}");
                FileLog($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    FileLog($"InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                    FileLog($"InnerStackTrace: {ex.InnerException.StackTrace}");
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            FileLog($"Prefix error: {ex}");
            return true;
        }
    }
}
