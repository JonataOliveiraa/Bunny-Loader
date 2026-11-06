using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace ExampleMod.Content.TileEntities;

public class BasicTileEntity : ModTileEntity
{
    private const int MaxFill = 3600;
    private const int SyncInterval = MaxFill / 10;

    private int waterFillLevel;

    public int WaterFillLevel
    {
        get { return waterFillLevel; }
        set
        {
            int newFillLevel = Math.Clamp(value, 0, MaxFill);
            bool syncNeeded = waterFillLevel / SyncInterval != newFillLevel / SyncInterval;
            waterFillLevel = newFillLevel;
            if (syncNeeded)
            {
                if ((Main.netMode & NetmodeID.Server) != 0)
                {
                    NetMessage.SendData(MessageID.TileEntitySharing, number: ID);
                }
            }
        }
    }

    public int WaterFillPercentage => WaterFillLevel * 100 / MaxFill;

    public int WaterFillStage => WaterFillLevel * 4 / MaxFill;

    public override void SaveData(TagCompound tag)
    {
        tag[nameof(WaterFillLevel)] = WaterFillLevel;
    }

    public override void LoadData(TagCompound tag)
    {
        WaterFillLevel = tag.GetInt(nameof(WaterFillLevel));
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(WaterFillLevel);
    }

    public override void NetReceive(BinaryReader reader)
    {
        WaterFillLevel = reader.ReadInt32();
    }

    public override void Update()
    {
        if (Main.raining)
        {
            WaterFillLevel += 1;
        }
    }

    public override bool IsTileValidForEntity(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.HasTile && tile.TileType == ModContent.TileType<BasicTileEntityTile>();
    }
}

public class BasicTileEntityTile : ModTile
{
    public static LocalizedText StatusText { get; private set; }

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = true;
        TileID.Sets.DisableSmartCursor[Type] = true;
        TileID.Sets.PreventsTileRemovalIfOnTopOfIt[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance<BasicTileEntity>().Generic_HookPostPlaceMyPlayer;
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(200, 200, 200), CreateMapEntryName());

        StatusText = this.GetLocalization(nameof(StatusText));
    }

    public override void KillMultiTile(int i, int j, int frameX, int frameY)
    {
        ModContent.GetInstance<BasicTileEntity>().Kill(i, j);
    }

    public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY)
    {
        if (TileEntity.TryGet(i, j, out BasicTileEntity tileEntity))
        {
            tileFrameY = (short)(tileFrameY + (tileEntity.WaterFillStage * 38));
        }
    }

    public override void MouseOver(int i, int j)
    {
        if (TileEntity.TryGet(i, j, out BasicTileEntity tileEntity))
        {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = -1;
            player.cursorItemIconText = StatusText.Format(tileEntity.WaterFillPercentage);
        }
    }

    public override bool RightClick(int i, int j)
    {
        if (!TileEntity.TryGet(i, j, out BasicTileEntity tileEntity))
        {
            return true;
        }

        if (tileEntity.WaterFillPercentage == 100)
        {
            SoundEngine.PlaySound(SoundID.Drown);
            WorldGen.KillTile(i, j, fail: true);
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetMessage.SendData(MessageID.TileManipulation, number: 0, number2: i, number3: j, number4: 1f);
            }
        }
        else
        {
            Main.NewText(StatusText.Format(tileEntity.WaterFillPercentage));
        }

        return true;
    }

    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (TileEntity.TryGet(i, j, out BasicTileEntity tileEntity) && tileEntity.WaterFillPercentage == 100)
        {
            fail = true;
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Item.NewItem(new EntitySource_TileBreak(i, j), tileEntity.Position.ToWorldCoordinates(16, 16), ItemID.WaterBucket);
                tileEntity.WaterFillLevel = 0;
            }

            if (Main.netMode != NetmodeID.Server)
            {
                Main.LocalPlayer.InterruptItemUsageIfOverTile(Type);
            }

            SoundEngine.PlaySound(SoundID.Drown);
        }
    }
}

public class BasicTileEntityItem : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<BasicTileEntityTile>());
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddRecipeGroup(RecipeGroups.Wood, 9)
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
