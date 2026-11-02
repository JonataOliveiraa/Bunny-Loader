using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe class PageIconHooks
{
    internal static void Install() => ItemHooks.Once("PageIcons", () => On_GUIPageIcons.GetCategoryTexture += GetCategoryTexture);

    internal static void InstallSign() => ItemHooks.Once("SignItem", () => On_GUINPCDialogue.DrawSign += DrawSign);

    internal static int ItemOf(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.active() && TileLoader.GetTile(tile.type) is ModTile mod ? TileLoader.DropType(mod.Type, TileLoader.StyleAt(x, y)) : 0;
    }

    static Texture2D GetCategoryTexture(On_GUIPageIcons.orig_GetCategoryTexture orig, GUIPageIcons self, GUIPageIcons.Category category, bool selected)
    {
        try
        {
            if (category == GUIPageIcons.Category.Shop)
            {
                int head = ShopHead();
                if (head >= 0) return TextureAssets.NpcHead[head].Value;
            }
            else
            {
                int item = 0;
                if (category == GUIPageIcons.Category.Chest)
                {
                    Player player = Main.LocalPlayer;
                    if (player.chest >= 0) item = ItemOf(player.chestX, player.chestY);
                }
                else if (category == GUIPageIcons.Category.Dresser) item = ItemOf(Main.interactedDresserTopLeftX, Main.interactedDresserTopLeftY);
                if (item > 0) return TextureAssets.Item[item].Value;
            }
        }
        catch (Exception e) { ItemHooks.Report("ModTile (page icon)", e); }
        return orig(self, category, selected);
    }

    static int ShopHead()
    {
        NPC npc = TownNPCHooks.TalkingTo();
        if (npc.IsNull || npc.type < NPCID.Count) return -1;
        int shimmer = npc.IsShimmerVariant ? BunnyLoader.Host.Native()->TownHeadSlot(npc.type, 1) : -1;
        return shimmer >= 0 ? shimmer : NPCHeadLoader.GetNPCHeadSlot(npc.type);
    }

    static void DrawSign(On_GUINPCDialogue.orig_DrawSign orig, GUINPCDialogue self)
    {
        orig(self);
        try
        {
            Player player = Main.LocalPlayer;
            if (player.sign < 0) return;
            Sign sign = Main.sign[player.sign];
            if (sign.IsNull) return;
            int item = ItemOf(sign.x, sign.y);
            if (item <= 0) return;
            Texture2D texture = TextureAssets.Item[item].Value;
            var animation = Main.itemAnimations[item];
            Rectangle frame = animation.IsNull ? new Rectangle(0, 0, texture.Width, texture.Height) : animation.GetFrame(texture, -1);
            NPCDialogue_Layout layout = NPCDialogue_Layout.Instance;
            Panel_Layout box = layout.DialogueItem;
            Vector2 position = LayoutCalculator.GetAnchoredPosition(box.AnchorControl, box.Anchor, box.Location);
            Vector2 size = box.Size;
            float scale = Math.Min((size.X - 22) / frame.Width, (size.Y - 22) / frame.Height);
            GUIString.Draw(layout.DialogueItemName, Lang.GetItemNameValue(item));
            Main.spriteBatch.Draw(texture, new Vector2(position.X, position.Y + 8), frame, new Color(255, 255, 255, 255), 0,
                new Vector2(frame.Width / 2, frame.Height / 2), scale, SpriteEffects.None, 0);
        }
        catch (Exception e) { ItemHooks.Report("ModTile (sign item)", e); }
    }
}
