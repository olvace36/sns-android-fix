using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
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
        _getAlloying = extensionsType?.GetMethod("GetBladeAlloying", new[] { typeof(MeleeWeapon) });
        _getCoating = extensionsType?.GetMethod("GetBladeCoating", new[] { typeof(MeleeWeapon) });
        _getGem = extensionsType?.GetMethod("GetExquisiteGemstone", new[] { typeof(MeleeWeapon) });

        harmony.Patch(
            AccessTools.Method(typeof(MeleeWeapon), "drawTooltip"),
            postfix: new HarmonyMethod(typeof(WeaponTooltipPatch)
                .GetMethod(nameof(DrawTooltipPostfix))));
    }

    public static void DrawTooltipPostfix(MeleeWeapon __instance,
        SpriteBatch spriteBatch, ref int x, ref int y, SpriteFont font)
    {
        // Blade Alloying
        string? alloyId = _getAlloying?.Invoke(null, new object[] { __instance }) as string;
        if (alloyId != null)
        {
            string alloyText = alloyId switch
            {
                "(O)334" => "+5% Damage",
                "(O)335" => "+10% Damage",
                "(O)336" => "+15% Damage",
                "(O)337" => "+20% Damage",
                "(O)910"  => "+25% Damage",
                _ => ""
            };
            if (alloyText.Length > 0)
            {
                Utility.drawTextWithShadow(spriteBatch, alloyText, font,
                    new Vector2((float)(x + 16 + 44), (float)(y - 32)),
                    new Color(0, 120, 0), 1f, -1f, -1, -1, 1f, 3);
            }
        }

        // Blade Coating
        string? coatingId = _getCoating?.Invoke(null, new object[] { __instance }) as string;
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
                Utility.drawTextWithShadow(spriteBatch, coatingText, font,
                    new Vector2((float)(x + 16 + 44), (float)(y - 16)),
                    new Color(80, 0, 150), 1f, -1f, -1, -1, 1f, 3);
            }
        }

        // Exquisite Gem
        string? gemId = _getGem?.Invoke(null, new object[] { __instance }) as string;
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
                Utility.drawTextWithShadow(spriteBatch, gemText, font,
                    new Vector2((float)(x + 16 + 44), (float)(y - 48)),
                    new Color(180, 120, 0), 1f, -1f, -1, -1, 1f, 3);
            }
        }
    }
}
