using System;
using System.Runtime.InteropServices;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CsPrimPoc;

[StructLayout(LayoutKind.Sequential)]
public struct SpriteVertex
{
    public Vector2 Position;
    public uint Color;
    public Vector2 TexCoord;

    public SpriteVertex(float x, float y, Color color, float u, float v)
    {
        Position = new Vector2(x, y);
        Color = (uint)(color.R | color.G << 8 | color.B << 16 | color.A << 24);
        TexCoord = new Vector2(u, v);
    }
}

public sealed class CsPrimPocMod : Mod
{
    static MiscShaderData modPass;

    public static MiscShaderData ModPass
    {
        get
        {
            if (modPass.IsNull)
            {
                modPass = new MiscShaderData(ModContent.GetInstance<CsPrimPocMod>().Assets.Request<Effect>("Effects/SpriteFx", AssetRequestMode.ImmediateLoad), "UV");
                GameShaders.Misc["CsPrimPocUV"] = modPass;
            }

            return modPass;
        }
    }
}

public sealed class DrawSystem : ModSystem
{
    static readonly string[] Cases = { "vanilla", "mod_pass" };

    static int frames;
    static bool logged, ended;
    static int pass, fail;

    static ILog Log => ModContent.GetInstance<CsPrimPocMod>().Logger;

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (Main.gameMenu) return;
        frames++;

        spriteBatch.End();

        for (int i = 0; i < Cases.Length; i++)
        {
            string name = Cases[i];

            try
            {
                Draw(i);
                if (!logged) Report(name, null);
            }
            catch (Exception e)
            {
                if (!logged) Report(name, e.GetType().Name + ": " + e.Message);
            }
        }

        EffectPass.BoundEffect = default;
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

        if (!logged)
        {
            logged = true;
            Log.Info($"csprimpoc screen {Main.screenWidth}x{Main.screenHeight}");
        }

        if (!ended && frames > 180)
        {
            ended = true;
            Log.Info($"csprimpoc END pass={pass} fail={fail}");
        }
    }

    static void Report(string name, string? problem)
    {
        if (problem == null) pass++;
        else fail++;

        Log.Info($"csprimpoc {name}: {(problem == null ? "ok" : "FAIL " + problem)}");
    }

    static void Draw(int i)
    {
        GraphicsDevice device = GraphicsDevice.Instance;
        int x = 100, y = 120 + i * 110, w = 400, h = 80;

        EffectPass.BoundEffect = default;
        device.Textures[0] = TextureAssets.MagicPixel.Value;

        if (i == 0)
        {
            device.Textures[0] = TextureAssets.Extra[193].Value;
            GameShaders.Misc["RainbowRod"].UseSaturation(-2.8f).UseOpacity(2f).Apply(default);
        }

        if (i == 1)
        {
            device.Textures[0] = TextureAssets.MagicPixel.Value;
            CsPrimPocMod.ModPass.Apply(default);
        }

        Span<SpriteVertex> v = stackalloc SpriteVertex[4];
        v[0] = new SpriteVertex(x, y, Color.Red, 0, 0);
        v[1] = new SpriteVertex(x + w, y, Color.Lime, 1, 0);
        v[2] = new SpriteVertex(x, y + h, Color.Blue, 0, 1);
        v[3] = new SpriteVertex(x + w, y + h, Color.White, 1, 1);

        short[] ix = { 0, 1, 2, 2, 1, 3 };

        GameArray<SpriteVertex> one = GameArray<SpriteVertex>.New(new GameClass("Microsoft.Xna.Framework.Graphics", "SpriteBatcher", "VertexData"), 4);
        v.CopyTo(one.AsSpan());
        GameArray<short> six = ix;

        device.DrawGeometry(new GameObject(one.Pointer), six, 4, 6, Vector3.Zero, Vector3.One);

        if (!logged) Log.Info($"csprimpoc draw {Cases[i]} {x},{y},{w},{h}");
    }
}
