using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader.Hooks;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Graphics.Effects;

namespace Terraria.ModLoader.Core;

internal static unsafe class BackgroundHooks
{
    const int SurfaceFlag = 0, CurrentFlag = 1, OldFlag = 2;
    const int MenuCloseTop = 320, FrontLayerPush = -180;

    sealed class Fill
    {
        public readonly int[] Slots;
        public readonly Texture2D?[] Textures;

        public Fill(int[] slots, Texture2D?[] textures)
        {
            Slots = slots;
            Textures = textures;
        }
    }

    static readonly Dictionary<int, Fill?> Fills = new();
    static readonly int* Flags = Awake();

    static bool step1Pending;
    static float step1Top;
    static int step1Push;
    static int chosenSurface = -1;
    static float[] fadeBuffer = Array.Empty<float>();

    static int* Awake()
    {
        int* flags = (int*)NativeMemory.Alloc((nuint)(3 * sizeof(int)));
        flags[SurfaceFlag] = flags[CurrentFlag] = flags[OldFlag] = 1;
        return flags;
    }

    static GateSpec Flag(int index, int mode) =>
        new() { Arg = HookGates.AbsoluteArg, PointerOffset = -1, FieldOffset = index * sizeof(int), FieldSize = 4, Flags = mode, Address = (IntPtr)Flags };

    static void Gate<THook>(int index, int mode = 0) where THook : Delegate
    {
        HookGates.Define<THook>(() => Flag(index, mode));
        HookGates.Want<THook>();
    }

    static bool SurfaceAsleep => Flags[SurfaceFlag] == 0;

    internal static void WakeSurface() => Flags[SurfaceFlag] = 1;

    internal static void InstallSurface() => ItemHooks.Once("SurfaceBackgrounds", () =>
    {
        On_Main.GetPreferredBGStyleForPlayer += GetPreferredBGStyleForPlayer;
        Gate<On_Main.hook_set_bgStyle>(SurfaceFlag, HookGates.Invert);
        On_Main.set_bgStyle += SetBackgroundStyle;
        Gate<On_Main.hook_UpdateBGVisibility_BackLayer>(SurfaceFlag);
        On_Main.UpdateBGVisibility_BackLayer += UpdateBGVisibility_BackLayer;
        Gate<On_Main.hook_UpdateBGVisibility_FrontLayer>(SurfaceFlag);
        On_Main.UpdateBGVisibility_FrontLayer += UpdateBGVisibility_FrontLayer;
        Gate<On_Main.hook_DrawSurfaceBG_BackMountainsStep1>(SurfaceFlag);
        On_Main.DrawSurfaceBG_BackMountainsStep1 += BackMountainsStep1;
        Gate<On_Main.hook_DrawSurfaceBG_BackMountainsStep2>(SurfaceFlag);
        On_Main.DrawSurfaceBG_BackMountainsStep2 += BackMountainsStep2;
        Gate<On_Main.hook_DrawSurfaceBG_GetFogPower>(SurfaceFlag);
        On_Main.DrawSurfaceBG_GetFogPower += GetFogPower;
    });

    internal static void InstallUnderground() => ItemHooks.Once("UndergroundBackgrounds", () =>
    {
        On_Main.DrawBackground_PickUndergroundBackgroundStyle += PickUndergroundBackgroundStyle;
        On_Main.set_undergroundBackground += SetUndergroundBackground;
        On_Main.set_oldUndergroundBackground += SetOldUndergroundBackground;
        Gate<On_Main.hook_get_backTextureValues>(CurrentFlag);
        On_Main.get_backTextureValues += GetBackTextureValues;
        Gate<On_Main.hook_get_oldBackTextureValues>(OldFlag);
        On_Main.get_oldBackTextureValues += GetOldBackTextureValues;
    });

    static void Ensure()
    {
        int total = SurfaceBackgroundStylesLoader.TotalCount;
        GameArray<float> front = Main.bgAlphaFrontLayer;
        if (!front.IsNull && front.Length < total) Main.bgAlphaFrontLayer = front.Resized(total);
        GameArray<float> back = Main.bgAlphaFarBackLayer;
        if (!back.IsNull && back.Length < total) Main.bgAlphaFarBackLayer = back.Resized(total);
    }

