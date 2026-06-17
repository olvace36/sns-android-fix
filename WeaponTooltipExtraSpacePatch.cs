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

    static int CalcExtra(Item? hoveredItem)
    {
        if (hoveredItem is not MeleeWeapon weapon) return 0;
        int extra = 0;
        if (_getAlloying?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        if (_getCoating?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        if (_getGem?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        return extra;
    }

    // คำนวณ box height แบบเดียวกับ drawHoverText จริงๆ
    static int CalcBoxHeight(MeleeWeapon weapon, SpriteFont font, string boldTitleText, int moneyAmountToDisplayAtBottom)
    {
        int num = 60;
        int num3 = Math.Max(num * 3,
            (int)((boldTitleText != null) ? (Game1.dialogueFont.MeasureString(boldTitleText).Y + 16f) : 0f) + 32)
            + (int)font.MeasureString("T").Y
            + (int)((moneyAmountToDisplayAtBottom > -1) ? (font.MeasureString(moneyAmountToDisplayAtBottom.ToString() ?? "").Y + 4f) : 0f);

        num3 += (!weapon.isScythe() ? (weapon.getNumberOfDescriptionCategories() * 4 * 12) : 0);
        num3 += (int)font.MeasureString(Game1.parseText(weapon.description, Game1.smallFont, weapon.getDescriptionWidth())).Y;

        if (weapon.GetTotalForgeLevels() > 0)
            num3 += (int)font.MeasureString("T").Y;

        foreach (var enchantment in weapon.enchantments)
        {
            if (!enchantment.IsForge() && enchantment.ShouldBeDisplayed())
                num3 += (int)font.MeasureString("T").Y + 12;
        }

        Monitor?.Log($"CalcBoxHeight: {weapon.Name} money={moneyAmountToDisplayAtBottom} num3={num3}", LogLevel.Info);
        return num3;
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

        // คำนวณ box height แบบเดียวกับ drawHoverText จริงๆ แทนที่จะใช้ getExtraSpaceNeededForTooltipSpecialIcons
        int baseHeight = CalcBoxHeight(weapon, Game1.smallFont, hoverTitle, moneyAmountToShowAtBottom);
        int boxHeight = baseHeight + extra;

        Monitor?.Log($"DrawMobileFloatingPrefix: {weapon.Name} money={moneyAmountToShowAtBottom} baseHeight={baseHeight} extra={extra} boxHeight={boxHeight}", LogLevel.Info);

        bool flag = hoveredItem is StardewValley.Object obj && obj.edibility.Value != -300;
        string? extraItemToShowIndex2 = extraItemToShowIndex != -1 ? "(O)" + extraItemToShowIndex : null;

        IClickableMenu.drawHoverText(b, hoverText, Game1.smallFont,
            heldItem ? 40 : 0, heldItem ? 40 : 0,
            moneyAmountToShowAtBottom, hoverTitle,
            flag ? (hoveredItem as StardewValley.Object)!.edibility.Value : -1,
            null, hoveredItem, currencySymbol,
            extraItemToShowIndex2, extraItemToShowAmount,
            x, y, 1f, craftingIngredients,
            boxHeightOverride: boxHeight);

        Monitor?.Log($"DrawMobileFloatingPrefix: drawHoverText called boxHeight={boxHeight}", LogLevel.Info);

        return false;
    }
}

