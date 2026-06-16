using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using System.Reflection;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class ModEntry : Mod
{
    private ModConfig _config = new();

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();

        ArsenalMenuPatch.Monitor = Monitor;
        RevalidateHealthPatch.Monitor = Monitor;
        SkillsPagePatch.Monitor = Monitor;
        FancyAlchemyMenuPatch.Monitor = Monitor;
        ShieldSigilMenuPatch.Monitor = Monitor;
        BuffedSkillLevelPatch.Monitor = Monitor;
        EquipmentMenuDebugPatch.Monitor = Monitor;
        SnsEquipmentMenu.Monitor = Monitor;
        WeaponTooltipPatch.Monitor = Monitor;
        AdventureBarPatch.Monitor = Monitor;

        var harmony = new Harmony(ModManifest.UniqueID);
        LevelUpMenuTranspilerFix.Apply(harmony);
        harmony.PatchAll();
        GuidebookMenuPatch.Apply(harmony);
        FancyAlchemyMenuPatch.Apply(harmony);
        ShieldSigilMenuPatch.Apply(harmony);
        SkillsPagePatch.Apply(helper, Monitor, harmony);
        BuffedSkillLevelPatch.Apply(harmony);
        EquipmentMenuDebugPatch.Apply(harmony);
        WeaponTooltipPatch.Apply(harmony);
        AdventureBarPatch.Apply(harmony);

        object? rogueSkill = null;
        object? paladinSkill = null;
        MethodInfo? getBuffedLevel = null;

        helper.Events.GameLoop.GameLaunched += (s, e) =>
        {
            var skillType = AccessTools.TypeByName("SpaceCore.Skills+Skill");
            getBuffedLevel = AccessTools.Method(
                AccessTools.TypeByName("SpaceCore.SkillExtensions"),
                "GetCustomBuffedSkillLevel",
                new[] { typeof(Farmer), skillType });
            rogueSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
                ?.GetProperty("RogueSkill", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            paladinSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModTOP")
                ?.GetProperty("PaladinSkill", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            RevalidateHealthPatch.InitCache();
            SnsEquipmentMenu.InitSlotIds();
        };

        helper.Events.GameLoop.SaveLoaded += (s, e) =>
        {
            RevalidateHealthPatch.Reset();
        };

        helper.Events.GameLoop.DayStarted += (s, e) =>
        {
            RevalidateHealthPatch.InitFromBaseLevel(Game1.player);
            LevelUpMenu.RevalidateHealth(Game1.player);
        };

        int lastRogueBuffed = 0;
        int lastPaladinBuffed = 0;

        helper.Events.GameLoop.UpdateTicked += (s, e) =>
        {
            if (!Context.IsWorldReady || getBuffedLevel == null) return;

            int rogueBuffed = rogueSkill != null
                ? (int)(getBuffedLevel.Invoke(null, new object[] { Game1.player, rogueSkill }) ?? 0)
                : 0;
            int paladinBuffed = paladinSkill != null
                ? (int)(getBuffedLevel.Invoke(null, new object[] { Game1.player, paladinSkill }) ?? 0)
                : 0;

            if (rogueBuffed != lastRogueBuffed || paladinBuffed != lastPaladinBuffed)
            {
                lastRogueBuffed = rogueBuffed;
                lastPaladinBuffed = paladinBuffed;
                LevelUpMenu.RevalidateHealth(Game1.player);
            }
        };

        helper.Events.Input.ButtonPressed += (s, e) =>
        {
            if (!Context.IsWorldReady) return;
            if (e.Button == _config.ToggleAdventureBar)
            {
                AdventureBarPatch.AetherOnly = !AdventureBarPatch.AetherOnly;
                Game1.playSound("smallSelect");
            }
        };
    }
}