    static int GetPreferredBGStyleForPlayer(On_Main.orig_GetPreferredBGStyleForPlayer orig)
    {
        int vanilla = orig();
        int style = vanilla;
        try
        {
            bool menu = Main.gameMenu;
            style = ChooseSurface(vanilla, menu);
            IReadOnlyList<GlobalBackgroundStyle> globals = GlobalBackgroundStyleLoader.All;
            for (int i = 0; i < globals.Count; i++) globals[i].ChooseSurfaceBackgroundStyle(ref style);
            bool modded = style >= SurfaceBackgroundStylesLoader.VanillaCount || Main.bgStyle >= SurfaceBackgroundStylesLoader.VanillaCount;
            if (modded) Ensure();
            chosenSurface = style;
            if (modded || (menu && MenuLoader.BackgroundSlot() >= 0)) WakeSurface();
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModSurfaceBackgroundStyle (choice)", e);
            return vanilla;
        }
        return style;
    }

    static void SetBackgroundStyle(On_Main.orig_set_bgStyle orig, int value)
    {
        if (SurfaceAsleep && value >= SurfaceBackgroundStylesLoader.VanillaCount)
        {
            WakeSurface();
            Ensure();
        }
        orig(value);
    }

    static int ChooseSurface(int vanilla, bool menu)
    {
        if (menu) return vanilla;
        var scene = SceneEffectLoader.Of(Main.LocalPlayer).surfaceBackground;
        if (SurfaceBackgroundStylesLoader.Get(scene.value) == null) return vanilla;
        bool forest = vanilla is 0 or 10 or 11 or 12;
        if (scene.priority >= SceneEffectPriority.BiomeHigh) return scene.value;
        if (scene.priority >= SceneEffectPriority.BiomeMedium && (forest || vanilla == 3 || vanilla == 7)) return scene.value;
        if (scene.priority >= SceneEffectPriority.BiomeLow && forest) return scene.value;
        return vanilla;
    }

    static void UpdateBGVisibility_BackLayer(On_Main.orig_UpdateBGVisibility_BackLayer orig, Main self, GameNullable<int> targetBiomeOverride, GameNullable<float> transitionAmountOverride)
    {
        if (SurfaceAsleep)
        {
            orig(self, targetBiomeOverride, transitionAmountOverride);
            return;
        }
        if (Main.gameMenu && !targetBiomeOverride.HasValue && MenuLoader.BackgroundSlot() is int menu and >= 0)
        {
            Ensure();
            Main.bgStyle = menu;
        }
        int style = targetBiomeOverride.HasValue ? targetBiomeOverride.Value : Main.bgStyle;
        if (SurfaceBackgroundStylesLoader.Get(style) == null)
        {
            orig(self, targetBiomeOverride, transitionAmountOverride);
            return;
        }
        Ensure();
        self.DrawBG_ModifyBGFarBackLayerAlpha(style, default, transitionAmountOverride);
    }

    static void UpdateBGVisibility_FrontLayer(On_Main.orig_UpdateBGVisibility_FrontLayer orig, Main self, GameNullable<int> targetBiomeOverride, GameNullable<float> transitionAmountOverride)
    {
        orig(self, targetBiomeOverride, transitionAmountOverride);
        if (SurfaceAsleep) return;
        if (Main.gameMenu && MenuLoader.BackgroundSlot() < 0)
        {
            SleepIfIdle();
            return;
        }
        int style = targetBiomeOverride.HasValue ? targetBiomeOverride.Value : Main.bgStyle;
        if (SurfaceBackgroundStylesLoader.Get(style) == null && GlobalBackgroundStyleLoader.All.Count == 0)
        {
            SleepIfIdle();
            return;
        }
        try
        {
            Ensure();
            GameArray<float> fades = Main.bgAlphaFrontLayer;
            int length = fades.Length;
            if (fadeBuffer.Length != length) fadeBuffer = new float[length];
            fades.AsSpan().CopyTo(fadeBuffer);
            float speed = transitionAmountOverride.HasValue ? transitionAmountOverride.Value : Main.backgroundLayerTransitionSpeed;
            SurfaceBackgroundStylesLoader.ModifyFarFades(style, fadeBuffer, speed);
            fadeBuffer.AsSpan(0, length).CopyTo(fades.AsSpan());
        }
        catch (Exception e) { ItemHooks.Report("ModSurfaceBackgroundStyle.ModifyFarFades", e); }
    }

