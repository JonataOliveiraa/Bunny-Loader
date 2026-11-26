using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.Graphics;

public readonly unsafe partial struct SpriteViewMatrix
{
    public Matrix NormalizedTransformationMatrix
    {
        get
        {
            float width = Main.screenWidth, height = Main.screenHeight;
            SpriteEffects effects = Effects;
            float fx = (effects & SpriteEffects.FlipHorizontally) != 0 ? -1 : 1, fy = (effects & SpriteEffects.FlipVertically) != 0 ? -1 : 1;
            float ox = fx < 0 ? width : 0, oy = fy < 0 ? height : 0;

            Vector2 zoom = Zoom;
            float zx = MathF.Round(zoom.X * 128) / 128, zy = MathF.Round(zoom.Y * 128) / 128;
            float tx = MathF.Round(width * 0.5f - width * 0.5f / zx), ty = MathF.Round(height * 0.5f - height * 0.5f / zy);
            const float Offset = 1f / 256 - 0.5f;

            float sx = fx * zx * 2 / width, sy = -fy * zy * 2 / height;
            float bx = ((ox - tx) * zx + Offset) * 2 / width - 1, by = 1 - ((oy - ty) * zy + Offset) * 2 / height;

            return new Matrix(sx, 0, 0, 0, 0, sy, 0, 0, 0, 0, -1, 0, bx, by, 0, 1);
        }
    }
}
