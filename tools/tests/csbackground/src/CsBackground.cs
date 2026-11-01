using System;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Liquid;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsBackground;

public sealed unsafe class CsBackgroundMod : Mod
{
    const string Bg = "Backgrounds/";

    static int frames, pass, fail;
    static bool done, inRain, inMap;
    static int rainMod, rainX = -1, mapMod, mapGame, mapW, mapH, gameW, gameH;
    static string mapColor = "";
    static int vanillaBg = -1, vanillaUg = -1, vanillaWater = -1;
    static readonly int[] waterfallDraws = new int[64];

    static ILog Log => ModContent.GetInstance<CsBackgroundMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Main.DrawRain += (orig, self) =>
        {
            inRain = true;
            try { orig(self); }
            finally { inRain = false; }
        };
        On_SpriteBatch.Draw_Texture2D_refVector2_refRectangle_refColor_float_Vector2_float += RainDraw;
        On_GUIMap.DrawMapFullscreenBackground += (orig, self, position, width, height) =>
        {
            inMap = true;
            try { orig(self, position, width, height); }
            finally { inMap = false; }
        };
        On_SpriteBatch.Draw_Texture2D_Rectangle_Color += MapDraw;
        On_WaterfallManager.DrawWaterfall_SpriteBatch_int_float += (orig, self, spriteBatch, style, alpha) =>
        {
            if ((uint)style < (uint)waterfallDraws.Length) waterfallDraws[style]++;
            orig(self, spriteBatch, style, alpha);
        };
    }

    static void RainDraw(On_SpriteBatch.orig_Draw_Texture2D_refVector2_refRectangle_refColor_float_Vector2_float orig, SpriteBatch self, Texture2D texture,
        ref Vector2 position, ref Rectangle srcRect, ref Color color, float rotation, Vector2 origin, float scale)
    {
        if (inRain && texture.Width == 16 && texture.Height == 44)
        {
            rainMod++;
            rainX = srcRect.X;
        }
        orig(self, texture, ref position, ref srcRect, ref color, rotation, origin, scale);
    }

    static void MapDraw(On_SpriteBatch.orig_Draw_Texture2D_Rectangle_Color orig, SpriteBatch self, Texture2D texture, Rectangle destinationRectangle, Color color)
    {
        if (inMap)
        {
            if (texture.Width == 37 && texture.Height == 23)
            {
                mapMod++;
                mapW = destinationRectangle.Width;
                mapH = destinationRectangle.Height;
                mapColor = Rgba(color);
            }
            else
            {
                mapGame++;
                gameW = destinationRectangle.Width;
                gameH = destinationRectangle.Height;
            }
        }
        orig(self, texture, destinationRectangle, color);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(self, ++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csbackground END pass={pass} fail={fail}");
    }

    static int Slot(string name) => BackgroundTextureLoader.GetBackgroundSlot(ModContent.GetInstance<CsBackgroundMod>(), Bg + name);

    static string Rgba(Color c) => $"{c.R},{c.G},{c.B},{c.A}";

    static void Step(Player p, int f)
    {
        if (f == 1)
        {
            Main.dayTime = true;
            Main.time = 27000;
        }
        switch (f)
        {
            case 10:
                Registration();
                BuildPool(p);
                break;
            case 60:
                vanillaBg = Main.bgStyle;
                vanillaUg = Main.undergroundBackground;
                vanillaWater = Main.waterStyle;
                Report("starts in a forest", vanillaBg is 0 or 10 or 11 or 12, $"bgStyle={vanillaBg} ug={vanillaUg} water={vanillaWater}");
                TestScene.On = true;
                break;
            case 120:
                Report("Priority None changes nothing", Main.bgStyle == vanillaBg && Main.undergroundBackground == vanillaUg && Main.waterStyle == vanillaWater, State());
                TestScene.Level = SceneEffectPriority.BiomeLow;
                break;
            case 400:
                for (int k = 0; k < 6; k++) Rain.NewRainForced(new Vector2(p.Center.X - 60 + k * 20, p.Center.Y - 260), new Vector2(0, 4));
                break;
            case 420:
                WaterOn(p);
                BackgroundOn();
                break;
            case 430:
                GUIInstance.Active.GUIMap.OpenFullscreenMap();
                break;
            case 470:
                Report("map open: the game's background is drawn", mapGame > 0 && mapMod == 0, $"game={mapGame} mod={mapMod}");
                MapScene.On = true;
                break;
            case 520:
                {
                    var scene = p.CurrentSceneEffect;
                    Report("the scene's map background channel", scene.mapBackground == "CsBackground/Map/TestMapBg" && scene.mapBackgroundSceneEffect is MapScene, $"{scene.mapBackground}");
                    Report("the map background is the mod texture over the whole map", mapMod > 0 && mapW == gameW && mapH == gameH && mapW > 0, $"mod={mapMod} {mapW}x{mapH} game {gameW}x{gameH}");
                    Report("on the surface, the sky color", mapColor == Rgba(Main.ColorOfTheSkies), $"{mapColor} sky {Rgba(Main.ColorOfTheSkies)}");
                    MapScene.Fullbright = true;
                    mapMod = 0;
                    break;
                }
            case 560:
                Report("MapBackgroundFullbright draws white", mapMod > 0 && mapColor == "255,255,255,255", mapColor);
                MapScene.Tint = true;
                mapMod = 0;
                break;
            case 600:
                Report("MapBackgroundColor changes the color", mapMod > 0 && mapColor == Rgba(MapScene.TintColor), mapColor);
                MapScene.On = MapScene.Fullbright = MapScene.Tint = false;
                break;
            case 610:
                mapMod = mapGame = 0;
                break;
            case 650:
                Report("map scene off: the game's background again", mapGame > 0 && mapMod == 0, $"game={mapGame} mod={mapMod}");
                GUIInstance.Active.GUIMap.CloseFullscreenMap();
                TestScene.On = false;
                break;
            case 900:
                Off(p);
                done = true;
                break;
        }
    }

    static void Registration()
    {
        var water = ModContent.GetInstance<TestWater>();
        var waterfall = ModContent.GetInstance<TestWaterfall>();
        var surface = ModContent.GetInstance<TestSurface>();
        var underground = ModContent.GetInstance<TestUnderground>();
        Report("style slots after the game's", water.Slot >= 15 && waterfall.Slot >= 28 && surface.Slot >= 16 && underground.Slot >= 22,
            $"water={water.Slot} waterfall={waterfall.Slot} surface={surface.Slot} underground={underground.Slot}");
        int s = water.Slot;
        bool tables = LiquidRenderer._liquidTextures.Length > s && TextureAssets.Liquid.Length > s && TextureAssets.LiquidSlope.Length > s &&
            WaterfallManager.waterfallTexture.Length > waterfall.Slot;
        Report("water and waterfall texture tables grew", tables && LiquidRenderer._liquidTextures[s].Pointer != IntPtr.Zero,
            $"{LiquidRenderer._liquidTextures.Length}/{TextureAssets.Liquid.Length}/{TextureAssets.LiquidSlope.Length} waterfalls {WaterfallManager.waterfallTexture.Length}");
        int far = Slot("ExampleBiomeSurfaceFar");
        Report("background textures after the game's, with width and height",
            far >= Main.maxBackgrounds && TextureAssets.Background.Length > far && TextureAssets.Background[far].Value.Width == 1024 &&
            Main.backgroundWidth[far] == 1024 && Main.backgroundHeight[far] == 408,
            $"{far} of {TextureAssets.Background.Length}, {Main.backgroundWidth[far]}x{Main.backgroundHeight[far]}");
        bool full = BackgroundTextureLoader.GetBackgroundSlot("CsBackground/" + Bg + "ExampleBiomeSurfaceFar") == far;
        bool found = BackgroundTextureLoader.TryGetBackgroundSlot("CsBackground/" + Bg + "ExampleBiomeSurfaceFar", out int tried) && tried == far;
        bool missing = !BackgroundTextureLoader.TryGetBackgroundSlot("CsBackground/" + Bg + "Missing", out _);
        bool threw = false;
        try { BackgroundTextureLoader.GetBackgroundSlot("CsBackground/" + Bg + "Missing"); }
        catch (Exception) { threw = true; }
        Report("GetBackgroundSlot with the mod and the full path, TryGet and the missing one", full && found && missing && threw, $"{full} {found} {missing} {threw}");
    }

    static void BuildPool(Player p)
    {
        int x0 = (int)((p.position.X + p.width) / 16) + 2;
        int floor = (int)((p.position.Y + p.height) / 16) + 2;
        int x1 = x0 + 15;
        for (int x = x0; x <= x1; x++)
        {
            for (int y = floor - 8; y < floor; y++)
            {
                WorldGen.KillTile(x, y, false, false, true);
                SetLiquid(x, y, 0);
            }
            WorldGen.KillTile(x, floor, false, false, true);
            WorldGen.PlaceTile(x, floor, TileID.Stone, true, true, -1, 0);
        }
        for (int y = floor - 4; y < floor; y++)
        {
            WorldGen.PlaceTile(x0, y, TileID.Stone, true, true, -1, 0);
            WorldGen.PlaceTile(x1, y, TileID.Stone, true, true, -1, 0);
        }
        for (int x = x1 + 1; x <= x1 + 2; x++)
        {
            for (int y = floor - 6; y < floor + 2; y++)
            {
                WorldGen.KillTile(x, y, false, false, true);
                SetLiquid(x, y, 0);
            }
        }
        WorldGen.PoundTile(x1, floor - 4);
        for (int x = x0 + 1; x < x1; x++)
        {
            for (int y = floor - 4; y < floor; y++) SetLiquid(x, y, 255);
        }
        WorldGen.RangeFrame(x0, floor - 8, x1, floor);
        Log.Info($"csbackground: pool at {x0}..{x1}, floor {floor}");
    }

    static void SetLiquid(int x, int y, byte amount)
    {
        Tile tile = Main.tile[x, y];
        tile.liquid = amount;
    }

    static string State()
    {
        int s = ModContent.GetInstance<TestSurface>().Slot;
        var front = Main.bgAlphaFrontLayer;
        var back = Main.bgAlphaFarBackLayer;
        return $"bgStyle={Main.bgStyle} ug={Main.undergroundBackground} water={Main.waterStyle} front={(front.Length > s ? front[s] : -1):0.00} far={(back.Length > s ? back[s] : -1):0.00} " +
            $"calls far={TestSurface.Far} mid={TestSurface.Middle} close={TestSurface.Close} fades={TestSurface.Fades} fill={TestUnderground.Fills}";
    }

    static void WaterOn(Player p)
    {
        int s = ModContent.GetInstance<TestWater>().Slot;
        Report("the scene sets the water style", Main.waterStyle == s, $"waterStyle={Main.waterStyle} expected {s}");
        int calculated = Main.CalculateWaterStyle(false);
        Report("CalculateWaterStyle returns the scene's water", calculated == s, $"{calculated}");
        var alpha = Main.liquidAlpha;
        string others = "";
        for (int k = 0; k < 15; k++)
            if (alpha[k] > 0) others += $" {k}:{alpha[k]:0.00}";
        Report("fade: the mod water at 1 and the game's at 0", alpha.Length > s && alpha[s] == 1 && others.Length == 0, $"mod={(alpha.Length > s ? alpha[s] : -1)} others={others}");
        Report("splash: Dust.dustWater is the water's", Dust.dustWater() == TestWater.Splash, $"{Dust.dustWater()}");
        int index = Rain.NewRainForced(p.Center, new Vector2(0, 5));
        int type = (uint)index < (uint)Main.rain.Length ? Main.rain[index].type : -1;
        if ((uint)index < (uint)Main.rain.Length) Main.rain[index].active = false;
        Report("rain: the water's variant", type == 1, $"type={type}");
        Report("rain drawn with the water's texture in the variant's column", rainMod > 0 && rainX == 4, $"draws={rainMod} x={rainX}");
        var map = Lighting.UsingNewLighting ? Lighting.NewEngine._workingLightMap : Lighting.LegacyEngine._workingLightMap;
        Vector3 v = map.LightDecayThroughWater;
        bool near(float x) => Math.Abs(x - 0.91f) < 0.01f;
        Report("light: LightColorMultiplier 1 becomes 0.91 in the LightMap", near(v.X) && near(v.Y) && near(v.Z), $"{v.X:0.000} {v.Y:0.000} {v.Z:0.000} new={Lighting.UsingNewLighting}");
        int wf = ModContent.GetInstance<TestWaterfall>().Slot;
        Report("the mod waterfall is drawn with its ColorMultiplier", waterfallDraws[wf] > 0 && TestWaterfall.Tints > 0, $"draws={waterfallDraws[wf]} tints={TestWaterfall.Tints}");
        string hair = Rgba(BiomeHair(p));
        Report("biome hair dye: the water's BiomeHairColor", hair == "255,0,0,255", hair);
    }

    static void BackgroundOn()
    {
        var surface = ModContent.GetInstance<TestSurface>();
        var underground = ModContent.GetInstance<TestUnderground>();
        Report("BiomeLow beats the forest: the mod's bgStyle", Main.bgStyle == surface.Slot, State());
        Report("the style's fades rose on both layers", Main.bgAlphaFrontLayer[surface.Slot] > 0.99f && Main.bgAlphaFarBackLayer[surface.Slot] > 0.99f, State());
        Report("the three layers asked for textures and ModifyFarFades ran", TestSurface.Far > 0 && TestSurface.Middle > 0 && TestSurface.Close > 0 && TestSurface.Fades > 0, State());
        Report("underground: the mod's style", Main.undergroundBackground == underground.Slot, State());
        var values = Main.instance.backTextureValues;
        var numbers = Main.instance.backTexture;
        string wrong = "";
        for (int k = 0; k < 4; k++)
        {
            int slot = Slot("ExampleBiomeUnderground" + k);
            if (numbers[k] != slot || values[k].Pointer != TextureAssets.Background[slot].Value.Pointer) wrong += $" {k}:{numbers[k]}";
        }
        Report("underground: the 4 textures swapped in backTexture and backTextureValues", wrong.Length == 0, wrong);
    }

    static void Off(Player p)
    {
        int water = ModContent.GetInstance<TestWater>().Slot, surface = ModContent.GetInstance<TestSurface>().Slot;
        Report("off: the game's water again", Main.waterStyle == vanillaWater, $"{Main.waterStyle} before {vanillaWater}");
        Report("off: the mod water's alpha is 0", Main.liquidAlpha[water] == 0, $"{Main.liquidAlpha[water]}");
        Report("off: the splash is the game's", Dust.dustWater() != TestWater.Splash, $"{Dust.dustWater()}");
        string hair = Rgba(BiomeHair(p));
        Report("off: the biome hair dye is the game's", hair != "255,0,0,255", hair);
        Report("off: the game's bgStyle again", Main.bgStyle == vanillaBg, State());
        Report("off: the game's underground again", Main.undergroundBackground == vanillaUg, State());
        Report("off: the mod's front layer faded out", Main.bgAlphaFrontLayer[surface] < 0.01f, State());
    }

    static MethodRef hairMethod;
    static StaticRef hairInstance;
    static readonly GameClass Lambdas = new("Terraria.Initializers", "DyeInitializer", "<>c");

    static Color BiomeHair(Player p)
    {
        ref readonly MethodRef m = ref GameInterop.Method(ref hairMethod, Lambdas, "Color <LoadLegacyHairdyes>b__5_6(Player player, Color newColor, ref bool lighting)", new[] { -1, -1, -1, -1 });
        IntPtr instance = *(IntPtr*)GameInterop.StaticAddress(ref hairInstance, Lambdas, "<>9");
        p.hairDyeColor = new Color(0, 0, 0, 0);
        Color white = new(255, 255, 255, 255);
        bool lighting = false;
        CallFrame frame = default;
        frame.X[0] = (long)instance;
        frame.X[1] = (long)p.Pointer;
        frame.X[2] = *(uint*)&white;
        frame.X[3] = (long)&lighting;
        frame.X[4] = (long)m.Info;
        frame.Ret = RetKind.Int;
        GameInterop.Call(m.Fn, &frame);
        uint result = (uint)frame.Out[0];
        return *(Color*)&result;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csbackground {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
