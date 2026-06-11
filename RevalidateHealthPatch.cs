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

        // Paladin skill +5 health per level
        var paladinSkill = AccessTools.Field(
            AccessTools.TypeByName("SwordAndSorcerySMAPI.ModTOP"),
            "PaladinSkill")?.GetValue(null);
        if (paladinSkill != null)
        {
            var getLevel = AccessTools.Method(
                AccessTools.TypeByName("SpaceCore.SkillExtensions"),
                "GetCustomSkillLevel");
            int level = (int)(getLevel?.Invoke(null, new object[] { farmer, paladinSkill }) ?? 0);
            bonus += level * 5;
            Monitor?.Log($"Paladin level={level}, bonus={level * 5}", LogLevel.Info);
        }

        // Rogue skill +3 health per level
        var rogueSkill = AccessTools.Field(
            AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS"),
            "RogueSkill")?.GetValue(null);
        if (rogueSkill != null)
        {
            var getLevel = AccessTools.Method(
                AccessTools.TypeByName("SpaceCore.SkillExtensions"),
                "GetCustomSkillLevel");
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
