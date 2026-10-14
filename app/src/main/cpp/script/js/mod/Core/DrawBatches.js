// O SpriteBatch do celular junta, entre o Begin e o End, tudo o que usa a
// mesma textura (BatchDeffered, ligado por padrão): duas cópias do mesmo NPC
// saem com os corpos juntos e depois os brilhos juntos, e o brilho de uma cai
// por cima do corpo da outra. No PC (Deferred) vale a ordem das chamadas.
// Restart fecha o lote (desenha o que já tem) e abre outro no mesmo estado:
// o que vier depois não se mistura com o que veio antes.
class DrawBatches {
    static #BEGIN = 'void Begin(SpriteSortMode sortMode, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState, RasterizerState rasterizerState, Effect effect, Nullable<Matrix> transformMatrix, bool defferedBatch)';
    static #IMMEDIATE = 1;

    // false se o lote não estava aberto (ou é Immediate, que já desenha na hora).
    static Restart(batch) {
        if (!batch || !batch._beginCalled) return false;

        const sortMode = batch._sortMode;
        if (sortMode === DrawBatches.#IMMEDIATE) return false;

        const blend = batch._blendState;
        const depth = batch._depthStencilState;
        const raster = batch._rasterizerState;
        const effect = batch._effect;
        const matrix = batch._hasMatrix ? batch._renderMatrix : null;
        const deferred = batch.BatchDeffered;
        batch['void End()']();
        batch[DrawBatches.#BEGIN](sortMode, blend, Terraria.Main.DefaultSamplerState, depth, raster, effect, matrix, deferred);
        return true;
    }
}
