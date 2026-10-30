using System;
using CsProjExtras.Content;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;
using GameRect = BunnyLoader.Interop.GameNullable<Microsoft.Xna.Framework.Rectangle>;

namespace CsProjExtras;

public static class Checks
{
    static int pass, fail, frames, hook = -1, own, other, measured, extrasBefore, postBefore;
    static bool done, measuring, hooked;
    static IntPtr ownTexture;

    static ILog Log => ModContent.GetInstance<CsProjExtrasMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        hook = -1;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 30)
            {
                DrawHook.Extras = false;
                hook = Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(0f, -100f), Vector2.Zero, ModContent.ProjectileType<DrawHook>(), 0, 0f, p.whoAmI);
            }
            if (frames == 40) Install();
            if (frames == 50) Phase(true, true);
            if (frames == 60)
            {
                Report("extras and sprite both drawn", own > 0 && other > 0, Counts());
                Phase(false, true);
            }
            if (frames == 70)
            {
                Report("PreDrawExtras false keeps only the sprite", own > 0 && other == 0, Counts());
                Phase(true, false);
            }
            if (frames == 80)
            {
                Report("PreDraw false keeps only the chain", own == 0 && other > 0, Counts());
                Phase(false, false);
            }
            if (frames == 90)
            {
                Report("both false skip the game's draw but still run the hooks", measured > 0 && own == 0 && other == 0 && DrawHook.ExtrasCalls > extrasBefore && DrawHook.PostDrawCalls > postBefore,
                    $"{Counts()} extras={DrawHook.ExtrasCalls - extrasBefore} post={DrawHook.PostDrawCalls - postBefore}");
                done = true;
            }
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done)
        {
            if ((uint)hook < 1000u && Main.projectile[hook].active) Main.projectile[hook].Kill();
            Log.Info($"csprojextras END pass={pass} fail={fail}");
        }
    }

    static void Phase(bool extras, bool sprite)
    {
        DrawHook.Extras = extras;
        DrawHook.Sprite = sprite;
        own = other = measured = 0;
        extrasBefore = DrawHook.ExtrasCalls;
        postBefore = DrawHook.PostDrawCalls;
    }

    static string Counts() => $"own={own} other={other} measured={measured}";

    static void Install()
    {
        if (hooked) return;
        hooked = true;
        ownTexture = TextureAssets.Projectile[ModContent.ProjectileType<DrawHook>()].Value.Pointer;
        On_Main.DrawProjDirect += Measure;
        On_DrawData.Draw_SpriteBatch += DrawData;
        On_SpriteBatch.Draw_Texture2D_Vector2_Nullable_Color_float_Vector2_float_SpriteEffects_float += DrawFloat;
        On_SpriteBatch.Draw_Texture2D_Vector2_Nullable_Color_float_Vector2_Vector2_SpriteEffects_float += DrawVector;
        On_SpriteBatch.Draw_Texture2D_refVector2_refRectangle_refColor_float_Vector2_float_SpriteEffects += DrawRefFloat;
        On_SpriteBatch.Draw_Texture2D_refVector2_refRectangle_refColor_float_Vector2_Vector2_SpriteEffects += DrawRefVector;
    }

    static void Measure(On_Main.orig_DrawProjDirect orig, Main self, Projectile proj, Player overridePlayer)
    {
        bool outer = measuring;
        measuring = proj.type == ModContent.ProjectileType<DrawHook>();
        if (measuring) measured++;
        try { orig(self, proj, overridePlayer); }
        finally { measuring = outer; }
    }

    static void Count(Texture2D texture)
    {
        if (!measuring) return;
        if (texture.Pointer == ownTexture) own++;
        else other++;
    }

    static void DrawData(On_DrawData.orig_Draw_SpriteBatch orig, ref DrawData self, SpriteBatch sb)
    {
        Count(self.texture);
        orig(ref self, sb);
    }

    static void DrawFloat(On_SpriteBatch.orig_Draw_Texture2D_Vector2_Nullable_Color_float_Vector2_float_SpriteEffects_float orig, SpriteBatch self, Texture2D texture,
        Vector2 position, GameRect sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
    {
        Count(texture);
        orig(self, texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
    }

    static void DrawVector(On_SpriteBatch.orig_Draw_Texture2D_Vector2_Nullable_Color_float_Vector2_Vector2_SpriteEffects_float orig, SpriteBatch self, Texture2D texture,
        Vector2 position, GameRect sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
    {
        Count(texture);
        orig(self, texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
    }

    static void DrawRefFloat(On_SpriteBatch.orig_Draw_Texture2D_refVector2_refRectangle_refColor_float_Vector2_float_SpriteEffects orig, SpriteBatch self, Texture2D texture,
        ref Vector2 position, ref Rectangle srcRect, ref Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects)
    {
        Count(texture);
        orig(self, texture, ref position, ref srcRect, ref color, rotation, origin, scale, effects);
    }

    static void DrawRefVector(On_SpriteBatch.orig_Draw_Texture2D_refVector2_refRectangle_refColor_float_Vector2_Vector2_SpriteEffects orig, SpriteBatch self, Texture2D texture,
        ref Vector2 position, ref Rectangle srcRect, ref Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects)
    {
        Count(texture);
        orig(self, texture, ref position, ref srcRect, ref color, rotation, origin, scale, effects);
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csprojextras {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
