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
    private static int _baseMaxHealth = -1;

    static void Postfix(Farmer farmer)
    {
        var skillType = AccessTools.TypeByName("SpaceCore.Skills+Skill");
        var getBuffedLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomBuffedSkillLevel",
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

        if (_baseMaxHealth < 0)
            _baseMaxHealth = farmer.maxHealth - newBonus;

        int expectedMaxHealth = _baseMaxHealth + newBonus;

        Monitor?.Log($"Paladin buffed={paladinBuffed}, Rogue buffed={rogueBuffed}", LogLevel.Info);
        Monitor?.Log($"RevalidateHealth: base={_baseMaxHealth}, newBonus={newBonus}, expected={expectedMaxHealth}, current={farmer.maxHealth}", LogLevel.Info);

        if (farmer.maxHealth != expectedMaxHealth)
        {
            int diff = expectedMaxHealth - farmer.maxHealth;
            farmer.maxHealth = expectedMaxHealth;
            farmer.health = Math.Min(farmer.health + diff, farmer.maxHealth);
            Monitor?.Log($"maxHealth set to={farmer.maxHealth}", LogLevel.Info);
        }
    }

    public static void Reset()
    {
        _baseMaxHealth = -1;
    }

    public static void InitFromBaseLevel(Farmer farmer)
    {
        var skillType = AccessTools.TypeByName("SpaceCore.Skills+Skill");
        var getBuffedLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomBuffedSkillLevel",
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

        // ลบ buffed bonus ออกเพื่อหา baseMaxHealth จริงๆ
        int buffedBonus = rogueBuffed * 3 + paladinBuffed * 5;
        _baseMaxHealth = farmer.maxHealth - buffedBonus;
        Monitor?.Log($"InitFromBaseLevel: Paladin buffed={paladinBuffed}, Rogue buffed={rogueBuffed}, buffedBonus={buffedBonus}, baseMaxHealth={_baseMaxHealth}", LogLevel.Info);
    }
}
