using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class BuilderToggleHooks
{
    static readonly List<BuilderToggle> Toggles = new();
    static int vanillaCount = -1;

    internal static int Register(BuilderToggle toggle)
    {
        if (vanillaCount < 0)
        {
            foreach (Player player in Main.player)
            {
                if (player.IsNull || player.builderAccStatus.IsNull) continue;
                vanillaCount = player.builderAccStatus.Length;
                break;
            }
            if (vanillaCount < 0) throw new InvalidOperationException("Native builder toggle state is not initialized.");
        }
        int type = vanillaCount + Toggles.Count;
        Toggles.Add(toggle);
        ItemHooks.Once("ModBuilderToggles", () => On_GUIBuilderAccToggles.Draw += Draw);
        return type;
    }

    static GameArray<int> States()
    {
        Player player = Main.LocalPlayer;
        var state = player.builderAccStatus;
        int count = vanillaCount + Toggles.Count;
        if (state.Length < count)
        {
            state = state.Resized(count);
            using var pin = new GameHandle(state.Pointer);
            player.builderAccStatus = state;
        }
        return state;
    }

    internal static int State(BuilderToggle toggle) => States()[toggle.Type];
    internal static void SetState(BuilderToggle toggle, int value) => States()[toggle.Type] = value;

    static List<BuilderToggle> Ordered()
    {
        var ordered = new List<BuilderToggle>(Toggles);
        foreach (var toggle in Toggles)
        {
            BuilderToggle? anchor = toggle.OrderPosition switch { BuilderToggle.Before before => before.Toggle, BuilderToggle.After after => after.Toggle, _ => null };
            if (anchor == null || !ordered.Contains(anchor)) continue;
            ordered.Remove(toggle);
            int index = ordered.IndexOf(anchor);
            ordered.Insert(index + (toggle.OrderPosition is BuilderToggle.After ? 1 : 0), toggle);
        }
        return ordered;
    }

    static void Sprite(SpriteBatch batch, BuilderToggleDrawParams p) =>
        batch.Draw(p.Texture, p.Position, p.Frame, p.Color, 0f, p.Frame.Size() / 2f, p.Scale, p.SpriteEffects, 0f);

    static void Draw(On_GUIBuilderAccToggles.orig_Draw orig, GUIBuilderAccToggles self)
    {
        orig(self);
        if (!self.Enabled || Main.gameMenu || Main.LocalPlayer.IsNull) return;
        Rectangle region = self.GetRegion();
        float cell = self.toggleSize.X > 0 ? self.toggleSize.X : 44f;
        float x = Math.Clamp(region.X, 0f, Math.Max(0f, Main.screenWidth - cell));
        float y = Math.Clamp(region.Y + region.Height + 4f, 0f, Math.Max(0f, Main.screenHeight - cell));
        SpriteBatch batch = Main.spriteBatch;
        foreach (BuilderToggle toggle in Ordered())
        {
            try
            {
                if (!toggle.Active()) continue;
                if (x + cell > Main.screenWidth) { x = region.X; y += cell; }
                var area = new Rectangle((int)x, (int)y, (int)cell, (int)cell);
                var texture = ModContent.Request<Texture2D>(toggle.Texture).Value;
                var draw = new BuilderToggleDrawParams { Texture = texture, Frame = texture.Frame(), Position = new Vector2(x + cell / 2, y + cell / 2), Scale = cell / 24f };
                if (toggle.Draw(batch, ref draw)) Sprite(batch, draw);
                if (area.Contains(Main.MouseScreen.ToPoint()))
                {
                    Main.LocalPlayer.mouseInterface = true;
                    var hover = ModContent.Request<Texture2D>(toggle.HoverTexture).Value;
                    var hoverDraw = new BuilderToggleDrawParams { Texture = hover, Frame = hover.Frame(), Position = draw.Position, Scale = draw.Scale, Color = Color.Yellow };
                    if (toggle.DrawHover(batch, ref hoverDraw)) Sprite(batch, hoverDraw);
                    Utils.DrawBorderString(batch, toggle.DisplayValue(), new Vector2(x, Math.Max(0f, y - 24)), Color.White, 0.8f);
                    if (Main.mouseLeft && Main.mouseLeftRelease)
                    {
                        Main.mouseLeftRelease = false;
                        SoundStyle? sound = (SoundStyle)SoundID.MenuTick;
                        if (toggle.OnLeftClick(ref sound))
                        {
                            toggle.CurrentState = (toggle.CurrentState + 1) % Math.Max(1, toggle.NumberOfStates);
                            if (sound.HasValue) SoundEngine.PlaySound(sound.Value);
                        }
                    }
                    if (Main.mouseRight && Main.mouseRightRelease) { Main.mouseRightRelease = false; toggle.OnRightClick(); }
                }
                x += cell;
            }
            catch (Exception e) { ItemHooks.Report($"{toggle.FullName}.BuilderToggle", e); }
        }
    }
}
