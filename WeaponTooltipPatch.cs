using System;
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

public class WeaponTooltipPatch
{
    internal static IMonitor? Monitor;
    private static MethodInfo? _getAlloying;
    private static MethodInfo? _getCoating;
    private static MethodInfo? _getGem;

    public static void Apply(Harmony harmony)
    {
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

        Monitor?.Log($"WeaponTooltipPatch: getAlloying={_getAlloying?.DeclaringType?.FullName}.{_getAlloying?.Name ?? "null"}", LogLevel.Info);
        Monitor?.Log($"WeaponTooltipPatch: getCoating={_getCoating?.DeclaringType?.FullName}.{_getCoating?.Name ?? "null"}", LogLevel.Info);
        Monitor?.Log($"WeaponTooltipPatch: getGem={_getGem?.DeclaringType?.FullName}.{_getGem?.Name ?? "null"}", LogLevel.Info);

        harmony.Patch(
            AccessTools.Method(typeof(MeleeWeapon), "drawTooltip"),
            postfix: new HarmonyMethod(typeof(WeaponTooltipPatch)
                .GetMethod(nameof(DrawTooltipPostfix))));

        harmony.Patch(
            AccessTools.Method(typeof(MeleeWeapon),
                "getExtraSpaceNeededForTooltipSpecialIcons"),
            postfix: new HarmonyMethod(typeof(WeaponTooltipPatch)
                .GetMethod(nameof(ExtraSpacePostfix))));

        Monitor?.Log("WeaponTooltipPatch applied!", LogLevel.Info);
    }

    static int CountLines(MeleeWeapon weapon)
    {
        int lines = 0;
        if (_getAlloying?.Invoke(null, new object[] { weapon }) is string alloy && alloy switch
        {
            "(O)334" or "(O)335" or "(O)336" or "(O)337" or "(O)910" => true,
            _ => false
        }) lines++;

        if (_getCoating?.Invoke(null, new object[] { weapon }) is string coat && coat switch
        {
            "(O)766" or "(O)767" or "(O)768" or "(O)684" or "(O)769" => true,
            _ => false
        }) lines++;

        if (_getGem?.Invoke(null, new object[] { weapon }) is string gem && gem switch
        {
            "(O)DN.SnS_ExquisiteEmerald" or "(O)DN.SnS_ExquisiteRuby" or
            "(O)DN.SnS_ExquisiteJade" or "(O)DN.SnS_ExquisiteAmethyst" or
            "(O)DN.SnS_ExquisiteDiamond" or "(O)ExquisiteAquamarine" or
            "(O)DN.SnS_ExquisiteTopaz" => true,
            _ => false
        }) lines++;

        return lines;
    }

    public static void ExtraSpacePostfix(MeleeWeapon __instance,
        SpriteFont font, int minWidth, int horizontalBuffer, int startingHeight,
        StringBuilder descriptionText, string boldTitleText,
        int moneyAmountToDisplayAtBottom, ref Point __result)
    {
        int lines = CountLines(__instance);
        if (lines == 0) return;
        int extraHeight = lines * ((int)font.MeasureString("TT").Y + 4);
        Monitor?.Log($"ExtraSpacePostfix: weapon={__instance.Name} lines={lines} extraHeight={extraHeight}", LogLevel.Info);
        __result = new Point(__result.X, __result.Y + extraHeight);
    }

