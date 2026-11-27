using System.Text;
using BunnyLoader.Interop;

namespace Microsoft.Xna.Framework.Graphics;

public readonly unsafe partial struct SpriteBatch
{
    public void Begin(SpriteSortMode sortMode, bool defferedBatch) => Begin_(GameSortMode(sortMode), defferedBatch);

    public void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred, BlendState blendState = default, SamplerState samplerState = default,
                      DepthStencilState depthStencilState = default, RasterizerState rasterizerState = default, Effect effect = default,
                      GameNullable<Matrix> transformMatrix = default, bool defferedBatch = true) =>
        Begin_(GameSortMode(sortMode), blendState, samplerState, depthStencilState, rasterizerState, effect, transformMatrix, defferedBatch);

    public void Begin(SpriteSortMode sortMode, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState,
                      RasterizerState rasterizerState, Effect? effect) =>
        Begin(sortMode, blendState, samplerState, depthStencilState, rasterizerState, effect ?? default, default(GameNullable<Matrix>));

    public void Begin(SpriteSortMode sortMode, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState,
                      RasterizerState rasterizerState, Effect? effect, GameNullable<Matrix> transformMatrix) =>
        Begin(sortMode, blendState, samplerState, depthStencilState, rasterizerState, effect ?? default, transformMatrix, true);

    static SpriteSortMode GameSortMode(SpriteSortMode sortMode) => sortMode > SpriteSortMode.Immediate ? SpriteSortMode.Deferred : sortMode;

    public void DrawString(SpriteFont spriteFont, string? text, Vector2 position, Color color) =>
        DrawString(spriteFont, text, position, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

    public void DrawString(SpriteFont spriteFont, StringBuilder text, Vector2 position, Color color) =>
        DrawString(spriteFont, text.ToString(), position, color);

    public void DrawString(SpriteFont spriteFont, StringBuilder text, Vector2 position, Color color, float rotation, Vector2 origin,
                           float scale, SpriteEffects effects, float layerDepth) =>
        DrawString(spriteFont, text.ToString(), position, color, rotation, origin, scale, effects, layerDepth);

    public void DrawString(SpriteFont spriteFont, StringBuilder text, Vector2 position, Color color, float rotation, Vector2 origin,
                           Vector2 scale, SpriteEffects effects, float layerDepth) =>
        DrawString(spriteFont, text.ToString(), position, color, rotation, origin, scale, effects, layerDepth);
}

public readonly unsafe partial struct SpriteFont
{
    public Vector2 MeasureString(StringBuilder text) => MeasureString(text.ToString());
}
