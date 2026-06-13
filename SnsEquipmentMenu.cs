using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace SnsAndroidFix;

public class SnsEquipmentMenu : IClickableMenu
{
    internal static IMonitor? Monitor;

    private const string ArmorSlotId   = "DN.SwordAndSorcery_Armor";
    private const string OffhandSlotId = "DN.SwordAndSorcery_Offhand";

    private static MethodInfo? _getItem;
    private static MethodInfo? _setItem;

    private ClickableTextureComponent _armorSlot  = null!;
    private ClickableTextureComponent _offhandSlot = null!;
    private InventoryMenu _inventory = null!;
    private ClickableTextureComponent _closeButton = null!;

    private Item? _heldItem;
    private Item? _hoveredItem;
    private string _hoverText = "";

    public SnsEquipmentMenu() : base(
        Game1.uiViewport.Width  / 2 - 428,
        Game1.uiViewport.Height / 2 - 328,
        856,
        656)
    {
        if (_getItem == null)
        {
            var api = GetSpaceCoreApi();
            _getItem = api?.GetType().GetMethod("GetItemInEquipmentSlot",
                new[] { typeof(Farmer), typeof(string) });
            _setItem = api?.GetType().GetMethod("SetItemInEquipmentSlot",
                new[] { typeof(Farmer), typeof(string), typeof(Item) });
        }

        int cx = xPositionOnScreen + IClickableMenu.borderWidth;
        int cy = yPositionOnScreen + IClickableMenu.borderWidth;

        // Armor slot
        _armorSlot = new ClickableTextureComponent(
            new Rectangle(cx + 40, cy + 60, 64, 64),
            LoadTexture("DN.SnS/ArmorSlot"),
            new Rectangle(0, 0, 16, 16), 4f)
        {
            myID = 200,
            label = "Armor",
            item  = GetSlotItem(ArmorSlotId)
        };

        // Offhand slot
        _offhandSlot = new ClickableTextureComponent(
            new Rectangle(cx + 40 + 216, cy + 60, 64, 64),
            LoadTexture("DN.SnS/OffhandSlot"),
            new Rectangle(0, 0, 16, 16), 4f)
        {
            myID = 201,
            label = "Offhand",
            item  = GetSlotItem(OffhandSlotId)
        };

        // inventory — วาง x ให้ตรงกับ menu, y ต่ำกว่า slot
        int invX = xPositionOnScreen + IClickableMenu.borderWidth;
        int invY = cy + 200;
        int invCols = 12;
        _inventory = new InventoryMenu(invX, invY, false, null, null, invCols);

        // close button
        _closeButton = new ClickableTextureComponent(
            new Rectangle(xPositionOnScreen + width - 36, yPositionOnScreen - 8, 48, 48),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12), 4f)
        {
            myID = 999
        };

