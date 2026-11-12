using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ModLoader.Default;
using Terraria.UI;

namespace Terraria.ModLoader.Core;

internal static unsafe class AccessorySlotPanel
{
    const int VisibleRows = 2;
    const int Dye = 0, Vanity = 1, Functional = 2;

    static readonly List<GameHandle> kept = new();
    static readonly GameClass ObjectClass = new("System", "Object");
    static MethodRef cloneMethod;
    static readonly int[] cloneLayout = { -1 };

    static readonly Vector2[,] vanillaPosition = new Vector2[3, 2];
    static readonly float[] vanillaScale = new float[3];
    static readonly bool[,] seen = new bool[3, 2];
    static readonly ItemGrid_Layout[] vanillaGrid = new ItemGrid_Layout[3];

    static readonly List<int> rows = new();
    static TransactionButton_Layout button;
    static nint buttonSource;
    static float buttonScale = 1f;
    static GameArray<Item> accessoryTemp, dyeTemp, accessoryOrigin, dyeOrigin;
    static Item air;
    static bool drawingMod;

    internal static bool Open { get; set; }

    internal static Rectangle ButtonRegion { get; private set; }

    internal static Rectangle HideShowRegion { get; private set; }

    internal static Rectangle UnequipRegion { get; private set; }

    internal static Rectangle PanelRegion { get; private set; }

    internal static void Install()
    {
        On_GUIEquipmentLoadout.DrawToggles += DrawToggles;
        On_GUIEquipment.AccessoryDrawImp += AccessoryDrawImp;
        On_GUIEquipment.AccessoryVanityDrawImp += AccessoryVanityDrawImp;
        On_GUIEquipment.AccessoryDyeDrawImp += AccessoryDyeDrawImp;
        On_GUIEquipment.ArmourDrawImp += ArmourDrawImp;
        On_GUIEquipment.ArmourDyeDrawImp += ArmourDyeDrawImp;
        On_GUIEquipment.ArmourVanityDrawImp += ArmourVanityDrawImp;
        On_GUIEquipment.QuickSlotDrawImp += QuickSlotDrawImp;
        On_GUIEquipment.QuickSlotDyeDrawImp += QuickSlotDyeDrawImp;
        On_GUIEquipment.ItemOver += ItemOverHook;
        On_GUIEquipment.DrawUnequipButton += DrawUnequipButton;
        On_GUIEquipment.DrawHideShowButton += DrawHideShowButton;
        On_GUIEquipment.DrawShareButton += DrawShareButton;
        On_Main.MouseText_DrawItemTooltip_GetLinesInfo += TooltipLines;
    }

    static nint Keep(nint pointer)
    {
        if (pointer != 0)
            kept.Add(new GameHandle(pointer));
        return pointer;
    }

