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
            Monitor.Log("DayStarted: calling RevalidateHealth", LogLevel.Info);
            LevelUpMenu.RevalidateHealth(Game1.player);
        };

        // เรียก RevalidateHealth ทันทีที่ใส่/ถอดแหวน
        helper.Events.Player.InventoryChanged += (s, e) =>
        {
            if (!Context.IsWorldReady) return;
            Monitor.Log("InventoryChanged: calling RevalidateHealth", LogLevel.Info);
            LevelUpMenu.RevalidateHealth(Game1.player);
        };
    }
}
