using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.UI;

namespace ExampleMod.Common.UI.ExampleCoinsUI;

internal class ExampleUIHoverImageButton : UIImageButton
{
    internal string hoverText;

    public ExampleUIHoverImageButton(Asset<Texture2D> texture, string hoverText) : base(texture)
    {
        this.hoverText = hoverText;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);

        if (IsMouseHovering)
            UICommon.TooltipMouseText(hoverText);
    }
}