    static nint Clone(nint obj)
    {
        ref readonly var m = ref GameInterop.Method(ref cloneMethod, ObjectClass, "object MemberwiseClone()", cloneLayout);
        CallFrame f;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out f);
        f.X[0] = obj;
        f.X[1] = (long)m.Info;
        f.Ret = RetKind.Int;
        GameInterop.Call(m.Fn, &f);
        return Keep((nint)(long)f.Out[0]);
    }

    static void Record(int column, ItemGrid_Layout grid, int index, Vector2 position, float scale)
    {
        if (drawingMod || index > 1)
            return;
        vanillaPosition[column, index] = position;
        seen[column, index] = true;
        if (index == 0)
        {
            vanillaScale[column] = scale;
            vanillaGrid[column] = grid;
        }
    }

    static void HideVanillaSelection(GUIEquipment self, Action draw)
    {
        if (drawingMod || selectedCell == null)
        {
            draw();
            return;
        }
        int piece = self.itemPieceSelected_internal;
        self.itemPieceSelected_internal = -1;
        try { draw(); }
        finally { self.itemPieceSelected_internal = piece; }
    }

    static void AccessoryDrawImp(On_GUIEquipment.orig_AccessoryDrawImp orig, GUIEquipment self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        HideVanillaSelection(self, () => orig(self, gridLayout, index, position, scale));
        Record(Functional, gridLayout, index, position, scale);
    }

    static void AccessoryVanityDrawImp(On_GUIEquipment.orig_AccessoryVanityDrawImp orig, GUIEquipment self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        HideVanillaSelection(self, () => orig(self, gridLayout, index, position, scale));
        Record(Vanity, gridLayout, index, position, scale);
    }

    static void AccessoryDyeDrawImp(On_GUIEquipment.orig_AccessoryDyeDrawImp orig, GUIEquipment self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        HideVanillaSelection(self, () => orig(self, gridLayout, index, position, scale));
        Record(Dye, gridLayout, index, position, scale);
    }

    static void ArmourDrawImp(On_GUIEquipment.orig_ArmourDrawImp orig, GUIEquipment self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale) =>
        HideVanillaSelection(self, () => orig(self, gridLayout, index, position, scale));

    static void ArmourDyeDrawImp(On_GUIEquipment.orig_ArmourDyeDrawImp orig, GUIEquipment self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale) =>
        HideVanillaSelection(self, () => orig(self, gridLayout, index, position, scale));

    static void ArmourVanityDrawImp(On_GUIEquipment.orig_ArmourVanityDrawImp orig, GUIEquipment self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale) =>
        HideVanillaSelection(self, () => orig(self, gridLayout, index, position, scale));

    static void QuickSlotDrawImp(On_GUIEquipment.orig_QuickSlotDrawImp orig, GUIEquipment self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale) =>
        HideVanillaSelection(self, () => orig(self, gridLayout, index, position, scale));

    static void QuickSlotDyeDrawImp(On_GUIEquipment.orig_QuickSlotDyeDrawImp orig, GUIEquipment self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale) =>
        HideVanillaSelection(self, () => orig(self, gridLayout, index, position, scale));

    static bool modOver;

    static void ItemOverHook(On_GUIEquipment.orig_ItemOver orig, GUIEquipment self, BunnyLoader.Interop.GameArray<Item> inv, int index, GUIEquipment.EquipmentItemType equipmentType, int itemContext, ref int itemDragStart)
    {
        if (!modOver && selectedCell != null && Main.mouseLeft)
            ClearSelection();
        orig(self, inv, index, equipmentType, itemContext, ref itemDragStart);
    }

    static void DrawToggles(On_GUIEquipmentLoadout.orig_DrawToggles orig, GUIEquipmentLoadout self)
    {
        orig(self);
        try { Draw(self); }
        catch (Exception e) { ItemHooks.Report("ModAccessorySlot panel", e); }
    }

    static void CollectRows(Player player, ModAccessorySlotPlayer slots)
    {
        rows.Clear();
        AccessorySlotLoader loader = AccessorySlotLoader.Instance;
        for (int i = 0; i < slots.SlotCount; i++)
        {
            ModAccessorySlot slot = loader.Get(i, player);
            bool usable = AccessorySlotHooks.WithPlayer(player, slot.IsEnabled);
            bool shown = usable || slot.IsVisibleWhenNotEnabled() || slots.SharedSlotHasLoadoutConflict(i, false) || slots.SharedSlotHasLoadoutConflict(i, true);
            if (!shown || slot.IsHidden() || slot.CustomLocation.HasValue)
                continue;
            rows.Add(i);
        }
    }

    static void Draw(GUIEquipmentLoadout loadout)
    {
        if (Main.gameMenu || !Main.playerInventory)
        {
            Open = false;
            ClearSelection();
            return;
        }
        Player player = Main.LocalPlayer;
        if (player.IsNull)
            return;
        ModAccessorySlotPlayer slots = AccessorySlotHooks.Of(player);
        GUIEquipment equipment = GUIInstance.Active.GUIEquipment;
        if (equipment.IsNull)
            return;
        RescueReturned(slots);
        overCell = null;

        DrawCustomLocations(equipment, player, slots);

        CollectRows(player, slots);
        if (rows.Count == 0 || !seen[Functional, 0] || !seen[Dye, 0] || !seen[Vanity, 0])
        {
            Open = false;
            ButtonRegion = default;
            PanelRegion = default;
            return;
        }

        GUIAccessoryInfoToggles info = GUIInstance.Active.GUIAccessoryInfoToggles;
        if (Open && (loadout.Expanded || (!info.IsNull && info.Expanded)))
            Open = false;

        DrawButton(loadout, info);
        if (!Open && selectedCell is (int selectedSlot, _) && (selectedSlot >= slots.SlotCount || !AccessorySlotLoader.Instance.Get(selectedSlot, player).CustomLocation.HasValue))
            ClearSelection();
        if (Open)
            DrawPanel(equipment, player, slots);
        else
            PanelRegion = default;
        DragOut(slots);
    }

    static void DrawButton(GUIEquipmentLoadout loadout, GUIAccessoryInfoToggles info)
    {
        Equipment_Layout layout = Equipment_Layout.Instance;
        TransactionButton_Layout star = layout.ShareButton;
        TransactionButton_Layout eye = layout.HideShowButton;
        if (star.IsNull || eye.IsNull)
            return;
        QuickActionButton_Layout source = star;
        if (button.IsNull || buttonSource != star.Pointer)
        {
            button = new TransactionButton_Layout(Clone(star.Pointer));
            button.DrawIcon = false;
            button.DrawItem = false;
            buttonSource = star.Pointer;
        }
        QuickActionButton_Layout ours = button;
        ours.AnchorControl = source.AnchorControl;
        ours.Anchor = source.Anchor;
        Rectangle starRegion = GUITransactionButton.GetRegion(star);
        Rectangle eyeRegion = GUITransactionButton.GetRegion(eye);
        HideShowRegion = eyeRegion;
        UnequipRegion = GUITransactionButton.GetRegion(layout.UnequipButton);
        float step = Math.Abs(eyeRegion.X - starRegion.X);
        if (step < starRegion.Width || step > starRegion.Width * 2f)
            step = starRegion.Width * 1.2f;
        float wanted = starRegion.X - step;
        ours.Location = source.Location;
        float at = GUITransactionButton.GetRegion(button).X;
        ours.Location = source.Location + new Vector2(100f, 0f);
        float unit = (GUITransactionButton.GetRegion(button).X - at) / 100f;
        ours.Location = source.Location + new Vector2(unit == 0f ? 0f : (wanted - at) / unit, 0f);

        var state = GUITransactionButton.Draw(button, default(Item), "", false, ref buttonScale, Open, false, false, false);
        Rectangle region = GUITransactionButton.GetRegion(button);
        ButtonRegion = region;
        DrawButtonIcon(region);
        if (state == GUITransactionButton.InputState.Clicked)
        {
            Open = !Open;
            Terraria.Audio.SoundEngine.PlaySound(12);
            if (Open)
            {
                loadout.CloseExpandedFilter();
                if (!info.IsNull)
                    info.CloseExpandedFilter();
            }
        }
    }

    static void DrawButtonIcon(Rectangle region)
    {
        var extra = TextureAssets.Extra;
        if (extra.IsNull || extra.Length <= 54)
            return;
        var asset = extra[54];
        if (asset == null || !asset.IsLoaded)
            return;
        Texture2D sheet = asset.Value;
        int frameWidth = sheet.Width / 3, frameHeight = sheet.Height / 7;
        var frame = new Rectangle(2 * frameWidth, 3 * frameHeight, frameWidth, frameHeight);
        float scale = Math.Min(region.Width, region.Height) * 0.6f / Math.Max(frameWidth, frameHeight);
        Vector2 center = new(region.X + region.Width / 2f, region.Y + region.Height / 2f);
        Main.spriteBatch.Draw(sheet, center, frame, Color.White, 0f, new Vector2(frameWidth / 2f, frameHeight / 2f), scale, SpriteEffects.None, 0f);
    }

    static void DrawPanel(GUIEquipment equipment, Player player, ModAccessorySlotPlayer slots)
    {
        float scale = vanillaScale[Functional];
        Texture2D back = TextureAssets.InventoryBack.Value;
        float cell = back.Width * scale;
        float pitch = seen[Functional, 1] ? vanillaPosition[Functional, 1].Y - vanillaPosition[Functional, 0].Y : cell + 4f * scale;
        if (pitch < cell)
            pitch = cell + 4f * scale;
        float gap = pitch - cell;
        float dyeOffset = 0f;
        float vanityOffset = vanillaPosition[Vanity, 0].X - vanillaPosition[Dye, 0].X;
        float functionalOffset = vanillaPosition[Functional, 0].X - vanillaPosition[Dye, 0].X;
        int pages = (rows.Count + VisibleRows - 1) / VisibleRows;
        float rowsHeight = VisibleRows * cell + (VisibleRows - 1) * gap;
        Equipment_Layout layout = Equipment_Layout.Instance;
        TransactionButton_Layout source = layout.HideShowButton;
        Rectangle sourceRegion = GUITransactionButton.GetRegion(source);
        float arrowWidth = pages > 1 ? sourceRegion.Width : 0f;
        float width = functionalOffset + cell + (pages > 1 ? gap * 2f + arrowWidth : 0f);

        Rectangle anchor = ButtonRegion;
        var origin = new Vector2(anchor.Right - width, anchor.Bottom + gap * 2f);
        PanelRegion = new Rectangle((int)origin.X, (int)origin.Y, (int)Math.Ceiling(width), (int)Math.Ceiling(rowsHeight));
        GUIInputRegionManager.Instance.RegisterInputRegion(PanelRegion);

        int page = Math.Clamp(slots.scrollRow / VisibleRows, 0, Math.Max(0, pages - 1));
        slots.scrollRow = page * VisibleRows;

        for (int r = 0; r < VisibleRows; r++)
        {
            int index = slots.scrollRow + r;
            float y = origin.Y + r * pitch;
            if (index >= rows.Count)
            {
                DrawPlaceholder(new Vector2(origin.X + dyeOffset, y), scale);
                DrawPlaceholder(new Vector2(origin.X + vanityOffset, y), scale);
                DrawPlaceholder(new Vector2(origin.X + functionalOffset, y), scale);
                continue;
            }
            int slot = rows[index];
            ModAccessorySlot modSlot = AccessorySlotLoader.Instance.Get(slot, player);
            if (modSlot.DrawDyeSlot) Cell(equipment, player, slots, modSlot, slot, Dye, new Vector2(origin.X + dyeOffset, y), scale, cell);
            if (modSlot.DrawVanitySlot) Cell(equipment, player, slots, modSlot, slot, Vanity, new Vector2(origin.X + vanityOffset, y), scale, cell);
            if (modSlot.DrawFunctionalSlot) Cell(equipment, player, slots, modSlot, slot, Functional, new Vector2(origin.X + functionalOffset, y), scale, cell);
        }

        PreviousPageRegion = default;
        NextPageRegion = default;
        if (pages <= 1)
            return;
        float column = origin.X + functionalOffset + cell + gap * 2f;
        if (PageButton(ref upButton, ref upScale, source, new Vector2(column, origin.Y), true, page > 0, out Rectangle up))
            slots.scrollRow = (page - 1) * VisibleRows;
        if (PageButton(ref downButton, ref downScale, source, new Vector2(column, origin.Y + rowsHeight - sourceRegion.Height), false, page < pages - 1, out Rectangle down))
            slots.scrollRow = (page + 1) * VisibleRows;
        PreviousPageRegion = up;
        NextPageRegion = down;
    }

    static TransactionButton_Layout upButton, downButton;
    static float upScale = 1f, downScale = 1f;

    internal static readonly Dictionary<(int Slot, int Column), Rectangle> CellRegions = new();

    internal static (int Slot, int Column)? SelectedCell => selectedCell;

    internal static Rectangle PreviousPageRegion { get; private set; }

    internal static Rectangle NextPageRegion { get; private set; }

    static bool PageButton(ref TransactionButton_Layout button, ref float scale, TransactionButton_Layout source, Vector2 wanted, bool pointsUp, bool enabled, out Rectangle region)
    {
        if (button.IsNull)
        {
            button = new TransactionButton_Layout(Clone(source.Pointer));
            button.DrawIcon = false;
            button.DrawItem = false;
        }
        QuickActionButton_Layout from = source;
        QuickActionButton_Layout ours = button;
        ours.AnchorControl = from.AnchorControl;
        ours.Anchor = from.Anchor;
        ours.Location = from.Location;
        Rectangle at = GUITransactionButton.GetRegion(button);
        ours.Location = from.Location + new Vector2(100f, 100f);
        Rectangle moved = GUITransactionButton.GetRegion(button);
        float unitX = (moved.X - at.X) / 100f, unitY = (moved.Y - at.Y) / 100f;
        ours.Location = from.Location + new Vector2(unitX == 0f ? 0f : (wanted.X - at.X) / unitX, unitY == 0f ? 0f : (wanted.Y - at.Y) / unitY);

        var state = GUITransactionButton.Draw(button, default(Item), "", !enabled, ref scale, false, false, false, false);
        region = GUITransactionButton.GetRegion(button);
        DrawArrow(region, pointsUp, enabled);
        if (!enabled || state != GUITransactionButton.InputState.Clicked)
            return false;
        Terraria.Audio.SoundEngine.PlaySound(12);
        return true;
    }

    static void DrawArrow(Rectangle region, bool pointsUp, bool enabled)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        int unit = Math.Max(1, region.Height / 12);
        const int Steps = 4;
        int centerX = region.X + region.Width / 2;
        int top = region.Y + region.Height / 2 - Steps * unit / 2;
        Color color = enabled ? Color.White : new Color(175, 175, 175, 200);
        for (int i = 0; i < Steps; i++)
        {
            int row = pointsUp ? i : Steps - 1 - i;
            int half = i * unit;
            Main.spriteBatch.Draw(pixel, new Rectangle(centerX - half - unit / 2, top + row * unit, half * 2 + unit, unit), color);
        }
    }

    static void DrawPlaceholder(Vector2 position, float scale)
    {
        Texture2D back = TextureAssets.InventoryBack.Value;
        Main.spriteBatch.Draw(back, position, null, new Color(30, 30, 45, 90), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    static void EnsureTemps()
    {
        if (!accessoryTemp.IsNull)
            return;
        air = Item.New();
        Keep(air.Pointer);
        accessoryTemp = GameArray<Item>.New(Item.Class, 20);
        Keep(accessoryTemp.Pointer);
        dyeTemp = GameArray<Item>.New(Item.Class, 10);
        Keep(dyeTemp.Pointer);
        accessoryOrigin = GameArray<Item>.New(Item.Class, 20);
        Keep(accessoryOrigin.Pointer);
        dyeOrigin = GameArray<Item>.New(Item.Class, 10);
        Keep(dyeOrigin.Pointer);
        foreach (GameArray<Item> array in new[] { accessoryTemp, dyeTemp, accessoryOrigin, dyeOrigin })
            for (int i = 0; i < array.Length; i++) array[i] = air;
    }

    static void Cell(GUIEquipment equipment, Player player, ModAccessorySlotPlayer slots, ModAccessorySlot modSlot, int slot, int column, Vector2 position, float scale, float cell)
    {
        AccessorySlotLoader loader = AccessorySlotLoader.Instance;
        bool usable = loader.ModdedIsSpecificItemSlotUnlockedAndUsable(slot, player, column == Vanity);
        var region = new Rectangle((int)position.X, (int)position.Y, (int)cell, (int)cell);
        CellRegions[(slot, column)] = region;
        bool over = GUIInputRegionManager.Instance.RegisterInputRegion(region);
        AccessorySlotType type = column switch { Dye => AccessorySlotType.DyeSlot, Vanity => AccessorySlotType.VanitySlot, _ => AccessorySlotType.FunctionalSlot };
        Item item = column switch
        {
            Dye => slots.exDyesAccessory[slot],
            Vanity => slots.exAccessorySlot[slot + slots.SlotCount],
            _ => slots.exAccessorySlot[slot]
        };

        bool custom = HasCustomLook(modSlot, column);
        if (custom && column == Functional && EyeTapped(slots, slot, position, scale))
            over = false;

        if (over && (usable || Main.mouseItem.IsAir))
            Over(equipment, player, slots, modSlot, slot, column, type);

        item = column switch
        {
            Dye => slots.exDyesAccessory[slot],
            Vanity => slots.exAccessorySlot[slot + slots.SlotCount],
            _ => slots.exAccessorySlot[slot]
        };
        if (AccessorySlotHooks.WithPlayer(player, () => modSlot.PreDraw(type, item, position, over)))
        {
            if (custom) DrawCustom(player, slots, modSlot, slot, column, type, item, position, scale);
            else DrawVanilla(equipment, player, slots, slot, column, position, scale, region);
        }
        if (!usable)
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, region, new Color(0, 0, 0, 110));
        AccessorySlotHooks.WithPlayer(player, () => modSlot.PostDraw(type, item, position, over));
    }

    static readonly Dictionary<Type, bool> colorOverrides = new();

    static string? BackgroundPath(ModAccessorySlot slot, int column) => column switch
    {
        Dye => slot.DyeBackgroundTexture,
        Vanity => slot.VanityBackgroundTexture,
        _ => slot.FunctionalBackgroundTexture
    };

    static string? IconPath(ModAccessorySlot slot, int column) => column switch
    {
        Dye => slot.DyeTexture,
        Vanity => slot.VanityTexture,
        _ => slot.FunctionalTexture
    };

    static bool HasCustomLook(ModAccessorySlot slot, int column)
    {
        if (BackgroundPath(slot, column) != null || IconPath(slot, column) != null)
            return true;
        Type type = slot.GetType();
        if (!colorOverrides.TryGetValue(type, out bool overridden))
        {
            var method = type.GetMethod(nameof(ModAccessorySlot.BackgroundDrawColor));
            colorOverrides[type] = overridden = method != null && method.DeclaringType != typeof(ModAccessorySlot);
        }
        return overridden;
    }

    static Texture2D Request(string? path) =>
        path != null && ModContent.RequestIfExists<Texture2D>(path, out var asset) ? asset.Value : default;

    static Rectangle EyeRegion(Vector2 position, float scale, bool hidden)
    {
        Equipment_Layout layout = Equipment_Layout.Instance;
        Texture2D eye = (hidden ? TextureAssets.InventoryTickOff : TextureAssets.InventoryTickOn).Value;
        Vector2 at = position + layout.VisibilityOffset * scale;
        return new Rectangle((int)at.X, (int)at.Y, (int)(eye.Width * scale), (int)(eye.Height * scale));
    }

    static bool EyeTapped(ModAccessorySlotPlayer slots, int slot, Vector2 position, float scale)
    {
        Rectangle eye = EyeRegion(position, scale, slots.exHideAccessory[slot]);
        if (!GUIInputRegionManager.Instance.RegisterInputRegion(eye))
            return false;
        if (Main.mouseLeft && Main.mouseLeftRelease)
        {
            slots.exHideAccessory[slot] = !slots.exHideAccessory[slot];
            Main.mouseLeftRelease = false;
            Terraria.Audio.SoundEngine.PlaySound(12);
        }
        return true;
    }

    static void DrawCustom(Player player, ModAccessorySlotPlayer slots, ModAccessorySlot modSlot, int slot, int column, AccessorySlotType type, Item item, Vector2 position, float scale)
    {
        int context = (int)type;
        string? backPath = BackgroundPath(modSlot, column);
        Texture2D back = Request(backPath);
        if (back.IsNull)
            back = TextureAssets.InventoryBack13.Value;
        Color color = backPath != null
            ? Main.inventoryBack
            : ItemSlot.GetColorByLoadout(column == Vanity ? AccessorySlotHooks.VanityProxySlot : AccessorySlotHooks.FunctionalProxySlot, context);
        AccessorySlotHooks.WithPlayer(player, () => modSlot.BackgroundDrawColor(type, ref color));
        Main.spriteBatch.Draw(back, position, null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        Vector2 center = position + new Vector2(back.Width, back.Height) * scale / 2f;
        if (item.IsAir)
        {
            Texture2D icon = Request(IconPath(modSlot, column));
            Rectangle frame;
            if (icon.IsNull)
            {
                var extra = TextureAssets.Extra;
                if (extra.IsNull || extra.Length <= 54 || extra[54] == null || !extra[54].IsLoaded)
                    return;
                icon = extra[54].Value;
                int fw = icon.Width / 3, fh = icon.Height / 7;
                frame = column switch
                {
                    Dye => new Rectangle(1 * fw, 0, fw, fh),
                    Vanity => new Rectangle(2 * fw, 0, fw, fh),
                    _ => new Rectangle(2 * fw, 3 * fh, fw, fh)
                };
            }
            else frame = new Rectangle(0, 0, icon.Width, icon.Height);
            Main.spriteBatch.Draw(icon, center, frame, Color.White * 0.35f, 0f, new Vector2(frame.Width / 2f, frame.Height / 2f), scale, SpriteEffects.None, 0f);
        }
        else ItemSlot.DrawItemIcon(item, context, Main.spriteBatch, center, scale, 32f, Color.White);

        if (column == Functional)
        {
            Equipment_Layout layout = Equipment_Layout.Instance;
            bool hidden = slots.exHideAccessory[slot];
            Texture2D eye = (hidden ? TextureAssets.InventoryTickOff : TextureAssets.InventoryTickOn).Value;
            Main.spriteBatch.Draw(eye, position + layout.VisibilityOffset * scale, null, layout.VisibilityColour, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    static (int Slot, int Column)? selectedCell, clickedCell, overCell;

    readonly record struct Selection(int Piece, GUIEquipment.EquipmentItemType Type, int Clicked, GUIEquipment.EquipmentItemType ClickedType, int Over, int CursorOver, int DragFunctional, int DragVanity, int DragDye);

    static Selection SaveSelection(GUIEquipment e) => new(e.itemPieceSelected_internal, e.itemType, e.lastItemClicked, e.lastItemTypeClicked, e.itemOver,
        e.cursorOver, e.itemDragStartAccessory, e.itemDragStartAccessoryVanity, e.itemDragStartAccessoryDye);

    static void RestoreSelection(GUIEquipment e, Selection s)
    {
        e.itemPieceSelected_internal = s.Piece;
        e.itemType = s.Type;
        e.lastItemClicked = s.Clicked;
        e.lastItemTypeClicked = s.ClickedType;
        e.itemOver = s.Over;
        e.cursorOver = s.CursorOver;
        e.itemDragStartAccessory = s.DragFunctional;
        e.itemDragStartAccessoryVanity = s.DragVanity;
        e.itemDragStartAccessoryDye = s.DragDye;
    }

    static int ProxyOf(int column) => column == Vanity ? AccessorySlotHooks.VanityProxySlot : AccessorySlotHooks.FunctionalProxySlot;

    static GUIEquipment.EquipmentItemType TypeOf(int column) => (GUIEquipment.EquipmentItemType)(column switch { Functional => 3, Dye => 4, _ => 5 });

    static void LoadCell(GUIEquipment e, (int Slot, int Column) cell)
    {
        int proxy = ProxyOf(cell.Column);
        GUIEquipment.EquipmentItemType type = TypeOf(cell.Column);
        e.itemPieceSelected_internal = selectedCell == cell ? proxy : -1;
        e.itemType = type;
        e.lastItemClicked = clickedCell == cell ? proxy : -1;
        e.lastItemTypeClicked = type;
        e.itemOver = overCell == cell ? proxy : -1;
        e.cursorOver = overCell == cell ? proxy : -1;
        e.itemDragStartAccessory = dragCell == cell && cell.Column == Functional ? proxy : -1;
        e.itemDragStartAccessoryVanity = dragCell == cell && cell.Column == Vanity ? proxy : -1;
        e.itemDragStartAccessoryDye = dragCell == cell && cell.Column == Dye ? proxy : -1;
    }

    static bool StoreCell(GUIEquipment e, (int Slot, int Column) cell)
    {
        int proxy = ProxyOf(cell.Column);
        GUIEquipment.EquipmentItemType type = TypeOf(cell.Column);
        bool selected = e.itemPieceSelected_internal == proxy && e.itemType == type;
        bool newly = selected && selectedCell != cell;
        if (selected) selectedCell = cell;
        else if (selectedCell == cell) selectedCell = null;
        if (e.lastItemClicked == proxy && e.lastItemTypeClicked == type) clickedCell = cell;
        else if (clickedCell == cell) clickedCell = null;
        if (e.itemOver == proxy) overCell = cell;
        else if (overCell == cell) overCell = null;
        return newly;
    }

    internal static void ClearSelection()
    {
        dragCell = null;
        selectedCell = null;
        clickedCell = null;
        overCell = null;
    }

    static void Swapped(Player player, ModAccessorySlotPlayer slots, int slot, int column, Action call)
    {
        var armor = player.armor;
        var dye = player.dye;
        var hide = player.hideVisibleAccessory;
        switch (column)
        {
            case Functional:
            {
                Item vanilla = armor[AccessorySlotHooks.FunctionalProxySlot];
                bool vanillaHide = hide[AccessorySlotHooks.FunctionalProxySlot];
                armor[AccessorySlotHooks.FunctionalProxySlot] = slots.exAccessorySlot[slot];
                hide[AccessorySlotHooks.FunctionalProxySlot] = slots.exHideAccessory[slot];
                try { call(); }
                finally
                {
                    slots.exAccessorySlot[slot] = armor[AccessorySlotHooks.FunctionalProxySlot];
                    slots.exHideAccessory[slot] = hide[AccessorySlotHooks.FunctionalProxySlot];
                    armor[AccessorySlotHooks.FunctionalProxySlot] = vanilla;
                    hide[AccessorySlotHooks.FunctionalProxySlot] = vanillaHide;
                }
                break;
            }
            case Vanity:
            {
                Item vanilla = armor[AccessorySlotHooks.VanityProxySlot];
                armor[AccessorySlotHooks.VanityProxySlot] = slots.exAccessorySlot[slot + slots.SlotCount];
                try { call(); }
                finally
                {
                    slots.exAccessorySlot[slot + slots.SlotCount] = armor[AccessorySlotHooks.VanityProxySlot];
                    armor[AccessorySlotHooks.VanityProxySlot] = vanilla;
                }
                break;
            }
            default:
            {
                Item vanilla = dye[AccessorySlotHooks.FunctionalProxySlot];
                dye[AccessorySlotHooks.FunctionalProxySlot] = slots.exDyesAccessory[slot];
                try { call(); }
                finally
                {
                    slots.exDyesAccessory[slot] = dye[AccessorySlotHooks.FunctionalProxySlot];
                    dye[AccessorySlotHooks.FunctionalProxySlot] = vanilla;
                }
                break;
            }
        }
    }

    static void DrawVanilla(GUIEquipment equipment, Player player, ModAccessorySlotPlayer slots, int slot, int column, Vector2 position, float scale, Rectangle region)
    {
        int offset = equipment.accOffset;
        Selection saved = SaveSelection(equipment);
        drawingMod = true;
        equipment.accOffset = 0;
        LoadCell(equipment, (slot, column));
        ControlAnchor.SetGridItemRegion(region);
        try
        {
            Swapped(player, slots, slot, column, () => WithoutLoadoutShare(column, () =>
            {
                switch (column)
                {
                    case Functional: equipment.AccessoryDrawImp(vanillaGrid[Functional], 0, position, scale); break;
                    case Vanity: equipment.AccessoryVanityDrawImp(vanillaGrid[Vanity], 0, position, scale); break;
                    default: equipment.AccessoryDyeDrawImp(vanillaGrid[Dye], 0, position, scale); break;
                }
            }));
        }
        finally
        {
            equipment.accOffset = offset;
            RestoreSelection(equipment, saved);
            drawingMod = false;
        }
    }

    static int dragStart = -1;
    static (int Slot, int Column)? dragCell;

    static void RescueReturned(ModAccessorySlotPlayer slots)
    {
        if (accessoryTemp.IsNull)
            return;
        foreach (GameArray<Item> array in new[] { accessoryOrigin, dyeOrigin, accessoryTemp, dyeTemp })
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].IsNull || array[i].IsAir) continue;
                Item item = array[i];
                array[i] = air;
                Rescue(slots, item);
            }
    }

    static void LoadTemps(ModAccessorySlotPlayer slots, int slot)
    {
        EnsureTemps();
        RescueReturned(slots);
        for (int i = 0; i < accessoryTemp.Length; i++) accessoryTemp[i] = air;
        for (int i = 0; i < dyeTemp.Length; i++) dyeTemp[i] = air;
        accessoryTemp[AccessorySlotHooks.FunctionalProxySlot] = slots.exAccessorySlot[slot];
        accessoryTemp[AccessorySlotHooks.VanityProxySlot] = slots.exAccessorySlot[slot + slots.SlotCount];
        dyeTemp[AccessorySlotHooks.FunctionalProxySlot] = slots.exDyesAccessory[slot];
    }

    static (int Slot, int Column)? pickedFrom;

    static void StoreTemps(ModAccessorySlotPlayer slots, int slot)
    {
        slots.exAccessorySlot[slot] = accessoryTemp[AccessorySlotHooks.FunctionalProxySlot];
        slots.exAccessorySlot[slot + slots.SlotCount] = accessoryTemp[AccessorySlotHooks.VanityProxySlot];
        slots.exDyesAccessory[slot] = dyeTemp[AccessorySlotHooks.FunctionalProxySlot];
        for (int i = 0; i < accessoryTemp.Length; i++)
        {
            if (i != AccessorySlotHooks.FunctionalProxySlot && i != AccessorySlotHooks.VanityProxySlot && !accessoryTemp[i].IsAir)
                Rescue(slots, accessoryTemp[i]);
            accessoryTemp[i] = air;
        }
        for (int i = 0; i < dyeTemp.Length; i++)
        {
            if (i != AccessorySlotHooks.FunctionalProxySlot && !dyeTemp[i].IsAir)
                Rescue(slots, dyeTemp[i]);
            dyeTemp[i] = air;
        }
    }

    static GameArray<Item> StorageOf(ModAccessorySlotPlayer slots, int column) => column == Dye ? slots.exDyesAccessory : slots.exAccessorySlot;

    static int IndexOf(ModAccessorySlotPlayer slots, int slot, int column) => column == Vanity ? slot + slots.SlotCount : slot;

    static void Rescue(ModAccessorySlotPlayer slots, Item item)
    {
        if (pickedFrom is (int slot, int column) && slot < slots.SlotCount && StorageOf(slots, column)[IndexOf(slots, slot, column)].IsAir)
        {
            StorageOf(slots, column)[IndexOf(slots, slot, column)] = item;
            pickedFrom = null;
            return;
        }
        if (Main.mouseItem.IsAir)
        {
            Main.mouseItem = item;
            return;
        }
        Player player = Main.LocalPlayer;
        player.QuickSpawnItem(player.GetSource_FromThis(), item);
    }

    static void WithoutLoadoutShare(int column, Action call)
    {
        GameArray<bool> share = ItemSlot.canLoadoutShareAt;
        int context = ContextOf(column);
        bool shared = !share.IsNull && (uint)context < (uint)share.Length && share[context];
        if (shared) share[context] = false;
        try { call(); }
        finally
        {
            if (shared) share[context] = true;
        }
    }

    static int ContextOf(int column) => column switch
    {
        Functional => ItemSlot.Context.EquipAccessory,
        Vanity => ItemSlot.Context.EquipAccessoryVanity,
        _ => ItemSlot.Context.EquipDye
    };

    static void Over(GUIEquipment equipment, Player player, ModAccessorySlotPlayer slots, ModAccessorySlot modSlot, int slot, int column, AccessorySlotType type)
    {
        LoadTemps(slots, slot);
        var cell = (slot, column);
        int proxy = ProxyOf(column);
        Selection saved = SaveSelection(equipment);
        LoadCell(equipment, cell);
        dragStart = dragCell == cell ? proxy : -1;
        modOver = true;
        try
        {
            AccessorySlotHooks.Scope(slot, () => WithoutLoadoutShare(column, () =>
                equipment.ItemOver(column == Dye ? dyeTemp : accessoryTemp, proxy, TypeOf(column), ContextOf(column), ref dragStart)));
        }
        finally { modOver = false; }
        if (dragStart == proxy) dragCell = cell;
        else if (dragCell == cell) dragCell = null;
        dragStart = -1;
        if (StoreCell(equipment, cell))
            saved = saved with { Clicked = -1 };
        RestoreSelection(equipment, saved);
        StoreTemps(slots, slot);

        AccessorySlotHooks.WithPlayer(player, () => modSlot.OnMouseHover(type));
    }

    static void DragOut(ModAccessorySlotPlayer slots)
    {
        if (dragCell is not (int slot, int column) cell)
            return;
        if (!Main.mouseLeft || !Main.mouseItem.IsAir || slot >= slots.SlotCount)
        {
            dragCell = null;
            return;
        }
        if (CellRegions.TryGetValue(cell, out Rectangle region) && region.Contains(Main.mouseX, Main.mouseY))
            return;
        dragCell = null;
        EnsureTemps();
        RescueReturned(slots);
        GameArray<Item> origin = column == Dye ? dyeOrigin : accessoryOrigin;
        GameArray<Item> storage = StorageOf(slots, column);
        int index = IndexOf(slots, slot, column), proxy = ProxyOf(column);
        origin[proxy] = storage[index];
        AccessorySlotHooks.Scope(slot, () => ItemSlot.LeftClick(origin, ContextOf(column), proxy));
        storage[index] = origin[proxy];
        origin[proxy] = air;
        if (!Main.mouseItem.IsAir)
            pickedFrom = cell;
        if (selectedCell == cell)
            selectedCell = null;
    }

    static void WithSelected(GUIEquipment self, Action orig)
    {
        if (selectedCell is not (int slot, int column) cell || Main.gameMenu)
        {
            orig();
            return;
        }
        Player player = Main.LocalPlayer;
        ModAccessorySlotPlayer slots = AccessorySlotHooks.Of(player);
        if (slot >= slots.SlotCount)
        {
            ClearSelection();
            orig();
            return;
        }
        Selection saved = SaveSelection(self);
        LoadCell(self, cell);
        try { Swapped(player, slots, slot, column, orig); }
        finally
        {
            StoreCell(self, cell);
            RestoreSelection(self, saved);
        }
    }

    static void DrawUnequipButton(On_GUIEquipment.orig_DrawUnequipButton orig, GUIEquipment self) => WithSelected(self, () => orig(self));

    static void DrawHideShowButton(On_GUIEquipment.orig_DrawHideShowButton orig, GUIEquipment self) => WithSelected(self, () => orig(self));

    static void DrawShareButton(On_GUIEquipment.orig_DrawShareButton orig, GUIEquipment self)
    {
        if (selectedCell == null || Main.gameMenu) orig(self);
    }

    static void TooltipLines(On_Main.orig_MouseText_DrawItemTooltip_GetLinesInfo orig, Item item, ref int yoyoLogo, ref int researchLine, ref int materialsLine, float oldKB, ref int numLines,
        GameArray<GameString> toolTipLine, GameArray<bool> preFixLine, GameArray<bool> badPreFixLine, ref int setBonusLine, ref Color setBonusColour, ref int sharedLine)
    {
        orig(item, ref yoyoLogo, ref researchLine, ref materialsLine, oldKB, ref numLines, toolTipLine, preFixLine, badPreFixLine, ref setBonusLine, ref setBonusColour, ref sharedLine);
        if (selectedCell == null && overCell == null || item.IsNull || sharedLine < 0 || sharedLine >= numLines ||
            item.tooltipContext is not (ItemSlot.Context.EquipAccessory or ItemSlot.Context.EquipAccessoryVanity or ItemSlot.Context.EquipDye) ||
            item.tooltipSlot is not (AccessorySlotHooks.FunctionalProxySlot or AccessorySlotHooks.VanityProxySlot))
            return;
        int removed = sharedLine;
        for (int k = removed; k < numLines - 1; k++)
        {
            toolTipLine[k] = toolTipLine[k + 1];
            preFixLine[k] = preFixLine[k + 1];
            badPreFixLine[k] = badPreFixLine[k + 1];
        }
        numLines--;
        sharedLine = -1;
        Shift(ref yoyoLogo, removed);
        Shift(ref researchLine, removed);
        Shift(ref materialsLine, removed);
        Shift(ref setBonusLine, removed);
    }

    static void Shift(ref int line, int removed)
    {
        if (line > removed) line--;
    }

    static void DrawCustomLocations(GUIEquipment equipment, Player player, ModAccessorySlotPlayer slots)
    {
        AccessorySlotLoader loader = AccessorySlotLoader.Instance;
        if (!seen[Functional, 0])
            return;
        float scale = vanillaScale[Functional];
        Texture2D back = TextureAssets.InventoryBack.Value;
        float cell = back.Width * scale;
        float vanityOffset = vanillaPosition[Vanity, 0].X - vanillaPosition[Functional, 0].X;
        float dyeOffset = vanillaPosition[Dye, 0].X - vanillaPosition[Functional, 0].X;
        for (int i = 0; i < slots.SlotCount; i++)
        {
            ModAccessorySlot slot = loader.Get(i, player);
            if (slot.CustomLocation is not Vector2 location || slot.IsHidden())
                continue;
            bool usable = AccessorySlotHooks.WithPlayer(player, slot.IsEnabled);
            if (!usable && !slot.IsVisibleWhenNotEnabled())
                continue;
            if (slot.DrawFunctionalSlot) Cell(equipment, player, slots, slot, i, Functional, location, scale, cell);
            if (slot.DrawVanitySlot) Cell(equipment, player, slots, slot, i, Vanity, location + new Vector2(vanityOffset, 0f), scale, cell);
            if (slot.DrawDyeSlot) Cell(equipment, player, slots, slot, i, Dye, location + new Vector2(dyeOffset, 0f), scale, cell);
        }
    }
}
