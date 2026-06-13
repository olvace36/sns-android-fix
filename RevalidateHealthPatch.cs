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

        int paladinBuffed = paladinSkill != null
            ? (int)(getBuffedLevel?.Invoke(null, new object[] { farmer, paladinSkill }) ?? 0)
            : 0;
        int rogueBuffed = rogueSkill != null
            ? (int)(getBuffedLevel?.Invoke(null, new object[] { farmer, rogueSkill }) ?? 0)
            : 0;

        int newBonus = rogueBuffed * 3 + paladinBuffed * 5;
        int diff = newBonus - _lastBonus;

        Monitor?.Log($"Paladin buffed={paladinBuffed}, Rogue buffed={rogueBuffed}", LogLevel.Info);
        Monitor?.Log($"RevalidateHealth: maxHealth before={farmer.maxHealth}, newBonus={newBonus}, lastBonus={_lastBonus}, diff={diff}", LogLevel.Info);

        if (diff != 0)
        {
            farmer.maxHealth += diff;
            farmer.health = Math.Min(farmer.health + diff, farmer.maxHealth);
            _lastBonus = newBonus;
            Monitor?.Log($"maxHealth after={farmer.maxHealth}", LogLevel.Info);
        }
    }

    // เซ็ต lastBonus จาก base level จริงๆ ตอน load
    // เพื่อไม่บวกซ้ำ SNS Transpiler ที่บวกไปแล้ว
    public static void InitFromBaseLevel(Farmer farmer)
    {
        var skillType = AccessTools.TypeByName("SpaceCore.Skills+Skill");
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
            ? (int)(getBaseLevel?.Invoke(null, new object[] { farmer, paladinSkill }) ?? 0)
            : 0;
        int rogueBase = rogueSkill != null
            ? (int)(getBaseLevel?.Invoke(null, new object[] { farmer, rogueSkill }) ?? 0)
            : 0;

        _lastBonus = rogueBase * 3 + paladinBase * 5;
        Monitor?.Log($"InitFromBaseLevel: Paladin base={paladinBase}, Rogue base={rogueBase}, lastBonus={_lastBonus}", LogLevel.Info);
    }

    public static void Reset()
    {
        _lastBonus = 0;
    }
}
