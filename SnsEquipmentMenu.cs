using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SnsAndroidFix;

/// <summary>
/// Equipment menu สำหรับ Android แทน SpaceCore.EquipmentMenu
/// แสดง Armor slot และ Offhand slot ของ SNS
/// </summary>
public class SnsEquipmentMenu : IClickableMenu
{
    internal static IMonitor? Monitor;

    private const string ArmorSlotId  = "DN.SwordAndSorcery_Armor";
    private const string OffhandSlotId = "DN.SwordAndSorcery_Offhand";

    private static MethodInfo? _getItem;
    private static MethodInfo? _setItem;

    // slots
    private ClickableTextureComponent _armorSlot  = null!;
    private ClickableTextureComponent _offhandSlot = null!;

    // inventory panel
    private InventoryMenu _inventory = null!;

    // close button
    private ClickableTextureComponent _closeButton = null!;

    // held item
    private Item? _heldItem;

    // hover
    private Item? _hoveredItem;
    private string _hoverText = "";

    public SnsEquipmentMenu() : base(
        Game1.uiViewport.Width  / 2 - (800 + IClickableMenu.borderWidth * 2) / 2,
        Game1.uiViewport.Height / 2 - (600 + IClickableMenu.borderWidth * 2) / 2,
        800 + IClickableMenu.borderWidth * 2,
        600 + IClickableMenu.borderWidth * 2)
    {
        // cache SpaceCore API methods
        if (_getItem == null)
        {
            var spaceCoreApi = GetSpaceCoreApi();
            _getItem = spaceCoreApi?.GetType().GetMethod("GetItemInEquipmentSlot",
                new[] { typeof(Farmer), typeof(string) });
            _setItem = spaceCoreApi?.GetType().GetMethod("SetItemInEquipmentSlot",
                new[] { typeof(Farmer), typeof(string), typeof(Item) });
        }

        int cx = xPositionOnScreen + IClickableMenu.borderWidth;
        int cy = yPositionOnScreen + IClickableMenu.borderWidth;

        // Armor slot
        var armorTex  = LoadTexture("DN.SnS/ArmorSlot");
        _armorSlot = new ClickableTextureComponent(
            new Rectangle(cx + 40, cy + 40, 64, 64),
            armorTex, new Rectangle(0, 0, 16, 16), 4f)
        {
            myID = 200,
            label = "Armor",
            item  = GetSlotItem(ArmorSlotId)
        };

        // Offhand slot
        var offhandTex = LoadTexture("DN.SnS/OffhandSlot");
        _offhandSlot = new ClickableTextureComponent(
            new Rectangle(cx + 40 + 216, cy + 40, 64, 64),
            offhandTex, new Rectangle(0, 0, 16, 16), 4f)
        {
            myID = 201,
            label = "Offhand",
            item  = GetSlotItem(OffhandSlotId)
        };

        // inventory panel
        _inventory = new InventoryMenu(
            cx,
            cy + 200,
            false);

        // close button (X) — ใช้ Game1.mouseCursors sprite เดิม
        _closeButton = new ClickableTextureComponent(
            new Rectangle(xPositionOnScreen + width - 36, yPositionOnScreen - 8, 48, 48),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            4f)
        {
            myID = 999
        };

        Monitor?.Log("SnsEquipmentMenu created!", LogLevel.Info);
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
            var api = GetSpaceCoreApi();
            return _getItem?.Invoke(api, new object[] { Game1.player, slotId }) as Item;
        }
        catch { return null; }
    }

    void SetSlotItem(string slotId, Item? item)
    {
        try
        {
            var api = GetSpaceCoreApi();
            _setItem?.Invoke(api, new object[] { Game1.player, slotId, item });
        }
        catch (Exception ex)
        {
            Monitor?.Log($"SetSlotItem error: {ex.Message}", LogLevel.Error);
        }
    }

    bool IsArmorItem(Item? item)
    {
        if (item == null) return true;
        var method = item.GetType().GetMethod("IsArmorItem",
            BindingFlags.Public | BindingFlags.Instance);
        if (method != null)
            return (bool)(method.Invoke(item, null) ?? false) && item is not MeleeWeapon;
        return false;
    }

    bool IsOffhandItem(Item? item)
    {
        if (item == null) return true;
        // offhand รับ Shield (MeleeWeapon category -98) หรือ item ที่ mod กำหนด
        return item is MeleeWeapon;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        // close button
        if (_closeButton.containsPoint(x, y))
        {
            ReturnHeldItem();
            exitThisMenu();
            return;
        }

        // armor slot
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

        // offhand slot
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

        // inventory
        var clicked = _inventory.getItemAt(x, y);
        if (clicked != null)
        {
            _heldItem = _inventory.rightClick(x, y, _heldItem);
            return;
        }

        // click นอก slot — คืน held item
        if (!isWithinBounds(x, y))
        {
            ReturnHeldItem();
            exitThisMenu();
        }
    }

    public override void releaseLeftClick(int x, int y)
    {
        // Android release — handle เหมือน receiveLeftClick
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
    }

    public override void draw(SpriteBatch b)
    {
        // dim background
        b.Draw(Game1.fadeToBlackRect,
            Game1.graphics.GraphicsDevice.Viewport.Bounds,
            Color.Black * 0.4f);

        // background box
        IClickableMenu.drawTextureBox(b,
            xPositionOnScreen, yPositionOnScreen,
            width, height, Color.White);

        // slot labels
        Utility.drawTextWithShadow(b, "Armor",
            Game1.smallFont,
            new Vector2(_armorSlot.bounds.X, _armorSlot.bounds.Y - 32),
            Game1.textColor);
        Utility.drawTextWithShadow(b, "Offhand",
            Game1.smallFont,
            new Vector2(_offhandSlot.bounds.X, _offhandSlot.bounds.Y - 32),
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

        // cursor
        if (!Game1.options.hardwareCursor)
            drawMouse(b);
    }

    public override void emergencyShutDown()
    {
        ReturnHeldItem();
        base.emergencyShutDown();
    }
}
