using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Menus;
using StardewValley.Objects;
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

        // patch drawHoverText แบบ string (ที่ drawToolTip เรียก)
        var drawHoverTextString = typeof(IClickableMenu).GetMethod(
            "drawHoverText",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[]
            {
                typeof(SpriteBatch), typeof(string), typeof(SpriteFont),
                typeof(int), typeof(int), typeof(int), typeof(string),
                typeof(int), typeof(string[]), typeof(Item), typeof(int),
                typeof(string), typeof(int), typeof(int), typeof(int),
                typeof(float), typeof(CraftingRecipe),
                typeof(IList<Item>), typeof(Texture2D), typeof(Rectangle?),
                typeof(Color?), typeof(Color?), typeof(float),
                typeof(int), typeof(int), typeof(int)
            },
            null);

        if (drawHoverTextString != null)
        {
            harmony.Patch(drawHoverTextString,
                prefix: new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
                    .GetMethod(nameof(DrawHoverTextPrefix))));
            Monitor?.Log("WeaponTooltipExtraSpacePatch applied!", LogLevel.Info);
        }
        else
            Monitor?.Log("drawHoverText(string) not found!", LogLevel.Warn);
    }

    static int CalcExtra(Item hoveredItem, SpriteFont font)
    {
        if (hoveredItem is not MeleeWeapon weapon) return 0;

        int extra = 0;
        int lineHeight = Math.Max((int)font.MeasureString("TT").Y, 48);
        if (_getAlloying?.Invoke(null, new object[] { weapon }) is string) extra += lineHeight;
        if (_getCoating?.Invoke(null, new object[] { weapon }) is string) extra += lineHeight;
        if (_getGem?.Invoke(null, new object[] { weapon }) is string) extra += lineHeight;
        return extra;
    }

    public static void DrawHoverTextPrefix(
        SpriteBatch b, string text, SpriteFont font,
        int xOffset, int yOffset, int moneyAmountToDisplayAtBottom,
        string boldTitleText, int healAmountToDisplay,
        string[] buffIconsToDisplay, Item hoveredItem, int currencySymbol,
        string extraItemToShowIndex, int extraItemToShowAmount,
        int overrideX, int overrideY, float alpha,
        CraftingRecipe craftingIngredients,
        IList<Item> additional_craft_materials,
        Texture2D boxTexture, Rectangle? boxSourceRect,
        Color? textColor, Color? textShadowColor, float boxScale,
        ref int boxWidthOverride, ref int boxHeightOverride, int stackNumber)
    {
        int extra = CalcExtra(hoveredItem, font);
        if (extra == 0) return;

        Monitor?.Log($"DrawHoverTextPrefix: extra={extra} boxHeightOverride={boxHeightOverride}", LogLevel.Info);

        if (boxHeightOverride > 0)
            boxHeightOverride += extra;
        else
            boxHeightOverride = extra; // vanilla จะคำนวณ height เองแต่เราบังคับ override
    }
}
