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

        // ลบ DrawTooltipPostfix ออก เพราะ DrawTooltipPrefix จัดการแล้ว
        Monitor?.Log("WeaponTooltipPatch applied!", LogLevel.Info);
    }
}
