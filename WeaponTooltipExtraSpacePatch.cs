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
                    .GetMethod(nameof(DrawHoverTextTranspiler))));
            Monitor?.Log("WeaponTooltipExtraSpacePatch Transpiler applied!", LogLevel.Info);
        }
        else
            Monitor?.Log("drawHoverText(StringBuilder) not found!", LogLevel.Warn);
    }

    public static bool SNSTooltipPrefix() => !Drawing;

    public static int CalcExtra(MeleeWeapon weapon)
    {
        int extra = 0;
        if (_getAlloying?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        if (_getCoating?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        if (_getGem?.Invoke(null, new object[] { weapon }) is string) extra += 48;
        Monitor?.Log($"CalcExtra: {weapon.Name} extra={extra}", LogLevel.Info);
        return extra;
    }

    public static IEnumerable<CodeInstruction> DrawHoverTextTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var calcExtra = AccessTools.Method(
            typeof(WeaponTooltipExtraSpacePatch), "CalcExtra");

        bool found = false;
        object? meleeWeaponOperand = null;

        for (int i = 0; i < codes.Count; i++)
        {
            // เก็บ operand ของ ldloc.s MeleeWeapon ก่อน GalaxySoul
            if (codes[i].opcode == OpCodes.Ldloc_S &&
                codes[i].operand?.ToString()?.Contains("MeleeWeapon") == true)
            {
                meleeWeaponOperand = codes[i].operand;
                Monitor?.Log($"Found MeleeWeapon ldloc.s at IL[{i}] operand={meleeWeaponOperand}", LogLevel.Info);
            }

            yield return codes[i];

            if (!found &&
                codes[i].opcode == OpCodes.Callvirt &&
                codes[i].operand?.ToString()?.Contains("GetEnchantmentLevel") == true &&
                codes[i].operand?.ToString()?.Contains("GalaxySoul") == true)
            {
                Monitor?.Log($"Found GalaxySoul at IL[{i}] meleeWeaponOperand={meleeWeaponOperand}", LogLevel.Info);

                for (int j = i + 1; j < Math.Min(i + 20, codes.Count); j++)
                {
                    yield return codes[j];
                    i = j;

                    if (codes[j].opcode == OpCodes.Br || codes[j].opcode == OpCodes.Br_S)
                    {
                        Monitor?.Log($"Found br at IL[{j}]", LogLevel.Info);
                        break;
                    }
                }

                if (meleeWeaponOperand != null)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_3);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, meleeWeaponOperand);
                    yield return new CodeInstruction(OpCodes.Call, calcExtra);
                    yield return new CodeInstruction(OpCodes.Add);
                    yield return new CodeInstruction(OpCodes.Stloc_3);
                    Monitor?.Log("DrawHoverTextTranspiler: injection with MeleeWeapon operand!", LogLevel.Info);
                }
                else
                {
                    Monitor?.Log("DrawHoverTextTranspiler: meleeWeaponOperand is null!", LogLevel.Warn);
                }

                found = true;
                Monitor?.Log("DrawHoverTextTranspiler: injection point found!", LogLevel.Info);
            }
        }

        if (!found)
            Monitor?.Log("DrawHoverTextTranspiler: injection point not found!", LogLevel.Warn);
    }
}
