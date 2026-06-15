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

    static void GetSkillLevels(Farmer farmer,
        out int rogueBase, out int rogueBuffed,
        out int paladinBase, out int paladinBuffed)
    {
        var skillType = AccessTools.TypeByName("SpaceCore.Skills+Skill");
        var getBuffed = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomBuffedSkillLevel",
            new[] { typeof(Farmer), skillType });
        var getBase = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomSkillLevel",
            new[] { typeof(Farmer), skillType });

        var rogueSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
            ?.GetProperty("RogueSkill", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
        var paladinSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModTOP")
            ?.GetProperty("PaladinSkill", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);

        rogueBase = rogueSkill != null
            ? (int)(getBase?.Invoke(null, new object[] { farmer, rogueSkill }) ?? 0) : 0;
        rogueBuffed = rogueSkill != null
            ? (int)(getBuffed?.Invoke(null, new object[] { farmer, rogueSkill }) ?? 0) : 0;
        paladinBase = paladinSkill != null
            ? (int)(getBase?.Invoke(null, new object[] { farmer, paladinSkill }) ?? 0) : 0;
        paladinBuffed = paladinSkill != null
            ? (int)(getBuffed?.Invoke(null, new object[] { farmer, paladinSkill }) ?? 0) : 0;
    }

    static void Postfix(Farmer farmer)
    {
        GetSkillLevels(farmer,
            out int rogueBase, out int rogueBuffed,
            out int paladinBase, out int paladinBuffed);

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
