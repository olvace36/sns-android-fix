using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceCore.Guidebooks;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

[HarmonyPatch(typeof(GuidebookMenu), MethodType.Constructor)]
public class GuidebookMenuPatch
{
    static void Postfix(GuidebookMenu __instance)
    {
        // เพิ่มปุ่ม X
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
