using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class GuidebookMenuPatch
{
    public static void Apply(Harmony harmony)
    {
        var guidebookType = AccessTools.TypeByName("SpaceCore.Guidebooks.GuidebookMenu");
        if (guidebookType == null) return;

        var constructor = guidebookType.GetConstructors()[0];
        harmony.Patch(constructor, postfix: new HarmonyMethod(
            typeof(GuidebookMenuPatch).GetMethod(nameof(Postfix))));
    }

    public static void Postfix(object __instance)
    {
        var closeButton = new ClickableTextureComponent(
            new Rectangle(Game1.uiViewport.Width - 68 - Game1.xEdge, 0, 68 + Game1.xEdge, 80),
            Game1.mobileSpriteSheet,
            new Rectangle(62, 0, 17, 17),
            4f, true);
        typeof(IClickableMenu).GetField("upperRightCloseButton",
            BindingFlags.Public | BindingFlags.Instance)
            ?.SetValue(__instance, closeButton);
    }
}
