using System;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.Graphics.Renderers;

namespace Terraria.ModLoader.Core;

internal static class PlayerDrawHooks
{
    delegate ref Color ColorField(PlayerDrawSet info);

    delegate Color Tint(Player player);

    static readonly (ColorField Field, Tint Tint)[] Colors =
    {
        (i => ref i.colorHair, p => p.GetHairColor(false)),
        (i => ref i.colorEyeWhites, _ => Color.White),
        (i => ref i.colorEyes, p => p.eyeColor),
        (i => ref i.colorHead, p => p.skinColor),
        (i => ref i.colorBodySkin, p => p.skinColor),
        (i => ref i.colorLegs, p => p.skinColor),
        (i => ref i.colorShirt, p => p.shirtColor),
        (i => ref i.colorUnderShirt, p => p.underShirtColor),
        (i => ref i.colorPants, p => p.pantsColor),
        (i => ref i.colorShoes, p => p.shoeColor),
        (i => ref i.colorArmorHead, _ => Color.White),
        (i => ref i.colorArmorBody, _ => Color.White),
        (i => ref i.colorArmorLegs, _ => Color.White),
        (i => ref i.colorMount, _ => Color.White),
    };

    static Camera? camera;

    static readonly PlayerHook HookDrawEffects = PlayerLoader.Hook(nameof(ModPlayer.DrawEffects));
    static readonly PlayerHook HookModifyDrawInfo = PlayerLoader.Hook(nameof(ModPlayer.ModifyDrawInfo));
    static readonly PlayerHook HookTransformDrawData = PlayerLoader.Hook(nameof(ModPlayer.TransformDrawData));
    static readonly PlayerHook HookDrawPlayer = PlayerLoader.Hook(nameof(ModPlayer.DrawPlayer));
    static readonly PlayerHook HookModifyScreenPosition = PlayerLoader.Hook(nameof(ModPlayer.ModifyScreenPosition));
    static readonly PlayerHook HookModifyZoom = PlayerLoader.Hook(nameof(ModPlayer.ModifyZoom));

    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModPlayer.DrawEffects)) || has(nameof(ModPlayer.ModifyDrawInfo)))
            ItemHooks.Once("PlayerDrawInfo", () => On_PlayerDrawSet.BoringSetup += BoringSetup);
        if (has(nameof(ModPlayer.HideDrawLayers)) || has(nameof(ModPlayer.ModifyDrawLayerOrdering))) InstallLayers();
        if (has(nameof(ModPlayer.TransformDrawData)))
            ItemHooks.Once("PlayerTransformDrawData", () => On_PlayerDrawLayers.DrawPlayer_RenderAllLayers += RenderAllLayers);
        if (has(nameof(ModPlayer.DrawPlayer)))
            ItemHooks.Once("PlayerDrawPlayer", () =>
            {
                On_LegacyPlayerRenderer.DrawPlayerFull += DrawPlayerFull;
                On_LegacyPlayerRenderer.PrepareDrawForFrame += PrepareDrawForFrame;
            });
        if (has(nameof(ModPlayer.ModifyScreenPosition))) InstallCamera(true);
        if (has(nameof(ModPlayer.ModifyZoom)))
            ItemHooks.Once("PlayerZoom", () => On_SpriteViewMatrix.set_Zoom += SetZoom);
    }

    static bool cameraPlayer, cameraSystem;

    internal static void InstallCamera(bool player)
    {
        if (player) cameraPlayer = true;
        else cameraSystem = true;
        ItemHooks.Once("ScreenPosition", () => On_Main.DoDraw_UpdateCameraPosition += UpdateCameraPosition);
    }

    internal static void InstallLayers() =>
        ItemHooks.Once("PlayerDrawLayers", () => On_LegacyPlayerRenderer.DrawPlayer_UseNormalLayers += UseNormalLayers);

    static void UseNormalLayers(On_LegacyPlayerRenderer.orig_DrawPlayer_UseNormalLayers orig, PlayerDrawSet drawinfo)
    {
        PlayerDrawLayer[] layers;
        bool vanilla;
        try { layers = PlayerDrawLayerLoader.GetDrawLayers(drawinfo, out vanilla); }
        catch (Exception e)
        {
            ItemHooks.Report("PlayerDrawLayers", e);
            orig(drawinfo);
            return;
        }
        if (vanilla)
        {
            orig(drawinfo);
            return;
        }
        PlayerDrawLayer.Transformation? active = null;
        foreach (PlayerDrawLayer layer in layers)
        {
            try { layer.DrawMerged(ref drawinfo, ref active); }
            catch (Exception e) { ItemHooks.Report($"{layer.FullName}.Draw", e); }
        }
        try { PlayerDrawLayer.Transformation.Switch(ref drawinfo, ref active, null); }
        catch (Exception e) { ItemHooks.Report("PlayerDrawLayers", e); }
        PlayerDrawLayers.DrawPlayer_MakeIntoFirstFractalAfterImage(drawinfo);
    }

    static byte Channel(float value) => (byte)Math.Clamp((int)(float.IsFinite(value) ? value : 0f), 0, 255);

    static void BoringSetup(On_PlayerDrawSet.orig_BoringSetup orig, PlayerDrawSet self, Player player, ref Vector2 drawPosition, float shadowOpacity, float rotation, ref Vector2 rotationOrigin, Projectile overrideHeldProjectile)
    {
        orig(self, player, ref drawPosition, shadowOpacity, rotation, ref rotationOrigin, overrideHeldProjectile);
        if (HookDrawEffects.Hooked.Length > 0) Effects(player, self);
        PlayerDrawSet info = self;
        PlayerLoader.Each(player, HookModifyDrawInfo, 0, ref info, static (m, _, ref i) => m.ModifyDrawInfo(ref i));
    }

    struct Tones
    {
        public float R, G, B, A;
        public bool FullBright;
    }

    static void Effects(Player player, PlayerDrawSet info)
    {
        var tones = new Tones { R = 1f, G = 1f, B = 1f, A = 1f };
        PlayerLoader.Each(player, HookDrawEffects, info, ref tones,
            static (m, i, ref t) => m.DrawEffects(i, ref t.R, ref t.G, ref t.B, ref t.A, ref t.FullBright));
        if (tones.R == 1f && tones.G == 1f && tones.B == 1f && tones.A == 1f && !tones.FullBright) return;
        float shadow = info.shadow;
        foreach ((ColorField field, Tint tint) in Colors)
        {
            ref Color color = ref field(info);
            Color baseColor = tones.FullBright ? player.GetImmuneAlpha(tint(player), shadow) : color;
            color = new Color(Channel(baseColor.R * tones.R), Channel(baseColor.G * tones.G), Channel(baseColor.B * tones.B), Channel(baseColor.A * tones.A));
        }
    }

    static void RenderAllLayers(On_PlayerDrawLayers.orig_DrawPlayer_RenderAllLayers orig, PlayerDrawSet drawinfo, GameArray<Vector2> positionalOffsets)
    {
        Player player = drawinfo.drawPlayer;
        if (!player.IsNull)
        {
            PlayerDrawSet info = drawinfo;
            PlayerLoader.Each(player, HookTransformDrawData, 0, ref info, static (m, _, ref i) => m.TransformDrawData(ref i));
        }
        orig(drawinfo, positionalOffsets);
    }

    static void DrawPlayerFull(On_LegacyPlayerRenderer.orig_DrawPlayerFull orig, LegacyPlayerRenderer self, Camera camera, Player drawPlayer)
    {
        Camera? outer = PlayerDrawHooks.camera;
        PlayerDrawHooks.camera = camera;
        try { orig(self, camera, drawPlayer); }
        finally { PlayerDrawHooks.camera = outer; }
    }

    static void PrepareDrawForFrame(On_LegacyPlayerRenderer.orig_PrepareDrawForFrame orig, LegacyPlayerRenderer self, Player drawPlayer)
    {
        orig(self, drawPlayer);
        if (camera is { } current) PlayerLoader.Each(drawPlayer, HookDrawPlayer, current, static (m, c) => m.DrawPlayer(c));
    }

    static void UpdateCameraPosition(On_Main.orig_DoDraw_UpdateCameraPosition orig)
    {
        orig();
        if (Main.gameMenu) return;
        if (cameraPlayer) PlayerLoader.Each(Main.LocalPlayer, HookModifyScreenPosition, static m => m.ModifyScreenPosition());
        if (cameraSystem) SystemLoader.ModifyScreenPosition();
    }

    static void SetZoom(On_SpriteViewMatrix.orig_set_Zoom orig, SpriteViewMatrix self, Vector2 value)
    {
        if (Main.gameMenu || self != Main.GameViewMatrix || value.X <= 0f)
        {
            orig(self, value);
            return;
        }
        float zoom = value.X;
        PlayerLoader.Each(Main.LocalPlayer, HookModifyZoom, 0, ref zoom, static (m, _, ref z) => m.ModifyZoom(ref z));
        float factor = float.IsFinite(zoom) && zoom > 0f ? zoom / value.X : 1f;
        orig(self, factor == 1f ? value : new Vector2(value.X * factor, value.Y * factor));
    }
}
