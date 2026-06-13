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

        int _lastRogueBuffed = 0;
        int _lastPaladinBuffed = 0;

        helper.Events.GameLoop.UpdateTicked += (s, e) =>
        {
            if (!Context.IsWorldReady) return;

            var skillType = AccessTools.TypeByName("SpaceCore.Skills+Skill");
            var getBuffedLevel = AccessTools.Method(
                AccessTools.TypeByName("SpaceCore.SkillExtensions"),
                "GetCustomBuffedSkillLevel",
                new[] { typeof(Farmer), skillType });
            var rogueSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
                ?.GetProperty("RogueSkill", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            var paladinSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModTOP")
                ?.GetProperty("PaladinSkill", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);

            int rogueBuffed = rogueSkill != null
                ? (int)(getBuffedLevel?.Invoke(null, new object[] { Game1.player, rogueSkill }) ?? 0)
                : 0;
            int paladinBuffed = paladinSkill != null
                ? (int)(getBuffedLevel?.Invoke(null, new object[] { Game1.player, paladinSkill }) ?? 0)
                : 0;

            bool buffChanged = rogueBuffed != _lastRogueBuffed || paladinBuffed != _lastPaladinBuffed;

            if (_pendingRevalidate || buffChanged)
            {
                _pendingRevalidate = false;
                _lastRogueBuffed = rogueBuffed;
                _lastPaladinBuffed = paladinBuffed;
                Monitor.Log($"UpdateTicked: buffChanged={buffChanged}, Rogue={rogueBuffed}, Paladin={paladinBuffed}", LogLevel.Info);
                LevelUpMenu.RevalidateHealth(Game1.player);
            }
        };
    }
}
