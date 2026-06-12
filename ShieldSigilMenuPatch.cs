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
        harmony.Patch(constructor,
            postfix: new HarmonyMethod(typeof(ShieldSigilMenuPatch)
                .GetMethod(nameof(ConstructorPostfix))));

        Monitor?.Log("ShieldSigilMenuPatch applied!", LogLevel.Info);
    }

    public static void ConstructorPostfix(object __instance)
    {
        if (__instance is not IClickableMenu menu) return;

        var closeButtonField = typeof(IClickableMenu).GetField("upperRightCloseButton",
            BindingFlags.Public | BindingFlags.Instance);

        var closeButton = new ClickableTextureComponent(
            new Rectangle(Game1.uiViewport.Width - 68 - Game1.xEdge, 0, 68 + Game1.xEdge, 80),
            Game1.mobileSpriteSheet,
            new Rectangle(62, 0, 17, 17),
            4f);

        closeButtonField?.SetValue(menu, closeButton);

        Monitor?.Log("ShieldSigilMenu close button added", LogLevel.Info);
    }
}
