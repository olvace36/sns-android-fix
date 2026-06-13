using System;
using System.Collections.Generic;
using System.Linq;
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

    // เก็บ GameMenu ก่อนเปิด SnsEquipmentMenu
    public static IClickableMenu? PreviousMenu;

    private static string? _armorSlotId;
    private static string? _offhandSlotId;

    private static MethodInfo? _getItem;
    private static MethodInfo? _setItem;

    private ClickableTextureComponent _armorSlot  = null!;
    private ClickableTextureComponent _offhandSlot = null!;
    private InventoryMenu _inventory = null!;

    private Item? _hoveredItem;
    private string _hoverText = "";

    private int _boxX, _boxY, _boxW, _boxH;
    private int _invBorderX, _invBorderY, _invBorderW, _invBorderH;

    public static void InitSlotIds()
    {
        try
        {
            var spaceCoreType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.FullName == "SpaceCore.SpaceCore");

            var equipmentSlots = spaceCoreType
                ?.GetField("EquipmentSlots", BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null);

            if (equipmentSlots == null) { Monitor?.Log("InitSlotIds: EquipmentSlots null", LogLevel.Warn); return; }

            var keys = equipmentSlots.GetType().GetProperty("Keys")?.GetValue(equipmentSlots) as IEnumerable<string>;
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    Monitor?.Log($"EquipmentSlots key: {key}", LogLevel.Info);
                    if (key.EndsWith("_Armor")) _armorSlotId = key;
                    if (key.EndsWith("_Offhand")) _offhandSlotId = key;
                }
            }
            Monitor?.Log($"InitSlotIds: armor={_armorSlotId ?? "null"} offhand={_offhandSlotId ?? "null"}", LogLevel.Info);
        }
        catch (Exception ex) { Monitor?.Log($"InitSlotIds error: {ex.Message}", LogLevel.Error); }
    }

    public SnsEquipmentMenu() : base(0, 0, 0, 0)
    {
        if (_getItem == null)
        {
            var api = GetSpaceCoreApi();
            _getItem = api?.GetType().GetMethod("GetItemInEquipmentSlot", new[] { typeof(Farmer), typeof(string) });
            _setItem = api?.GetType().GetMethod("SetItemInEquipmentSlot", new[] { typeof(Farmer), typeof(string), typeof(Item) });
        }

        int vw = Game1.uiViewport.Width;
        int vh = Game1.uiViewport.Height;

        int menuX = vw / 2 - 350 - IClickableMenu.borderWidth;
        int menuW = 700 + IClickableMenu.borderWidth * 2;
        int menuY = vh / 2 - 150 - 100 - IClickableMenu.borderWidth;
        int menuH = 300 + IClickableMenu.borderWidth * 2;

        _boxX = menuX; _boxY = menuY; _boxW = menuW; _boxH = menuH;

        int slotAreaX = menuX + IClickableMenu.borderWidth;
        int slotAreaY = menuY + IClickableMenu.borderWidth;

        _armorSlot = new ClickableTextureComponent(
            new Rectangle(slotAreaX + 40, slotAreaY + 60, 64, 64),
            LoadTexture("DN.SnS/ArmorSlot"), new Rectangle(0, 0, 16, 16), 4f)
        { myID = 200, label = "Armor", item = _armorSlotId != null ? GetSlotItem(_armorSlotId) : null };

        _offhandSlot = new ClickableTextureComponent(
            new Rectangle(slotAreaX + 40 + 216, slotAreaY + 60, 64, 64),
            LoadTexture("DN.SnS/OffhandSlot"), new Rectangle(0, 0, 16, 16), 4f)
        { myID = 201, label = "Offhand", item = _offhandSlotId != null ? GetSlotItem(_offhandSlotId) : null };

        int newSq = 80; int hGap = 8; int verticalGap = 8;
        int capacity = 36; int cols = capacity / 3;
        int totalWidth = cols * (newSq + hGap) - hGap;
        int startX = menuX + (menuW - totalWidth) / 2;
        int startY = menuY + menuH + 8;
        int totalHeight = vh - startY - 44;

        _inventory = new InventoryMenu(startX, startY, true);

        var invMenu = (object)_inventory;
        var type = invMenu.GetType();
        type.GetField("squareSide")?.SetValue(invMenu, newSq);
        type.GetField("scaleFactor")?.SetValue(invMenu, (float)newSq / 64f);
        type.GetField("yPositionOnScreen")?.SetValue(invMenu, startY);
        type.GetField("xPositionOnScreen")?.SetValue(invMenu, startX);
        type.GetField("width", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, totalWidth);
        type.GetField("height", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, totalHeight);
        type.GetField("xOffset")?.SetValue(invMenu, 0);
        type.GetField("yOffset")?.SetValue(invMenu, 0);
        type.GetField("hGap")?.SetValue(invMenu, hGap);
        type.GetField("drawSlots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, true);
        type.GetField("showTrash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, false);
        type.GetField("showOrganizeButton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, false);
        type.GetField("tapHoldTime", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(invMenu, 0f);

        var inventorySlots = type.GetField("inventory")?.GetValue(invMenu) as List<ClickableComponent>;
        if (inventorySlots != null)
        {
            for (int j = 0; j < inventorySlots.Count; j++)
            {
                int row = j / cols; int col = j % cols;
                inventorySlots[j].bounds.X = startX + col * (newSq + hGap);
                inventorySlots[j].bounds.Y = startY + row * (newSq + verticalGap);
                inventorySlots[j].bounds.Width = newSq + hGap;
                inventorySlots[j].bounds.Height = newSq + verticalGap;
            }
        }

        _invBorderX = startX - IClickableMenu.borderWidth;
        _invBorderY = startY - IClickableMenu.borderWidth;
        _invBorderW = totalWidth + IClickableMenu.borderWidth * 2;
        _invBorderH = totalHeight + IClickableMenu.borderWidth * 2;

        // ปุ่ม X mobile
        var closeButton = new ClickableTextureComponent(
            new Rectangle(vw - 68 - Game1.xEdge, 0, 68 + Game1.xEdge, 80),
            Game1.mobileSpriteSheet, new Rectangle(62, 0, 17, 17), 4f, true);
        typeof(IClickableMenu).GetField("upperRightCloseButton", BindingFlags.Public | BindingFlags.Instance)
            ?.SetValue(this, closeButton);

        Monitor?.Log($"SnsEquipmentMenu created! startX={startX} startY={startY}", LogLevel.Info);
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

    static Func<Item, bool>? GetSlotValidator(string slotId)
    {
        try
        {
            var spaceCoreType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.FullName == "SpaceCore.SpaceCore");

            var equipmentSlots = spaceCoreType
                ?.GetField("EquipmentSlots", BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null);
            if (equipmentSlots == null) return null;

            var tryGetValue = equipmentSlots.GetType()
                .GetMethod("TryGetValue", new[] { typeof(string), equipmentSlots.GetType().GetGenericArguments()[1].MakeByRefType() });
            object?[] args = new object?[] { slotId, null };
            bool found = (bool)(tryGetValue?.Invoke(equipmentSlots, args) ?? false);
            if (!found || args[1] == null) return null;

            return args[1].GetType()
                .GetProperty("SlotValidator", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(args[1]) as Func<Item, bool>;
        }
        catch { return null; }
    }

    bool IsValidForSlot(string slotId, Item? item)
    {
        if (item == null) return true;
        var validator = GetSlotValidator(slotId);
        if (validator == null) return false;
        return validator.Invoke(item);
    }

    Item? GetSelectedItem()
    {
        int selected = _inventory.currentlySelectedItem;
        if (selected < 0 || selected >= Game1.player.Items.Count) return null;
        return Game1.player.Items[selected];
    }

    void TryEquipItem(string? slotId, ClickableTextureComponent slot, bool playSound)
    {
        if (slotId == null) return;

        var selectedItem = GetSelectedItem();

        if (selectedItem == null)
        {
            // ถอด item ออกจาก slot
            var existing = GetSlotItem(slotId);
            if (existing == null) return;
            if (Game1.player.addItemToInventoryBool(existing))
            {
                SetSlotItem(slotId, null);
                slot.item = null;
                if (playSound) Game1.playSound("dwop");
                Monitor?.Log($"Unequipped {existing.DisplayName} from {slotId}", LogLevel.Info);
            }
            return;
        }

        if (!IsValidForSlot(slotId, selectedItem)) return;

        int selected = _inventory.currentlySelectedItem;
        var old = GetSlotItem(slotId);
        SetSlotItem(slotId, selectedItem);
        Game1.player.Items[selected] = old;
        slot.item = GetSlotItem(slotId);
        _inventory.currentlySelectedItem = -1;

        if (playSound) Game1.playSound(old != null ? "dwop" : "crit");
        Monitor?.Log($"Equipped {selectedItem.DisplayName} → {slotId}", LogLevel.Info);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        // ปุ่ม X — restore GameMenu กลับ
        var closeBtn = typeof(IClickableMenu).GetField("upperRightCloseButton",
            BindingFlags.Public | BindingFlags.Instance)?.GetValue(this) as ClickableTextureComponent;
        if (closeBtn != null && closeBtn.containsPoint(x, y))
        {
            Game1.activeClickableMenu = PreviousMenu;
            PreviousMenu = null;
            Monitor?.Log("SnsEquipmentMenu closed, restored GameMenu", LogLevel.Info);
            return;
        }

        if (_armorSlot.containsPoint(x, y)) { TryEquipItem(_armorSlotId, _armorSlot, playSound); return; }
        if (_offhandSlot.containsPoint(x, y)) { TryEquipItem(_offhandSlotId, _offhandSlot, playSound); return; }

        if (_inventory.isWithinBounds(x, y))
        {
            _inventory.receiveLeftClick(x, y, playSound);
            return;
        }
    }

    public override void releaseLeftClick(int x, int y)
    {
        _inventory.releaseLeftClick(x, y);
    }

    public override void leftClickHeld(int x, int y)
    {
        _inventory.leftClickHeld(x, y);
    }

    public override void performHoverAction(int x, int y)
    {
        _hoveredItem = null; _hoverText = "";
        _armorSlot.tryHover(x, y, 0.1f);
        _offhandSlot.tryHover(x, y, 0.1f);
        _inventory.performHoverAction(x, y);

        if (_armorSlot.containsPoint(x, y) && _armorSlot.item != null)
        { _hoveredItem = _armorSlot.item; _hoverText = _armorSlot.item.getDescription(); }
        else if (_offhandSlot.containsPoint(x, y) && _offhandSlot.item != null)
        { _hoveredItem = _offhandSlot.item; _hoverText = _offhandSlot.item.getDescription(); }
    }

    public override void update(GameTime time) { _inventory.update(time); }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);

        IClickableMenu.drawTextureBox(b, _boxX, _boxY, _boxW, _boxH, Color.White);
        Utility.drawTextWithShadow(b, "Armor", Game1.smallFont, new Vector2(_armorSlot.bounds.X, _armorSlot.bounds.Y - 28), Game1.textColor);
        Utility.drawTextWithShadow(b, "Offhand", Game1.smallFont, new Vector2(_offhandSlot.bounds.X, _offhandSlot.bounds.Y - 28), Game1.textColor);
        _armorSlot.draw(b); _armorSlot.drawItem(b, 0, 0);
        _offhandSlot.draw(b); _offhandSlot.drawItem(b, 0, 0);

        IClickableMenu.drawTextureBox(b, _invBorderX, _invBorderY, _invBorderW, _invBorderH, Color.White);
        _inventory.draw(b);

        var closeBtn = typeof(IClickableMenu).GetField("upperRightCloseButton",
            BindingFlags.Public | BindingFlags.Instance)?.GetValue(this) as ClickableTextureComponent;
        closeBtn?.draw(b);

        if (_hoveredItem != null)
            IClickableMenu.drawToolTip(b, _hoverText, _hoveredItem.DisplayName, _hoveredItem, false);

        if (!Game1.options.hardwareCursor) drawMouse(b);
    }

    public override void emergencyShutDown()
    {
        if (PreviousMenu != null)
        {
            Game1.activeClickableMenu = PreviousMenu;
            PreviousMenu = null;
        }
        base.emergencyShutDown();
    }
}
