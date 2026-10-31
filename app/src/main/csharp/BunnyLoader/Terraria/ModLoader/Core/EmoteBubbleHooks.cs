using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe class EmoteBubbleHooks
{
    static readonly Dictionary<int, IntPtr> live = new();
    static readonly List<(int Id, IntPtr Bubble, int Frame, int Counter)> saved = new();
    static ModEmoteBubble? menuEmote;
    static int menuIcon;

    static readonly int[] MenuCategories = { EmoteID.Category.General, EmoteID.Category.RockPaperScissors, EmoteID.Category.Items,
        EmoteID.Category.BiomesAndEvents, EmoteID.Category.Town, EmoteID.Category.Critters, EmoteID.Category.Bosses };

    internal static void Install() => ItemHooks.Once("EmoteBubbles", () =>
    {
        On_EmoteBubble.Draw += Draw;
        On_EmoteBubble.UpdateAll += UpdateAll;
        On_EmoteBubble.NewBubble += NewBubble;
        On_EmoteBubble.NewBubbleNPC += NewBubbleNPC;
        On_EmoteBubble.NewLocalOnlyBubble += NewLocalOnlyBubble;
        On_GUIEmotesWindow.GetEmotesGeneral += (orig, self, emotes) => { orig(self, emotes); AddToMenu(emotes, 0); };
        On_GUIEmotesWindow.GetEmotesRPS += (orig, self, emotes) => { orig(self, emotes); AddToMenu(emotes, 1); };
        On_GUIEmotesWindow.GetEmotesItems += (orig, self, emotes) => { orig(self, emotes); AddToMenu(emotes, 2); };
        On_GUIEmotesWindow.GetEmotesBiomesAndEvents += (orig, self, emotes) => { orig(self, emotes); AddToMenu(emotes, 3); };
        On_GUIEmotesWindow.GetEmotesTownNPCs += (orig, self, emotes) => { orig(self, emotes); AddToMenu(emotes, 4); };
        On_GUIEmotesWindow.GetEmotesCritters += (orig, self, emotes) => { orig(self, emotes); AddToMenu(emotes, 5); };
        On_GUIEmotesWindow.GetEmotesBosses += (orig, self, emotes) => { orig(self, emotes); AddToMenu(emotes, 6); };
        On_GUIEmotesWindow.EmoteDraw += EmoteDraw;
        On_GUIEmotesWindow.GetFrame += GetMenuFrame;
    });

    internal static void InstallNPC(NPCMethod methods)
    {
        if ((methods & NPCMethod.PickEmote) != 0) ItemHooks.Once("NPCPickEmote", () => On_EmoteBubble.PickNPCEmote += PickNPCEmote);
        if ((methods & NPCMethod.EmoteBubblePosition) != 0) ItemHooks.Once("NPCEmotePosition", () => On_EmoteBubble.GetPosition += GetPosition);
    }

    static bool Wanted(EmoteBubble bubble) => EmoteBubbleLoader.GetEmoteBubble(bubble.emote) != null || EmoteBubbleLoader.Globals.Count > 0;

    static void Track(EmoteBubble bubble)
    {
        lock (live) live[bubble.ID] = bubble.Pointer;
    }

    static void Spawned(int id)
    {
        if (!new GameDictionary(EmoteBubble.byID).TryGetValue(id, out IntPtr pointer) || pointer == IntPtr.Zero) return;
        var bubble = new EmoteBubble(pointer);
        if (!Wanted(bubble)) return;
        Track(bubble);
        EmoteBubbleLoader.OnSpawn(bubble);
    }

    static int NewBubble(On_EmoteBubble.orig_NewBubble orig, int emoticon, WorldUIAnchor bubbleAnchor, int time)
    {
        int id = orig(emoticon, bubbleAnchor, time);
        try { Spawned(id); }
        catch (Exception e) { ItemHooks.Report("emote bubble spawn", e); }
        return id;
    }

    static int NewBubbleNPC(On_EmoteBubble.orig_NewBubbleNPC orig, WorldUIAnchor bubbleAnchor, int time, WorldUIAnchor other)
    {
        int id = orig(bubbleAnchor, time, other);
        try { Spawned(id); }
        catch (Exception e) { ItemHooks.Report("emote bubble spawn", e); }
        return id;
    }

    static int NewLocalOnlyBubble(On_EmoteBubble.orig_NewLocalOnlyBubble orig, int emoticon, WorldUIAnchor bubbleAnchor, int time)
    {
        int id = orig(emoticon, bubbleAnchor, time);
        try { Spawned(id); }
        catch (Exception e) { ItemHooks.Report("emote bubble spawn", e); }
        return id;
    }

    static void UpdateAll(On_EmoteBubble.orig_UpdateAll orig)
    {
        lock (live)
        {
            if (live.Count == 0)
            {
                orig();
                return;
            }
            saved.Clear();
            foreach (var (id, pointer) in live)
            {
                var bubble = new EmoteBubble(pointer);
                saved.Add((id, pointer, bubble.frame, bubble.frameCounter));
            }
        }
        orig();
        var byId = new GameDictionary(EmoteBubble.byID);
        lock (live)
        {
            foreach (var (id, pointer, frame, counter) in saved)
            {
                if (!byId.TryGetValue(id, out IntPtr current) || current != pointer)
                {
                    live.Remove(id);
                    EmoteBubbleLoader.Forget(pointer);
                    continue;
                }
                var bubble = new EmoteBubble(pointer);
                bubble.frame = frame;
                bubble.frameCounter = counter;
                if (!EmoteBubbleLoader.UpdateFrame(bubble) || bubble.lifeTime <= 0 || ++bubble.frameCounter < 8) continue;
                bubble.frameCounter = 0;
                if (++bubble.frame >= 2) bubble.frame = 0;
            }
        }
    }

    static Texture2D Sheet => new(Host.Native()->TextureAssetValue(TextureAssets.Extra[48].Pointer));

    static void Draw(On_EmoteBubble.orig_Draw orig, EmoteBubble self, SpriteBatch sb)
    {
        ModEmoteBubble? own = EmoteBubbleLoader.InstanceOf(self);
        if (own == null && (EmoteBubbleLoader.Globals.Count == 0 || self.emote < 0))
        {
            orig(self, sb);
            return;
        }
        Track(self);
        try { DrawBubble(self, sb, own); }
        catch (Exception e) { ItemHooks.Report($"{own?.FullName ?? "GlobalEmoteBubble"} draw", e); }
    }

    static void DrawBubble(EmoteBubble bubble, SpriteBatch sb, ModEmoteBubble? own)
    {
        Texture2D sheet = Sheet;
        int rows = EmoteBubble.EMOTE_SHEET_VERTICAL_FRAMES;
        Vector2 at = bubble.GetPosition(out SpriteEffects effect);
        var position = new Vector2(MathF.Floor(at.X), MathF.Floor(at.Y));
        bool hidden = bubble.lifeTime < 6 || bubble.lifeTimeStart - bubble.lifeTime < 6;
        Rectangle back = sheet.Frame(8, rows, hidden ? 0 : 1, 0);
        var origin = new Vector2(back.Width / 2, back.Height);
        if (Main.LocalPlayer.gravDir == -1f)
        {
            origin.Y = 0f;
            effect |= SpriteEffects.FlipVertically;
            position = Main.ReverseGravitySupport(position, 0f);
        }
        Texture2D texture;
        Rectangle frame;
        if (own != null)
        {
            texture = EmoteBubbleLoader.TextureOf(own);
            int half = texture.Width / 2;
            frame = EmoteBubbleLoader.GetFrame(bubble) ?? new Rectangle(half * bubble.frame, 0, half, texture.Height);
        }
        else
        {
            texture = sheet;
            int emote = bubble.emote;
            if ((emote == 87 || emote == 89) && (effect & SpriteEffects.FlipHorizontally) != 0)
            {
                effect &= ~SpriteEffects.FlipHorizontally;
                position.X += 4f;
            }
            frame = EmoteBubbleLoader.GetFrame(bubble) ?? sheet.Frame(8, rows, emote * 2 % 8 + bubble.frame, 1 + emote / 4);
        }
        if (EmoteBubbleLoader.PreDraw(bubble, sb, texture, position, frame, origin, effect))
        {
            sb.Draw(sheet, position, back, Color.White, 0f, origin, 1f, effect, 0f);
            if (!hidden) sb.Draw(texture, position, frame, Color.White, 0f, origin, 1f, effect, 0f);
        }
        EmoteBubbleLoader.PostDraw(bubble, sb, texture, position, frame, origin, effect);
    }

    static void AddToMenu(GameObject emotes, int category)
    {
        try
        {
            var list = new GameList(emotes);
            foreach (ModEmoteBubble emote in EmoteBubbleLoader.InCategory(MenuCategories[category]))
            {
                bool unlocked;
                try { unlocked = emote.IsUnlocked(); }
                catch (Exception e)
                {
                    ItemHooks.Report($"{emote.FullName}.IsUnlocked", e);
                    unlocked = false;
                }
                if (unlocked) list.Add(emote.Type);
            }
        }
        catch (Exception e) { ItemHooks.Report("emote menu", e); }
    }

    static void EmoteDraw(On_GUIEmotesWindow.orig_EmoteDraw orig, GUIEmotesWindow self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        var entries = new GameList(self._emoteEntries);
        ModEmoteBubble? own = (uint)index < (uint)entries.Count ? EmoteBubbleLoader.GetEmoteBubble(entries.Get<int>(index)) : null;
        if (own == null || !MenuIconReady())
        {
            orig(self, gridLayout, index, position, scale);
            return;
        }
        menuEmote = own;
        Host.Native()->SetEmoteMenuIcon(EmoteBubbleLoader.TextureOf(own).Pointer);
        try { orig(self, gridLayout, index, position, scale); }
        finally
        {
            Host.Native()->SetEmoteMenuIcon(IntPtr.Zero);
            menuEmote = null;
        }
    }

    static bool MenuIconReady()
    {
        if (menuIcon == 0) menuIcon = Host.Native()->InstallEmoteMenuIcon() != 0 ? 1 : -1;
        return menuIcon > 0;
    }

    static Rectangle GetMenuFrame(On_GUIEmotesWindow.orig_GetFrame orig, GUIEmotesWindow self, int emoteIndex)
    {
        Rectangle frame = orig(self, emoteIndex);
        ModEmoteBubble? own = menuEmote;
        if (own == null && EmoteBubbleLoader.Globals.Count == 0) return frame;
        int anim = frame.Width > 0 ? frame.X / frame.Width % 2 : 0;
        if (own == null) return EmoteBubbleLoader.GetFrameInEmoteMenu(emoteIndex, anim, self._frameCounter) ?? frame;
        Texture2D texture = EmoteBubbleLoader.TextureOf(own);
        int half = texture.Width / 2;
        return EmoteBubbleLoader.GetFrameInEmoteMenu(own.Type, anim, self._frameCounter) ?? new Rectangle(anim * half, 0, half, texture.Height);
    }

    static NPC AnchoredNPC(EmoteBubble bubble)
    {
        WorldUIAnchor anchor = bubble.anchor;
        if (anchor.IsNull || anchor.type != WorldUIAnchor.AnchorType.Entity || anchor.entity.IsNull) return default;
        return NPC.Is(anchor.entity) ? new NPC(anchor.entity.Pointer) : default;
    }

    static void PickNPCEmote(On_EmoteBubble.orig_PickNPCEmote orig, EmoteBubble self, WorldUIAnchor other)
    {
        orig(self, other);
        NPC npc = AnchoredNPC(self);
        if (npc.IsNull || !NPCLoader.HasHooks(npc, NPCMethod.PickEmote)) return;
        Player closest = Main.player[Player.FindClosest(npc.position, npc.width, npc.height)];
        var emotes = new List<int> { self.emote };
        int? picked = NPCLoader.PickEmote(npc, closest, emotes, other);
        if (picked.HasValue) self.emote = picked.Value;
        else if (emotes.Count > 1) self.emote = emotes[Main.rand.Next(emotes.Count)];
    }

    static Vector2 GetPosition(On_EmoteBubble.orig_GetPosition orig, EmoteBubble self, out SpriteEffects effect)
    {
        Vector2 position = orig(self, out effect);
        NPC npc = AnchoredNPC(self);
        if (!npc.IsNull && NPCLoader.HasHooks(npc, NPCMethod.EmoteBubblePosition)) NPCLoader.EmoteBubblePosition(npc, ref position, ref effect);
        return position;
    }
}
