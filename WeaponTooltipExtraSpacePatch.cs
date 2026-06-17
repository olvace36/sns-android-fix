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

        var drawMobileFloating = typeof(IClickableMenu).GetMethod(
            "drawMobileFloatingToolTip",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[]
            {
                typeof(SpriteBatch), typeof(int), typeof(int), typeof(int),
                typeof(int), typeof(string), typeof(string), typeof(Item),
                typeof(bool), typeof(int), typeof(int), typeof(int),
                typeof(int), typeof(CraftingRecipe), typeof(int), typeof(int)
            },
            null);

        if (drawMobileFloating != null)
        {
            harmony.Patch(drawMobileFloating,
                prefix: new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
                    .GetMethod(nameof(DrawMobileFloatingPrefix))));
            Monitor?.Log("drawMobileFloatingToolTip patch applied!", LogLevel.Info);
        }
        else
            Monitor?.Log("drawMobileFloatingToolTip not found!", LogLevel.Warn);

        Monitor?.Log("WeaponTooltipExtraSpacePatch applied!", LogLevel.Info);
    }

    public static bool SNSTooltipPrefix() => !Drawing;

    static int CalcExtra(Item? hoveredItem)
    {
        if (hoveredItem is not MeleeWeapon weapon) return 0;
        int extra = 0;
        if (_getAlloying?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        if (_getCoating?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        if (_getGem?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        return extra;
    }

    public static bool DrawMobileFloatingPrefix(
        IClickableMenu __instance,
        SpriteBatch b, int x, int y, int inventoryPosition,
        int squareSide, string hoverText, string hoverTitle,
        Item hoveredItem, bool heldItem, int healAmountToDisplay,
        int currencySymbol, int extraItemToShowIndex,
        int extraItemToShowAmount, CraftingRecipe craftingIngredients,
        int moneyAmountToShowAtBottom, int stackNumber)
    {
        if (hoveredItem is not MeleeWeapon weapon) return true;

        int extra = CalcExtra(weapon);
        if (extra == 0) return true;

        // ใช้ LastY จาก DrawTooltipPostfix ซึ่งรวม extra ไปแล้ว
        int boxHeight = WeaponTooltipPatch.LastY;

        Monitor?.Log($"DrawMobileFloatingPrefix: {weapon.Name} extra={extra} LastY={WeaponTooltipPatch.LastY} boxHeight={boxHeight}", LogLevel.Info);

        if (boxHeight <= 0) return true;

        bool flag = hoveredItem is StardewValley.Object obj && obj.edibility.Value != -300;
        string[]? buffIconsToDisplay = null;
        string? extraItemToShowIndex2 = extraItemToShowIndex != -1 ? "(O)" + extraItemToShowIndex : null;

        Drawing = true;
        try
        {
            IClickableMenu.drawHoverText(b, hoverText, Game1.smallFont,
                heldItem ? 40 : 0, heldItem ? 40 : 0,
                moneyAmountToShowAtBottom, hoverTitle,
                flag ? (hoveredItem as StardewValley.Object)!.edibility.Value : -1,
                buffIconsToDisplay, hoveredItem, currencySymbol,
                extraItemToShowIndex2, extraItemToShowAmount,
                x, y, 1f, craftingIngredients,
                boxHeightOverride: boxHeight);
        }
        finally
        {
            Drawing = false;
        }

        return false;
    }
}
