using System;
using System.Collections.Generic;
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

        harmony.PatchAll();
        Monitor?.Log("WeaponTooltipExtraSpacePatch applied!", LogLevel.Info);
    }

    static void Postfix(MeleeWeapon __instance,
        SpriteFont font, int minWidth, int horizontalBuffer, int startingHeight,
        StringBuilder descriptionText, string boldTitleText,
        int moneyAmountToDisplayAtBottom, ref Point __result)
    {
        int extra = 0;
        int lineHeight = Math.Max((int)font.MeasureString("TT").Y, 48);

        if (_getAlloying?.Invoke(null, new object[] { __instance }) is string) extra += lineHeight;
        if (_getCoating?.Invoke(null, new object[] { __instance }) is string) extra += lineHeight;
        if (_getGem?.Invoke(null, new object[] { __instance }) is string) extra += lineHeight;

        if (extra == 0) return;

        // เหมือน Ring — return startingHeight + extra
        __result = new Point(__result.X, startingHeight + extra);
        Monitor?.Log($"ExtraSpacePostfix: {__instance.Name} startingHeight={startingHeight} extra={extra} result.Y={__result.Y}", LogLevel.Info);
    }
}
