using System.Reflection;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

[HarmonyPatch(typeof(LevelUpMenu), "RevalidateHealth")]
public class RevalidateHealthPatch
{
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
        }

        if (bonus > 0)
        {
            farmer.maxHealth += bonus;
            farmer.health = Math.Min(farmer.health + bonus, farmer.maxHealth);
        }
    }
}
