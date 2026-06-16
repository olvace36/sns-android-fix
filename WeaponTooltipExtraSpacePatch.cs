using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Menus;
using StardewValley.Tools;

namespace SnsAndroidFix;

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

        harmony.Patch(
            AccessTools.Method(typeof(MeleeWeapon), "drawTooltip"),
            prefix: new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
                .GetMethod(nameof(DrawTooltipPrefix))));

        // patch SNS MeleeWeaponTooltipPatch2.Postfix ให้ skip ถ้า Drawing=true
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

    public static bool DrawTooltipPrefix(MeleeWeapon __instance,
        SpriteBatch spriteBatch, ref int x, ref int y, SpriteFont font,
        float alpha, StringBuilder overrideText)
    {
        if (Drawing) return true;

        int extra = CalcExtra(__instance);
        if (extra == 0) return true;

        int salePrice = __instance.salePrice();
        var description = new StringBuilder(__instance.description ?? "");

        // ดึง vanilla height โดยไม่รวม SNS extra (SNS บวกไว้ใน boxHeightOverride แล้ว)
        // ใช้ base class method ไม่ใช่ instance method ที่ SNS override
        Point vanillaSpace = ((Item)__instance).getExtraSpaceNeededForTooltipSpecialIcons(
            font, 0, 92, 0, description, __instance.DisplayName, salePrice);

        // SNS extra อยู่ใน vanillaSpace.Y แล้ว เราบวกแค่ effect text ของเรา
        int boxHeight = vanillaSpace.Y + extra;

        Monitor?.Log($"DrawTooltipPrefix: {__instance.Name} vanillaSpace.Y={vanillaSpace.Y} extra={extra} boxHeight={boxHeight}", LogLevel.Info);

        Drawing = true;
        try
        {
            IClickableMenu.drawHoverText(
                spriteBatch,
                overrideText?.ToString() ?? __instance.getDescription(),
                font,
                0, 0, salePrice,
                __instance.DisplayName,
                -1, null, __instance,
                0, null, -1,
                x, y, alpha,
                boxHeightOverride: boxHeight);
        }
        finally
        {
            Drawing = false;
        }

        return false;
    }
}
