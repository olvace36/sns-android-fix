using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using SpaceCore;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace SnsAndroidFix;

public class BuffedSkillLevelPatch
{
    internal static IMonitor? Monitor;

    public static void Apply(Harmony harmony)
    {
        // 1. Slingshot/Bow damage — Rogue
        harmony.Patch(
            AccessTools.Method(typeof(Slingshot), "GetAmmoDamage"),
            postfix: new HarmonyMethod(typeof(BuffedSkillLevelPatch)
                .GetMethod(nameof(SlingshotDamagePostfix))));

        // 2. Scythe drop essence — Druid
        harmony.Patch(
            AccessTools.Method(typeof(Grass), "performToolAction"),
            prefix: new HarmonyMethod(typeof(BuffedSkillLevelPatch)
                .GetMethod(nameof(ScytheDropPrefix))));

        // 3. HoeDirt drop essence — Druid
        harmony.Patch(
            AccessTools.Method(typeof(GameLocation), "makeHoeDirt"),
            postfix: new HarmonyMethod(typeof(BuffedSkillLevelPatch)
                .GetMethod(nameof(HoeDirtPostfix))));

        // 4. Gem to Exquisite — Rogue
        harmony.Patch(
            AccessTools.Method(typeof(Game1), "createObjectDebris",
                new[] { typeof(string), typeof(int), typeof(int), typeof(long), typeof(GameLocation) }),
            prefix: new HarmonyMethod(typeof(BuffedSkillLevelPatch)
                .GetMethod(nameof(GemExquisitePrefix))));

        // 5. RecalculateAether — ใช้ string overload ทั้งหมด
        var recalcMethod = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
            ?.GetMethod("RecalculateAether", BindingFlags.NonPublic | BindingFlags.Static);
        if (recalcMethod != null)
        {
            harmony.Patch(recalcMethod,
                transpiler: new HarmonyMethod(typeof(BuffedSkillLevelPatch)
                    .GetMethod(nameof(ReplaceSkillLevelStringTranspiler))));
            Monitor?.Log("RecalculateAether patch applied!", LogLevel.Info);
        }
        else Monitor?.Log("RecalculateAether not found!", LogLevel.Warn);

        // 6. Aether regen — Druid, Bard, Sorcery
        var timeChangedMethod = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
            ?.GetMethod("GameLoop_TimeChanged", BindingFlags.NonPublic | BindingFlags.Instance);
        if (timeChangedMethod != null)
        {
            harmony.Patch(timeChangedMethod,
                transpiler: new HarmonyMethod(typeof(BuffedSkillLevelPatch)
                    .GetMethod(nameof(ReplaceSkillLevelTranspiler))));
            Monitor?.Log("GameLoop_TimeChanged patch applied!", LogLevel.Info);
        }
        else Monitor?.Log("GameLoop_TimeChanged not found!", LogLevel.Warn);

        // 7. Spell damage — Sorcery
        var spellsType = AccessTools.TypeByName("SwordAndSorcerySMAPI.Framework.Abilities.Spells");
        var getSpellDamage = spellsType?.GetMethod("GetSpellDamange",
            BindingFlags.Public | BindingFlags.Static);
        if (getSpellDamage != null)
        {
            harmony.Patch(getSpellDamage,
                transpiler: new HarmonyMethod(typeof(BuffedSkillLevelPatch)
                    .GetMethod(nameof(ReplaceSkillLevelStringTranspiler))));
            Monitor?.Log("GetSpellDamange patch applied!", LogLevel.Info);
        }
        else Monitor?.Log("GetSpellDamange not found!", LogLevel.Warn);

        Monitor?.Log("BuffedSkillLevelPatch applied!", LogLevel.Info);
    }

    static int GetBuffedRogueLevel()
    {
        var getBuffed = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomBuffedSkillLevel",
            new[] { typeof(Farmer), AccessTools.TypeByName("SpaceCore.Skills+Skill") });
        var rogueSkill = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
            ?.GetProperty("RogueSkill", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
        return rogueSkill != null
            ? (int)(getBuffed?.Invoke(null, new object[] { Game1.player, rogueSkill }) ?? 0)
            : 0;
    }

