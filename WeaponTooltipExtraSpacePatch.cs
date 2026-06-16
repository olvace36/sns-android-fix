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

    // helper method ที่จะถูกเรียกใน transpiler
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

        // หา GetEnchantmentLevel<GalaxySoulEnchantment> ซึ่งอยู่หลัง foreach loop
        var getEnchantmentLevel = AccessTools.Method(
            typeof(MeleeWeapon),
            "GetEnchantmentLevel",
            null,
            new[] { typeof(GalaxySoulEnchantment) });

        var getExtraHeight = AccessTools.Method(
            typeof(WeaponTooltipExtraSpacePatch),
            "GetExtraHeight");

        bool found = false;
        for (int i = 0; i < codes.Count; i++)
        {
            yield return codes[i];

            // หลัง GalaxySoulEnchantment check จบ แทรก num3 += GetExtraHeight(hoveredItem, font)
            if (!found && codes[i].Calls(getEnchantmentLevel))
            {
                // หา stloc ของ num3 หลัง getEnchantmentLevel
                for (int j = i + 1; j < Math.Min(i + 10, codes.Count); j++)
                {
                    yield return codes[j];
                    i = j;
                    if (codes[j].opcode == OpCodes.Add)
                    {
                        // แทรก: num3 += GetExtraHeight(hoveredItem, font)
                        // load num3
                        yield return new CodeInstruction(OpCodes.Ldloc_S, (byte)5); // num3
                        // load hoveredItem parameter
                        yield return new CodeInstruction(OpCodes.Ldarg, 9); // hoveredItem
                        // load font parameter
                        yield return new CodeInstruction(OpCodes.Ldarg_2); // font
                        // call GetExtraHeight
                        yield return new CodeInstruction(OpCodes.Call, getExtraHeight);
                        // num3 += extra
                        yield return new CodeInstruction(OpCodes.Add);
                        yield return new CodeInstruction(OpCodes.Stloc_S, (byte)5); // num3
                        found = true;
                        break;
                    }
                }
            }
        }

        if (!found)
            Monitor?.Log("DrawHoverTextTranspiler: injection point not found!", LogLevel.Warn);
    }
}
