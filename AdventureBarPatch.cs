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

    // AdventureBar (SwordAndSorcerySMAPI.Framework.Menus.AdventureBar.AdventureBar) is an
    // `internal` class in its own assembly, so we can't name it or cast to it directly from
    // here — reflection is the only way to reach its Type and its static Hide field. This
    // part can't be avoided the way the mana/position reads below could.
    private static Type? _adventureBarType;
    private static FieldInfo? _hideField;

    private static MethodInfo? _getFarmerExtData;
    private static FieldInfo? _manaField;
    private static FieldInfo? _maxManaField;
    private static PropertyInfo? _netIntValueProperty;

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

        var farmerExtDataType = AccessTools.TypeByName("SwordAndSorcerySMAPI.FarmerExtData");
        var extensionsType = AccessTools.TypeByName("SwordAndSorcerySMAPI.Extensions");

        _getFarmerExtData = extensionsType?.GetMethod("GetFarmerExtData",
            BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(Farmer) }, null);

        if (farmerExtDataType != null)
        {
            _manaField = farmerExtDataType.GetField("mana",
                BindingFlags.Public | BindingFlags.Instance);
            _maxManaField = farmerExtDataType.GetField("maxMana",
                BindingFlags.Public | BindingFlags.Instance);
            if (_manaField != null)
                _netIntValueProperty = _manaField.FieldType
                    .GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        }

        var drawMethod = _adventureBarType.GetMethod("draw",
            new[] { typeof(SpriteBatch) });
        if (drawMethod != null)
            harmony.Patch(drawMethod,
                prefix: new HarmonyMethod(typeof(AdventureBarPatch)
                    .GetMethod(nameof(DrawPrefix))));
        else
            Monitor?.Log("AdventureBar draw method not found!", LogLevel.Warn);
    }

    private static object[]? _farmerExtDataArgs;
    private static int _cachedMana;
    private static int _cachedMaxMana;
    private static int _framesSinceManaRead = int.MaxValue; // force a read on the first call

    // Mana only changes when the player casts something or regenerates over time — it doesn't
    // need to be re-read via reflection on every single draw call. Refreshing every 6 frames
    // (~4x/sec even at 24fps) is still visually smooth for a bar that fills gradually, and
    // cuts the reflection calls here by ~85%.
    private const int RefreshEveryNFrames = 6;

    static void DrawAetherBar(SpriteBatch b, int xPos, int yPos, int width)
    {
        if (_getFarmerExtData == null) return;

        _framesSinceManaRead++;
        if (_framesSinceManaRead >= RefreshEveryNFrames)
        {
            _framesSinceManaRead = 0;

            // Reused across frames instead of allocating a new object[1] every single draw call.
            _farmerExtDataArgs ??= new object[] { Game1.player };

            var farmerExtData = _getFarmerExtData.Invoke(null, _farmerExtDataArgs);
            if (farmerExtData != null)
            {
                var manaNetInt = _manaField?.GetValue(farmerExtData);
                var maxManaNetInt = _maxManaField?.GetValue(farmerExtData);

                _cachedMana = (int?)_netIntValueProperty?.GetValue(manaNetInt) ?? 0;
                _cachedMaxMana = (int?)_netIntValueProperty?.GetValue(maxManaNetInt) ?? 0;
            }
        }

        int mana = _cachedMana;
        int maxMana = _cachedMaxMana;

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
