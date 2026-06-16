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
            Monitor?.Log("WeaponTooltipExtraSpacePatch applied!", LogLevel.Info);
        }
        else
            Monitor?.Log("drawHoverText(StringBuilder) not found!", LogLevel.Warn);
    }

    public static int CalcExtra(Item hoveredItem)
    {
        if (hoveredItem is not MeleeWeapon weapon) return 0;
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
        bool firstGalaxySoul = false;

        for (int i = 0; i < codes.Count; i++)
        {
            yield return codes[i];

            // หา GetEnchantmentLevel<GalaxySoulEnchantment> ครั้งแรก โดยเช็ค string
            if (!found && !firstGalaxySoul &&
                codes[i].opcode == OpCodes.Callvirt &&
                codes[i].operand?.ToString()?.Contains("GetEnchantmentLevel") == true &&
                codes[i].operand?.ToString()?.Contains("GalaxySoul") == true)
            {
                firstGalaxySoul = true;
                Monitor?.Log($"Found GalaxySoul at IL[{i}]", LogLevel.Info);

                // เดิน forward หา stloc.3 (num3)
                for (int j = i + 1; j < Math.Min(i + 15, codes.Count); j++)
                {
                    yield return codes[j];
                    i = j;

                    if (codes[j].opcode == OpCodes.Stloc_3)
                    {
                        // แทรก: num3 += CalcExtra(hoveredItem)
                        yield return new CodeInstruction(OpCodes.Ldloc_3);
                        yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)9);
                        yield return new CodeInstruction(OpCodes.Call, calcExtra);
                        yield return new CodeInstruction(OpCodes.Add);
                        yield return new CodeInstruction(OpCodes.Stloc_3);

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
