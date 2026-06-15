using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

[HarmonyPatch(typeof(LevelUpMenu), "RevalidateHealth")]
public class RevalidateHealthPatch
{
    internal static IMonitor? Monitor;

    // cache reflection
    private static MethodInfo? _getBuffed;
    private static MethodInfo? _getBase;
    private static object? _rogueSkill;
    private static object? _paladinSkill;

    public static void InitCache()
    {
        var skillType = AccessTools.TypeByName("SpaceCore.Skills+Skill");
        _getBuffed = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomBuffedSkillLevel",
            new[] { typeof(Farmer), skillType });
        _getBase = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomSkillLevel",
            new[] { typeof(Farmer), skillType });
        _rogueSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
            ?.GetProperty("RogueSkill", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
        _paladinSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModTOP")
            ?.GetProperty("PaladinSkill", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
    }

    static int CalcVanillaBaseMaxHealth(Farmer farmer)
    {
        int num = 100;
        if (farmer.mailReceived.Contains("qiCave"))
            num += 25;
        for (int i = 1; i <= farmer.GetUnmodifiedSkillLevel(4); i++)
        {
            if (!farmer.newLevels.Contains(new Point(4, i)) && i != 5 && i != 10)
                num += 5;
        }
        if (farmer.professions.Contains(24))
            num += 15;
        if (farmer.professions.Contains(27))
            num += 25;
        return num;
    }

    static void Postfix(Farmer farmer)
    {
        if (_getBuffed == null || _getBase == null) return;

        int rogueBase = _rogueSkill != null
            ? (int)(_getBase.Invoke(null, new object[] { farmer, _rogueSkill }) ?? 0) : 0;
        int rogueBuffed = _rogueSkill != null
            ? (int)(_getBuffed.Invoke(null, new object[] { farmer, _rogueSkill }) ?? 0) : 0;
        int paladinBase = _paladinSkill != null
            ? (int)(_getBase.Invoke(null, new object[] { farmer, _paladinSkill }) ?? 0) : 0;
        int paladinBuffed = _paladinSkill != null
            ? (int)(_getBuffed.Invoke(null, new object[] { farmer, _paladinSkill }) ?? 0) : 0;

        int vanillaBase = CalcVanillaBaseMaxHealth(farmer);
        int snsBaseBonus = rogueBase * 3 + paladinBase * 5;
        int buffOnlyBonus = (rogueBuffed - rogueBase) * 3 + (paladinBuffed - paladinBase) * 5;
        int expectedMaxHealth = vanillaBase + snsBaseBonus + buffOnlyBonus;

        if (farmer.maxHealth != expectedMaxHealth)
        {
            int diff = expectedMaxHealth - farmer.maxHealth;
            farmer.maxHealth = expectedMaxHealth;
            farmer.health = Math.Min(farmer.health + diff, farmer.maxHealth);
        }
    }

    public static void Reset() { }
    public static void InitFromBaseLevel(Farmer farmer) { }
}