    static int GetBuffedDruidLevel()
    {
        var getBuffed = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomBuffedSkillLevel",
            new[] { typeof(Farmer), typeof(string) });
        return (int)(getBuffed?.Invoke(null, new object[] { Game1.player, "DestyNova.SwordAndSorcery.Druidics" }) ?? 0);
    }

    // ใช้ reflection แทน IsBow() ที่ไม่มีใน Android
    static bool IsBow(Slingshot slingshot)
    {
        var method = typeof(Slingshot).GetMethod("IsBow",
            BindingFlags.Public | BindingFlags.Instance);
        if (method != null)
            return (bool)method.Invoke(slingshot, null);
        return slingshot.QualifiedItemId?.Contains("Bow") == true
            || slingshot.Name?.Contains("Bow") == true;
    }

    // ใช้ reflection แทน NetHashSet<string> ที่ไม่มีใน Android
    static bool PlayerEventsSeen(string eventId)
    {
        var eventsSeen = typeof(Farmer)
            .GetField("eventsSeen", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(Game1.player);
        if (eventsSeen == null) return false;
        var containsMethod = eventsSeen.GetType()
            .GetMethod("Contains", new[] { typeof(string) });
        return (bool)(containsMethod?.Invoke(eventsSeen, new object[] { eventId }) ?? false);
    }

    // ใช้ reflection แทน Profession cast ที่ไม่มีใน Android
    static bool HasProfession(object? prof)
    {
        if (prof == null) return false;
        var hasProf = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "HasCustomProfession",
            new[] { typeof(Farmer), AccessTools.TypeByName("SpaceCore.Skills+Profession") });
        return (bool)(hasProf?.Invoke(null, new object[] { Game1.player, prof }) ?? false);
    }

    // 1. Slingshot/Bow damage
    public static void SlingshotDamagePostfix(Slingshot __instance, ref int __result)
    {
        if (!IsBow(__instance)) return;
        int level = GetBuffedRogueLevel();
        __result = 25 + 5 * level;
    }

    // 2. Scythe drop essence
    public static bool ScytheDropPrefix(Grass __instance, Tool t)
    {
        if (!PlayerEventsSeen("SnS.Ch2.Hector.12") ||
            !((TerrainFeature)(object)__instance).Location.IsFarm) return true;

        var val = t as MeleeWeapon;
        if (val == null || !((Tool)(object)val).isScythe()) return true;

        float num = 0.05f;
        num += GetBuffedDruidLevel() * 0.001f;
        if (Game1.player.hasOrWillReceiveMail("BrokenCircletPower")) num += 0.01f;

        var profType = AccessTools.TypeByName("SwordAndSorcerySMAPI.Framework.ModSkills.DruidicsSkill");
        var prof = profType?.GetProperty("ProfessionAgricultureYggdrasil",
            BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (HasProfession(prof)) num += 0.01f;

        if (Game1.random.NextDouble() < (double)(2f * num))
            Game1.createItemDebris(
                (Item)(object)new StardewValley.Object("DN.SnS_druidicessence", 1, false, -1, 0),
                ((TerrainFeature)(object)__instance).Tile * 64f, -1, null, -1, false);

        return false;
    }

    // 3. HoeDirt drop essence
    public static void HoeDirtPostfix(GameLocation __instance, Vector2 tileLocation)
    {
        if (!PlayerEventsSeen("SnS.Ch2.Hector.12") || !__instance.IsFarm) return;

        float num = 0.025f;
        num += GetBuffedDruidLevel() * 0.001f;
        if (Game1.player.hasOrWillReceiveMail("BrokenCircletPower")) num += 0.01f;

        var profType = AccessTools.TypeByName("SwordAndSorcerySMAPI.Framework.ModSkills.DruidicsSkill");
        var prof = profType?.GetProperty("ProfessionAgricultureYggdrasil",
            BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (HasProfession(prof)) num += 0.01f;

        if (Game1.random.NextDouble() < (double)(4f * num))
            Game1.createItemDebris(
                (Item)(object)new StardewValley.Object("DN.SnS_druidicessence", 1, false, -1, 0),
                tileLocation * 64f, -1, null, -1, false);
    }

    // 4. Gem to Exquisite
    public static void GemExquisitePrefix(ref string id)
    {
        var isBreakingStone = AccessTools.TypeByName("SwordAndSorcerySMAPI.GameLocationBreakingStoneFlagPatch")
            ?.GetField("IsBreakingStone", BindingFlags.Public | BindingFlags.Static);
        int breaking = (int?)isBreakingStone?.GetValue(null) ?? 0;
        if (breaking <= 0) return;

        double num = 1.0;
        if (Game1.player.hasOrWillReceiveMail("StygiumPendantPower")) num = 2.0;

        int rogueLevel = GetBuffedRogueLevel();

        var exquisiteMap = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
            ?.GetField("ExquisiteGemMappings", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null) as Dictionary<string, string>;
        var pureOreMap = AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
            ?.GetField("PureOreMappings", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null) as Dictionary<string, string>;

        if (rogueLevel >= 4 && exquisiteMap != null &&
            exquisiteMap.TryGetValue(id, out var exquisite) &&
            Game1.random.NextDouble() < 0.25 * num)
        {
            id = exquisite;
            return;
        }
        if (rogueLevel >= 2 && pureOreMap != null &&
            pureOreMap.TryGetValue(id, out var pure) &&
            Game1.random.NextDouble() < 0.15 * num)
        {
            id = pure;
        }
    }

    // Transpiler สำหรับ method ที่ใช้ Skill object overload
    public static IEnumerable<CodeInstruction> ReplaceSkillLevelTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var skillType = AccessTools.TypeByName("SpaceCore.Skills+Skill");
        var getSkillLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomSkillLevel",
            new[] { typeof(Farmer), skillType });
        var getBuffedLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomBuffedSkillLevel",
            new[] { typeof(Farmer), skillType });

        int replaced = 0;
        foreach (var code in instructions)
        {
            if (code.Calls(getSkillLevel))
            {
                code.operand = getBuffedLevel;
                replaced++;
            }
            yield return code;
        }
        Monitor?.Log($"ReplaceSkillLevelTranspiler: replaced {replaced} calls", LogLevel.Info);
    }

    // Transpiler สำหรับ method ที่ใช้ string overload
    public static IEnumerable<CodeInstruction> ReplaceSkillLevelStringTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var getSkillLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomSkillLevel",
            new[] { typeof(Farmer), typeof(string) });
        var getBuffedLevel = AccessTools.Method(
            AccessTools.TypeByName("SpaceCore.SkillExtensions"),
            "GetCustomBuffedSkillLevel",
            new[] { typeof(Farmer), typeof(string) });

        int replaced = 0;
        foreach (var code in instructions)
        {
            if (code.Calls(getSkillLevel))
            {
                code.operand = getBuffedLevel;
                replaced++;
            }
            yield return code;
        }
        Monitor?.Log($"ReplaceSkillLevelStringTranspiler: replaced {replaced} calls", LogLevel.Info);
    }
}

