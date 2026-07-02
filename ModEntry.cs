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
        WeaponTooltipExtraSpacePatch.Monitor = Monitor;
        AdventureBarPatch.Monitor = Monitor;
        MonsterDamagePatch.Monitor = Monitor;

        var harmony = new Harmony(ModManifest.UniqueID);
        LevelUpMenuTranspilerFix.Apply(harmony);
        harmony.PatchAll();
        GuidebookMenuPatch.Apply(harmony);
        FancyAlchemyMenuPatch.Apply(harmony);
        ShieldSigilMenuPatch.Apply(harmony);
        SkillsPagePatch.Apply(helper, Monitor, harmony);
        BuffedSkillLevelPatch.Apply(harmony);
        EquipmentMenuDebugPatch.Apply(harmony);
        WeaponTooltipPatch.Apply(harmony, helper.Translation);
        AdventureBarPatch.Apply(harmony);
        MonsterDamagePatch.Apply(harmony);
        // WeaponTooltipExtraSpacePatch.Apply ย้ายไป GameLaunched

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

            // patch หลัง SMAPI rewrite เสร็จ
            WeaponTooltipExtraSpacePatch.Apply(harmony);
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

        // Reused across calls instead of allocating a new object[2] every check.
        object[]? rogueArgs = null;
        object[]? paladinArgs = null;

        // OneSecondUpdateTicked is a SMAPI event that fires once per real second instead of
        // every tick (~60/sec). A buffed-skill display doesn't need faster than that — buffs
        // come from potions/abilities the player just used, so a ~1s delay before the HP bar
        // catches up is unnoticeable. This is a much bigger cut than polling every few ticks.
        helper.Events.GameLoop.OneSecondUpdateTicked += (s, e) =>
        {
            if (!Context.IsWorldReady || getBuffedLevel == null) return;

            if (rogueSkill != null)
                rogueArgs ??= new object[] { Game1.player, rogueSkill };
            if (paladinSkill != null)
                paladinArgs ??= new object[] { Game1.player, paladinSkill };

            int rogueBuffed = rogueArgs != null
                ? (int)(getBuffedLevel.Invoke(null, rogueArgs) ?? 0)
                : 0;
            int paladinBuffed = paladinArgs != null
                ? (int)(getBuffedLevel.Invoke(null, paladinArgs) ?? 0)
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

