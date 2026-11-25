using System;
using BunnyLoader;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CsSpriteFx
{
    public sealed class SpriteFxSystem : ModSystem
    {
        public override void PostDrawInterface(SpriteBatch spriteBatch) => CsSpriteFxMod.Draw();

        public override void PostDrawTiles() => CsSpriteFxMod.DrawWorld();
    }

    public sealed class CsSpriteFxMod : Mod
    {
        const int End = 1500;
        const string Dissolve = "CsSpriteFx:Dissolve";
        static int frame, pass, fail, drawn;
        static Effect effect;
        static bool ready;
        static Asset<Texture2D>? square, gradientV;

        static ILog Log => ModContent.GetInstance<CsSpriteFxMod>().Logger;

        public override void Load()
        {
            square = Assets.Request<Texture2D>("Content/Square");
            gradientV = Assets.Request<Texture2D>("Content/GradientV");
            On_Player.Update += Update;
        }

        static void Report(string name, bool ok, string detail)
        {
            if (ok) pass++;
            else fail++;
            Log.Info($"csspritefx {name}: {(ok ? "ok" : "FAIL")} {detail}");
        }

        static void Update(On_Player.orig_Update orig, Player self, int i)
        {
            orig(self, i);
            if (Main.gameMenu || i != Main.myPlayer || frame > End) return;
            frame++;
            try { Step(); }
            catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
            if (frame == End) Log.Info($"csspritefx END pass={pass} fail={fail}");
        }

        static void Step()
        {
            switch (frame)
            {
                case 30:
                {
                    Main.dayTime = true;
                    Main.time = 27000;
                    Mod mod = ModContent.GetInstance<CsSpriteFxMod>();
                    Asset<Effect> asset = mod.Assets.Request<Effect>("Effects/SpriteFx");
                    Report("the effect asset loads", !asset.Value.IsNull && asset.IsLoaded, asset.Name);
                    Report("ModContent finds the same asset", ModContent.Request<Effect>("CsSpriteFx/Effects/SpriteFx") == asset, "");
                    effect = asset.Value;
                    EffectPassCollection passes = effect.CurrentTechnique.Passes;
                    Report("the effect lists its passes", passes.Count == 3 && passes[0].Name == "UV" && passes["Tint"].Name == "Tint" && passes["Nope"].IsNull,
                        $"{passes.Count}");
                    Report("the effect lists its parameters", !effect.Parameters["uColor"].IsNull && !effect.Parameters["uOpacity"].IsNull && !effect.Parameters["uTime"].IsNull,
                        $"{effect.Parameters.Count}");
                    EffectTechniqueCollection techniques = effect.Techniques;
                    Report("the effect lists its techniques", techniques.Count == 2 && techniques[0] == effect.CurrentTechnique && techniques["Technique1"] == effect.CurrentTechnique
                        && !techniques["Inverted"].Passes["Invert"].IsNull && techniques["Nope"].IsNull, $"{techniques.Count}");
                    effect.Parameters["uOpacity"].SetValue(1);
                    Report("SetValue takes an int", effect.Parameters["uOpacity"].Float == 1f, $"{effect.Parameters["uOpacity"].Float}");
                    bool notSampler = false;
                    try { effect.Parameters["uColor"].SetValue(square!.Value); }
                    catch (NotSupportedException) { notSampler = true; }
                    Report("a texture on a non-sampler parameter is refused", notSampler, "");
                    GameShaders.Misc[Dissolve] = new MiscShaderData(asset, "Dissolve").UseImage1(mod.Assets.Request<Texture2D>("Content/Gradient"));
                    Report("GameShaders.Misc keeps a mod shader", GameShaders.Misc.ContainsKey(Dissolve) && !GameShaders.Misc[Dissolve].IsNull, "");
                    bool refused = false;
                    try { _ = ModContent.Request<Effect>("Terraria/Effects/Nope"); }
                    catch (NotSupportedException) { refused = true; }
                    Report("game effects are refused with a clear error", refused, "");
                    ready = true;
                    break;
                }
                case 300:
                    Log.Info($"csspritefx SHOT sprites drawn={drawn}");
                    break;
                case 1400:
                {
                    long[] s = FxcEffect.SpriteDrawStats();
                    string text = string.Join(" ", s);
                    Report("draws were marked", s[0] > 0, text);
                    Report("marked draws reached the render thread", s[1] > 0, text);
                    Report("all four passes linked", s[3] >= 4 && s[4] == 0, text);
                    Report("no marked draw was lost", s[2] == 0, text);
                    break;
                }
            }
        }

        static void Square(Vector2 center)
        {
            Main.spriteBatch.Draw(square!.Value, center, null, Color.White, 0f, new Vector2(8, 8), 4f, SpriteEffects.None, 0f);
        }

        internal static void DrawWorld()
        {
            if (!ready) return;
            Vector2 at = Main.LocalPlayer.Center - Main.screenPosition + new Vector2(0, -70);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            Main.spriteBatch.Draw(square!.Value, at, null, Color.White, 0f, new Vector2(8, 8), 2f, SpriteEffects.None, 0f);
            Main.spriteBatch.End();
        }

        internal static void Draw()
        {
            if (!ready || square == null) return;
            Vector2 at = new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.3f);
            Matrix matrix = Main.UIScaleMatrix;
            SpriteBatch sb = Main.spriteBatch;
            sb.End();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, matrix);
            Square(at + new Vector2(-260, 0));
            sb.End();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, effect, matrix);
            Square(at + new Vector2(-156, 0));
            sb.End();

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, matrix);
            effect.Parameters["uOpacity"].SetValue(1f);
            effect.Parameters["uColor"].SetValue(new Vector3(0, 1, 0));
            effect.CurrentTechnique.Passes["Tint"].Apply();
            Square(at + new Vector2(-52, 0));
            effect.Parameters["uColor"].SetValue(new Vector3(1, 0, 1));
            effect.CurrentTechnique.Passes["Tint"].Apply();
            Square(at + new Vector2(52, 0));
            sb.End();

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, matrix);
            GameShaders.Misc[Dissolve].UseOpacity(0.5f).Apply(null);
            Square(at + new Vector2(156, 0));
            sb.End();

            effect.CurrentTechnique = effect.Techniques["Inverted"];
            effect.Parameters["uColor"].SetValue(new Vector3(0, 1, 1));
            effect.Parameters["uOpacity"].SetValue(1);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, effect, matrix);
            Square(at + new Vector2(-156, 104));
            sb.End();
            effect.CurrentTechnique = effect.Techniques[0];

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, matrix);
            effect.Parameters["uImage1"].SetValue(gradientV!.Value);
            effect.Parameters["uOpacity"].SetValue(0.5f);
            effect.CurrentTechnique.Passes["Dissolve"].Apply();
            Square(at + new Vector2(156, 104));
            sb.End();
            effect.Parameters["uImage1"].SetValue(default(Texture2D));

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, matrix);
            drawn++;
        }
    }
}
