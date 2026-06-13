using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using System.Reflection;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        ArsenalMenuPatch.Monitor = Monitor;
        RevalidateHealthPatch.Monitor = Monitor;
        SkillsPagePatch.Monitor = Monitor;
        FancyAlchemyMenuPatch.Monitor = Monitor;
        ShieldSigilMenuPatch.Monitor = Monitor;
        var harmony = new Harmony(ModManifest.UniqueID);
        LevelUpMenuTranspilerFix.Apply(harmony);
        harmony.PatchAll();
        GuidebookMenuPatch.Apply(harmony);
        FancyAlchemyMenuPatch.Apply(harmony);
        ShieldSigilMenuPatch.Apply(harmony);
        SkillsPagePatch.Apply(helper, Monitor, harmony);

        helper.Events.GameLoop.SaveLoaded += (s, e) =>
        {
            RevalidateHealthPatch.Reset();
        };

        helper.Events.GameLoop.DayStarted += (s, e) =>
        {
            Monitor.Log("DayStarted: init from base level then RevalidateHealth", LogLevel.Info);
            RevalidateHealthPatch.InitFromBaseLevel(Game1.player);
            LevelUpMenu.RevalidateHealth(Game1.player);
        };

        bool _pendingRevalidate = false;
        helper.Events.Player.InventoryChanged += (s, e) =>
        {
            if (!Context.IsWorldReady) return;
            _pendingRevalidate = true;
        };

        helper.Events.GameLoop.UpdateTicked += (s, e) =>
        {
            if (!_pendingRevalidate || !Context.IsWorldReady) return;
            _pendingRevalidate = false;
            Monitor.Log("UpdateTicked: calling RevalidateHealth after inventory change", LogLevel.Info);
            LevelUpMenu.RevalidateHealth(Game1.player);
        };
    }
}
