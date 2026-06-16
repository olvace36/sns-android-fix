using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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

        var clickMethod = _adventureBarType.GetMethod("receiveLeftClick",
            BindingFlags.Public | BindingFlags.Instance);
        if (clickMethod != null)
            harmony.Patch(clickMethod,
                prefix: new HarmonyMethod(typeof(AdventureBarPatch)
                    .GetMethod(nameof(ReceiveLeftClickPrefix))));
    }

    public static bool DrawPrefix(object __instance, SpriteBatch b)
    {
        if (_adventureBarType == null) return true;

        bool hide = (bool?)_hideField?.GetValue(null) ?? false;
        if (hide || Game1.activeClickableMenu != null || Game1.CurrentEvent != null)
            return true; // ให้ original จัดการ return เร็ว

        if (!AetherOnly) return true; // วาดปกติ

        // AetherOnly mode — วาดแค่ aether bar
        var xPos = (int)_adventureBarType
            .GetField("xPositionOnScreen", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance)!;
        var yPos = (int)_adventureBarType
            .GetField("yPositionOnScreen", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance)!;
        var height = (int)_adventureBarType
            .GetField("height", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance)!;
        var width = (int)_adventureBarType
            .GetField("width", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance)!;

        var farmerExtDataMethod = AccessTools.Method(
            AccessTools.TypeByName("SwordAndSorcerySMAPI.FarmerExtDataExtensions"),
            "GetFarmerExtData",
            new[] { typeof(Farmer) });
        if (farmerExtDataMethod == null) return true;

        var farmerExtData = farmerExtDataMethod.Invoke(null, new object[] { Game1.player });
        if (farmerExtData == null) return true;

        var mana = (int)(farmerExtData.GetType()
            .GetField("mana", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(farmerExtData) ?? 0);
        var maxMana = (int)(farmerExtData.GetType()
            .GetField("maxMana", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(farmerExtData) ?? 0);

        // วาด aether bar box
        IClickableMenu.drawTextureBox(b, xPos, yPos, width, 56, Color.White);

        float num = maxMana > 0 ? Math.Min(1f, (float)mana / maxMana) : 0f;
        var barColor = Color.Aqua;
        if (num > 0f)
            b.Draw(Game1.staminaRect,
                new Rectangle(xPos + 12, yPos + 8,
                    (int)((float)(width - 24) * num), 32), barColor);

        string text = $"{mana}/{maxMana}";
        b.DrawString(Game1.smallFont, text,
            new Vector2((float)(width / 2) - Game1.smallFont.MeasureString(text).X / 2f,
            (float)(yPos + 10)), Color.Black);

        return false; // skip original draw
    }

    public static bool ReceiveLeftClickPrefix(object __instance, int x, int y)
    {
        if (_adventureBarType == null) return true;

        var xPos = (int)(_adventureBarType
            .GetField("xPositionOnScreen", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) ?? 0);
        var yPos = (int)(_adventureBarType
            .GetField("yPositionOnScreen", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) ?? 0);
        var height = (int)(_adventureBarType
            .GetField("height", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) ?? 0);
        var width = (int)(_adventureBarType
            .GetField("width", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) ?? 0);

        // aether bar bounds
        var aetherBounds = AetherOnly
            ? new Rectangle(xPos, yPos, width, 56)
            : new Rectangle(xPos, yPos + height - 12, width, 56);

        if (aetherBounds.Contains(x, y))
        {
            AetherOnly = !AetherOnly;
            Game1.playSound("smallSelect");
            return false;
        }

        return true;
    }
}
