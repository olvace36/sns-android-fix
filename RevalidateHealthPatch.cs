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

    static void Postfix(Farmer farmer)
    {
        int bonus = 0;

        var skillType = AccessTools.TypeByName("SpaceCore.Skills+Skill");
        var getLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomSkillLevel",
            new[] { typeof(Farmer), skillType });

        // Paladin skill +5 health per level
        var paladinSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModTOP")
            ?.GetProperty("PaladinSkill", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
        Monitor?.Log($"paladinSkill={paladinSkill?.GetType().Name ?? "null"}", LogLevel.Info);
        if (paladinSkill != null)
        {
            int level = (int)(getLevel?.Invoke(null, new object[] { farmer, paladinSkill }) ?? 0);
            bonus += level * 5;
            Monitor?.Log($"Paladin level={level}, bonus={level * 5}", LogLevel.Info);
        }

        // Rogue skill +3 health per level
        var rogueSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
            ?.GetProperty("RogueSkill", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
        Monitor?.Log($"rogueSkill={rogueSkill?.GetType().Name ?? "null"}", LogLevel.Info);
        if (rogueSkill != null)
        {
            int level = (int)(getLevel?.Invoke(null, new object[] { farmer, rogueSkill }) ?? 0);
            bonus += level * 3;
            Monitor?.Log($"Rogue level={level}, bonus={level * 3}", LogLevel.Info);
        }

        Monitor?.Log($"RevalidateHealth: farmer={farmer.Name}, maxHealth before={farmer.maxHealth}, bonus={bonus}", LogLevel.Info);

        if (bonus > 0)
        {
            farmer.maxHealth += bonus;
            farmer.health = Math.Min(farmer.health + bonus, farmer.maxHealth);
            Monitor?.Log($"maxHealth after={farmer.maxHealth}", LogLevel.Info);
        }
    }
}
