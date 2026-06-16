using System;
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

public class WeaponTooltipPatch
{
    internal static IMonitor? Monitor;
    private static ITranslationHelper? _translation;
    private static MethodInfo? _getAlloying;
    private static MethodInfo? _getCoating;
    private static MethodInfo? _getGem;

    public static void Apply(Harmony harmony, ITranslationHelper translation)
    {
        _translation = translation;

        var extensionsType = AccessTools.TypeByName("SwordAndSorcerySMAPI.ArsenalExtensions");
        if (extensionsType == null)
        {
            Monitor?.Log("ArsenalExtensions not found!", LogLevel.Warn);
            return;
        }

        foreach (var m in extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var ps = m.GetParameters();
            if (ps.Length != 1 || ps[0].ParameterType != typeof(MeleeWeapon)) continue;
            if (m.Name == "GetBladeAlloying") _getAlloying = m;
            if (m.Name == "GetBladeCoating") _getCoating = m;
            if (m.Name == "GetExquisiteGemstone") _getGem = m;
        }

        Monitor?.Log($"WeaponTooltipPatch: getAlloying={_getAlloying != null}, getCoating={_getCoating != null}, getGem={_getGem != null}", LogLevel.Info);

        var drawTooltipPostfix = new HarmonyMethod(typeof(WeaponTooltipPatch)
            .GetMethod(nameof(DrawTooltipPostfix)));
        drawTooltipPostfix.priority = Priority.Low;

        harmony.Patch(
            AccessTools.Method(typeof(MeleeWeapon), "drawTooltip"),
            postfix: drawTooltipPostfix);

        Monitor?.Log("WeaponTooltipPatch applied!", LogLevel.Info);
    }

    static string GetText(string key)
    {
        var t = _translation?.Get(key);
        return t?.HasValue() == true ? t.ToString() : "";
    }

    public static void DrawTooltipPostfix(MeleeWeapon __instance,
        SpriteBatch spriteBatch, ref int x, ref int y, SpriteFont font)
    {
        // skip ถ้า DrawTooltipPrefix กำลังวาดอยู่
        if (WeaponTooltipExtraSpacePatch.Drawing) return;

        Monitor?.Log($"DrawTooltipPostfix called: {__instance.Name} x={x} y={y}", LogLevel.Info);

        string? alloyId = _getAlloying?.Invoke(null, new object[] { __instance }) as string;
        if (alloyId == null)
            ((Item)__instance).modData.TryGetValue("swordandsorcery/BladeAlloying", out alloyId);

        if (alloyId != null)
        {
            string text = GetText($"tooltip.alloying.{alloyId}");
            if (text.Length > 0)
            {
                Monitor?.Log($"DrawTooltip: alloy={alloyId} text={text} y={y}", LogLevel.Info);
                Utility.drawTextWithShadow(spriteBatch, text, font,
                    new Vector2((float)(x + 16 + 44), (float)(y + 16 + 12)),
                    Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
                y += Math.Max((int)font.MeasureString("TT").Y, 48);
            }
        }

        string? coatingId = _getCoating?.Invoke(null, new object[] { __instance }) as string;
        if (coatingId == null)
            ((Item)__instance).modData.TryGetValue("swordandsorcery/BladeCoating", out coatingId);

        if (coatingId != null)
        {
            string text = GetText($"tooltip.coating.{coatingId}");
            if (text.Length > 0)
            {
                Monitor?.Log($"DrawTooltip: coating={coatingId} text={text} y={y}", LogLevel.Info);
                Utility.drawTextWithShadow(spriteBatch, text, font,
                    new Vector2((float)(x + 16 + 44), (float)(y + 16 + 12)),
                    Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
                y += Math.Max((int)font.MeasureString("TT").Y, 48);
            }
        }

        string? gemId = _getGem?.Invoke(null, new object[] { __instance }) as string;
        if (gemId == null)
            ((Item)__instance).modData.TryGetValue("swordandsorcery/ExquisiteGemstone", out gemId);

        if (gemId != null)
        {
            string text = GetText($"tooltip.gem.{gemId}");
            if (text.Length > 0)
            {
                Monitor?.Log($"DrawTooltip: gem={gemId} text={text} y={y}", LogLevel.Info);
                Utility.drawTextWithShadow(spriteBatch, text, font,
                    new Vector2((float)(x + 16 + 44), (float)(y + 16 + 12)),
                    Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
                y += Math.Max((int)font.MeasureString("TT").Y, 48);
            }
        }
    }
}
