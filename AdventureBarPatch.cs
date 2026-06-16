using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

public class AdventureBarPatch
{
    internal static IMonitor? Monitor;
    public static bool AetherOnly = false;

    private static Type? _adventureBarType;
    private static FieldInfo? _hideField;
    private static FieldInfo? _xField;
    private static FieldInfo? _yField;
    private static FieldInfo? _heightField;
    private static FieldInfo? _widthField;

    private const int AetherBarHeight = 56;
    private const int AetherBarOffsetFromBottom = 12;

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
        _xField = typeof(IClickableMenu).GetField("xPositionOnScreen",
            BindingFlags.Public | BindingFlags.Instance);
        _yField = typeof(IClickableMenu).GetField("yPositionOnScreen",
            BindingFlags.Public | BindingFlags.Instance);
        _heightField = typeof(IClickableMenu).GetField("height",
            BindingFlags.Public | BindingFlags.Instance);
        _widthField = typeof(IClickableMenu).GetField("width",
            BindingFlags.Public | BindingFlags.Instance);

        var drawMethod = _adventureBarType.GetMethod("draw",
            new[] { typeof(SpriteBatch) });
        if (drawMethod != null)
            harmony.Patch(drawMethod,
                prefix: new HarmonyMethod(typeof(AdventureBarPatch)
                    .GetMethod(nameof(DrawPrefix))));
    }

    static (int x, int y, int w, int h) GetBounds(object instance)
    {
        int x = (int?)_xField?.GetValue(instance) ?? 0;
        int y = (int?)_yField?.GetValue(instance) ?? 0;
        int w = (int?)_widthField?.GetValue(instance) ?? 0;
        int h = (int?)_heightField?.GetValue(instance) ?? 0;
        return (x, y, w, h);
    }

    static Rectangle GetAetherOnlyBounds(int xPos, int width)
    {
        int vh = Game1.uiViewport.Height;
        int aetherY = vh / 2 - AetherBarHeight / 2;
        return new Rectangle(xPos, aetherY, width, AetherBarHeight);
    }

    static void DrawAetherBar(SpriteBatch b, int xPos, int yPos, int width)
    {
        var farmerExtDataMethod = AccessTools.Method(
            AccessTools.TypeByName("SwordAndSorcerySMAPI.FarmerExtDataExtensions"),
            "GetFarmerExtData",
            new[] { typeof(Farmer) });
        if (farmerExtDataMethod == null) return;

        var farmerExtData = farmerExtDataMethod.Invoke(null, new object[] { Game1.player });
        if (farmerExtData == null) return;

        var manaField = farmerExtData.GetType()
            .GetField("mana", BindingFlags.Public | BindingFlags.Instance);
        var maxManaField = farmerExtData.GetType()
            .GetField("maxMana", BindingFlags.Public | BindingFlags.Instance);

        int mana = (int)(manaField?.GetValue(farmerExtData) ?? 0);
        int maxMana = (int)(maxManaField?.GetValue(farmerExtData) ?? 0);

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

    public static bool DrawPrefix(object __instance, SpriteBatch b)
    {
        if (_adventureBarType == null) return true;

        bool hide = (bool?)_hideField?.GetValue(null) ?? false;
        if (hide || Game1.activeClickableMenu != null || Game1.CurrentEvent != null)
            return true;

        if (!AetherOnly) return true;

        var (xPos, yPos, width, height) = GetBounds(__instance);
        var bounds = GetAetherOnlyBounds(xPos, width);
        DrawAetherBar(b, bounds.X, bounds.Y, bounds.Width);
        return false;
    }
}