    static void SleepIfIdle()
    {
        try
        {
            if (GlobalBackgroundStyleLoader.All.Count > 0 || chosenSurface >= SurfaceBackgroundStylesLoader.VanillaCount ||
                Main.bgStyle >= SurfaceBackgroundStylesLoader.VanillaCount) return;
            IReadOnlyList<ModSurfaceBackgroundStyle> styles = SurfaceBackgroundStylesLoader.All;
            if (Shown(Main.bgAlphaFarBackLayer, styles) || Shown(Main.bgAlphaFrontLayer, styles)) return;
            Flags[SurfaceFlag] = 0;
        }
        catch (Exception e) { ItemHooks.Report("ModSurfaceBackgroundStyle (idle check)", e); }
    }

    static bool Shown(GameArray<float> fades, IReadOnlyList<ModSurfaceBackgroundStyle> styles)
    {
        if (fades.IsNull) return false;
        int length = fades.Length;
        for (int i = 0; i < styles.Count; i++)
        {
            int slot = styles[i].Slot;
            if ((uint)slot < (uint)length && fades[slot] > 0) return true;
        }
        return false;
    }

    static void BackMountainsStep1(On_Main.orig_DrawSurfaceBG_BackMountainsStep1 orig, Main self, float backgroundTopMagicNumber, float bgGlobalScaleMultiplier, int pushBGTopHack)
    {
        orig(self, backgroundTopMagicNumber, bgGlobalScaleMultiplier, pushBGTopHack);
        if (SurfaceAsleep) return;
        step1Pending = true;
        step1Top = backgroundTopMagicNumber;
        step1Push = pushBGTopHack;
        DrawBack(self, false);
    }

    static void BackMountainsStep2(On_Main.orig_DrawSurfaceBG_BackMountainsStep2 orig, Main self, int pushBGTopHack)
    {
        orig(self, pushBGTopHack);
        if (SurfaceAsleep) return;
        DrawBack(self, true);
    }

    static float GetFogPower(On_Main.orig_DrawSurfaceBG_GetFogPower orig)
    {
        if (step1Pending)
        {
            step1Pending = false;
            if (!SurfaceAsleep) DrawClose(Main.instance);
        }
        return orig();
    }

    static void Blit(Texture2D texture, float x, float y, int w, int h, Color color, float scale) =>
        Main.spriteBatch.Draw(texture, new Vector2(x, y), new Rectangle(0, 0, w, h), color, 0f, new Vector2(0, 0), scale, SpriteEffects.None, 0f);

    static void DrawBack(Main self, bool middle)
    {
        IReadOnlyList<ModSurfaceBackgroundStyle> styles = SurfaceBackgroundStylesLoader.All;
        if (styles.Count == 0 || (Main.gameMenu && MenuLoader.BackgroundSlot() < 0)) return;
        GameArray<float> fades = Main.bgAlphaFarBackLayer;
        int length = fades.Length;
        for (int i = 0; i < styles.Count; i++)
        {
            ModSurfaceBackgroundStyle style = styles[i];
            float alpha = style.Slot < length ? fades[style.Slot] : 0;
            if (!(alpha > 0)) continue;
            try
            {
                int slot = middle ? style.ChooseMiddleTexture() : style.ChooseFarTexture();
                if (BackgroundTextureLoader.Texture(slot) is not Texture2D texture) continue;
                Color color = Main.ColorOfSurfaceBackgroundsBase * alpha;
                Main.ColorOfSurfaceBackgroundsModified = color;
                int w = Main.backgroundWidth[slot], h = Main.backgroundHeight[slot];
                int loops = self.bgLoops, startX = self.bgStartX, step = Main.bgWidthScaled;
                float topY = self.bgTopY, scale = Main.bgScale;
                for (int k = 0; k < loops; k++) Blit(texture, startX + step * k, topY, w, h, color, scale);
            }
            catch (Exception e) { ItemHooks.Report($"{style.FullName}.{(middle ? "ChooseMiddleTexture" : "ChooseFarTexture")}", e); }
        }
    }

