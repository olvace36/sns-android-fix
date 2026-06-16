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
using StardewValley.Menus;
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
            Monitor?.Log("drawToolTip patch applied!", LogLevel.Info);
        }
        else
            Monitor?.Log("drawToolTip not found!", LogLevel.Warn);

        // Log IL ของ drawHoverText StringBuilder
        var drawHoverTextSB = typeof(IClickableMenu).GetMethod(
            "drawHoverText",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[]
            {
                typeof(SpriteBatch), typeof(StringBuilder), typeof(SpriteFont),
                typeof(int), typeof(int), typeof(int), typeof(string),
                typeof(int), typeof(string[]), typeof(Item), typeof(int),
                typeof(string), typeof(int), typeof(int), typeof(int),
                typeof(float), typeof(CraftingRecipe),
                typeof(IList<Item>), typeof(Texture2D), typeof(Rectangle?),
                typeof(Color?), typeof(Color?), typeof(float),
                typeof(int), typeof(int), typeof(int)
            },
            null);

        if (drawHoverTextSB != null)
        {
            harmony.Patch(drawHoverTextSB,
                transpiler: new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
                    .GetMethod(nameof(LogILTranspiler))));
            Monitor?.Log("IL logger applied!", LogLevel.Info);
        }
        else
            Monitor?.Log("drawHoverText(StringBuilder) not found!", LogLevel.Warn);
    }

    static int CalcExtra(Item? hoveredItem)
    {
        if (hoveredItem is not MeleeWeapon weapon) return 0;
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
        if (hoveredItem is not MeleeWeapon) return true;

        int extra = CalcExtra(hoveredItem);
        if (extra == 0) return true;

        Monitor?.Log($"DrawToolTipPrefix: extra={extra}", LogLevel.Info);

        IClickableMenu.drawHoverText(b, hoverText, Game1.smallFont,
            heldItem ? 40 : 0, heldItem ? 40 : 0,
            moneyAmountToShowAtBottom, hoverTitle,
            -1, null, hoveredItem, currencySymbol,
            extraItemToShowIndex, extraItemToShowAmount,
            -1, -1, 1f, craftingIngredients,
            boxHeightOverride: extra);

        return false;
    }

    public static IEnumerable<CodeInstruction> LogILTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        int i = 0;
        foreach (var code in instructions)
        {
            Monitor?.Log($"IL[{i++}]: {code.opcode} {code.operand}", LogLevel.Info);
            yield return code;
        }
    }
}