        Monitor?.Log($"SnsEquipmentMenu created! pos=({xPositionOnScreen},{yPositionOnScreen}) size=({width},{height})", LogLevel.Info);
        Monitor?.Log($"invX={invX} invY={invY}", LogLevel.Info);
    }

    static object? GetSpaceCoreApi()
    {
        return AccessTools.TypeByName("SwordAndSorcerySMAPI.ModSnS")
            ?.GetProperty("SpaceCore", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
    }

    static Texture2D? LoadTexture(string assetName)
    {
        try { return Game1.content.Load<Texture2D>(assetName); }
        catch { return null; }
    }

    Item? GetSlotItem(string slotId)
    {
        try
        {
            return _getItem?.Invoke(GetSpaceCoreApi(), new object[] { Game1.player, slotId }) as Item;
        }
        catch { return null; }
    }

    void SetSlotItem(string slotId, Item? item)
    {
        try
        {
            _setItem?.Invoke(GetSpaceCoreApi(), new object[] { Game1.player, slotId, item });
        }
        catch (Exception ex)
        {
            Monitor?.Log($"SetSlotItem error: {ex.Message}", LogLevel.Error);
        }
    }

    bool IsArmorItem(Item? item)
    {
        if (item == null) return true;
        var method = item.GetType().GetMethod("IsArmorItem", BindingFlags.Public | BindingFlags.Instance);
        return method != null && (bool)(method.Invoke(item, null) ?? false) && item is not MeleeWeapon;
    }

    bool IsOffhandItem(Item? item)
    {
        if (item == null) return true;
        return item is MeleeWeapon;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_closeButton.containsPoint(x, y))
        {
            ReturnHeldItem();
            exitThisMenu();
            return;
        }

        if (_armorSlot.containsPoint(x, y))
        {
            if (_heldItem == null || IsArmorItem(_heldItem))
            {
                var old = GetSlotItem(ArmorSlotId);
                SetSlotItem(ArmorSlotId, _heldItem);
                _heldItem = old;
                _armorSlot.item = GetSlotItem(ArmorSlotId);
                if (playSound) Game1.playSound(_heldItem != null ? "dwop" : "crit");
            }
            return;
        }

        if (_offhandSlot.containsPoint(x, y))
        {
            if (_heldItem == null || IsOffhandItem(_heldItem))
            {
                var old = GetSlotItem(OffhandSlotId);
                SetSlotItem(OffhandSlotId, _heldItem);
                _heldItem = old;
                _offhandSlot.item = GetSlotItem(OffhandSlotId);
                if (playSound) Game1.playSound(_heldItem != null ? "dwop" : "crit");
            }
            return;
        }

        // inventory click
        Item? fromInv = _inventory.getItemAt(x, y);
        if (fromInv != null || _inventory.isWithinBounds(x, y))
        {
            _heldItem = _inventory.leftClick(x, y, _heldItem);
            return;
        }

        if (!isWithinBounds(x, y))
        {
            ReturnHeldItem();
            exitThisMenu();
        }
    }

    public override void releaseLeftClick(int x, int y)
    {
        receiveLeftClick(x, y, playSound: false);
    }

    void ReturnHeldItem()
    {
        if (_heldItem == null) return;
        if (!Game1.player.addItemToInventoryBool(_heldItem))
            Game1.createItemDebris(_heldItem, Game1.player.getStandingPosition(),
                Game1.player.FacingDirection);
        _heldItem = null;
    }

    public override void performHoverAction(int x, int y)
    {
        _hoveredItem = null;
        _hoverText   = "";

        _armorSlot.tryHover(x, y, 0.1f);
        _offhandSlot.tryHover(x, y, 0.1f);
        _inventory.performHoverAction(x, y);

        if (_armorSlot.containsPoint(x, y) && _armorSlot.item != null)
        {
            _hoveredItem = _armorSlot.item;
            _hoverText   = _armorSlot.item.getDescription();
        }
        else if (_offhandSlot.containsPoint(x, y) && _offhandSlot.item != null)
        {
            _hoveredItem = _offhandSlot.item;
            _hoverText   = _offhandSlot.item.getDescription();
        }
        else if (_inventory.hover(x, y, _heldItem) is Item hovered)
        {
            _hoveredItem = hovered;
            _hoverText   = hovered.getDescription();
        }
    }

    public override void draw(SpriteBatch b)
    {
        // dim
        b.Draw(Game1.fadeToBlackRect,
            Game1.graphics.GraphicsDevice.Viewport.Bounds,
            Color.Black * 0.4f);

        // background
        IClickableMenu.drawTextureBox(b,
            xPositionOnScreen, yPositionOnScreen,
            width, height, Color.White);

        // slot labels
        Utility.drawTextWithShadow(b, "Armor", Game1.smallFont,
            new Vector2(_armorSlot.bounds.X, _armorSlot.bounds.Y - 28),
            Game1.textColor);
        Utility.drawTextWithShadow(b, "Offhand", Game1.smallFont,
            new Vector2(_offhandSlot.bounds.X, _offhandSlot.bounds.Y - 28),
            Game1.textColor);

        // slots
        _armorSlot.draw(b);
        _armorSlot.drawItem(b, 0, 0);
        _offhandSlot.draw(b);
        _offhandSlot.drawItem(b, 0, 0);

        // inventory
        _inventory.draw(b);

        // close button
        _closeButton.draw(b);

        // held item
        _heldItem?.drawInMenu(b,
            new Vector2(Game1.getOldMouseX() + 8, Game1.getOldMouseY() + 8), 1f);

        // tooltip
        if (_hoveredItem != null)
            IClickableMenu.drawToolTip(b, _hoverText, _hoveredItem.DisplayName,
                _hoveredItem, _heldItem != null);

        if (!Game1.options.hardwareCursor)
            drawMouse(b);
    }

    public override void emergencyShutDown()
    {
        ReturnHeldItem();
        base.emergencyShutDown();
    }
}