    static void DrawClose(Main self)
    {
        IReadOnlyList<ModSurfaceBackgroundStyle> styles = SurfaceBackgroundStylesLoader.All;
        if (styles.Count == 0 || (Main.gameMenu && MenuLoader.BackgroundSlot() < 0)) return;
        GameArray<float> fades = Main.bgAlphaFrontLayer;
        int length = fades.Length;
        for (int i = 0; i < styles.Count; i++)
        {
            ModSurfaceBackgroundStyle style = styles[i];
            float alpha = style.Slot < length ? fades[style.Slot] : 0;
            if (!(alpha > 0)) continue;
            try
            {
                if (!style.PreDrawCloseBackground(Main.spriteBatch)) continue;
                float scale = 1.25f, a = 1800f, b = 1750f;
                double parallax = 0.37;
                int slot = style.ChooseCloseTexture(ref scale, ref parallax, ref a, ref b);
                if (BackgroundTextureLoader.Texture(slot) is not Texture2D texture) continue;
                float bgScale = scale * 2f;
                int w = Main.backgroundWidth[slot], h = Main.backgroundHeight[slot];
                int width = (int)(w * bgScale);
                if (width <= 0 || !(parallax > 0)) continue;
                SkyManager.Instance.DrawToDepth(Main.spriteBatch, (float)(1.0 / parallax));
                Vector2 screen = Main.screenPosition;
                if (screen.Y >= Main.worldSurface * 16.0 + 16.0) continue;
                double x = screen.X * parallax;
                int startX = (int)(-Math.IEEERemainder(x, width) - width / 2);
                float topY = Main.gameMenu ? MenuCloseTop + step1Push + FrontLayerPush : (int)(step1Top * a + b) + (int)self.scAdj + step1Push;
                int loops = Main.screenWidth / width + 2;
                Color color = Main.ColorOfSurfaceBackgroundsBase * alpha;
                Main.ColorOfSurfaceBackgroundsModified = color;
                for (int k = 0; k < loops; k++) Blit(texture, startX + width * k, topY, w, h, color, bgScale);
            }
            catch (Exception e) { ItemHooks.Report($"{style.FullName} (close background)", e); }
        }
    }

    static int Cave()
    {
        int x = (int)((Main.screenPosition.X + Main.screenWidth / 2) / 16);
        GameArray<int> backX = Main.caveBackX, backStyle = Main.caveBackStyle;
        return (x > backX[0] ? x > backX[1] ? x > backX[2] ? backStyle[3] : backStyle[2] : backStyle[1] : backStyle[0]) + 3;
    }

    static int PickUndergroundBackgroundStyle(On_Main.orig_DrawBackground_PickUndergroundBackgroundStyle orig, double magmaLayer)
    {
        int vanilla = orig(magmaLayer);
        lock (Fills) Fills.Clear();
        int style = vanilla;
        try
        {
            style = ChooseUnderground(vanilla);
            IReadOnlyList<GlobalBackgroundStyle> globals = GlobalBackgroundStyleLoader.All;
            for (int i = 0; i < globals.Count; i++) globals[i].ChooseUndergroundBackgroundStyle(ref style);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModUndergroundBackgroundStyle (choice)", e);
            style = vanilla;
        }
        TrackUnderground(style);
        return style;
    }

    static void TrackUnderground(int picked)
    {
        int vanilla = UndergroundBackgroundStylesLoader.VanillaCount;
        bool modded = picked >= vanilla;
        if (GlobalBackgroundStyleLoader.All.Count > 0)
        {
            Flags[CurrentFlag] = Flags[OldFlag] = 1;
            return;
        }
        if (!modded && Flags[CurrentFlag] == 0 && Flags[OldFlag] == 0) return;
        try
        {
            int current = Main.undergroundBackground, old = Main.oldUndergroundBackground;
            Flags[CurrentFlag] = modded || current >= vanilla ? 1 : 0;
            Flags[OldFlag] = old >= vanilla ? 1 : 0;
        }
        catch (Exception e)
        {
            Flags[CurrentFlag] = Flags[OldFlag] = 1;
            ItemHooks.Report("ModUndergroundBackgroundStyle (tracking)", e);
        }
    }

