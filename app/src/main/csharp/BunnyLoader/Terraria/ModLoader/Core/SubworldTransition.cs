using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.UI;

namespace Terraria.ModLoader.Core;

internal static class SubworldTransition
{
    enum Stage { None, Leaving, Covered, Arriving }

    const string LayerName = "BunnyLoader: Subworld Transition";

    static volatile Stage stage;
    static ModSubworld? owner;
    static long started;
    static float seconds;
    static Action? then;

    internal static bool Active => stage != Stage.None;

    internal static float LeaveSeconds(ModSubworld? subworld) =>
        subworld == null ? 0f : Read(subworld, nameof(ModSubworld.TransitionOutSeconds), () => subworld.TransitionOutSeconds, 0f);

    static float ArriveSeconds(ModSubworld subworld) => Read(subworld, nameof(ModSubworld.TransitionInSeconds), () => subworld.TransitionInSeconds, 0f);

    static bool LoadingScreen(ModSubworld subworld) => Read(subworld, nameof(ModSubworld.ShowLoadingScreen), () => subworld.ShowLoadingScreen, true);

    static T Read<T>(ModSubworld subworld, string name, Func<T> value, T fallback)
    {
        try { return value(); }
        catch (Exception e)
        {
            ItemHooks.Report($"{subworld.FullName}.{name}", e);
            return fallback;
        }
    }

    internal static void Leave(ModSubworld? subworld, Action? next)
    {
        Stop();
        float leave = LeaveSeconds(subworld);
        if (subworld == null || (leave <= 0f && ArriveSeconds(subworld) <= 0f && LoadingScreen(subworld)))
        {
            next?.Invoke();
            return;
        }

        ItemHooks.Once("Subworld transitions", () => On_Main.DoDraw += DoDraw);
        InterfaceLayerHooks.Install();
        owner = subworld;
        if (leave <= 0f)
        {
            stage = Stage.Covered;
            next?.Invoke();
            return;
        }

        then = next;
        seconds = leave;
        started = Stopwatch.GetTimestamp();
        stage = Stage.Leaving;
    }

    internal static void Arrive()
    {
        ModSubworld? subworld = owner;
        if (stage == Stage.None || subworld == null) return;
        float arrive = ArriveSeconds(subworld);
        if (arrive <= 0f)
        {
            Stop();
            return;
        }

        seconds = arrive;
        started = Stopwatch.GetTimestamp();
        stage = Stage.Arriving;
    }

    internal static void Stop()
    {
        stage = Stage.None;
        owner = null;
        then = null;
    }

    static float Elapsed => (float)((Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency) / Math.Max(seconds, 0.001f);

    internal static void Update()
    {
        if (stage != Stage.Leaving || Elapsed < 1f) return;
        Action? next = then;
        then = null;
        stage = Stage.Covered;
        try { next?.Invoke(); }
        catch (Exception e)
        {
            ItemHooks.Report("Subworld transition", e);
            Stop();
        }
    }

    static bool Current(out ModSubworld subworld, out float progress)
    {
        subworld = owner!;
        Stage current = stage;
        progress = current switch
        {
            Stage.Leaving => Math.Min(Elapsed, 1f),
            Stage.Arriving => 1f - Math.Min(Elapsed, 1f),
            _ => 1f,
        };
        if (current == Stage.None || subworld == null) return false;
        if (current != Stage.Arriving || progress > 0f) return true;
        Stop();
        return false;
    }

    static void Draw(SpriteBatch batch, ModSubworld subworld, float progress, bool cover)
    {
        try
        {
            if (cover) batch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black);
            subworld.DrawTransition(batch, progress);
        }
        catch (Exception e)
        {
            ItemHooks.Report(subworld.FullName + ".DrawTransition", e);
            Stop();
        }
    }

    internal static void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (!Active) return;
        layers.Add(new LegacyGameInterfaceLayer(LayerName, () =>
        {
            if (!Main.gameMenu && Current(out ModSubworld subworld, out float progress)) Draw(Main.spriteBatch, subworld, progress, false);
            return true;
        }, InterfaceScaleType.UI));
    }

    static void DoDraw(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);
        if (!Main.gameMenu || !Current(out ModSubworld subworld, out float progress) || LoadingScreen(subworld)) return;
        bool ui = XNAUnityRunner.RenderingUI;
        if (!ui) XNAUnityRunner.SetupRenderingUI(true);
        SpriteBatch batch = Main.spriteBatch;
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, default, Matrix.Identity);
        try { Draw(batch, subworld, progress, true); }
        finally
        {
            batch.End();
            if (!ui) XNAUnityRunner.SetupRenderingUI(false);
        }
    }
}
