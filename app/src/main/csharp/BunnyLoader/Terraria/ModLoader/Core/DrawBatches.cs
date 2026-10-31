using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader.Core;

internal static class DrawBatches
{
    internal static bool Restart(SpriteBatch batch)
    {
        if (batch.IsNull || !batch._beginCalled) return false;
        SpriteSortMode sortMode = batch._sortMode;
        if (sortMode == SpriteSortMode.Immediate) return false;
        BlendState blend = batch._blendState;
        DepthStencilState depth = batch._depthStencilState;
        RasterizerState raster = batch._rasterizerState;
        Effect effect = batch._effect;
        Matrix? matrix = batch._hasMatrix ? batch._renderMatrix : null;
        bool deferred = batch.BatchDeffered;
        batch.End();
        batch.Begin(sortMode, blend, Main.DefaultSamplerState, depth, raster, effect, matrix, deferred);
        return true;
    }
}
