using BunnyLoader.Interop;

namespace Microsoft.Xna.Framework.Graphics;

public readonly unsafe partial struct SpriteBatch
{
    public void Begin(SpriteSortMode sortMode, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState,
                      RasterizerState rasterizerState, Effect? effect) =>
        Begin(sortMode, blendState, samplerState, depthStencilState, rasterizerState, effect ?? default, default(GameNullable<Matrix>));

    public void Begin(SpriteSortMode sortMode, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState,
                      RasterizerState rasterizerState, Effect? effect, GameNullable<Matrix> transformMatrix) =>
        Begin(sortMode, blendState, samplerState, depthStencilState, rasterizerState, effect ?? default, transformMatrix, true);
}
