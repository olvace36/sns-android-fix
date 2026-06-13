using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class EquipmentMenuDebugPatch
{
    internal static IMonitor? Monitor;

    public static void Apply(Harmony harmony)
    {
        var inventoryPageType = typeof(InventoryPage);
        var receiveLeftClick = inventoryPageType.GetMethod("receiveLeftClick",
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
            // ดูว่ากดโดน equipment button ไหม
            var compsField = AccessTools.TypeByName("SpaceCore.InventoryPageConstructorPatch")
                ?.GetField("comps", BindingFlags.Public | BindingFlags.Static);
            var comps = compsField?.GetValue(null);

            var getOrCreateValue = comps?.GetType()
                .GetMethod("GetOrCreateValue");
            var holder = getOrCreateValue?.Invoke(comps, new object[] { __instance });
            var btn = holder?.GetType()
                .GetField("Value", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(holder) as ClickableTextureComponent;

            if (btn == null)
            {
                Monitor?.Log("Equipment button not found (btn=null)", LogLevel.Warn);
                return true;
            }

            if (!btn.bounds.Contains(x, y))
                return true;

            // กดโดน — ลอง new EquipmentMenu() แบบ safe
            Monitor?.Log("Equipment button clicked! Trying to open EquipmentMenu...", LogLevel.Info);

            try
            {
                var equipmentMenuType = AccessTools.TypeByName("SpaceCore.EquipmentMenu");
                if (equipmentMenuType == null)
                {
                    Monitor?.Log("EquipmentMenu type not found!", LogLevel.Error);
                    return false;
                }

                Monitor?.Log("EquipmentMenu type found, calling constructor...", LogLevel.Info);
                var menu = Activator.CreateInstance(equipmentMenuType);
                Monitor?.Log("EquipmentMenu created successfully!", LogLevel.Info);

                var setChildMenu = typeof(IClickableMenu).GetMethod("SetChildMenu",
                    BindingFlags.Public | BindingFlags.Instance);
                setChildMenu?.Invoke(Game1.activeClickableMenu, new object[] { menu });
                Monitor?.Log("SetChildMenu called!", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"EquipmentMenu crash: {ex}", LogLevel.Error);
                if (ex.InnerException != null)
                    Monitor?.Log($"InnerException: {ex.InnerException}", LogLevel.Error);
            }

            return false;
        }
        catch (Exception ex)
        {
            Monitor?.Log($"EquipmentMenuDebugPatch error: {ex}", LogLevel.Error);
            return true;
        }
    }
}
