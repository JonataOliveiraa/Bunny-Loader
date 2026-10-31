using System;
using System.IO;
using CsNpcExtra.Content;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;

namespace CsNpcExtra;

public static class Checks
{
    public static bool Freeze;
    static int pass, fail, frames, townie = -1, plain = -1, critter = -1, looter = -1, keeper = -1, bunny = -1, guide = -1, merchant = -1;
    static bool done, loadPhase;

    static ILog Log => ModContent.GetInstance<CsNpcExtraMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        townie = plain = critter = looter = keeper = bunny = guide = merchant = -1;
        done = loadPhase = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 20) loadPhase = FindKeeper() >= 0;
            if (loadPhase)
            {
                if (frames == 30) CheckLoaded();
            }
            else
            {
                if (frames == 30) Spawn(p);
                if (frames > 30 && frames < 110 && (uint)townie < 200u) Main.instance.currentNPCShowingChatBubble = townie;
                if (frames == 40) Direct(p);
                if (frames == 50) Statue(p);
                if (frames == 110) Drawn();
                if (frames == 120) Kill();
                if (frames == 130) Save();
            }
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csnpcextra END pass={pass} fail={fail}");
    }

    static int FindKeeper()
    {
        for (int i = 0; i < 200; i++)
            if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<Keeper>()) return i;
        return -1;
    }

    static int Spawn(Player p, int dx, int dy, int type) =>
        NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X + dx, (int)p.Center.Y + dy, type);

    static void Spawn(Player p)
    {
        int rollsBefore = Townie.Rolls, profilesBefore = ExtraGlobal.Profiles;
        townie = Spawn(p, 120, 10, ModContent.NPCType<Townie>());
        guide = Spawn(p, -220, 10, NPCID.Guide);
        plain = Spawn(p, 60, -40, ModContent.NPCType<Plain>());
        critter = Spawn(p, 30, -10, ModContent.NPCType<Critter>());
        looter = Spawn(p, -60, -40, ModContent.NPCType<Looter>());
        keeper = Spawn(p, -120, -60, ModContent.NPCType<Keeper>());
        bunny = Spawn(p, 40, -10, NPCID.Bunny);
        Main.npc[plain].life = 50;
        Main.npc[townie].ForcePartyHatOn = true;
        Main.npc[plain].ForcePartyHatOn = true;
        ((Keeper)Main.npc[keeper].ModNPC!).Data = 77;
        merchant = Spawn(p, -160, 10, NPCID.TravellingMerchant);
        Main.npc[merchant].GetGlobalNPC<ExtraGlobal>().Keep = 5;
        Main.npc[guide].GetGlobalNPC<ExtraGlobal>().Mark = 4242;
        NPC t = Main.npc[townie];
        Report("TownNPCProfile rolls the variation and names a new town NPC", Townie.Rolls > rollsBefore && t.GivenName == "ProfileName",
            $"rolls={Townie.Rolls - rollsBefore} name='{t.GivenName}'");
        Report("ModifyTownNPCProfile sees a vanilla town NPC, which keeps its own profile when it returns null",
            ExtraGlobal.Profiles > profilesBefore && !string.IsNullOrEmpty(Main.npc[guide].GivenName), $"calls={ExtraGlobal.Profiles - profilesBefore} name='{Main.npc[guide].GivenName}'");
    }

    static void Direct(Player p)
    {
        NPC pl = Main.npc[plain];
        Report("CanFallThroughPlatforms decides the platform fall", pl.Collision_DecideFallThroughPlatforms(), "");

        Rectangle victim = p.Hitbox, box = pl.Hitbox;
        int slot = -1;
        float mult = 1f;
        NPC.GetMeleeCollisionData(ref victim, plain, ref slot, ref mult, ref box);
        Report("ModifyCollisionData changes the cooldown slot, damage and hitbox", slot == ImmunityCooldownID.Bosses && Math.Abs(mult - 0.5f) < 0.001f && box.Width == pl.width + 20,
            $"slot={slot} mult={mult} width={box.Width}");

        Report("UsesPartyHat false hides a forced party hat", !pl.UsesPartyHat(), "");
        string typeName = Main.npc[townie].TypeName ?? "";
        Report("ModifyTypeName changes TypeName", typeName.EndsWith("+mod"), $"'{typeName}'");

        NPC t = Main.npc[townie];
        var above = new Rectangle((int)t.Center.X - 2, (int)t.position.Y - 60, 4, 4);
        int hovers = Townie.Hovers;
        Main.instance.HoverOverNPCs(above);
        Report("ModifyHoverBoundingBox grows the hover box and PreHoverInteract sees the mouse inside it", Townie.Hovers > hovers && Townie.HoverIntersects,
            $"calls={Townie.Hovers - hovers} intersects={Townie.HoverIntersects}");
        int plainHovers = Plain.HoverCalls;
        Main.instance.HoverOverNPCs(new Rectangle((int)pl.Center.X - 2, (int)pl.Center.Y - 2, 4, 4));
        Report("PreHoverInteract false blocks the hover and the NPC stays active", Plain.HoverCalls > plainHovers && pl.active, $"calls={Plain.HoverCalls - plainHovers} active={pl.active}");

        Item net = p.inventory[49];
        int old = net.type, oldStack = net.stack;
        net.SetDefaults(ItemID.BugNet, default);
        NPC c = Main.npc[critter], b = Main.npc[bunny];
        var swing = Rectangle.Union(c.Hitbox, b.Hitbox);
        int catches = ExtraGlobal.Catches;
        p.ItemCheck_CatchCritters(net, swing);
        net.SetDefaults(old, default);
        net.stack = oldStack;
        Report("CanBeCaughtBy true catches a mod NPC and OnCaughtBy runs", Critter.Caught == 1 && !Critter.Failed && !c.active, $"caught={Critter.Caught} failed={Critter.Failed} active={c.active}");
        Report("CanBeCaughtBy false keeps a vanilla critter", ExtraGlobal.Catches > catches && b.active, $"calls={ExtraGlobal.Catches - catches} active={b.active}");
    }

    static void Statue(Player p)
    {
        int x = (int)(p.Center.X / 16f) + 10, y = (int)(p.Bottom.Y / 16f) - 1;
        bool placed = WorldGen.PlaceTile(x, y, TileID.Statues, true, true, -1, 40);
        int left = -1, top = -1;
        for (int i = x - 2; i <= x + 2 && left < 0; i++)
            for (int j = y - 4; j <= y + 1; j++)
            {
                Tile tile = Main.tile[i, j];
                if (!tile.HasTile || tile.TileType != TileID.Statues) continue;
                left = i - tile.frameX % 36 / 18;
                top = j - tile.frameY % 54 / 18;
                break;
            }
        if (left < 0)
        {
            Report("CanGoToStatue sends a mod NPC to a king statue", false, $"statue not placed (placed={placed})");
            return;
        }
        Freeze = true;
        Wiring.HitWireSingle(left, top);
        NPC t = Main.npc[townie];
        float distance = Math.Abs(t.Center.X - (left * 16 + 16));
        Report("CanGoToStatue sends a mod NPC to a king statue and OnGoToStatue runs", Townie.Statues == 1 && Townie.StatueKing && distance < 32,
            $"calls={Townie.Statues} king={Townie.StatueKing} distance={distance}");
        for (int i = left; i < left + 2; i++)
            for (int j = top; j < top + 3; j++)
                WorldGen.KillTile(i, j, noItem: true);
        Freeze = false;
    }

    static void Drawn()
    {
        NPC t = Main.npc[townie];
        Report("PartyHatPosition runs while the hat is drawn", Townie.Hats > 0, $"calls={Townie.Hats}");
        Report("ChatBubblePosition runs while the bubble is drawn", Townie.Bubbles > 0, $"calls={Townie.Bubbles}");
        Report("DrawHealthBar false replaces the vanilla bar", Plain.HealthBars > 0, $"calls={Plain.HealthBars} position={Plain.HealthBarPosition}");
        Report("BuffTownNPC adds defense after the town AI", ExtraGlobal.Buffs > 0 && Townie.MaxDefense >= t.defDefense + 7,
            $"calls={ExtraGlobal.Buffs} defense={Townie.MaxDefense} base={t.defDefense}");
        Asset<Texture2D> alt = ModContent.Request<Texture2D>("CsNpcExtra/Content/Townie_Alt");
        Report("TownNPCProfile picks the texture the town NPC draws with", Townie.Textures > 0 && TextureAssets.Npc[t.type] == alt,
            $"calls={Townie.Textures} same={TextureAssets.Npc[t.type] == alt}");
    }

    static int Gels()
    {
        int count = 0;
        for (int i = 0; i < Main.item.Length; i++)
            if (Main.item[i].active && Main.item[i].type == ItemID.Gel) count++;
        return count;
    }

    static int gelsBefore;

    static void Kill()
    {
        CsNpcExtraMod.Texts.Clear();
        NPC t = Main.npc[townie];
        t.life = 0;
        t.checkDead();
        gelsBefore = Gels();
        NPC l = Main.npc[looter];
        l.life = 0;
        l.checkDead();
    }

    static void Save()
    {
        bool custom = false, vanilla = false;
        foreach ((string said, Color color) in CsNpcExtraMod.Texts)
        {
            custom |= said == "csnpcextra townie fell" && color.R == 10 && color.G == 200 && color.B == 30;
            vanilla |= color.R == 255 && color.G == 25 && color.B == 25;
        }
        Report("ModifyDeathMessage replaces the town NPC death message", Townie.Deaths == 1 && custom && !vanilla,
            $"calls={Townie.Deaths} custom={custom} vanilla={vanilla} texts={string.Join(" | ", CsNpcExtraMod.Texts.ConvertAll(t => $"{t.Text} ({t.Color.R},{t.Color.G},{t.Color.B})"))}");
        Report("SpecialOnKill true skips the loot", Looter.Special == 1 && Gels() == gelsBefore && !Main.npc[looter].active,
            $"calls={Looter.Special} gels={Gels() - gelsBefore}");

        WorldFile.SaveWorld();
        FileData data = Main.ActiveWorldFileData;
        string path = data.Path + ".npcs.bl";
        string text = File.Exists(path) ? File.ReadAllText(path) : "";
        bool modLine = false, globalLine = false, vanillaLine = false;
        foreach (string line in text.Split('\n'))
        {
            modLine |= line.StartsWith("npc\t") && line.Contains("/Keeper\t") && line.Contains("77");
            globalLine |= line.StartsWith("gdata\t22\t") && line.Contains("4242");
            vanillaLine |= line.StartsWith("vnpc\t368\t") && line.Contains("\"keep\":5");
        }
        Report("SaveData writes a non-town mod NPC with NeedSaving", modLine, "");
        Report("GlobalNPC.SaveData writes data for a vanilla town NPC", globalLine, "");
        Report("GlobalNPC.NeedSaving keeps a vanilla NPC the game does not save", vanillaLine, "");
        Log.Info("csnpcextra phase=save (run again with KEEPSAVES=1 to check the load)");
        done = true;
    }

    static void CheckLoaded()
    {
        int k = FindKeeper();
        var own = Main.npc[k].ModNPC as Keeper;
        Report("LoadData restores a mod NPC's data", own != null && own.Loaded == 77, $"loaded={own?.Loaded}");
        int mark = ExtraGlobal.LastMark, keep = ExtraGlobal.LastKeep;
        Report("GlobalNPC.LoadData restores a vanilla town NPC's data", mark == 4242, $"mark={mark}");
        Report("a vanilla NPC kept by NeedSaving comes back with its data", keep == 5, $"keep={keep}");
        Log.Info("csnpcextra phase=load");
        done = true;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csnpcextra {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
