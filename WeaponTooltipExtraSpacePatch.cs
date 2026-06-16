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

        // patch getExtraSpaceNeededForTooltipSpecialIcons ของ MeleeWeapon ตรงๆ
        var extraSpacePostfix = new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
            .GetMethod(nameof(ExtraSpacePostfix)));
        extraSpacePostfix.priority = Priority.Low;

        harmony.Patch(
            AccessTools.Method(typeof(MeleeWeapon),
                "getExtraSpaceNeededForTooltipSpecialIcons"),
            postfix: extraSpacePostfix);

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
