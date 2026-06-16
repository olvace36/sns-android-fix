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

        // patch drawTooltip ของ MeleeWeapon
        harmony.Patch(
            AccessTools.Method(typeof(MeleeWeapon), "drawTooltip"),
            prefix: new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
                .GetMethod(nameof(DrawTooltipPrefix))));

        Monitor?.Log("WeaponTooltipExtraSpacePatch applied!", LogLevel.Info);

        // IL logger สำหรับ drawHoverText StringBuilder
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
    }

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
        int extra = CalcExtra(__instance);
        if (extra == 0) return true;

        // ดึง vanilla height จาก getExtraSpaceNeededForTooltipSpecialIcons
        var description = new StringBuilder(__instance.description ?? "");
        Point vanillaSpace = __instance.getExtraSpaceNeededForTooltipSpecialIcons(
            font, 0, 92, 0, description, __instance.DisplayName, -1);

        Monitor?.Log($"DrawTooltipPrefix: {__instance.Name} vanillaSpace.Y={vanillaSpace.Y} extra={extra}", LogLevel.Info);

        // เรียก drawHoverText เองพร้อม boxHeightOverride
        IClickableMenu.drawHoverText(
            spriteBatch,
            overrideText?.ToString() ?? __instance.getDescription(),
            font,
            0, 0, -1,
            __instance.DisplayName,
            -1, null, __instance,
            0, null, -1,
            x, y, alpha,
            boxHeightOverride: vanillaSpace.Y + extra);

        Monitor?.Log($"DrawTooltipPrefix: called drawHoverText with boxHeightOverride={vanillaSpace.Y + extra}", LogLevel.Info);

        return false; // skip original
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
