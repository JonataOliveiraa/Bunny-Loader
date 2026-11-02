using ExampleMod.Backgrounds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content;

public class ExampleModMenu : ModMenu
{
    private const string menuAssetPath = "ExampleMod/Assets/Textures/Menu";

    private Asset<Texture2D> logoTexture = null!;
    private Asset<Texture2D> sunTexture = null!;
    private Asset<Texture2D> moonTexture = null!;

    public override void Load()
    {
        logoTexture = ModContent.Request<Texture2D>($"{menuAssetPath}/ExampleModTitle");
        sunTexture = ModContent.Request<Texture2D>($"{menuAssetPath}/ExampleSun");
        moonTexture = ModContent.Request<Texture2D>($"{menuAssetPath}/ExampliumMoon");
    }

    public override Asset<Texture2D> Logo => logoTexture;

    public override Asset<Texture2D> SunTexture => sunTexture;

    public override Asset<Texture2D> MoonTexture => moonTexture;

    public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Music/MysteriousMystery");

    public override ModSurfaceBackgroundStyle MenuBackgroundStyle => ModContent.GetInstance<ExampleSurfaceBackgroundStyle>();

    public override string DisplayName => "Example ModMenu";

    public override void OnSelected()
    {
        SoundEngine.PlaySound(SoundID.Thunder);
    }

    public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color drawColor)
    {
        drawColor = Color.White;
        return true;
    }
}
