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

    private Item? _heldItem;
    private Item? _hoveredItem;
    private string _hoverText = "";

    // กล่อง slot ด้านบน
    private int _boxX, _boxY, _boxW, _boxH;

    public SnsEquipmentMenu() : base(0, 0, 0, 0)
    {
        if (_getItem == null)
        {
            var api = GetSpaceCoreApi();
            _getItem = api?.GetType().GetMethod("GetItemInEquipmentSlot",
                new[] { typeof(Farmer), typeof(string) });
            _setItem = api?.GetType().GetMethod("SetItemInEquipmentSlot",
                new[] { typeof(Farmer), typeof(string), typeof(Item) });
        }

        int vw = Game1.uiViewport.Width;
        int vh = Game1.uiViewport.Height;

        // กล่อง slot ด้านบน — เหมือน ArsenalMenu box
        int menuX = vw / 2 - 350 - IClickableMenu.borderWidth;
        int menuW = 700 + IClickableMenu.borderWidth * 2;
        int menuY = vh / 2 - 150 - 100 - IClickableMenu.borderWidth;
        int menuH = 300 + IClickableMenu.borderWidth * 2;

        _boxX = menuX;
        _boxY = menuY;
        _boxW = menuW;
        _boxH = menuH;

        int slotAreaX = menuX + IClickableMenu.borderWidth;
        int slotAreaY = menuY + IClickableMenu.borderWidth;

        // Armor slot
        _armorSlot = new ClickableTextureComponent(
            new Rectangle(slotAreaX + 40, slotAreaY + 60, 64, 64),
            LoadTexture("DN.SnS/ArmorSlot"),
            new Rectangle(0, 0, 16, 16), 4f)
        {
            myID = 200,
            label = "Armor",
            item  = GetSlotItem(ArmorSlotId)
        };

        // Offhand slot
        _offhandSlot = new ClickableTextureComponent(
            new Rectangle(slotAreaX + 40 + 216, slotAreaY + 60, 64, 64),
            LoadTexture("DN.SnS/OffhandSlot"),
            new Rectangle(0, 0, 16, 16), 4f)
        {
            myID = 201,
            label = "Offhand",
            item  = GetSlotItem(OffhandSlotId)
        };

        // inventory — เหมือน ArsenalMenuPatch
        int newSq = 80;
        int hGap = 8;
        int verticalGap = 8;
        int rows = 3;
        int capacity = 36;
        int cols = capacity / rows;

        int totalWidth = cols * (newSq + hGap) - hGap;
        int startX = menuX + (menuW - totalWidth) / 2;
        int startY = menuY + menuH + 8;
        int totalHeight = vh - startY - 44;

        _inventory = new InventoryMenu(startX, startY, false, null, null, cols);

        // rebuild slots ขนาดใหม่ เหมือน ArsenalMenuPatch
        var invType = _inventory.GetType();
        var inventorySlots = invType.GetField("inventory")?.GetValue(_inventory) as List<ClickableComponent>;
        if (inventorySlots != null)
        {
            for (int j = 0; j < inventorySlots.Count; j++)
            {
                int row = j / cols;
                int col = j % cols;
                inventorySlots[j].bounds.X = startX + col * (newSq + hGap);
                inventorySlots[j].bounds.Y = startY + row * (newSq + verticalGap);
                inventorySlots[j].bounds.Width = newSq + hGap;
                inventorySlots[j].bounds.Height = newSq + verticalGap;
            }
        }

        // ปุ่ม X แบบ mobile เหมือน ArsenalMenuPatch
        var closeButton = new ClickableTextureComponent(
            new Rectangle(vw - 68 - Game1.xEdge, 0, 68 + Game1.xEdge, 80),
            Game1.mobileSpriteSheet,
            new Rectangle(62, 0, 17, 17),
            4f, true);
        typeof(IClickableMenu).GetField("upperRightCloseButton",
            BindingFlags.Public | BindingFlags.Instance)
            ?.SetValue(this, closeButton);

        Monitor?.Log($"SnsEquipmentMenu created! startX={startX} startY={startY} totalHeight={totalHeight}", LogLevel.Info);
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
        try { return _getItem?.Invoke(GetSpaceCoreApi(), new object[] { Game1.player, slotId }) as Item; }
        catch { return null; }
    }

    void SetSlotItem(string slotId, Item? item)
    {
        try { _setItem?.Invoke(GetSpaceCoreApi(), new object[] { Game1.player, slotId, item }); }
        catch (Exception ex) { Monitor?.Log($"SetSlotItem error: {ex.Message}", LogLevel.Error); }
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
        // ปุ่ม X
        var closeBtn = typeof(IClickableMenu).GetField("upperRightCloseButton",
            BindingFlags.Public | BindingFlags.Instance)?.GetValue(this) as ClickableTextureComponent;
        if (closeBtn != null && closeBtn.containsPoint(x, y))
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
        if (_inventory.isWithinBounds(x, y))
        {
            _heldItem = _inventory.leftClick(x, y, _heldItem);
            return;
        }

        // กดนอก — ไม่ปิด menu
    }

    public override void releaseLeftClick(int x, int y)
    {
        // ไม่ทำอะไร ป้องกัน menu ปิดเมื่อปล่อยนิ้ว
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

        // กล่อง slot บน
        IClickableMenu.drawTextureBox(b, _boxX, _boxY, _boxW, _boxH, Color.White);

        // labels
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

        // กล่อง inventory
        int invBorderX = _inventory.xPositionOnScreen - IClickableMenu.borderWidth;
        int invBorderY = _inventory.yPositionOnScreen - IClickableMenu.borderWidth;
        int invBorderW = _inventory.width + IClickableMenu.borderWidth * 2 + 8;
        int invBorderH = Game1.uiViewport.Height - invBorderY - 36;
        IClickableMenu.drawTextureBox(b, invBorderX, invBorderY, invBorderW, invBorderH, Color.White);
        _inventory.draw(b);

        // ปุ่ม X
        var closeBtn = typeof(IClickableMenu).GetField("upperRightCloseButton",
            BindingFlags.Public | BindingFlags.Instance)?.GetValue(this) as ClickableTextureComponent;
        closeBtn?.draw(b);

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

