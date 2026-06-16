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

    // cache farmerExtData reflection
    private static MethodInfo? _getFarmerExtData;
    private static FieldInfo? _manaField;
    private static FieldInfo? _maxManaField;

    private const int AetherBarHeight = 56;

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

        // cache farmerExtData
        var farmerExtDataType = AccessTools.TypeByName("SwordAndSorcerySMAPI.FarmerExtData");
        _getFarmerExtData = AccessTools.Method(
            AccessTools.TypeByName("SwordAndSorcerySMAPI.FarmerExtDataExtensions"),
            "GetFarmerExtData",
            new[] { typeof(Farmer) });
        if (farmerExtDataType != null)
        {
            _manaField = farmerExtDataType.GetField("mana",
                BindingFlags.Public | BindingFlags.Instance);
            _maxManaField = farmerExtDataType.GetField("maxMana",
                BindingFlags.Public | BindingFlags.Instance);
        }

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

    static void DrawAetherBar(SpriteBatch b, int xPos, int yPos, int width)
    {
        if (_getFarmerExtData == null) return;

        var farmerExtData = _getFarmerExtData.Invoke(null, new object[] { Game1.player });
        if (farmerExtData == null) return;

        int mana = (int)(_manaField?.GetValue(farmerExtData) ?? 0);
        int maxMana = (int)(_maxManaField?.GetValue(farmerExtData) ?? 0);

        float num = maxMana > 0 ? Math.Min(1f, (float)mana / maxMana) : 0f;

        // วาด bar ตาม original code — x=12 เสมอ
        if (num > 0f)
            b.Draw(Game1.staminaRect,
                new Rectangle(12, yPos,
                    (int)((float)(width - 24) * num), 32), Color.Aqua);

        string text = $"{mana}/{maxMana}";
        b.DrawString(Game1.smallFont, text,
            new Vector2((float)(width / 2) - Game1.smallFont.MeasureString(text).X / 2f,
            (float)(yPos + 2)), Color.Black);
    }

    public static bool DrawPrefix(object __instance, SpriteBatch b)
    {
        if (_adventureBarType == null) return true;

        bool hide = (bool?)_hideField?.GetValue(null) ?? false;
        if (hide || Game1.activeClickableMenu != null || Game1.CurrentEvent != null)
            return true;

        if (!AetherOnly) return true;

        // มุมซ้ายล่าง
        var (xPos, yPos, width, height) = GetBounds(__instance);
        int vh = Game1.uiViewport.Height;
        int aetherY = vh - AetherBarHeight - 16;

        DrawAetherBar(b, xPos, aetherY, width);
        return false;
    }
}
