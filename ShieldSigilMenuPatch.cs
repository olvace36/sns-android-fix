using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class ShieldSigilMenuPatch
{
    internal static IMonitor? Monitor;

    public static void Apply(Harmony harmony)
    {
        var menuType = AccessTools.TypeByName("SwordAndSorcerySMAPI.Framework.Menus.ShieldSigilMenu");
        if (menuType == null)
        {
            Monitor?.Log("ShieldSigilMenu type not found!", LogLevel.Error);
            return;
        }

        var constructor = menuType.GetConstructors()[0];
        if (constructor == null)
        {
            Monitor?.Log("ShieldSigilMenu constructor not found!", LogLevel.Error);
            return;
        }

        harmony.Patch(constructor,
            postfix: new HarmonyMethod(typeof(ShieldSigilMenuPatch)
                .GetMethod(nameof(ConstructorPostfix))));

        Monitor?.Log("ShieldSigilMenuPatch applied!", LogLevel.Info);
    }

    public static void ConstructorPostfix(object __instance)
    {
        if (__instance is not IClickableMenu menu) return;

        int x = menu.xPositionOnScreen + menu.width - 32;
        int y = menu.yPositionOnScreen - 8;

        menu.upperRightCloseButton = new ClickableTextureComponent(
            new Rectangle(x, y, 17 * 4, 17 * 4),
            Game1.mobileSpriteSheet,
            new Rectangle(62, 0, 17, 17),
            4f);

        Monitor?.Log($"ShieldSigilMenu close button added at x={x} y={y}", LogLevel.Info);
    }
}
