using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using System.Reflection;
using StardewValley;
using StardewValley.Menus;
using SpaceCore;
using SwordAndSorcerySMAPI;

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

        string? rogueSkillId = null;
        string? paladinSkillId = null;

        helper.Events.GameLoop.GameLaunched += (s, e) =>
        {
            // Resolved via direct references now (SpaceCore.dll and SwordAndSorcerySMAPI.dll
            // are already project references) instead of reflection — GetCustomBuffedSkillLevel
            // has a string-id overload (SpaceCore.SkillExtensions), so we only need each
            // skill's Id once here, not the MethodInfo/Type objects from before.
            rogueSkillId = ModSnS.RogueSkill?.Id;
            paladinSkillId = ModTOP.PaladinSkill?.Id;
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

        // Checking every single tick (~60/sec) is overkill for a buff bar that only needs
        // to look responsive — every 4th tick (~15/sec) is still imperceptible and cuts the
        // work by 75%.
        const int checkEveryNTicks = 4;
        uint tickCounter = 0;

        helper.Events.GameLoop.UpdateTicked += (s, e) =>
        {
            if (!Context.IsWorldReady) return;

            tickCounter++;
            if (tickCounter % checkEveryNTicks != 0) return;

            // Direct compiled calls now — no reflection, no per-call array allocation.
            int rogueBuffed = rogueSkillId != null
                ? Game1.player.GetCustomBuffedSkillLevel(rogueSkillId)
                : 0;
            int paladinBuffed = paladinSkillId != null
                ? Game1.player.GetCustomBuffedSkillLevel(paladinSkillId)
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

