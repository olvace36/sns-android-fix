using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace SnsAndroidFix;

public class SkillRingsPatch
{
    internal static IMonitor? Monitor;
    private static object? _skillRingsInstance;
    private static MethodInfo? _onUpdateTicked;

    public static void Apply(IModHelper helper, Harmony harmony)
    {
        helper.Events.GameLoop.GameLaunched += (s, e) =>
        {
            var skillRingsType = AccessTools.TypeByName("SkillRings.ModEntry");
            if (skillRingsType == null)
            {
                Monitor?.Log("SkillRings not found", LogLevel.Warn);
                return;
            }

            foreach (var mod in helper.ModRegistry.GetAll())
            {
                var modObj = mod.GetType()
                    .GetProperty("Mod", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(mod);
                if (modObj?.GetType().FullName == "SkillRings.ModEntry")
                {
                    _skillRingsInstance = modObj;
                    _onUpdateTicked = skillRingsType.GetMethod("onUpdateTicked",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    Monitor?.Log($"SkillRings instance found! onUpdateTicked={_onUpdateTicked != null}", LogLevel.Info);
                    break;
                }
            }
        };

        helper.Events.Player.InventoryChanged += (s, e) =>
        {
            if (!Context.IsWorldReady) return;
            if (_skillRingsInstance == null || _onUpdateTicked == null) return;

            Monitor?.Log("InventoryChanged: forcing SkillRings update", LogLevel.Info);
            try
            {
                _onUpdateTicked.Invoke(_skillRingsInstance, new object[] { null, null });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                Monitor?.Log($"SkillRings update failed: {inner.GetType().Name}: {inner.Message}", LogLevel.Warn);
            }
        };
    }
}