    static void SetUndergroundBackground(On_Main.orig_set_undergroundBackground orig, int value)
    {
        if (value >= UndergroundBackgroundStylesLoader.VanillaCount) Flags[CurrentFlag] = 1;
        orig(value);
        if (Main.undergroundBackground >= UndergroundBackgroundStylesLoader.VanillaCount) Flags[CurrentFlag] = 1;
    }

    static void SetOldUndergroundBackground(On_Main.orig_set_oldUndergroundBackground orig, int value)
    {
        if (value >= UndergroundBackgroundStylesLoader.VanillaCount) Flags[OldFlag] = 1;
        orig(value);
        if (Main.oldUndergroundBackground >= UndergroundBackgroundStylesLoader.VanillaCount) Flags[OldFlag] = 1;
    }

    static int ChooseUnderground(int vanilla)
    {
        if (Main.gameMenu || !Main.BackgroundEnabled) return vanilla;
        var scene = SceneEffectLoader.Of(Main.LocalPlayer).undergroundBackground;
        if (UndergroundBackgroundStylesLoader.Get(scene.value) == null) return vanilla;
        if (scene.priority >= SceneEffectPriority.BiomeHigh) return scene.value;
        bool cave = vanilla == Cave();
        if (scene.priority >= SceneEffectPriority.BiomeMedium && (cave || vanilla == 1 || vanilla == 11)) return scene.value;
        if (scene.priority >= SceneEffectPriority.BiomeLow && cave) return scene.value;
        return vanilla;
    }

    static GameArray<Texture2D> GetBackTextureValues(On_Main.orig_get_backTextureValues orig, Main self)
    {
        if (Flags[CurrentFlag] == 0) return orig(self);
        GameArray<Texture2D> values = orig(self);
        Patch(Main.undergroundBackground, self, false, values);
        return values;
    }

    static GameArray<Texture2D> GetOldBackTextureValues(On_Main.orig_get_oldBackTextureValues orig, Main self)
    {
        if (Flags[OldFlag] == 0) return orig(self);
        GameArray<Texture2D> values = orig(self);
        Patch(Main.oldUndergroundBackground, self, true, values);
        return values;
    }

    static Fill? FillOf(int style, Main self, bool old)
    {
        lock (Fills)
        {
            if (Fills.TryGetValue(style, out Fill? known)) return known;
        }
        Fill? fill = null;
        bool mod = UndergroundBackgroundStylesLoader.Get(style) != null;
        if (mod || GlobalBackgroundStyleLoader.All.Count > 0)
        {
            int[] slots = { -1, -1, -1, -1, 6 };
            if (!mod)
            {
                GameArray<int> numbers = old ? self.oldBackTexture : self.backTexture;
                for (int k = 0; k < 5 && k < numbers.Length; k++) slots[k] = numbers[k];
            }
            UndergroundBackgroundStylesLoader.FillTextureArray(style, slots);
            var textures = new Texture2D?[5];
            bool any = false;
            for (int k = 0; k < 5; k++)
            {
                textures[k] = BackgroundTextureLoader.Texture(slots[k]);
                any |= textures[k] != null;
            }
            if (any) fill = new Fill(slots, textures);
        }
        lock (Fills) Fills[style] = fill;
        return fill;
    }

    static void Patch(int style, Main self, bool old, GameArray<Texture2D> values)
    {
        if (values.IsNull || values.Length < 5) return;
        if (style < UndergroundBackgroundStylesLoader.VanillaCount && GlobalBackgroundStyleLoader.All.Count == 0) return;
        try
        {
            if (FillOf(style, self, old) is not Fill fill) return;
            int last = fill.Textures[3] != null ? 3 : Array.FindLastIndex(fill.Textures, t => t != null);
            if (values[last].Pointer == fill.Textures[last]!.Value.Pointer) return;
            GameArray<int> numbers = old ? self.oldBackTexture : self.backTexture;
            for (int k = 0; k < 5; k++)
            {
                if (fill.Textures[k] is not Texture2D texture) continue;
                values[k] = texture;
                if (k < numbers.Length) numbers[k] = fill.Slots[k];
            }
        }
        catch (Exception e) { ItemHooks.Report("ModUndergroundBackgroundStyle.FillTextureArray", e); }
    }
}
