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
        Monitor?.Log("AdventureBar type found!", LogLevel.Info);

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

        var farmerExtDataType = AccessTools.TypeByName("SwordAndSorcerySMAPI.FarmerExtData");
        var extensionsType = AccessTools.TypeByName("SwordAndSorcerySMAPI.Extensions");

        Monitor?.Log($"FarmerExtData type={farmerExtDataType?.Name ?? "null"}", LogLevel.Info);
        Monitor?.Log($"Extensions type={extensionsType?.Name ?? "null"}", LogLevel.Info);

        // แก้จาก FarmerExtDataExtensions เป็น Extensions
        _getFarmerExtData = extensionsType?.GetMethod("GetFarmerExtData",
            BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(Farmer) }, null);

        Monitor?.Log($"GetFarmerExtData={_getFarmerExtData != null}", LogLevel.Info);

        if (farmerExtDataType != null)
        {
            _manaField = farmerExtDataType.GetField("mana",
                BindingFlags.Public | BindingFlags.Instance);
            _maxManaField = farmerExtDataType.GetField("maxMana",
                BindingFlags.Public | BindingFlags.Instance);
            Monitor?.Log($"manaField={_manaField != null}, maxManaField={_maxManaField != null}", LogLevel.Info);
        }

        var drawMethod = _adventureBarType.GetMethod("draw",
            new[] { typeof(SpriteBatch) });
        if (drawMethod != null)
        {
            harmony.Patch(drawMethod,
                prefix: new HarmonyMethod(typeof(AdventureBarPatch)
                    .GetMethod(nameof(DrawPrefix))));
            Monitor?.Log("AdventureBarPatch draw patch applied!", LogLevel.Info);
        }
        else
        {
            Monitor?.Log("draw method not found!", LogLevel.Warn);
        }
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
        if (_getFarmerExtData == null)
        {
            Monitor?.Log("DrawAetherBar: _getFarmerExtData is null!", LogLevel.Warn);
            return;
        }

        var farmerExtData = _getFarmerExtData.Invoke(null, new object[] { Game1.player });
        if (farmerExtData == null)
        {
            Monitor?.Log("DrawAetherBar: farmerExtData is null!", LogLevel.Warn);
            return;
        }

        int mana = (int)(_manaField?.GetValue(farmerExtData) ?? 0);
        int maxMana = (int)(_maxManaField?.GetValue(farmerExtData) ?? 0);

        Monitor?.Log($"DrawAetherBar: x={xPos} y={yPos} w={width} mana={mana}/{maxMana}", LogLevel.Info);

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

        // กลางจอ
        int vh = Game1.uiViewport.Height;
        int vw = Game1.uiViewport.Width;
        int aetherX = vw / 2 - width / 2;
        int aetherY = vh / 2 - AetherBarHeight / 2;

        Monitor?.Log($"DrawPrefix: AetherOnly=true x={aetherX} y={aetherY} w={width}", LogLevel.Info);

        DrawAetherBar(b, aetherX, aetherY, width);
        return false;
    }
}
