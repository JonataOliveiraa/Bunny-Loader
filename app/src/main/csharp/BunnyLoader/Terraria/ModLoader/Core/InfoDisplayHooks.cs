using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.IO;

namespace Terraria.ModLoader.Core;

internal static class InfoDisplayHooks
{
    const string SaveKey = "bunny:hiddenInfo";

    static readonly List<InfoDisplay> Displays = new();
    static readonly List<Row> Rows = new();
    static readonly Dictionary<IntPtr, HashSet<string>> Hidden = new();
    static int vanilla;

    readonly record struct Row(InfoDisplay Display, bool Hidden, string Text, Color? Color);

    internal static void Register(InfoDisplay display)
    {
        Displays.Add(display);
        ItemHooks.Once("ModInfoDisplays", () =>
        {
            On_GUIAccessoryInfo.BuildInfoAccs += BuildInfoAccs;
            On_GUIAccessoryInfo.ItemDraw += ItemDraw;
            On_GUIAccessoryInfoToggles.InfoAccDraw += InfoAccDraw;
            On_GUIAccessoryInfoToggles.InfoAccOver += InfoAccOver;
            On_Player.InternalSavePlayerFile += InternalSavePlayerFile;
            On_Player.LoadPlayer += LoadPlayer;
        });
    }

    static HashSet<string> HiddenOf(Player player)
    {
        if (!Hidden.TryGetValue(player.Pointer, out HashSet<string>? set)) Hidden[player.Pointer] = set = new HashSet<string>();
        return set;
    }

    internal static bool IsHidden(Player player, InfoDisplay display) => !player.IsNull && HiddenOf(player).Contains(display.FullName);

    static void BuildInfoAccs(On_GUIAccessoryInfo.orig_BuildInfoAccs orig, GUIAccessoryInfo self)
    {
        int before = self.lastRebuild;
        orig(self);
        if (self.lastRebuild == before || Main.gameMenu) return;
        vanilla = self.numInfoAccessories;
        Rows.Clear();
        Player player = Main.LocalPlayer;
        if (player.IsNull) return;
        foreach (InfoDisplay display in Displays)
        {
            try
            {
                if (!display.Active()) continue;
                bool hidden = IsHidden(player, display);
                if (hidden && !Main.playerInventory) continue;
                Color color = Color.White, shadow = Color.Black;
                string text = display.DisplayValue(ref color, ref shadow) ?? "";
                Rows.Add(new Row(display, hidden, text, color == Color.White ? null : color));
            }
            catch (Exception e) { ItemHooks.Report($"{display.FullName}.InfoDisplay", e); }
        }
        self.numInfoAccessories = vanilla + Rows.Count;
    }

    static bool ModRow(int index, out Row row)
    {
        row = default;
        if (index < vanilla || index - vanilla >= Rows.Count) return false;
        row = Rows[index - vanilla];
        return true;
    }

