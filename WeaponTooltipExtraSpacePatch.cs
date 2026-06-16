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

        // patch drawMobileToolTip แทน drawHoverText
        var drawMobileToolTipMethod = typeof(IClickableMenu).GetMethod(
            "drawMobileToolTip",
            BindingFlags.Public | BindingFlags.Static);

        if (drawMobileToolTipMethod != null)
        {
            harmony.Patch(drawMobileToolTipMethod,
                prefix: new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
                    .GetMethod(nameof(DrawMobileToolTipPrefix))));
            Monitor?.Log("drawMobileToolTip patch applied!", LogLevel.Info);
        }
        else
            Monitor?.Log("drawMobileToolTip not found!", LogLevel.Warn);
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

    public static void DrawMobileToolTipPrefix(
        SpriteBatch b, int x, int y, int width, ref int height,
        int paragraphGap, string hoverText, string hoverTitle,
        Item hoveredItem, bool heldItem, int healAmountToDisplay,
        int currencySymbol, string extraItemToShowIndexStr,
        int extraItemToShowAmount, CraftingRecipe craftingIngredients,
        int moneyAmountToShowAtBottom, int currency,
        bool inStockAndBuyable, bool drawSmall)
    {
        if (hoveredItem is not MeleeWeapon weapon) return;

        int extra = CalcExtra(weapon);
        if (extra == 0) return;

        height += extra;
        Monitor?.Log($"DrawMobileToolTipPrefix: {weapon.Name} extra={extra} new height={height}", LogLevel.Info);
    }
}
