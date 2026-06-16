using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Tools;

namespace SnsAndroidFix;

public class MonsterDamagePatch
{
    internal static IMonitor? Monitor;

    public static void Apply(Harmony harmony)
    {
        var monsterTakeDamageType = AccessTools.TypeByName("SwordAndSorcerySMAPI.MonsterTakeDamagePatch");
        if (monsterTakeDamageType == null)
        {
            Monitor?.Log("MonsterTakeDamagePatch type not found!", LogLevel.Warn);
            return;
        }

        var prefixMethod = monsterTakeDamageType.GetMethod("Prefix",
            BindingFlags.Public | BindingFlags.Static);
        if (prefixMethod == null)
        {
            Monitor?.Log("MonsterTakeDamagePatch.Prefix not found!", LogLevel.Warn);
            return;
        }

        harmony.Patch(prefixMethod,
            prefix: new HarmonyMethod(typeof(MonsterDamagePatch)
                .GetMethod(nameof(DamageLogPrefix))));

        Monitor?.Log("MonsterDamagePatch applied!", LogLevel.Info);
    }

    public static void DamageLogPrefix(Monster __instance, ref int damage, Farmer who)
    {
        var currentTool = who.CurrentTool;
        var weapon = currentTool as MeleeWeapon;
        if (weapon == null) return;

        var getAlloying = AccessTools.Method(
            AccessTools.TypeByName("SwordAndSorcerySMAPI.ArsenalExtensions"),
            "GetBladeAlloying",
            new[] { typeof(MeleeWeapon) });

        string? alloyId = getAlloying?.Invoke(null, new object[] { weapon }) as string;
        Monitor?.Log($"MonsterTakeDamage: weapon={weapon.Name} alloyId={alloyId ?? "null"} damage before={damage}", LogLevel.Info);
    }
}
