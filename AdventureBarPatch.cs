using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using SwordAndSorcerySMAPI;

namespace SnsAndroidFix;

public class AdventureBarPatch
{
    internal static IMonitor? Monitor;
    public static bool AetherOnly = false;

    // AdventureBar (SwordAndSorcerySMAPI.Framework.Menus.AdventureBar.AdventureBar) is an
    // `internal` class in its own assembly, so we can't name it or cast to it directly from
    // here — reflection is the only way to reach its Type and its static Hide field. This
    // part can't be avoided the way the mana/position reads below could.
    private static Type? _adventureBarType;
    private static FieldInfo? _hideField;

    private const int AetherBarHeight = 56;
    private const int AetherBarMargin = 16;

    public static void Apply(Harmony harmony)
    {
        _adventureBarType = AccessTools.TypeByName(
            "SwordAndSorcerySMAPI.Framework.Menus.AdventureBar.AdventureBar");
        if (_adventureBarType == null)
        {
            Monitor?.Log("AdventureBar type not found!", LogLevel.Warn);
            return;
        }

        _hideField = _adventureBarType.GetField("Hide",
            BindingFlags.Public | BindingFlags.Static);

        var drawMethod = _adventureBarType.GetMethod("draw",
            new[] { typeof(SpriteBatch) });
        if (drawMethod != null)
            harmony.Patch(drawMethod,
                prefix: new HarmonyMethod(typeof(AdventureBarPatch)
                    .GetMethod(nameof(DrawPrefix))));
        else
            Monitor?.Log("AdventureBar draw method not found!", LogLevel.Warn);
    }

    static void DrawAetherBar(SpriteBatch b, int xPos, int yPos, int width)
    {
        // Direct call — SwordAndSorcerySMAPI.FarmerExtData and Extensions are both public,
        // so no reflection is needed here at all (previously: 1 MethodInfo.Invoke +
        // 2 FieldInfo.GetValue + 2 PropertyInfo.GetValue, every single frame this bar is
        // visible — i.e. constantly, since AetherOnly is normally left on).
        FarmerExtData extData = Game1.player.GetFarmerExtData();
        int mana = extData.mana.Value;
        int maxMana = extData.maxMana.Value;

        IClickableMenu.drawTextureBox(b, xPos, yPos, width, AetherBarHeight, Color.White);

        float num = maxMana > 0 ? Math.Min(1f, (float)mana / maxMana) : 0f;
        if (num > 0f)
            b.Draw(Game1.staminaRect,
                new Rectangle(xPos + 12, yPos + 8,
                    (int)((float)(width - 24) * num), 32), Color.Aqua);

        string text = $"{mana}/{maxMana}";
        b.DrawString(Game1.smallFont, text,
            new Vector2(xPos + (float)(width / 2) - Game1.smallFont.MeasureString(text).X / 2f,
            (float)(yPos + 10)), Color.Black);
    }

    // __instance is declared as IClickableMenu (the public base class AdventureBar inherits
    // from) instead of `object` — Harmony matches this fine since the real instance IS an
    // IClickableMenu, and it lets us read xPositionOnScreen/width/etc as normal public
    // fields instead of via reflection.
    public static bool DrawPrefix(IClickableMenu __instance, SpriteBatch b)
    {
        if (_adventureBarType == null) return true;

        bool hide = (bool?)_hideField?.GetValue(null) ?? false;
        if (hide || Game1.activeClickableMenu != null || Game1.CurrentEvent != null)
            return true;

        if (!AetherOnly) return true;

        int width = __instance.width;
        int vh = Game1.uiViewport.Height;
        int aetherX = AetherBarMargin;
        int aetherY = vh - AetherBarHeight - AetherBarMargin;

        DrawAetherBar(b, aetherX, aetherY, width);
        return false;
    }
}
