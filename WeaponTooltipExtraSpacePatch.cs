using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
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

        // patch drawToolTip ด้วย signature ที่ถูกต้อง
        var drawToolTipMethod = typeof(IClickableMenu).GetMethod(
            "drawToolTip",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[]
            {
                typeof(SpriteBatch), typeof(string), typeof(string),
                typeof(Item), typeof(bool), typeof(int), typeof(int),
                typeof(string), typeof(int), typeof(CraftingRecipe), typeof(int)
            },
            null);

        if (drawToolTipMethod != null)
        {
            harmony.Patch(drawToolTipMethod,
                prefix: new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
                    .GetMethod(nameof(DrawToolTipPrefix))));
            Monitor?.Log($"drawToolTip patch applied! params={drawToolTipMethod.GetParameters().Length}", LogLevel.Info);
        }
        else
        {
            Monitor?.Log("drawToolTip not found! trying any overload...", LogLevel.Warn);

            // ลองหาทุก overload
            var methods = typeof(IClickableMenu).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "drawToolTip").ToArray();
            Monitor?.Log($"Found {methods.Length} drawToolTip overloads", LogLevel.Info);
            foreach (var m in methods)
                Monitor?.Log($"  params={m.GetParameters().Length}: {string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}", LogLevel.Info);
        }
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

    public static bool DrawToolTipPrefix(
        SpriteBatch b, string hoverText, string hoverTitle,
        Item hoveredItem, bool heldItem, int healAmountToDisplay,
        int currencySymbol, string extraItemToShowIndex,
        int extraItemToShowAmount, CraftingRecipe craftingIngredients,
        int moneyAmountToShowAtBottom)
    {
        if (hoveredItem is not MeleeWeapon weapon) return true;

        int extra = CalcExtra(weapon);
        Monitor?.Log($"DrawToolTipPrefix: {weapon.Name} extra={extra}", LogLevel.Info);
        if (extra == 0) return true;

        Drawing = true;
        try
        {
            int salePrice = weapon.salePrice();
            IClickableMenu.drawHoverText(b, hoverText, Game1.smallFont,
                heldItem ? 40 : 0, heldItem ? 40 : 0,
                moneyAmountToShowAtBottom, hoverTitle,
                -1, null, hoveredItem,
                currencySymbol, extraItemToShowIndex, extraItemToShowAmount,
                -1, -1, 1f, craftingIngredients,
                boxHeightOverride: -1);
        }
        finally
        {
            Drawing = false;
        }

        return false;
    }
}
