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
    private static MethodInfo? _getAlloying;
    private static MethodInfo? _getGem;
    private static MethodInfo? _setAlloying;
    private static MethodInfo? _setGem;

    public static void Apply(Harmony harmony)
    {
        var extensionsType = AccessTools.TypeByName("SwordAndSorcerySMAPI.ArsenalExtensions");
        if (extensionsType != null)
        {
            foreach (var m in extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(MeleeWeapon))
                {
                    if (m.Name == "GetBladeAlloying") _getAlloying = m;
                    if (m.Name == "GetExquisiteGemstone") _getGem = m;
                }
                if (ps.Length == 2 && ps[0].ParameterType == typeof(MeleeWeapon) && ps[1].ParameterType == typeof(string))
                {
                    if (m.Name == "SetBladeAlloying") _setAlloying = m;
                    if (m.Name == "SetExquisiteGemstone") _setGem = m;
                }
            }
        }

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
                .GetMethod(nameof(BeforePrefix))),
            postfix: new HarmonyMethod(typeof(MonsterDamagePatch)
                .GetMethod(nameof(AfterPrefix))));

        Monitor?.Log("MonsterDamagePatch applied!", LogLevel.Info);
    }

    static string? MapAlloyId(string? id) => id switch
    {
        "(O)DN.SnS_PureCopperOre"      => "(O)334",
        "(O)DN.SnS_PureIronOre"        => "(O)335",
        "(O)DN.SnS_PureGoldOre"        => "(O)336",
        "(O)DN.SnS_PureIridiumOre"     => "(O)337",
        "(O)DN.SnS_PureRadioactiveOre" => "(O)910",
        _ => null
    };

    static string? MapGemId(string? id) => id switch
    {
        "(O)DN.SnS_ExquisiteAquamarine" => "(O)ExquisiteAquamarine",
        _ => null
    };

    static string? GetId(MeleeWeapon w, MethodInfo? m)
        => m?.Invoke(null, new object[] { w }) as string;

    static void SetId(MeleeWeapon w, MethodInfo? m, string id)
        => m?.Invoke(null, new object[] { w, id });

    public static void BeforePrefix(Monster __instance, ref int damage, Farmer who,
        out (string? origAlloy, string? origGem) __state)
    {
        __state = (null, null);

        var weapon = who.CurrentTool as MeleeWeapon;
        if (weapon == null) return;

        string? alloyId = GetId(weapon, _getAlloying);
        string? mappedAlloy = MapAlloyId(alloyId);
        if (mappedAlloy != null)
        {
            __state.origAlloy = alloyId;
            SetId(weapon, _setAlloying, mappedAlloy);
            Monitor?.Log($"BeforePrefix: {weapon.Name} alloy {alloyId} → {mappedAlloy} damage before={damage}", LogLevel.Info);
        }

        string? gemId = GetId(weapon, _getGem);
        string? mappedGem = MapGemId(gemId);
        if (mappedGem != null)
        {
            __state.origGem = gemId;
            SetId(weapon, _setGem, mappedGem);
            Monitor?.Log($"BeforePrefix: {weapon.Name} gem {gemId} → {mappedGem}", LogLevel.Info);
        }
    }

    public static void AfterPrefix(Monster __instance, ref int damage, Farmer who,
        (string? origAlloy, string? origGem) __state)
    {
        var weapon = who.CurrentTool as MeleeWeapon;
        if (weapon == null) return;

        if (__state.origAlloy != null)
        {
            Monitor?.Log($"AfterPrefix: {weapon.Name} damage after={damage} alloy={__state.origAlloy}", LogLevel.Info);
            SetId(weapon, _setAlloying, __state.origAlloy);
        }

        if (__state.origGem != null)
            SetId(weapon, _setGem, __state.origGem);
    }
}
