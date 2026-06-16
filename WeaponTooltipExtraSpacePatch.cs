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

        var drawHoverTextMethod = typeof(IClickableMenu).GetMethod(
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

        if (drawHoverTextMethod != null)
        {
            harmony.Patch(drawHoverTextMethod,
                transpiler: new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
                    .GetMethod(nameof(DrawHoverTextTranspiler))));
            Monitor?.Log("WeaponTooltipExtraSpacePatch applied!", LogLevel.Info);
        }
        else
            Monitor?.Log("drawHoverText not found!", LogLevel.Warn);
    }

    public static int GetExtraHeight(Item hoveredItem, SpriteFont font)
    {
        if (hoveredItem is not MeleeWeapon weapon) return 0;

        int extra = 0;
        int lineHeight = Math.Max((int)font.MeasureString("TT").Y, 48);
        if (_getAlloying?.Invoke(null, new object[] { weapon }) is string) extra += lineHeight;
        if (_getCoating?.Invoke(null, new object[] { weapon }) is string) extra += lineHeight;
        if (_getGem?.Invoke(null, new object[] { weapon }) is string) extra += lineHeight;
        return extra;
    }

    public static IEnumerable<CodeInstruction> DrawHoverTextTranspiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new List<CodeInstruction>(instructions);
        var getExtraHeight = AccessTools.Method(
            typeof(WeaponTooltipExtraSpacePatch), "GetExtraHeight");

        // หา getDescriptionWidth ซึ่งมีแค่ใน MeleeWeapon block
        var getDescriptionWidth = AccessTools.Method(
            typeof(MeleeWeapon), "getDescriptionWidth");

        bool found = false;

        for (int i = 0; i < codes.Count; i++)
        {
            yield return codes[i];

            // หลัง getDescriptionWidth ถูกเรียก หา stloc ที่เก็บ num3
            if (!found && codes[i].Calls(getDescriptionWidth))
            {
                // เดิน forward หา instruction ที่ add แล้ว stloc
                for (int j = i + 1; j < Math.Min(i + 15, codes.Count); j++)
                {
                    yield return codes[j];
                    i = j;

                    // หา stloc หลัง add (น่าจะเป็น num3 += ...)
                    if (codes[j].opcode == OpCodes.Stloc_S || codes[j].opcode == OpCodes.Stloc)
                    {
                        var operand = codes[j].operand;
                        // แทรก num3 += GetExtraHeight(hoveredItem, font)
                        yield return new CodeInstruction(OpCodes.Ldloc_S, operand);
                        yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)9); // hoveredItem
                        yield return new CodeInstruction(OpCodes.Ldarg_2); // font
                        yield return new CodeInstruction(OpCodes.Call, getExtraHeight);
                        yield return new CodeInstruction(OpCodes.Add);
                        yield return new CodeInstruction(OpCodes.Stloc_S, operand);

                        found = true;
                        Monitor?.Log("DrawHoverTextTranspiler: injection point found!", LogLevel.Info);
                        break;
                    }
                }
            }
        }

        if (!found)
            Monitor?.Log("DrawHoverTextTranspiler: injection point not found!", LogLevel.Warn);
    }
}
