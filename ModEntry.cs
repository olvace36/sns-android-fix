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
        BuffedSkillLevelPatch.Monitor = Monitor;
        EquipmentMenuDebugPatch.Monitor = Monitor;
        SnsEquipmentMenu.Monitor = Monitor;

        var harmony = new Harmony(ModManifest.UniqueID);
        LevelUpMenuTranspilerFix.Apply(harmony);
        harmony.PatchAll();
        GuidebookMenuPatch.Apply(harmony);
        FancyAlchemyMenuPatch.Apply(harmony);
        ShieldSigilMenuPatch.Apply(harmony);
        SkillsPagePatch.Apply(helper, Monitor, harmony);
        BuffedSkillLevelPatch.Apply(harmony);
        EquipmentMenuDebugPatch.Apply(harmony);

        // cache ไว้ใช้ใน UpdateTicked — lookup ครั้งเดียว
        var skillType = AccessTools.TypeByName("SpaceCore.Skills+Skill");
        var getBuffedLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomBuffedSkillLevel",
            new[] { typeof(Farmer), skillType });

        object? rogueSkill = null;
        object? paladinSkill = null;

        helper.Events.GameLoop.GameLaunched += (s, e) =>
        {
            rogueSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
                ?.GetProperty("RogueSkill", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            paladinSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModTOP")
                ?.GetProperty("PaladinSkill", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            SnsEquipmentMenu.InitSlotIds();
            Monitor.Log($"GameLaunched: rogueSkill={rogueSkill?.GetType().Name ?? "null"}, paladinSkill={paladinSkill?.GetType().Name ?? "null"}", LogLevel.Info);
        };

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

        int lastRogueBuffed = 0;
        int lastPaladinBuffed = 0;

        helper.Events.GameLoop.UpdateTicked += (s, e) =>
        {
            if (!Context.IsWorldReady) return;

            int rogueBuffed = rogueSkill != null
                ? (int)(getBuffedLevel?.Invoke(null, new object[] { Game1.player, rogueSkill }) ?? 0)
                : 0;
            int paladinBuffed = paladinSkill != null
                ? (int)(getBuffedLevel?.Invoke(null, new object[] { Game1.player, paladinSkill }) ?? 0)
                : 0;

            if (rogueBuffed != lastRogueBuffed || paladinBuffed != lastPaladinBuffed)
            {
                lastRogueBuffed = rogueBuffed;
                lastPaladinBuffed = paladinBuffed;
                Monitor.Log($"UpdateTicked: buffChanged=True, Rogue={rogueBuffed}, Paladin={paladinBuffed}", LogLevel.Info);
                LevelUpMenu.RevalidateHealth(Game1.player);
            }
        };
    }
}