    static void ItemDraw(On_GUIAccessoryInfo.orig_ItemDraw orig, GUIAccessoryInfo self, ItemGrid_Layout gridLayout, int i, Vector2 position, float scale)
    {
        if (!ModRow(self.itemOffset + i, out Row row))
        {
            orig(self, gridLayout, i, position, scale);
            return;
        }
        try
        {
            AccessoryInfo_Layout layout = AccessoryInfo_Layout.Instance;
            Texture2D texture = ModContent.Request<Texture2D>(row.Display.Texture).Value;
            var region = new Rectangle((int)position.X, (int)position.Y, (int)(texture.Width * scale), (int)(texture.Height * scale));
            ControlAnchor.SetGridItemRegion(region);
            Main.spriteBatch.Draw(texture, position, new Rectangle(0, 0, texture.Width, texture.Height), row.Hidden ? layout.DisabledIconColour : layout.IconColour, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            self.lastDrawRegion = self.lastDrawRegion.Width == 0 ? region : Rectangle.Union(self.lastDrawRegion, region);
            if (row.Hidden) GUIString.Draw(layout.DisabledItemText, row.Text);
            else if (row.Color is Color color) GUIString.Draw(layout.ItemText, row.Text, color);
            else GUIString.Draw(layout.ItemText, row.Text);
        }
        catch (Exception e) { ItemHooks.Report($"{row.Display.FullName}.InfoDisplay draw", e); }
    }

    static void InfoAccDraw(On_GUIAccessoryInfoToggles.orig_InfoAccDraw orig, GUIAccessoryInfoToggles self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        if (!ModRow(index, out Row row))
        {
            orig(self, gridLayout, index, position, scale);
            return;
        }
        try
        {
            Equipment_Layout layout = Equipment_Layout.Instance;
            Texture2D back = (row.Hidden ? TextureAssets.InventoryBack7 : TextureAssets.InventoryBackHighlight7).Value;
            Vector2 backSize = new Vector2(back.Width, back.Height) * scale;
            ControlAnchor.SetGridItemRegion(new Rectangle((int)position.X, (int)position.Y, (int)backSize.X, (int)backSize.Y));
            Main.spriteBatch.Draw(back, position, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            if (self.accOver == index)
            {
                Main.instance.MouseText(row.Display.DisplayName.Value);
                Texture2D highlight = TextureAssets.InventoryHighlight.Value;
                Main.spriteBatch.Draw(highlight, position + (new Vector2(back.Width, back.Height) - new Vector2(highlight.Width, highlight.Height)) / 2f * scale, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            Texture2D icon = ModContent.Request<Texture2D>(row.Display.Texture).Value;
            float iconScale = layout.InfoAccessoriesIconScale * IconZoom(icon);
            Vector2 iconAt = position + (backSize - new Vector2(icon.Width, icon.Height) * iconScale) / 2f;
            Main.spriteBatch.Draw(icon, iconAt, new Rectangle(0, 0, icon.Width, icon.Height), row.Hidden ? layout.InfoAccDisabled : layout.InfoAccNormal, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
            Texture2D eye = (row.Hidden ? TextureAssets.InventoryTickOff : TextureAssets.InventoryTickOn).Value;
            Main.spriteBatch.Draw(eye, position + layout.InfoAccVisOffset * scale, null, layout.InfoAccVisColour, 0f, Vector2.Zero, layout.InfoAccVisScale, SpriteEffects.None, 0f);
        }
        catch (Exception e) { ItemHooks.Report($"{row.Display.FullName}.InfoDisplay toggle", e); }
    }

    static int IconZoom(Texture2D icon)
    {
        var popups = TextureAssets.InfoPopup;
        if (popups.IsNull || popups.Length == 0 || icon.Height <= 0) return 1;
        var popup = popups[0];
        if (popup == null || !popup.IsLoaded) return 1;
        return Math.Max(1, popup.Value.Height / icon.Height);
    }

    static void InfoAccOver(On_GUIAccessoryInfoToggles.orig_InfoAccOver orig, GUIAccessoryInfoToggles self, int index)
    {
        if (!ModRow(index, out Row row))
        {
            orig(self, index);
            return;
        }
        self.accOver = index;
        if (!Main.mouseLeft || !Main.mouseLeftRelease) return;
        Main.mouseLeftRelease = false;
        Terraria.Audio.SoundEngine.PlaySound(12);
        HashSet<string> hidden = HiddenOf(Main.LocalPlayer);
        if (!hidden.Remove(row.Display.FullName)) hidden.Add(row.Display.FullName);
        GUIInstance.Active.GUIAccessoryInfo.lastRebuild = -1;
    }

    static void InternalSavePlayerFile(On_Player.orig_InternalSavePlayerFile orig, PlayerFileData playerFile)
    {
        orig(playerFile);
        try
        {
            Player player = playerFile.IsNull ? default : playerFile.Player;
            if (player.IsNull || PlayerHooks.DataFile(playerFile) is not string file) return;
            JsonObject all = PlayerHooks.Read(file);
            HashSet<string> hidden = HiddenOf(player);
            if (hidden.Count > 0) all[SaveKey] = new JsonArray(hidden.OrderBy(name => name).Select(name => (JsonNode)name).ToArray());
            else if (all.ContainsKey(SaveKey)) all.Remove(SaveKey);
            else return;
            if (all.Count > 0) File.WriteAllText(file, all.ToJsonString());
            else File.Delete(file);
        }
        catch (Exception e) { ItemHooks.Report("InfoDisplay save", e); }
    }

    static PlayerFileData LoadPlayer(On_Player.orig_LoadPlayer orig, string? playerPath, bool cloudSave)
    {
        PlayerFileData data = orig(playerPath, cloudSave);
        try
        {
            if (data.IsNull || PlayerHooks.DataFile(data) is not string file) return data;
            Player player = data.Player;
            if (player.IsNull) return data;
            HashSet<string> hidden = HiddenOf(player);
            hidden.Clear();
            if (PlayerHooks.Read(file)[SaveKey] is JsonArray saved)
                foreach (JsonNode? name in saved)
                    if (name?.GetValue<string>() is string value) hidden.Add(value);
        }
        catch (Exception e) { ItemHooks.Report("InfoDisplay load", e); }
        return data;
    }
}
