using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsBugs;

public sealed class CsBugsMod : Mod
{
    static int frame, pass, fail, pet, bone, person, guide, cx, floor;
    static int boneFrames, interactable = -1;
    static bool wantTap, tapped;
    static readonly HashSet<int> boneSeen = new();

    static ILog Log => ModContent.GetInstance<CsBugsMod>().Logger;

    static int Npc(string name) => ModContent.Find<ModNPC>("ExampleMod/" + name).Type;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Main.DrawProjDirect += DrawProj;
    }

    static void DrawProj(On_Main.orig_DrawProjDirect orig, Main self, Projectile proj, Player overridePlayer)
    {
        bool tap = wantTap && proj.whoAmI == interactable;
        Player player = Main.LocalPlayer;
        if (tap)
        {
            wantTap = false;
            player.checkProjectilesFrame = XNAUnityRunner._frame;
            player.lastMouseInterface = false;
            player.releaseUseTile = true;
            Main.SmartInteractProj = interactable;
            Main.gamePaused = false;
        }
        orig(self, proj, overridePlayer);
        if (tap)
            tapped = !player.releaseUseTile;
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csbugs {name}: {(ok ? "ok" : "FAIL")} {detail}");
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

    static int Floor(int x, int fromY)
    {
        for (int y = fromY; y < Main.maxTilesY - 10; y++)
            if (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType])
                return y;
        return fromY;
    }

    static void Step(Player self)
    {
        if (frame > 30 && frame < 300)
            WatchBone();
        switch (frame)
        {
            case 30:
                cx = (int)(self.Center.X / 16);
                floor = Floor(cx, (int)(self.Bottom.Y / 16) - 2);
                Heads(self);
                Save();
                Emote(self);
                Interactable(self);
                break;
            case 60:
                Guide();
                break;
            case 200:
                Log.Info("csbugs SHOT walkers");
                break;
            case 300:
                Report("the bone merchant walks through its town frames", boneSeen.Count >= 4 && boneSeen.All(f => f < 25), "frames=" + string.Join(",", boneSeen.OrderBy(f => f)));
                MenuEmotes();
                break;
            case 330:
                Log.Info("csbugs SHOT emotes");
                break;
            case 400:
                Main.playerInventory = false;
                wantTap = true;
                break;
            case 420:
                Report("a tap reaches the interactable projectile", tapped);
                break;
            case 620:
                GUIInstance.Active.GUIEmotesWindow.SetCategory(GUIEmotesWindow.Category.TownNPCs);
                break;
            case 640:
                Log.Info("csbugs SHOT emotetown");
                break;
            case 930:
                GUIInstance.Active.GUIEmotesWindow.SetCategory(GUIEmotesWindow.Category.Items);
                break;
            case 950:
                Log.Info("csbugs SHOT emoteitems");
                break;
            case 1240:
                ClickEmote();
                break;
            case 1260:
                GUIInstance.Active.GUIEmotesWindow.SetDisplayingEmotes(false);
                Log.Info("csbugs SHOT bubble");
                break;
            case 1550:
                StartMusic();
                break;
            case 1730:
                ModInvasion crusade = ModContent.Find<ModInvasion>("ExampleMod/PaladinCrusade");
                Report("the invasion changes the music", Main.newMusic == crusade.Music, $"new={Main.newMusic} wanted={crusade.Music} active={InvasionLoader.Active?.Name}");
                Log.Info($"csbugs END pass={pass} fail={fail}");
                break;
        }
    }

    static void Heads(Player self)
    {
        pet = NPC.NewNPC(self.GetSource_FromThis(), (cx + 6) * 16, floor * 16, Npc("ExampleTownPet"));
        bone = NPC.NewNPC(self.GetSource_FromThis(), (cx + 12) * 16, floor * 16, Npc("ExampleBoneMerchant"));
        person = NPC.NewNPC(self.GetSource_FromThis(), (cx - 6) * 16, floor * 16, Npc("ExamplePerson"));
        var slots = new HashSet<int>();
        NPC petNpc = Main.npc[pet];
        for (int variant = 0; variant <= 6; variant++)
        {
            petNpc.townNpcVariationIndex = variant;
            int head = petNpc.ModNPC.TownNPCProfile().GetHeadTextureIndex(petNpc);
            Report($"pet variant {variant} has a real head", head > 0 && TextureAssets.NpcHead[head] != null && TextureAssets.NpcHead[head].Value.Width > 0, $"head={head}");
            slots.Add(head);
        }
        Report("each pet variant has its own head", slots.Count == 7, "heads=" + string.Join(",", slots));
        Report("an NPC without a head reads 0, not -1", NPC.TypeToDefaultHeadIndex(Npc("ExampleBoneMerchant")) >= 0, $"head={NPC.TypeToDefaultHeadIndex(Npc("ExampleBoneMerchant"))}");
        GUIHousingMenu housing = GUIInstance.Active.GUIHousingMenu;
        housing.RefreshNPCEntries();
        Report("the housing list opens with a town pet", new GameList(housing._activeNPCEntries).Count > 0, $"entries={new GameList(housing._activeNPCEntries).Count}");
        var list = new List<int>();
        NPCLoader.PickEmote(Main.npc[person], self, list, default);
        Report("PickEmote works without another NPC", list.Count > 0, $"count={list.Count}");
    }

    static void WatchBone()
    {
        NPC npc = Main.npc[bone];
        Main.playerInventory = false;
        npc.velocity.X = 1f;
        if (npc.frame.Height > 0)
            boneSeen.Add(npc.frame.Y / npc.frame.Height);
    }

    static void Save()
    {
        var tag = new TagCompound();
        int modType = ModContent.Find<ModItem>("ExampleMod/ExampleSandBlock").Type;
        tag["items"] = new List<Item> { new Item(ItemID.Wood, 7), new Item(modType, 3), new Item(ItemID.TerraBlade, 1, PrefixID.Legendary) };
        List<Item> back = TagIO.FromStream(ToStream(tag)).Get<List<Item>>("items");
        Report("items go through a tag", back.Count == 3 && back[0].type == ItemID.Wood && back[0].stack == 7 && back[1].type == modType && back[1].stack == 3 && back[2].prefix == PrefixID.Legendary, $"types={string.Join(",", back.Select(i => i.type))}");
        ModSystem merchant = ModLoader.GetMod("ExampleMod").GetContent<ModSystem>().First(s => s.Name == "TravelingMerchantSystem");
        var world = new TagCompound();
        merchant.SaveWorldData(world);
        Report("TravelingMerchantSystem saves", world.ContainsKey("shopItems"));
    }

    static System.IO.MemoryStream ToStream(TagCompound tag)
    {
        var stream = new System.IO.MemoryStream();
        TagIO.ToStream(tag, stream);
        stream.Position = 0;
        return stream;
    }

    static void Emote(Player self)
    {
        int mine = ModContent.Find<ModEmoteBubble>("ExampleMod/MinionBossEmote").Type;
        Log.Info($"csbugs emote count={EmoteID.Count} mine={mine}");
        Report("the mod emote does not take a vanilla id", mine >= EmoteID.Count && EmoteBubbleLoader.GetEmoteBubble(EmoteID.Count - 1) == null);
        EmoteBubble.MakeLocalPlayerEmote(mine);
    }

    static void MenuEmotes()
    {
        GUIEmotesWindow window = GUIInstance.Active.GUIEmotesWindow;
        window.SetDisplayingEmotes(true);
        var seen = new Dictionary<string, string>();
        foreach (GUIEmotesWindow.Category category in Enum.GetValues(typeof(GUIEmotesWindow.Category)))
        {
            try
            {
                window.SetCategory(category);
            }
            catch (Exception e)
            {
                Log.Info($"csbugs emote category {category}: {e.Message}");
                continue;
            }
            var entries = new GameList(window._emoteEntries);
            var ids = new List<int>();
            for (int k = 0; k < entries.Count; k++)
                ids.Add(entries.Get<int>(k));
            Log.Info($"csbugs emote category {category}: {string.Join(",", ids)}");
        }
        window.SetCategory(GUIEmotesWindow.Category.Bosses);
    }

    static void ClickEmote()
    {
        GUIEmotesWindow window = GUIInstance.Active.GUIEmotesWindow;
        window.SetCategory(GUIEmotesWindow.Category.TownNPCs);
        var entries = new GameList(window._emoteEntries);
        int index = 0;
        while (index < entries.Count && entries.Get<int>(index) < EmoteID.Count)
            index++;
        int before = new GameDictionary(EmoteBubble.byID).Count;
        Main.mouseLeft = false;
        Main.mouseLeftRelease = false;
        window.EmoteOver(index);
        Main.mouseLeftRelease = true;
        Report("tapping a mod emote shows its bubble", new GameDictionary(EmoteBubble.byID).Count > before, $"before={before} after={new GameDictionary(EmoteBubble.byID).Count}");
    }

    static void Guide()
    {
        guide = NPC.NewNPC(Main.LocalPlayer.GetSource_FromThis(), (cx + 2) * 16, floor * 16, NPCID.Guide);
        Main.LocalPlayer.SetTalkNPC(guide);
        Main.npcChatText = "x";
        GUIInstance.Active.GUINPCDialogue.Option2Clicked();
        Report("the guide's crafting button works", Main.InGuideCraftMenu, $"menu={Main.InGuideCraftMenu}");
        Main.InGuideCraftMenu = false;
        Main.LocalPlayer.SetTalkNPC(-1);
        Main.npcChatText = "";
        Main.playerInventory = false;
        Main.npc[guide].active = false;
    }

    static void Interactable(Player self)
    {
        interactable = Projectile.NewProjectile(self.GetSource_FromThis(), self.Center + new Vector2(30, -40), Vector2.Zero, ModContent.Find<ModProjectile>("ExampleMod/ExampleInteractableProjectile").Type, 0, 0, Main.myPlayer);
        Report("the mod projectile is interactable", Main.projectile[interactable].IsInteractable() && self.IsProjectileInteractableAndInInteractionRange(Main.projectile[interactable], self.Center));
    }

    static void StartMusic()
    {
        Main.dayTime = false;
        Main.time = 0;
        ModInvasion crusade = ModContent.Find<ModInvasion>("ExampleMod/PaladinCrusade");
        Report("the invasion starts", InvasionLoader.Start(crusade), $"active={InvasionLoader.Active?.Name}");
    }
}
