using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

[HarmonyPatch(typeof(LevelUpMenu), "RevalidateHealth")]
public class RevalidateHealthPatch
{
    internal static IMonitor? Monitor;

    static int GetBaseMaxHealth(Farmer farmer, int rogueBase, int paladinBase)
    {
        // vanilla base = 100 + combat level * 5
        int vanillaBase = 100 + farmer.CombatLevel * 5;
        // SNS base bonus
        int snsBonus = rogueBase * 3 + paladinBase * 5;
        return vanillaBase + snsBonus;
    }

    static void Postfix(Farmer farmer)
    {
        var skillType = AccessTools.TypeByName("SpaceCore.Skills+Skill");
        var getBuffedLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomBuffedSkillLevel",
            new[] { typeof(Farmer), skillType });
        var getBaseLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomSkillLevel",
            new[] { typeof(Farmer), skillType });

        var paladinSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModTOP")
            ?.GetProperty("PaladinSkill", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
        var rogueSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
            ?.GetProperty("RogueSkill", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);

        int paladinBase = paladinSkill != null
            ? (int)(getBaseLevel?.Invoke(null, new object[] { farmer, paladinSkill }) ?? 0) : 0;
        int rogueBase = rogueSkill != null
            ? (int)(getBaseLevel?.Invoke(null, new object[] { farmer, rogueSkill }) ?? 0) : 0;
        int paladinBuffed = paladinSkill != null
            ? (int)(getBuffedLevel?.Invoke(null, new object[] { farmer, paladinSkill }) ?? 0) : 0;
        int rogueBuffed = rogueSkill != null
            ? (int)(getBuffedLevel?.Invoke(null, new object[] { farmer, rogueSkill }) ?? 0) : 0;

        // buff only bonus (จากแหวน)
        int buffOnlyBonus = (rogueBuffed - rogueBase) * 3 + (paladinBuffed - paladinBase) * 5;

        int baseMaxHealth = GetBaseMaxHealth(farmer, rogueBase, paladinBase);
        int expectedMaxHealth = baseMaxHealth + buffOnlyBonus;

        Monitor?.Log($"Paladin base={paladinBase} buffed={paladinBuffed}, Rogue base={rogueBase} buffed={rogueBuffed}", LogLevel.Info);
        Monitor?.Log($"RevalidateHealth: vanillaBase={100 + farmer.CombatLevel * 5}, snsBase={rogueBase * 3 + paladinBase * 5}, buffOnly={buffOnlyBonus}, expected={expectedMaxHealth}, current={farmer.maxHealth}", LogLevel.Info);

        if (farmer.maxHealth != expectedMaxHealth)
        {
            int diff = expectedMaxHealth - farmer.maxHealth;
            farmer.maxHealth = expectedMaxHealth;
            farmer.health = Math.Min(farmer.health + diff, farmer.maxHealth);
            Monitor?.Log($"maxHealth set to={farmer.maxHealth}", LogLevel.Info);
        }
    }

    public static void Reset() { }
    public static void InitFromBaseLevel(Farmer farmer) { }
}
