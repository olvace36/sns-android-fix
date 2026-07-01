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

        // Reused across calls instead of allocating a new object[2] on every single tick.
        // The array contents (player, skill) don't change tick-to-tick, so we build each
        // one once as soon as its skill reference is resolved.
        object[]? rogueArgs = null;
        object[]? paladinArgs = null;

        // Checking every single tick (~60/sec) is overkill for a buff bar that only needs
        // to look responsive — every 4th tick (~15/sec) is still imperceptible and cuts the
        // reflection Invoke calls by 75%.
        const int checkEveryNTicks = 4;
        uint tickCounter = 0;

        helper.Events.GameLoop.UpdateTicked += (s, e) =>
        {
            if (!Context.IsWorldReady || getBuffedLevel == null) return;

            tickCounter++;
            if (tickCounter % checkEveryNTicks != 0) return;

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
