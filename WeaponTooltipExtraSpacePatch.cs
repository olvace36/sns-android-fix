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

        // patch drawHoverText แบบ string — method สั้นมาก แค่เรียก StringBuilder version
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
                transpiler: new HarmonyMethod(typeof(WeaponTooltipExtraSpacePatch)
                    .GetMethod(nameof(DrawHoverTextStringTranspiler))));
            Monitor?.Log("WeaponTooltipExtraSpacePatch applied!", LogLevel.Info);
        }
        else
            Monitor?.Log("drawHoverText(string) not found!", LogLevel.Warn);
    }

    public static int CalcExtra(Item hoveredItem, SpriteFont font)
    {
        if (hoveredItem is not MeleeWeapon weapon) return 0;

        int extra = 0;
        int lineHeight = Math.Max((int)font.MeasureString("TT").Y, 48);
        if (_getAlloying?.Invoke(null, new object[] { weapon }) is string) extra += lineHeight;
        if (_getCoating?.Invoke(null, new object[] { weapon }) is string) extra += lineHeight;
        if (_getGem?.Invoke(null, new object[] { weapon }) is string) extra += lineHeight;
        return extra;
    }

    public static IEnumerable<CodeInstruction> DrawHoverTextStringTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var calcExtra = AccessTools.Method(typeof(WeaponTooltipExtraSpacePatch), "CalcExtra");

        // หา call drawHoverText StringBuilder แล้วแทรก extra ก่อนส่ง boxHeightOverride
        // boxHeightOverride เป็น parameter ที่ 25 (index 24) ของ string version
        // ใน IL จะเป็น ldarg.s 24 ก่อน call drawHoverText StringBuilder

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

        bool found = false;
        for (int i = 0; i < codes.Count; i++)
        {
            // ก่อน call drawHoverText StringBuilder แทรก code เพิ่ม extra
            if (!found && drawHoverTextSB != null && codes[i].Calls(drawHoverTextSB))
            {
                // ตอนนี้ stack มี boxHeightOverride อยู่บนสุด
                // แทรก: stack = stack + CalcExtra(hoveredItem, font)
                // load hoveredItem (arg 9 ของ string version)
                yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)9);
                // load font (arg 2)
                yield return new CodeInstruction(OpCodes.Ldarg_2);
                // call CalcExtra
                yield return new CodeInstruction(OpCodes.Call, calcExtra);
                // add
                yield return new CodeInstruction(OpCodes.Add);

                found = true;
                Monitor?.Log("DrawHoverTextStringTranspiler: injection point found!", LogLevel.Info);
            }

            yield return codes[i];
        }

        if (!found)
            Monitor?.Log("DrawHoverTextStringTranspiler: injection point not found!", LogLevel.Warn);
    }
}
