using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace SnsAndroidFix;

[HarmonyPatch(typeof(MeleeWeapon), "getExtraSpaceNeededForTooltipSpecialIcons")]
public class WeaponTooltipExtraSpacePatch
{
    internal static IMonitor? Monitor;
    private static MethodInfo? _getAlloying;
    private static MethodInfo? _getCoating;
    private static MethodInfo? _getGem;
    public static bool Drawing = false;

    public static void Apply(Harmony harmony)
    {
        var extensionsType = AccessTools.TypeByName("SwordAndSorcerySMAPI.ArsenalExtensions");
        if (extensionsType != null)
        {
            foreach (var m in extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var ps = m.GetParameters();
                if (ps.Length != 1 || ps[0].ParameterType != typeof(MeleeWeapon)) continue;
                if (m.Name == "GetBladeAlloying") _getAlloying = m;
                if (m.Name == "GetBladeCoating") _getCoating = m;
                if (m.Name == "GetExquisiteGemstone") _getGem = m;
            }
        }

        var sns2Type = AccessTools.TypeByName("SwordAndSorcerySMAPI.MeleeWeaponTooltipPatch2");
        var sns2Postfix = sns2Type?.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
        if (sns2Postfix != null)
        {
            harmony.Patch(sns2Postfix,
                prefix: new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
                    .GetMethod(nameof(SNSTooltipPrefix))));
            Monitor?.Log("SNS MeleeWeaponTooltipPatch2 patch applied!", LogLevel.Info);
        }
        else
            Monitor?.Log("SNS MeleeWeaponTooltipPatch2.Postfix not found!", LogLevel.Warn);

        harmony.PatchAll();
        Monitor?.Log("WeaponTooltipExtraSpacePatch applied!", LogLevel.Info);
    }

    public static bool SNSTooltipPrefix() => !Drawing;

    static int CalcExtra(MeleeWeapon weapon)
    {
        int extra = 0;
        if (_getAlloying?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        if (_getCoating?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        if (_getGem?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        return extra;
    }

    static void Postfix(MeleeWeapon __instance,
        SpriteFont font, int minWidth, int horizontalBuffer, int startingHeight,
        StringBuilder descriptionText, string boldTitleText,
        int moneyAmountToDisplayAtBottom, ref Point __result)
    {
        int extra = CalcExtra(__instance);
        if (extra == 0) return;

        __result = new Point(__result.X, __result.Y + extra);
    }
}

// แยกออกมาเพื่อ log call stack
[HarmonyPatch(typeof(MeleeWeapon), "drawTooltip")]
public class WeaponTooltipCallStackPatch
{
    static IMonitor? Monitor => WeaponTooltipExtraSpacePatch.Monitor;

    static void Prefix(MeleeWeapon __instance)
    {
        if ((__instance.modData.TryGetValue("swordandsorcery/BladeAlloying", out _) ||
             __instance.modData.TryGetValue("swordandsorcery/BladeCoating", out _) ||
             __instance.modData.TryGetValue("swordandsorcery/ExquisiteGemstone", out _)))
        {
            Monitor?.Log($"drawTooltip call stack:\n{System.Environment.StackTrace}", LogLevel.Info);
        }
    }
}