    public static void DrawTooltipPostfix(MeleeWeapon __instance,
        SpriteBatch spriteBatch, ref int x, ref int y, SpriteFont font)
    {
        Monitor?.Log($"DrawTooltipPostfix called: {__instance.Name} x={x} y={y}", LogLevel.Info);

        string? alloyId = _getAlloying?.Invoke(null, new object[] { __instance }) as string;
        Monitor?.Log($"alloyId={alloyId ?? "null"}", LogLevel.Info);

        // ถ้า alloyId null ลองดึงจาก modData โดยตรง
        if (alloyId == null)
        {
            alloyId = ((Item)__instance).modData.TryGetValue("swordandsorcery/BladeAlloying", out string val) ? val : null;
            Monitor?.Log($"alloyId from modData={alloyId ?? "null"}", LogLevel.Info);
        }

        if (alloyId != null)
        {
string alloyText = alloyId switch
{
    "(O)DN.SnS_PureCopperOre"      => "+5% Damage",
    "(O)DN.SnS_PureIronOre"        => "+10% Damage",
    "(O)DN.SnS_PureGoldOre"        => "+15% Damage",
    "(O)DN.SnS_PureIridiumOre"     => "+20% Damage",
    "(O)DN.SnS_PureRadioactiveOre" => "+25% Damage",
    _ => ""
};
            if (alloyText.Length > 0)
            {
                Monitor?.Log($"DrawTooltip: alloy={alloyId} text={alloyText} y={y}", LogLevel.Info);
                Utility.drawTextWithShadow(spriteBatch, alloyText, font,
                    new Vector2((float)(x + 16 + 44), (float)(y + 16 + 12)),
                    new Color(0, 120, 0), 1f, -1f, -1, -1, 1f, 3);
                y += Math.Max((int)font.MeasureString("TT").Y, 48);
            }
        }

        string? coatingId = _getCoating?.Invoke(null, new object[] { __instance }) as string;
        if (coatingId == null)
            coatingId = ((Item)__instance).modData.TryGetValue("swordandsorcery/BladeCoating", out string val) ? val : null;

        if (coatingId != null)
        {
            string coatingText = coatingId switch
            {
                "(O)766" => "Slows enemies",
                "(O)767" => "Hits flying enemies",
                "(O)768" => "Explodes on kill",
                "(O)684" => "Double drop on kill",
                "(O)769" => "Attacks all directions",
                _ => ""
            };
            if (coatingText.Length > 0)
            {
                Monitor?.Log($"DrawTooltip: coating={coatingId} text={coatingText} y={y}", LogLevel.Info);
                Utility.drawTextWithShadow(spriteBatch, coatingText, font,
                    new Vector2((float)(x + 16 + 44), (float)(y + 16 + 12)),
                    new Color(80, 0, 150), 1f, -1f, -1, -1, 1f, 3);
                y += Math.Max((int)font.MeasureString("TT").Y, 48);
            }
        }

        string? gemId = _getGem?.Invoke(null, new object[] { __instance }) as string;
        if (gemId == null)
            gemId = ((Item)__instance).modData.TryGetValue("swordandsorcery/ExquisiteGemstone", out string val) ? val : null;

        if (gemId != null)
        {
            string gemText = gemId switch
            {
                "(O)DN.SnS_ExquisiteEmerald"  => "On hit: +1.5 Speed (5s)",
                "(O)DN.SnS_ExquisiteRuby"     => "On hit: 30% damage x3",
                "(O)DN.SnS_ExquisiteJade"     => "On hit: +2 Stamina",
                "(O)DN.SnS_ExquisiteAmethyst" => "On hit: 15% Stun (3s)",
                "(O)DN.SnS_ExquisiteDiamond"  => "On hit: +1 HP",
                "(O)ExquisiteAquamarine"      => "On hit: 15% Crit multiplier",
                "(O)DN.SnS_ExquisiteTopaz"    => "-15% damage taken",
                _ => ""
            };
            if (gemText.Length > 0)
            {
                Monitor?.Log($"DrawTooltip: gem={gemId} text={gemText} y={y}", LogLevel.Info);
                Utility.drawTextWithShadow(spriteBatch, gemText, font,
                    new Vector2((float)(x + 16 + 44), (float)(y + 16 + 12)),
                    new Color(180, 120, 0), 1f, -1f, -1, -1, 1f, 3);
                y += Math.Max((int)font.MeasureString("TT").Y, 48);
            }
        }
    }
}
