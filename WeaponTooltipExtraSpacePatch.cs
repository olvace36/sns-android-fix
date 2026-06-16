using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;

namespace SnsAndroidFix;

public class WeaponTooltipExtraSpacePatch
{
    internal static IMonitor? Monitor;
    private static MethodInfo? _getAlloying;
    private static MethodInfo? _getCoating;
    private static MethodInfo? _getGem;

    public static void Apply(Harmony harmony)
    {
        var extensionsType = AccessTools.TypeByName("SwordAndSorcerySMAPI.ArsenalExtensions");
        foreach (var m in extensionsType?.GetMethods(BindingFlags.Public | BindingFlags.Static) ?? Array.Empty<MethodInfo>())
        {
            var ps = m.GetParameters();
            if (ps.Length != 1 || ps[0].ParameterType != typeof(MeleeWeapon)) continue;
            if (m.Name == "GetBladeAlloying") _getAlloying = m;
            if (m.Name == "GetBladeCoating") _getCoating = m;
            if (m.Name == "GetExquisiteGemstone") _getGem = m;
        }

        var patch1Type = AccessTools.TypeByName("SwordAndSorcerySMAPI.MeleeWeaponTooltipPatch1");
        if (patch1Type == null)
        {
            Monitor?.Log("MeleeWeaponTooltipPatch1 not found!", LogLevel.Warn);
            return;
        }

        var postfix = patch1Type.GetMethod("Postfix",
            BindingFlags.Public | BindingFlags.Static);
        if (postfix == null)
        {
            Monitor?.Log("MeleeWeaponTooltipPatch1.Postfix not found!", LogLevel.Warn);
            return;
        }

        harmony.Patch(postfix,
            postfix: new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
                .GetMethod(nameof(ExtraSpacePostfix))));

        Monitor?.Log("WeaponTooltipExtraSpacePatch applied!", LogLevel.Info);
    }

    static string? GetId(MeleeWeapon weapon, MethodInfo? method)
        => method?.Invoke(null, new object[] { weapon }) as string;

    public static void ExtraSpacePostfix(MeleeWeapon __instance,
        SpriteFont font, ref Point __result)
    {
        int extra = 0;
        int lineHeight = Math.Max((int)font.MeasureString("TT").Y, 48);

        if (GetId(__instance, _getAlloying) != null) extra += lineHeight;
        if (GetId(__instance, _getCoating) != null) extra += lineHeight;
        if (GetId(__instance, _getGem) != null) extra += lineHeight;

        __result = new Point(__result.X, __result.Y + extra);
    }
}
