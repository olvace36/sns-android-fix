using System.Collections;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using SwordAndSorcerySMAPI;

namespace SnsAndroidFix;

[HarmonyPatch(typeof(ArsenalMenu), MethodType.Constructor)]
public class ArsenalMenuPatch
{
    internal static IMonitor? Monitor;

    static void Postfix(ArsenalMenu __instance)
    {
        var field = typeof(ArsenalMenu).GetField("invMenu",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return;

        var invMenu = field.GetValue(__instance);
        if (invMenu == null) return;

        var type = invMenu.GetType();

        // log ชื่อ properties ทั้งหมด
        foreach (var prop in type.GetProperties())
            Monitor?.Log($"prop: {prop.Name}", LogLevel.Info);

        // log ชื่อ fields ทั้งหมด
        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            Monitor?.Log($"field: {f.Name}", LogLevel.Info);
    }
}
