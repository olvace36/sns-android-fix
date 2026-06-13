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
    private static int _lastBonus = 0;

    static void Postfix(Farmer farmer)
    {
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

        // Rogue skill +3 health per level
        var rogueSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
            ?.GetProperty("RogueSkill", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
        Monitor?.Log($"rogueSkill={rogueSkill?.GetType().Name ?? "null"}", LogLevel.Info);

        int paladinLevel = paladinSkill != null
            ? (int)(getLevel?.Invoke(null, new object[] { farmer, paladinSkill }) ?? 0)
            : 0;
        int rogueLevel = rogueSkill != null
            ? (int)(getLevel?.Invoke(null, new object[] { farmer, rogueSkill }) ?? 0)
            : 0;

        int newBonus = rogueLevel * 3 + paladinLevel * 5;
        int diff = newBonus - _lastBonus;

        Monitor?.Log($"Paladin level={paladinLevel}, Rogue level={rogueLevel}", LogLevel.Info);
        Monitor?.Log($"RevalidateHealth: farmer={farmer.Name}, maxHealth before={farmer.maxHealth}, newBonus={newBonus}, lastBonus={_lastBonus}, diff={diff}", LogLevel.Info);

        if (diff != 0)
        {
            farmer.maxHealth += diff;
            farmer.health = Math.Min(farmer.health + diff, farmer.maxHealth);
            _lastBonus = newBonus;
            Monitor?.Log($"maxHealth after={farmer.maxHealth}", LogLevel.Info);
        }
    }

    public static void Reset()
    {
        _lastBonus = 0;
    }
}
