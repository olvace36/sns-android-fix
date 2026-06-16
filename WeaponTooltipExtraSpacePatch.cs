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

    public static int TestCall()
    {
        Monitor?.Log("TestCall was called!", LogLevel.Info);
        return 0;
    }

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
        var testCall = AccessTools.Method(
            typeof(WeaponTooltipExtraSpacePatch), "TestCall");

        bool found = false;
        object? meleeWeaponOperand = null;

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Ldloc_S &&
                codes[i].operand?.ToString()?.Contains("MeleeWeapon") == true)
            {
                meleeWeaponOperand = codes[i].operand;
            }

            if (!found && meleeWeaponOperand != null &&
                (codes[i].opcode == OpCodes.Br || codes[i].opcode == OpCodes.Br_S))
            {
                if (i > 0 && codes[i-1].opcode == OpCodes.Stloc_3)
                {
                    Monitor?.Log($"Found br at IL[{i}] — injecting before br", LogLevel.Info);

                    // TestCall ก่อนเพื่อตรวจว่า code ถูกเรียก
                    yield return new CodeInstruction(OpCodes.Call, testCall);
                    yield return new CodeInstruction(OpCodes.Pop);

                    // CalcExtra จริงๆ
                    yield return new CodeInstruction(OpCodes.Ldloc_3);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, meleeWeaponOperand);
                    yield return new CodeInstruction(OpCodes.Call, calcExtra);
                    yield return new CodeInstruction(OpCodes.Add);
                    yield return new CodeInstruction(OpCodes.Stloc_3);

                    found = true;
                    Monitor?.Log("DrawHoverTextTranspiler: injection complete!", LogLevel.Info);
                }
            }

            yield return codes[i];
        }

        if (!found)
            Monitor?.Log("DrawHoverTextTranspiler: injection point not found!", LogLevel.Warn);
    }
}
