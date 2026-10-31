using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.DataStructures;

public struct BossBarDrawParams
{
    public Texture2D BarTexture;

    public Vector2 BarCenter;

    public Color BarColor;

    public Texture2D IconTexture;

    public Rectangle IconFrame;

    public Color IconColor;

    public float Life;

    public float LifeMax;

    public float Shield;

    public float ShieldMax;

    public float IconScale;

    public bool ShowText;

    public Vector2 TextOffset;

    public BossBarDrawParams(Texture2D barTexture, Vector2 barCenter, Texture2D iconTexture, Rectangle iconFrame, Color iconColor, float life, float lifeMax,
                             Color? barColor = null, float shield = 0f, float shieldMax = 0f, float iconScale = 1f, bool showText = true, Vector2 textOffset = default)
    {
        BarTexture = barTexture;
        BarCenter = barCenter;
        BarColor = barColor ?? Color.White;
        IconTexture = iconTexture;
        IconFrame = iconFrame;
        IconColor = iconColor;
        Life = life;
        LifeMax = lifeMax;
        Shield = shield;
        ShieldMax = shieldMax;
        IconScale = iconScale;
        ShowText = showText;
        TextOffset = textOffset;
    }

    public void Deconstruct(out Texture2D barTexture, out Vector2 barCenter, out Color barColor, out Texture2D iconTexture, out Rectangle iconFrame, out Color iconColor,
                            out float life, out float lifeMax, out float shield, out float shieldMax, out float iconScale, out bool showText, out Vector2 textOffset)
    {
        barTexture = BarTexture;
        barCenter = BarCenter;
        barColor = BarColor;
        iconTexture = IconTexture;
        iconFrame = IconFrame;
        iconColor = IconColor;
        life = Life;
        lifeMax = LifeMax;
        shield = Shield;
        shieldMax = ShieldMax;
        iconScale = IconScale;
        showText = ShowText;
        textOffset = TextOffset;
    }
}
