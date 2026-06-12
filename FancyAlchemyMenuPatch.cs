using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class FancyAlchemyMenuPatch
{
    internal static IMonitor? Monitor;

    public static void Apply(Harmony harmony)
    {
        var menuType = AccessTools.TypeByName("SwordAndSorcerySMAPI.Framework.Menus.FancyAlchemyMenu");
        if (menuType == null)
        {
            Monitor?.Log("FancyAlchemyMenu type not found!", LogLevel.Error);
            return;
        }

        var constructor = menuType.GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            null, System.Type.EmptyTypes, null)
            ?? menuType.GetConstructors()[0];

        if (constructor == null)
        {
            Monitor?.Log("FancyAlchemyMenu constructor not found!", LogLevel.Error);
            return;
        }

        harmony.Patch(constructor,
            postfix: new HarmonyMethod(typeof(FancyAlchemyMenuPatch)
                .GetMethod(nameof(ConstructorPostfix))));

        Monitor?.Log("FancyAlchemyMenuPatch applied!", LogLevel.Info);
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

        Monitor?.Log($"FancyAlchemyMenu close button added at x={x} y={y}", LogLevel.Info);
    }
}
