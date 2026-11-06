using System;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsBlockF;

public sealed class CsBlockFMod : Mod
{
    static int frame, pass, fail, floor, boneIndex, merchantIndex, javelin, cx, wallX, wallY, glowDraws, pylonX, pylonY;

    static Asset<Texture2D> glow;

    internal static ILog Log => ModContent.GetInstance<CsBlockFMod>().Logger;

    static int Tile(string name) => ModContent.Find<ModTile>("ExampleMod/" + name).Type;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Main.EntitySpriteDraw_Texture2D_Vector2_Rectangle_Color_float_Vector2_float_SpriteEffects_float += CountGlow;
    }

    static void CountGlow(On_Main.orig_EntitySpriteDraw_Texture2D_Vector2_Rectangle_Color_float_Vector2_float_SpriteEffects_float orig, Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float worthless)
    {
        if (glow != null && glow.IsLoaded && texture.Pointer == glow.Value.Pointer)
            glowDraws++;
        orig(texture, position, sourceRectangle, color, rotation, origin, scale, effects, worthless);
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csblockf {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        self.creativeGodMode = true;
        frame++;
        try
        {
            Step(self);
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
        }
    }

    static int FindFloor(int x, int fromY)
    {
        for (int y = fromY; y < Main.maxTilesY - 10; y++)
        {
            Tile tile = Main.tile[x, y];
            if (tile.HasTile && Main.tileSolid[tile.TileType])
                return y;
        }
        return fromY;
    }

    static void Step(Player self)
    {
        switch (frame)
        {
            case 30:
                cx = (int)(self.Center.X / 16);
                floor = FindFloor(cx, (int)(self.Bottom.Y / 16) - 2);
                Tables();
                TownFlags(self);
                StartGlow(self);
                Smart(self);
                WallPlace(self);
                WallExplode(self);
                StartWallDraw(self);
                break;
            case 40:
                Gender();
                break;
            case 90:
                TownRest();
                Report("killing one cell takes the whole pylon", !Main.tile[pylonX, pylonY - 2].HasTile, $"has={Main.tile[pylonX, pylonY - 2].HasTile}");
                Report("the glow texture is drawn over the projectile", glowDraws > 0, $"draws={glowDraws}");
                Main.projectile[javelin].active = false;
                Report("the wall hooks run in the wall pass", WallProbe.Pre > 0 && WallProbe.Post > 0, $"pre={WallProbe.Pre} post={WallProbe.Post}");
                Log.Info("csblockf SHOT walls");
                break;
            case 330:
                Tile cell = Main.tile[wallX, wallY];
                Report("hiding a wall in PreDraw keeps the wall", cell.WallType == WallID.Stone && !cell.invisibleWall(), $"wall={cell.WallType}");
                Log.Info($"csblockf END pass={pass} fail={fail}");
                break;
        }
    }

    static void Tables()
    {
        int block = Tile("ExampleBlock"), chest = Tile("ExampleChest");
        Report("a mod block is not frame important", !Main.tileFrameImportant[block] && Main.tileSolid[block]);
        Report("a mod object still is", Main.tileFrameImportant[chest]);
    }

    static void TownFlags(Player self)
    {
        int bone = ModContent.Find<ModNPC>("ExampleMod/ExampleBoneMerchant").Type;
        int merchant = ModContent.Find<ModNPC>("ExampleMod/ExampleTravelingMerchant").Type;
        boneIndex = NPC.NewNPC(self.GetSource_FromThis(), (cx + 8) * 16, floor * 16, bone);
        merchantIndex = NPC.NewNPC(self.GetSource_FromThis(), (cx - 8) * 16, floor * 16, merchant);
        NPC boneNpc = Main.npc[boneIndex], merchantNpc = Main.npc[merchantIndex];
        Report("ActsLikeTownNPC makes the NPC count as a town one", !boneNpc.townNPC && boneNpc.isLikeATownNPC, $"town={boneNpc.townNPC}");
        merchantNpc.homeless = true;
        merchantNpc.lookForHomeTimeout = 0;
        Report("no homeless text for a stay-homeless NPC", !NPC.CanShowHomelessText(merchantIndex));
        WorldGen.prioritizedTownNPCType = 0;
        WorldGen.UpdatePrioritizedTownNPC();
        Report("the stay-homeless NPC never gets the free house", WorldGen.prioritizedTownNPCType != merchant && Main.npc[merchantIndex].active && Main.npc[merchantIndex].townNPC, $"priority={WorldGen.prioritizedTownNPCType}");
        WorldGen.prioritizedTownNPCType = 0;
    }

    static void TownRest()
    {
        NPC bone = Main.npc[boneIndex];
        bone.velocity = Vector2.Zero;
        Report("ActsLikeTownNPC lets the player talk to it", bone.CanBeTalkedTo, $"ai={bone.aiStyle} vy={bone.velocity.Y}");
        bone.active = false;
        Main.npc[merchantIndex].active = false;
    }

    static void Gender()
    {
        int hair = ModContent.Find<ModHair>("ExampleMod/ExampleHair").Type;
        GUIPlayerCreateMenu menu = GUIInstance.Active.GUIPlayerCreateMenu;
        Player keep = Main.PendingPlayer;
        Main.PendingPlayer = Player.New();
        Player pending = Main.PendingPlayer;
        var styles = Main.Hairstyles;
        bool wasCreation = styles._isAtCharacterCreation;
        styles._isAtCharacterCreation = true;
        styles.UpdateUnlocks();
        styles.RebuildList();
        var hairs = new BunnyLoader.Interop.GameList(styles.AvailableHairstyles);
        Log.Info($"csblockf hair list {hairs.Count}");
        if (hairs.Count == 0)
            hairs.Add(hair);
        int hits = 0, male = 0;
        for (int k = 0; k < 400 && hits < 12; k++)
        {
            menu.RandomisePlayer(false);
            if (pending.hair != hair)
                continue;
            hits++;
            if (pending.Male)
                male++;
        }
        Report("the mod hair picks its gender when randomizing", hits > 0 && male == 0, $"hits={hits} male={male}");
        styles._isAtCharacterCreation = wasCreation;
        styles.RebuildList();
        Main.PendingPlayer = keep;
    }

    static void StartGlow(Player self)
    {
        glow = ModContent.Request<Texture2D>("ExampleMod/Content/Projectiles/ExampleJavelinProjectile_Glow", AssetRequestMode.ImmediateLoad);
        javelin = Projectile.NewProjectile(self.GetSource_FromThis(), self.Center + new Vector2(60, -40), Vector2.Zero, ModContent.Find<ModProjectile>("ExampleMod/ExampleJavelinProjectile").Type, 0, 0, Main.myPlayer);
    }

    static void Smart(Player self)
    {
        pylonX = cx + 14;
        pylonY = FindFloor(pylonX, floor - 20) - 1;
        for (int x = pylonX - 2; x <= pylonX + 2; x++)
            for (int y = pylonY - 5; y <= pylonY; y++)
            {
                Tile tile = Main.tile[x, y];
                tile.HasTile = false;
            }
        int type = Tile("ExamplePylonTile");
        WorldGen.PlaceObject(pylonX, pylonY, type, true);
        Report("the pylon is placed", Main.tile[pylonX, pylonY].HasTile && Main.tile[pylonX, pylonY].TileType == type, $"has={Main.tile[pylonX, pylonY].HasTile}");
        var settings = new SmartInteractScanSettings
        {
            player = self,
            DemandOnlyZeroDistanceTargets = true,
            FullInteraction = false,
            mousevec = new Vector2(pylonX * 16 + 8, pylonY * 16 - 8),
            LX = pylonX - 10,
            HX = pylonX + 10,
            LY = pylonY - 10,
            HY = pylonY + 10,
        };
        var provider = TileSmartInteractCandidateProvider.New();
        provider.ClearSelfAndPrepareForCheck();
        bool won = provider.ProvideCandidate(settings, out ISmartInteractCandidate candidate);
        Report("smart interaction aims at a mod tile", won && !candidate.IsNull && Main.SmartInteractTileCoordsCount > 0, $"won={won} count={Main.SmartInteractTileCoordsCount}");
        provider.ClearSelfAndPrepareForCheck();
        settings.mousevec = new Vector2(pylonX * 16 + 200, pylonY * 16 - 200);
        Report("no aim far from it", !provider.ProvideCandidate(settings, out candidate));
        provider.ClearSelfAndPrepareForCheck();
        WorldGen.KillTile(pylonX, pylonY, false, false, true);
    }

    static void WallPlace(Player self)
    {
        wallX = cx - 12;
        wallY = FindFloor(wallX, floor - 20) - 4;
        Tile cell = Main.tile[wallX, wallY];
        cell.WallType = 0;
        Item held = self.inventory[self.selectedItem];
        int oldType = held.type, oldStack = held.stack;
        held.SetDefaults(ItemID.StoneWall, default);
        held.stack = 10;
        int oldX = Player.tileTargetX, oldY = Player.tileTargetY;
        Player.tileTargetX = wallX;
        Player.tileTargetY = wallY;
        WallProbe.Block = true;
        self.PlaceThing_Walls();
        Report("CanPlace false stops the wall", Main.tile[wallX, wallY].WallType == 0 && WallProbe.PlaceCalls > 0, $"calls={WallProbe.PlaceCalls} wall={Main.tile[wallX, wallY].WallType}");
        WallProbe.Block = false;
        Player.tileTargetX = oldX;
        Player.tileTargetY = oldY;
        held.SetDefaults(oldType, default);
        held.stack = oldStack;
    }

    static void WallExplode(Player self)
    {
        Tile cell = Main.tile[wallX, wallY];
        cell.WallType = WallID.Stone;
        Projectile bomb = Main.projectile[Projectile.NewProjectile(self.GetSource_FromThis(), new Vector2(wallX * 16 + 8, wallY * 16 + 8), Vector2.Zero, ProjectileID.WoodenArrowFriendly, 0, 0, Main.myPlayer)];
        WallProbe.Block = true;
        bomb.ExplodeTiles(bomb.Center, 3, wallX - 3, wallX + 3, wallY - 3, wallY + 3, true);
        Report("CanExplode false keeps the wall", Main.tile[wallX, wallY].WallType == WallID.Stone && WallProbe.ExplodeCalls > 0, $"calls={WallProbe.ExplodeCalls} wall={Main.tile[wallX, wallY].WallType}");
        WallProbe.Block = false;
        bomb.ExplodeTiles(bomb.Center, 3, wallX - 3, wallX + 3, wallY - 3, wallY + 3, true);
        Report("CanExplode true lets it break", Main.tile[wallX, wallY].WallType != WallID.Stone, $"wall={Main.tile[wallX, wallY].WallType}");
        bomb.active = false;
    }

    static void StartWallDraw(Player self)
    {
        wallX = cx + 3;
        wallY = floor - 5;
        for (int x = wallX - 2; x <= wallX + 2; x++)
            for (int y = wallY - 2; y <= wallY + 2; y++)
            {
                Tile cell = Main.tile[x, y];
                cell.WallType = WallID.Stone;
                WorldGen.SquareWallFrame(x, y);
            }
        WallProbe.HideX = wallX;
        WallProbe.HideY = wallY;
    }
}

public sealed class WallProbe : GlobalWall
{
    public static bool Block;
    public static int PlaceCalls, ExplodeCalls, Pre, Post, HideX, HideY;

    public override bool CanPlace(int i, int j, int type)
    {
        PlaceCalls++;
        return !Block;
    }

    public override bool CanExplode(int i, int j, int type)
    {
        ExplodeCalls++;
        return !Block;
    }

    public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
    {
        Pre++;
        return i != HideX || j != HideY;
    }

    public override void PostDraw(int i, int j, int type, SpriteBatch spriteBatch)
    {
        Post++;
    }
}
